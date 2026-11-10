using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using LibreHardwareMonitor.Hardware;
using WindowsStats.Model;

namespace WindowsStats.Monitoring
{
    public class HardwareMonitor : IDisposable
    {
        private readonly Computer _computer;
        private readonly object _lock = new object();
        private HardwareSnapshot _lastSnapshot;
        private readonly List<HistoryPoint> _history = new List<HistoryPoint>();
        private const int MaxHistory = 60;

        // Drive Performance Counters: Letter -> (Read, Write, Idle)
        private readonly Dictionary<string, PerformanceCounter> _driveReadCounters = new Dictionary<string, PerformanceCounter>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PerformanceCounter> _driveWriteCounters = new Dictionary<string, PerformanceCounter>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PerformanceCounter> _driveIdleCounters = new Dictionary<string, PerformanceCounter>(StringComparer.OrdinalIgnoreCase);

        // LAN Performance Counters
        private PerformanceCounter _lanRecvCounter;
        private PerformanceCounter _lanSentCounter;
        private string _lanCounterInstance;

        // Fallback network delta tracking
        private readonly Dictionary<string, long> _netLastIn = new Dictionary<string, long>();
        private readonly Dictionary<string, long> _netLastOut = new Dictionary<string, long>();
        private DateTime _lastNetTime = DateTime.UtcNow;

        // Process CPU tracking
        private readonly Process _currentProcess;
        private TimeSpan _lastCpuTime;
        private DateTime _lastProcCheck;

        // System info cache
        private readonly string _hostname;
        private readonly string _osVersion;
        private readonly bool _isElevated;
        private string _motherboardName = "Standard Motherboard";
        private string _cachedLanIp = "127.0.0.1";

        private Thread _workerThread;
        private volatile bool _running;
        private int _pollingIntervalMs = 2000;
        private int _activeClientsCount = 0;

        public event Action<HardwareSnapshot> OnSnapshotUpdated;

        public HardwareMonitor(int pollingIntervalMs = 2000)
        {
            _pollingIntervalMs = pollingIntervalMs;
            _hostname = Environment.MachineName;
            _osVersion = Environment.OSVersion.VersionString;

            try
            {
                WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                _isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                _isElevated = false;
            }

            _currentProcess = Process.GetCurrentProcess();
            _lastCpuTime = _currentProcess.TotalProcessorTime;
            _lastProcCheck = DateTime.UtcNow;

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsMemoryEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = false,
                IsNetworkEnabled = true,
                IsStorageEnabled = true
            };

            // Pre-seed an initial snapshot so GetLatestSnapshot() is NEVER null and never blocks
            _lastSnapshot = CreateInitialSnapshot();
        }

        private HardwareSnapshot CreateInitialSnapshot()
        {
            HardwareSnapshot snap = new HardwareSnapshot();
            snap.Timestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            snap.System.Hostname = _hostname;
            snap.System.OsVersion = _osVersion;
            snap.System.Port = 8420;
            snap.System.IsElevated = _isElevated;
            snap.System.Motherboard = _motherboardName;
            snap.System.PrimaryLanIp = _cachedLanIp;
            snap.Cpu.Name = "Intel Core (" + Environment.ProcessorCount + " Cores)";
            snap.Cpu.CoreCount = Environment.ProcessorCount;
            snap.Memory.TotalFormatted = "-- GB";
            snap.Network.PrimaryAdapter = "LAN-Anschluss";
            snap.Network.Ipv4 = _cachedLanIp;
            return snap;
        }

        private void InitMotherboard()
        {
            try
            {
                foreach (var hw in _computer.Hardware)
                {
                    if (hw.HardwareType == HardwareType.Motherboard)
                    {
                        _motherboardName = hw.Name;
                        break;
                    }
                }
            }
            catch {}
        }

        private void InitDriveCounters()
        {
            try
            {
                var cat = new PerformanceCounterCategory("LogicalDisk");
                string[] instances = cat.GetInstanceNames();

                foreach (var inst in instances)
                {
                    // Only match single drive letters like "C:", "D:", etc.
                    if (inst.Length == 2 && inst[1] == ':')
                    {
                        try
                        {
                            var rc = new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", inst, true);
                            var wc = new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", inst, true);
                            var ic = new PerformanceCounter("LogicalDisk", "% Idle Time", inst, true);

                            rc.NextValue();
                            wc.NextValue();
                            ic.NextValue();

                            _driveReadCounters[inst] = rc;
                            _driveWriteCounters[inst] = wc;
                            _driveIdleCounters[inst] = ic;
                        }
                        catch {}
                    }
                }
            }
            catch {}
        }

        private void InitLanCounters()
        {
            try
            {
                var cat = new PerformanceCounterCategory("Network Interface");
                string[] instances = cat.GetInstanceNames();

                string bestInstance = null;
                foreach (var inst in instances)
                {
                    // Look for physical Intel / Realtek / Ethernet instance
                    if (inst.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        inst.IndexOf("Ethernet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        inst.IndexOf("I219", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        bestInstance = inst;
                        break;
                    }
                }

                if (bestInstance == null && instances.Length > 0)
                {
                    // Filter out loopback or vmware if possible
                    foreach (var inst in instances)
                    {
                        if (inst.IndexOf("VMware", StringComparison.OrdinalIgnoreCase) < 0 &&
                            inst.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) < 0 &&
                            inst.IndexOf("Loopback", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            bestInstance = inst;
                            break;
                        }
                    }
                }

                if (bestInstance != null)
                {
                    _lanCounterInstance = bestInstance;
                    _lanRecvCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", bestInstance, true);
                    _lanSentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", bestInstance, true);
                    _lanRecvCounter.NextValue();
                    _lanSentCounter.NextValue();
                    Console.WriteLine("LAN Performance Counter connected to: " + bestInstance);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Warning initializing LAN counter: " + ex.Message);
            }
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "WindowsStats_HardwareMonitorWorker"
            };
            _workerThread.Start();
        }

        public void Stop()
        {
            _running = false;
            if (_workerThread != null && _workerThread.IsAlive)
            {
                _workerThread.Join(2000);
            }
        }

        public void SetPollingInterval(int ms)
        {
            if (ms < 200) ms = 200;
            if (ms > 10000) ms = 10000;
            _pollingIntervalMs = ms;
        }

        public void RegisterClientConnected()
        {
            Interlocked.Increment(ref _activeClientsCount);
        }

        public void RegisterClientDisconnected()
        {
            Interlocked.Decrement(ref _activeClientsCount);
        }

        public HardwareSnapshot GetLatestSnapshot()
        {
            lock (_lock)
            {
                return _lastSnapshot ?? CreateInitialSnapshot();
            }
        }

        private void WorkerLoop()
        {
            try
            {
                // 1. Fast Windows Performance Counters first (< 20ms)
                InitDriveCounters();
                InitLanCounters();

                // 2. Probing hardware sensors (CPU, GPU, Motherboard)
                _computer.Open();
                InitMotherboard();
                UpdateHardware();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Warning in worker init: " + ex.Message);
            }

            Thread.Sleep(300);

            // Initial baseline snapshot (pre-populates 60 history points)
            try
            {
                lock (_lock)
                {
                    _lastSnapshot = CollectSnapshot();
                }
            }
            catch {}

            while (_running)
            {
                try
                {
                    HardwareSnapshot snapshot = CollectSnapshot();
                    lock (_lock)
                    {
                        _lastSnapshot = snapshot;
                    }

                    if (OnSnapshotUpdated != null)
                    {
                        OnSnapshotUpdated(snapshot);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Monitoring error: " + ex.Message);
                }

                int sleepMs = _pollingIntervalMs;
                if (_activeClientsCount <= 0 && sleepMs < 3000)
                {
                    sleepMs = 3000;
                }
                Thread.Sleep(sleepMs);
            }
        }

        private void UpdateHardware()
        {
            foreach (var hw in _computer.Hardware)
            {
                try
                {
                    hw.Update();
                    foreach (var sub in hw.SubHardware)
                    {
                        sub.Update();
                    }
                }
                catch {}
            }
        }

        private HardwareSnapshot CollectSnapshot()
        {
            UpdateHardware();

            HardwareSnapshot snap = new HardwareSnapshot();
            snap.Timestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            // 1. Process resources
            double procCpu = 0;
            try
            {
                DateTime now = DateTime.UtcNow;
                TimeSpan totalCpu = _currentProcess.TotalProcessorTime;
                double elapsedSec = (now - _lastProcCheck).TotalSeconds;
                if (elapsedSec > 0.1)
                {
                    double cpuDelta = (totalCpu - _lastCpuTime).TotalSeconds;
                    procCpu = (cpuDelta / (Environment.ProcessorCount * elapsedSec)) * 100.0;
                    _lastCpuTime = totalCpu;
                    _lastProcCheck = now;
                }
                _currentProcess.Refresh();
            }
            catch {}

            // 2. System info
            long uptimeSec = (long)(Environment.TickCount / 1000.0);
            TimeSpan ut = TimeSpan.FromSeconds(uptimeSec);
            string uptimeStr = string.Format("{0}d {1}h {2}m {3}s", ut.Days, ut.Hours, ut.Minutes, ut.Seconds);

            snap.System.Hostname = _hostname;
            snap.System.OsVersion = _osVersion;
            snap.System.UptimeSeconds = uptimeSec;
            snap.System.UptimeFormatted = uptimeStr;
            snap.System.IsElevated = _isElevated;
            snap.System.Motherboard = _motherboardName;
            snap.System.Port = 8420;
            snap.System.ProcessMemoryMb = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);
            snap.System.ProcessCpuPercent = Math.Max(0, procCpu);

            // 3. CPU
            IHardware cpuHw = null;
            foreach (var h in _computer.Hardware)
            {
                if (h.HardwareType == HardwareType.Cpu)
                {
                    cpuHw = h;
                    break;
                }
            }

            if (cpuHw != null)
            {
                snap.Cpu.Name = cpuHw.Name;
                Regex coreRegex = new Regex(@"^(?:CPU\s+Core|Core)\s*#?(\d+)$", RegexOptions.IgnoreCase);

                double totalLoad = 0;
                double maxClock = 0;
                double pkgTemp = 0;
                double pkgPower = 0;

                Dictionary<int, CpuCoreInfo> coreMap = new Dictionary<int, CpuCoreInfo>();

                foreach (var s in cpuHw.Sensors)
                {
                    if (s.Value == null) continue;
                    float val = s.Value.Value;

                    if (s.SensorType == SensorType.Load)
                    {
                        if (s.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase))
                        {
                            totalLoad = val;
                        }
                        else
                        {
                            Match m = coreRegex.Match(s.Name);
                            if (m.Success)
                            {
                                int id = int.Parse(m.Groups[1].Value);
                                if (!coreMap.ContainsKey(id))
                                {
                                    coreMap[id] = new CpuCoreInfo { Id = id, Name = "Core #" + id };
                                }
                                coreMap[id].Load = val;
                            }
                        }
                    }
                    else if (s.SensorType == SensorType.Clock)
                    {
                        if (val > maxClock) maxClock = val;
                        Match m = coreRegex.Match(s.Name);
                        if (m.Success)
                        {
                            int id = int.Parse(m.Groups[1].Value);
                            if (!coreMap.ContainsKey(id))
                            {
                                coreMap[id] = new CpuCoreInfo { Id = id, Name = "Core #" + id };
                            }
                            coreMap[id].ClockMhz = val;
                        }
                    }
                    else if (s.SensorType == SensorType.Temperature)
                    {
                        if (s.Name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            s.Name.IndexOf("Core Max", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            s.Name.IndexOf("Average", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (val > pkgTemp) pkgTemp = val;
                        }
                        Match m = coreRegex.Match(s.Name);
                        if (m.Success)
                        {
                            int id = int.Parse(m.Groups[1].Value);
                            if (!coreMap.ContainsKey(id))
                            {
                                coreMap[id] = new CpuCoreInfo { Id = id, Name = "Core #" + id };
                            }
                            coreMap[id].Temperature = val;
                        }
                    }
                    else if (s.SensorType == SensorType.Power)
                    {
                        if (s.Name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            pkgPower = val;
                        }
                    }
                }

                snap.Cpu.TotalLoad = totalLoad;
                snap.Cpu.MaxClockMhz = maxClock;
                snap.Cpu.Temperature = pkgTemp;
                snap.Cpu.PackagePower = pkgPower;

                var sortedCores = coreMap.Values.OrderBy(c => c.Id).ToList();
                snap.Cpu.Cores = sortedCores;
                snap.Cpu.CoreCount = sortedCores.Count > 0 ? sortedCores.Count : Environment.ProcessorCount;
            }
            else
            {
                snap.Cpu.Name = "Processor (" + Environment.ProcessorCount + " Cores)";
                snap.Cpu.CoreCount = Environment.ProcessorCount;
            }

            // 4. Memory
            IHardware memHw = null;
            foreach (var h in _computer.Hardware)
            {
                if (h.HardwareType == HardwareType.Memory)
                {
                    memHw = h;
                    break;
                }
            }

            if (memHw != null)
            {
                double usedGb = 0;
                double availGb = 0;
                double loadPct = 0;
                double vUsedGb = 0;
                double vAvailGb = 0;
                double vLoadPct = 0;

                foreach (var s in memHw.Sensors)
                {
                    if (s.Value == null) continue;
                    float v = s.Value.Value;
                    if (s.SensorType == SensorType.Data)
                    {
                        if (s.Name.Equals("Memory Used", StringComparison.OrdinalIgnoreCase)) usedGb = v;
                        else if (s.Name.Equals("Memory Available", StringComparison.OrdinalIgnoreCase)) availGb = v;
                        else if (s.Name.Equals("Virtual Memory Used", StringComparison.OrdinalIgnoreCase)) vUsedGb = v;
                        else if (s.Name.Equals("Virtual Memory Available", StringComparison.OrdinalIgnoreCase)) vAvailGb = v;
                    }
                    else if (s.SensorType == SensorType.Load)
                    {
                        if (s.Name.Equals("Memory", StringComparison.OrdinalIgnoreCase)) loadPct = v;
                        else if (s.Name.Equals("Virtual Memory", StringComparison.OrdinalIgnoreCase)) vLoadPct = v;
                    }
                }

                long usedBytes = (long)(usedGb * 1024 * 1024 * 1024);
                long availBytes = (long)(availGb * 1024 * 1024 * 1024);
                long totalBytes = usedBytes + availBytes;

                snap.Memory.TotalPhysicalBytes = totalBytes;
                snap.Memory.UsedPhysicalBytes = usedBytes;
                snap.Memory.AvailablePhysicalBytes = availBytes;
                snap.Memory.LoadPercent = loadPct;
                snap.Memory.TotalFormatted = string.Format("{0:F1} GB", (totalBytes / (1024.0 * 1024.0 * 1024.0)));
                snap.Memory.UsedFormatted = string.Format("{0:F1} GB", usedGb);
                snap.Memory.AvailableFormatted = string.Format("{0:F1} GB", availGb);

                snap.Memory.VirtualUsedBytes = (long)(vUsedGb * 1024 * 1024 * 1024);
                snap.Memory.VirtualTotalBytes = (long)((vUsedGb + vAvailGb) * 1024 * 1024 * 1024);
                snap.Memory.VirtualLoadPercent = vLoadPct;
            }

            // 5. GPU
            IHardware gpuHw = null;
            foreach (var h in _computer.Hardware)
            {
                if (h.HardwareType == HardwareType.GpuIntel ||
                    h.HardwareType == HardwareType.GpuNvidia ||
                    h.HardwareType == HardwareType.GpuAmd)
                {
                    gpuHw = h;
                    break;
                }
            }

            if (gpuHw != null)
            {
                snap.Gpu.Name = gpuHw.Name;
                foreach (var s in gpuHw.Sensors)
                {
                    if (s.Value == null) continue;
                    float v = s.Value.Value;
                    if (s.SensorType == SensorType.Load)
                    {
                        if (s.Name.IndexOf("3D", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            s.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            snap.Gpu.Load3D = v;
                        }
                        else if (s.Name.IndexOf("Video Decode", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            snap.Gpu.LoadVideo = v;
                        }
                    }
                    else if (s.SensorType == SensorType.SmallData || s.SensorType == SensorType.Data)
                    {
                        if (s.Name.IndexOf("Memory Used", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            snap.Gpu.MemoryUsedMb = (s.SensorType == SensorType.SmallData) ? v : (v * 1024);
                        }
                        else if (s.Name.IndexOf("Memory Total", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            snap.Gpu.MemoryTotalMb = (s.SensorType == SensorType.SmallData) ? v : (v * 1024);
                        }
                    }
                    else if (s.SensorType == SensorType.Temperature)
                    {
                        snap.Gpu.Temperature = v;
                    }
                }
            }
            else
            {
                snap.Gpu.Name = "Standard GPU";
            }

            // 6. Storage (Sichere Erfassung aller 12 Laufwerke)
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    try
                    {
                        if (!drive.IsReady) continue;

                        string letter = drive.Name.Replace("\\", "");
                        long total = 0;
                        long free = 0;
                        try { total = drive.TotalSize; } catch {}
                        try { free = drive.TotalFreeSpace; } catch {}

                        if (total <= 0) continue;

                        long used = Math.Max(0, total - free);
                        double usedPct = ((double)used / total) * 100.0;
                        if (double.IsNaN(usedPct) || double.IsInfinity(usedPct)) usedPct = 0;

                        double rSpeed = 0;
                        double wSpeed = 0;
                        double activityPct = 0;

                        if (_driveReadCounters.ContainsKey(letter))
                        {
                            try { rSpeed = _driveReadCounters[letter].NextValue(); } catch {}
                        }
                        if (_driveWriteCounters.ContainsKey(letter))
                        {
                            try { wSpeed = _driveWriteCounters[letter].NextValue(); } catch {}
                        }
                        if (_driveIdleCounters.ContainsKey(letter))
                        {
                            try
                            {
                                float idle = _driveIdleCounters[letter].NextValue();
                                activityPct = Math.Max(0.0, Math.Min(100.0, 100.0 - idle));
                            }
                            catch {}
                        }

                        string vLabel = "";
                        try { vLabel = drive.VolumeLabel; } catch {}

                        snap.Storage.Add(new DriveItem
                        {
                            Name = drive.Name,
                            DriveLetter = letter,
                            VolumeLabel = string.IsNullOrEmpty(vLabel) ? "Lokaler Datenträger" : vLabel,
                            DriveFormat = drive.DriveFormat,
                            TotalBytes = total,
                            FreeBytes = free,
                            UsedBytes = used,
                            UsedPercent = usedPct,
                            ActivityPercent = activityPct,
                            ReadSpeedBytesPerSec = rSpeed,
                            WriteSpeedBytesPerSec = wSpeed
                        });
                    }
                    catch {}
                }
            }
            catch {}

            // 7. Network (FOKUS AUF DEN PHYSIKALISCHEN LAN-ANSCHLUSS)
            try
            {
                DateTime nowTime = DateTime.UtcNow;
                double deltaSec = (nowTime - _lastNetTime).TotalSeconds;
                if (deltaSec <= 0.001) deltaSec = 1.0;

                NetworkInterface lanNic = null;
                List<NetworkInterface> otherNics = new List<NetworkInterface>();

                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;

                    string desc = ni.Description.ToLowerInvariant();
                    string name = ni.Name.ToLowerInvariant();

                    bool isVirtual = desc.Contains("vmware") || desc.Contains("virtual") ||
                                     desc.Contains("hyper-v") || desc.Contains("tap") ||
                                     desc.Contains("vpn") || name.Contains("vethernet") ||
                                     name.Contains("vmnet");

                    if (!isVirtual && lanNic == null)
                    {
                        // Check if it's the physical Ethernet adapter or has a default gateway
                        var ipProps = ni.GetIPProperties();
                        if (ipProps.GatewayAddresses.Count > 0 || name.Contains("ethernet") || desc.Contains("intel") || desc.Contains("ethernet"))
                        {
                            lanNic = ni;
                            continue;
                        }
                    }

                    otherNics.Add(ni);
                }

                // If no physical LAN detected by gateway, fallback to first Ethernet
                if (lanNic == null && otherNics.Count > 0)
                {
                    lanNic = otherNics[0];
                    otherNics.RemoveAt(0);
                }

                double lanDownRate = 0;
                double lanUpRate = 0;
                long lanTotalIn = 0;
                long lanTotalOut = 0;
                string lanIp = "127.0.0.1";
                string lanGateway = "--";
                string lanSpeedStr = "1 Gbit/s";

                if (lanNic != null)
                {
                    var ipProps = lanNic.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            lanIp = addr.Address.ToString();
                            break;
                        }
                    }

                    if (ipProps.GatewayAddresses.Count > 0)
                    {
                        lanGateway = ipProps.GatewayAddresses[0].Address.ToString();
                    }

                    if (lanNic.Speed > 0)
                    {
                        lanSpeedStr = string.Format("{0:F0} Gbit/s", lanNic.Speed / 1000000000.0);
                        if (lanNic.Speed < 1000000000)
                        {
                            lanSpeedStr = string.Format("{0:F0} Mbit/s", lanNic.Speed / 1000000.0);
                        }
                    }

                    var stats = lanNic.GetIPv4Statistics();
                    lanTotalIn = stats.BytesReceived;
                    lanTotalOut = stats.BytesSent;

                    // 1. High-precision IPv4 Statistics byte delta (always calculated)
                    double deltaDown = 0;
                    double deltaUp = 0;
                    if (_netLastIn.ContainsKey(lanNic.Id))
                    {
                        long diffIn = lanTotalIn - _netLastIn[lanNic.Id];
                        long diffOut = lanTotalOut - _netLastOut[lanNic.Id];
                        if (diffIn > 0) deltaDown = diffIn / deltaSec;
                        if (diffOut > 0) deltaUp = diffOut / deltaSec;
                    }

                    // 2. Query Windows PerformanceCounter if available
                    if (_lanRecvCounter != null && _lanSentCounter != null)
                    {
                        try
                        {
                            lanDownRate = _lanRecvCounter.NextValue();
                            lanUpRate = _lanSentCounter.NextValue();
                        }
                        catch {}
                    }

                    // 3. Take the maximum of performance counter and byte delta to never miss throughput
                    if (deltaDown > lanDownRate) lanDownRate = deltaDown;
                    if (deltaUp > lanUpRate) lanUpRate = deltaUp;

                    _netLastIn[lanNic.Id] = lanTotalIn;
                    _netLastOut[lanNic.Id] = lanTotalOut;

                    _cachedLanIp = lanIp;

                    // Add physical LAN adapter as #1
                    snap.Network.Adapters.Add(new NetworkAdapterItem
                    {
                        Name = "LAN-Anschluss (" + lanNic.Name + ")",
                        Description = lanNic.Description + " • " + lanSpeedStr,
                        Ipv4 = lanIp,
                        Status = "Verbunden",
                        IsLan = true,
                        DownloadSpeedBytesPerSec = lanDownRate,
                        UploadSpeedBytesPerSec = lanUpRate,
                        TotalBytesReceived = lanTotalIn,
                        TotalBytesSent = lanTotalOut
                    });
                }

                // Add other active adapters (e.g. VMware) as secondary
                foreach (var other in otherNics)
                {
                    try
                    {
                        string otherIp = "";
                        foreach (var addr in other.GetIPProperties().UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                otherIp = addr.Address.ToString();
                                break;
                            }
                        }

                        var oStats = other.GetIPv4Statistics();
                        long oBytesIn = oStats.BytesReceived;
                        long oBytesOut = oStats.BytesSent;
                        double oDown = 0;
                        double oUp = 0;

                        if (_netLastIn.ContainsKey(other.Id))
                        {
                            long diffIn = oBytesIn - _netLastIn[other.Id];
                            long diffOut = oBytesOut - _netLastOut[other.Id];
                            if (diffIn > 0) oDown = diffIn / deltaSec;
                            if (diffOut > 0) oUp = diffOut / deltaSec;
                        }

                        _netLastIn[other.Id] = oBytesIn;
                        _netLastOut[other.Id] = oBytesOut;

                        snap.Network.Adapters.Add(new NetworkAdapterItem
                        {
                            Name = other.Name,
                            Description = other.Description,
                            Ipv4 = otherIp,
                            Status = "Verbunden",
                            IsLan = false,
                            DownloadSpeedBytesPerSec = oDown,
                            UploadSpeedBytesPerSec = oUp,
                            TotalBytesReceived = oBytesIn,
                            TotalBytesSent = oBytesOut
                        });
                    }
                    catch {}
                }

                _lastNetTime = nowTime;

                // Primary Network is ALWAYS the physical LAN port
                snap.Network.PrimaryAdapter = lanNic != null ? "LAN-Anschluss (" + lanNic.Name + ")" : "LAN-Anschluss";
                snap.Network.Description = lanNic != null ? lanNic.Description : "Intel(R) Ethernet Connection (2) I219-V";
                snap.Network.Ipv4 = lanIp;
                snap.Network.Gateway = lanGateway;
                snap.Network.LinkSpeed = lanSpeedStr;
                snap.Network.DownloadSpeedBytesPerSec = lanDownRate;
                snap.Network.UploadSpeedBytesPerSec = lanUpRate;
                snap.Network.TotalBytesReceived = lanTotalIn;
                snap.Network.TotalBytesSent = lanTotalOut;

                snap.System.PrimaryLanIp = _cachedLanIp;
            }
            catch {}

            // 8. Rolling History Buffer (Guaranteed 60 points from the very first frame)
            HistoryPoint hp = new HistoryPoint
            {
                Timestamp = snap.Timestamp,
                CpuLoad = snap.Cpu.TotalLoad,
                MemoryLoad = snap.Memory.LoadPercent,
                DownloadSpeedKb = snap.Network.DownloadSpeedBytesPerSec / 1024.0,
                UploadSpeedKb = snap.Network.UploadSpeedBytesPerSec / 1024.0
            };

            lock (_history)
            {
                if (_history.Count == 0)
                {
                    for (int i = 59; i >= 0; i--)
                    {
                        _history.Add(new HistoryPoint
                        {
                            Timestamp = snap.Timestamp - i,
                            CpuLoad = snap.Cpu.TotalLoad,
                            MemoryLoad = snap.Memory.LoadPercent,
                            DownloadSpeedKb = snap.Network.DownloadSpeedBytesPerSec / 1024.0,
                            UploadSpeedKb = snap.Network.UploadSpeedBytesPerSec / 1024.0
                        });
                    }
                }
                else
                {
                    _history.Add(hp);
                    while (_history.Count > MaxHistory)
                    {
                        _history.RemoveAt(0);
                    }
                }
                snap.History = new List<HistoryPoint>(_history);
            }

            return snap;
        }

        public void Dispose()
        {
            Stop();

            foreach (var c in _driveReadCounters.Values) { try { c.Dispose(); } catch {} }
            foreach (var c in _driveWriteCounters.Values) { try { c.Dispose(); } catch {} }
            foreach (var c in _driveIdleCounters.Values) { try { c.Dispose(); } catch {} }
            _driveReadCounters.Clear();
            _driveWriteCounters.Clear();
            _driveIdleCounters.Clear();

            if (_lanRecvCounter != null) { try { _lanRecvCounter.Dispose(); } catch {} _lanRecvCounter = null; }
            if (_lanSentCounter != null) { try { _lanSentCounter.Dispose(); } catch {} _lanSentCounter = null; }

            try
            {
                _computer.Close();
            }
            catch {}
        }
    }
}

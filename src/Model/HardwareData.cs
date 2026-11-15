using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WindowsStats.Model
{
    public class SystemInfo
    {
        public string Hostname;
        public string OsVersion;
        public long UptimeSeconds;
        public string UptimeFormatted;
        public bool IsElevated;
        public string Motherboard;
        public string PrimaryLanIp;
        public int Port;
        public double ProcessMemoryMb;
        public double ProcessCpuPercent;
    }

    public class CpuCoreInfo
    {
        public int Id;
        public string Name;
        public double Load;
        public double ClockMhz;
        public double Temperature;
    }

    public class CpuInfo
    {
        public string Name;
        public int CoreCount;
        public double TotalLoad;
        public double Temperature;
        public double PackagePower;
        public double MaxClockMhz;
        public List<CpuCoreInfo> Cores;

        public CpuInfo()
        {
            Cores = new List<CpuCoreInfo>();
        }
    }

    public class MemoryInfo
    {
        public long TotalPhysicalBytes;
        public long UsedPhysicalBytes;
        public long AvailablePhysicalBytes;
        public double LoadPercent;
        public string TotalFormatted;
        public string UsedFormatted;
        public string AvailableFormatted;

        public long VirtualTotalBytes;
        public long VirtualUsedBytes;
        public double VirtualLoadPercent;
    }

    public class GpuInfo
    {
        public string Name;
        public double Load3D;
        public double LoadVideo;
        public double MemoryUsedMb;
        public double MemoryTotalMb;
        public double Temperature;
    }

    public class DriveItem
    {
        public string Name;
        public string DriveLetter;
        public string VolumeLabel;
        public string DriveFormat;
        public long TotalBytes;
        public long FreeBytes;
        public long UsedBytes;
        public double UsedPercent;       // Speicherplatz-Auslastung %
        public double ActivityPercent;   // I/O-Auslastung / Aktive Zeit % (100 - % Idle Time)
        public double ReadSpeedBytesPerSec;
        public double WriteSpeedBytesPerSec;
        public DiskHealthInfo Health;
    }

    public class DiskHealthInfo
    {
        public int DiskIndex;
        public string Model;
        public string CleanModel;
        public string Status;             // "Healthy", "Warning", "Critical"
        public string Assessment;         // "Gesund", "Beobachten", "Austausch empfohlen"
        public string BadgeColor;         // "#00e676", "#ffb300", "#ff5252"
        public string InterfaceType;      // "NVMe", "SATA", "USB", "SCSI", "IDE"
        public string MediaType;          // "SSD", "HDD"
        public string SerialNumber;
        public string AssignedLetters;
        public int BadBlocks;
        public int IoRetries;
        public int HardwareErrors;
        public string Summary;
        public string LastChecked;
    }

    public class DiskHealthReport
    {
        public string LastChecked;
        public string OverallStatus;
        public string OverallColor;
        public List<DiskHealthInfo> Disks;

        public DiskHealthReport()
        {
            Disks = new List<DiskHealthInfo>();
            LastChecked = "--";
            OverallStatus = "Initialisiere...";
            OverallColor = "#00e676";
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder(2048);
            sb.Append("{");
            sb.Append("\"lastChecked\":\"").Append(Escape(LastChecked)).Append("\",");
            sb.Append("\"overallStatus\":\"").Append(Escape(OverallStatus)).Append("\",");
            sb.Append("\"overallColor\":\"").Append(Escape(OverallColor)).Append("\",");
            sb.Append("\"disksCount\":").Append(Disks.Count).Append(",");
            sb.Append("\"disks\":[");
            for (int i = 0; i < Disks.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var dh = Disks[i];
                sb.Append("{");
                sb.Append("\"diskIndex\":").Append(dh.DiskIndex).Append(",");
                sb.Append("\"model\":\"").Append(Escape(dh.Model)).Append("\",");
                sb.Append("\"cleanModel\":\"").Append(Escape(dh.CleanModel)).Append("\",");
                sb.Append("\"status\":\"").Append(Escape(dh.Status)).Append("\",");
                sb.Append("\"assessment\":\"").Append(Escape(dh.Assessment)).Append("\",");
                sb.Append("\"badgeColor\":\"").Append(Escape(dh.BadgeColor)).Append("\",");
                sb.Append("\"interfaceType\":\"").Append(Escape(dh.InterfaceType)).Append("\",");
                sb.Append("\"mediaType\":\"").Append(Escape(dh.MediaType)).Append("\",");
                sb.Append("\"assignedLetters\":\"").Append(Escape(dh.AssignedLetters)).Append("\",");
                sb.Append("\"badBlocks\":").Append(dh.BadBlocks).Append(",");
                sb.Append("\"ioRetries\":").Append(dh.IoRetries).Append(",");
                sb.Append("\"summary\":\"").Append(Escape(dh.Summary)).Append("\",");
                sb.Append("\"lastChecked\":\"").Append(Escape(dh.LastChecked)).Append("\"");
                sb.Append("}");
            }
            sb.Append("]");
            sb.Append("}");
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }
    }

    public class NetworkAdapterItem
    {
        public string Name;
        public string Description;
        public string Ipv4;
        public string Status;
        public bool IsLan;
        public double DownloadSpeedBytesPerSec;
        public double UploadSpeedBytesPerSec;
        public long TotalBytesReceived;
        public long TotalBytesSent;
    }

    public class NetworkInfo
    {
        public string PrimaryAdapter;
        public string Description;
        public string Ipv4;
        public string Gateway;
        public string LinkSpeed;
        public double DownloadSpeedBytesPerSec;
        public double UploadSpeedBytesPerSec;
        public long TotalBytesReceived;
        public long TotalBytesSent;
        public List<NetworkAdapterItem> Adapters;

        public NetworkInfo()
        {
            Adapters = new List<NetworkAdapterItem>();
        }
    }

    public class HistoryPoint
    {
        public long Timestamp;
        public double CpuLoad;
        public double MemoryLoad;
        public double DownloadSpeedKb;
        public double UploadSpeedKb;
    }

    public class HardwareSnapshot
    {
        public long Timestamp;
        public SystemInfo System;
        public CpuInfo Cpu;
        public MemoryInfo Memory;
        public GpuInfo Gpu;
        public List<DriveItem> Storage;
        public NetworkInfo Network;
        public List<HistoryPoint> History;
        public DiskHealthReport HealthReport;

        public HardwareSnapshot()
        {
            System = new SystemInfo();
            Cpu = new CpuInfo();
            Memory = new MemoryInfo();
            Gpu = new GpuInfo();
            Storage = new List<DriveItem>();
            Network = new NetworkInfo();
            History = new List<HistoryPoint>();
            HealthReport = new DiskHealthReport();
        }

        private static string Fmt(double val, int decimals = 1)
        {
            if (double.IsNaN(val) || double.IsInfinity(val)) return "0.0";
            return val.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder(8192);
            sb.Append("{");
            sb.Append("\"timestamp\":").Append(Timestamp).Append(",");

            // System
            sb.Append("\"system\":{");
            sb.Append("\"hostname\":\"").Append(Escape(System.Hostname)).Append("\",");
            sb.Append("\"osVersion\":\"").Append(Escape(System.OsVersion)).Append("\",");
            sb.Append("\"uptimeSeconds\":").Append(System.UptimeSeconds).Append(",");
            sb.Append("\"uptimeFormatted\":\"").Append(Escape(System.UptimeFormatted)).Append("\",");
            sb.Append("\"isElevated\":").Append(System.IsElevated ? "true" : "false").Append(",");
            sb.Append("\"motherboard\":\"").Append(Escape(System.Motherboard)).Append("\",");
            sb.Append("\"primaryLanIp\":\"").Append(Escape(System.PrimaryLanIp)).Append("\",");
            sb.Append("\"port\":").Append(System.Port).Append(",");
            sb.Append("\"processMemoryMb\":").Append(Fmt(System.ProcessMemoryMb)).Append(",");
            sb.Append("\"processCpuPercent\":").Append(Fmt(System.ProcessCpuPercent, 2));
            sb.Append("},");

            // CPU
            sb.Append("\"cpu\":{");
            sb.Append("\"name\":\"").Append(Escape(Cpu.Name)).Append("\",");
            sb.Append("\"coreCount\":").Append(Cpu.CoreCount).Append(",");
            sb.Append("\"totalLoad\":").Append(Fmt(Cpu.TotalLoad)).Append(",");
            sb.Append("\"temperature\":").Append(Fmt(Cpu.Temperature)).Append(",");
            sb.Append("\"packagePower\":").Append(Fmt(Cpu.PackagePower)).Append(",");
            sb.Append("\"maxClockMhz\":").Append(Fmt(Cpu.MaxClockMhz)).Append(",");
            sb.Append("\"cores\":[");
            for (int i = 0; i < Cpu.Cores.Count; i++)
            {
                if (i > 0) sb.Append(",");
                CpuCoreInfo c = Cpu.Cores[i];
                sb.Append("{");
                sb.Append("\"id\":").Append(c.Id).Append(",");
                sb.Append("\"name\":\"").Append(Escape(c.Name)).Append("\",");
                sb.Append("\"load\":").Append(Fmt(c.Load)).Append(",");
                sb.Append("\"clockMhz\":").Append(Fmt(c.ClockMhz)).Append(",");
                sb.Append("\"temp\":").Append(Fmt(c.Temperature));
                sb.Append("}");
            }
            sb.Append("]");
            sb.Append("},");

            // Memory
            sb.Append("\"memory\":{");
            sb.Append("\"totalPhysicalBytes\":").Append(Memory.TotalPhysicalBytes).Append(",");
            sb.Append("\"usedPhysicalBytes\":").Append(Memory.UsedPhysicalBytes).Append(",");
            sb.Append("\"availablePhysicalBytes\":").Append(Memory.AvailablePhysicalBytes).Append(",");
            sb.Append("\"loadPercent\":").Append(Fmt(Memory.LoadPercent)).Append(",");
            sb.Append("\"totalFormatted\":\"").Append(Escape(Memory.TotalFormatted)).Append("\",");
            sb.Append("\"usedFormatted\":\"").Append(Escape(Memory.UsedFormatted)).Append("\",");
            sb.Append("\"availableFormatted\":\"").Append(Escape(Memory.AvailableFormatted)).Append("\",");
            sb.Append("\"virtualTotalBytes\":").Append(Memory.VirtualTotalBytes).Append(",");
            sb.Append("\"virtualUsedBytes\":").Append(Memory.VirtualUsedBytes).Append(",");
            sb.Append("\"virtualLoadPercent\":").Append(Fmt(Memory.VirtualLoadPercent));
            sb.Append("},");

            // GPU
            sb.Append("\"gpu\":{");
            sb.Append("\"name\":\"").Append(Escape(Gpu.Name)).Append("\",");
            sb.Append("\"load3D\":").Append(Fmt(Gpu.Load3D)).Append(",");
            sb.Append("\"loadVideo\":").Append(Fmt(Gpu.LoadVideo)).Append(",");
            sb.Append("\"memoryUsedMb\":").Append(Fmt(Gpu.MemoryUsedMb)).Append(",");
            sb.Append("\"memoryTotalMb\":").Append(Fmt(Gpu.MemoryTotalMb)).Append(",");
            sb.Append("\"temperature\":").Append(Fmt(Gpu.Temperature));
            sb.Append("},");

            // Storage (Alle Laufwerke)
            sb.Append("\"storage\":[");
            for (int i = 0; i < Storage.Count; i++)
            {
                if (i > 0) sb.Append(",");
                DriveItem d = Storage[i];
                sb.Append("{");
                sb.Append("\"name\":\"").Append(Escape(d.Name)).Append("\",");
                sb.Append("\"driveLetter\":\"").Append(Escape(d.DriveLetter)).Append("\",");
                sb.Append("\"volumeLabel\":\"").Append(Escape(d.VolumeLabel)).Append("\",");
                sb.Append("\"format\":\"").Append(Escape(d.DriveFormat)).Append("\",");
                sb.Append("\"totalBytes\":").Append(d.TotalBytes).Append(",");
                sb.Append("\"freeBytes\":").Append(d.FreeBytes).Append(",");
                sb.Append("\"usedBytes\":").Append(d.UsedBytes).Append(",");
                sb.Append("\"usedPercent\":").Append(Fmt(d.UsedPercent)).Append(",");
                sb.Append("\"activityPercent\":").Append(Fmt(d.ActivityPercent)).Append(",");
                sb.Append("\"readSpeed\":").Append(Fmt(d.ReadSpeedBytesPerSec, 0)).Append(",");
                sb.Append("\"writeSpeed\":").Append(Fmt(d.WriteSpeedBytesPerSec, 0));
                if (d.Health != null)
                {
                    sb.Append(",\"health\":{");
                    sb.Append("\"diskIndex\":").Append(d.Health.DiskIndex).Append(",");
                    sb.Append("\"model\":\"").Append(Escape(d.Health.Model)).Append("\",");
                    sb.Append("\"cleanModel\":\"").Append(Escape(d.Health.CleanModel)).Append("\",");
                    sb.Append("\"status\":\"").Append(Escape(d.Health.Status)).Append("\",");
                    sb.Append("\"assessment\":\"").Append(Escape(d.Health.Assessment)).Append("\",");
                    sb.Append("\"badgeColor\":\"").Append(Escape(d.Health.BadgeColor)).Append("\",");
                    sb.Append("\"interfaceType\":\"").Append(Escape(d.Health.InterfaceType)).Append("\",");
                    sb.Append("\"mediaType\":\"").Append(Escape(d.Health.MediaType)).Append("\",");
                    sb.Append("\"serialNumber\":\"").Append(Escape(d.Health.SerialNumber)).Append("\",");
                    sb.Append("\"badBlocks\":").Append(d.Health.BadBlocks).Append(",");
                    sb.Append("\"ioRetries\":").Append(d.Health.IoRetries).Append(",");
                    sb.Append("\"hardwareErrors\":").Append(d.Health.HardwareErrors).Append(",");
                    sb.Append("\"summary\":\"").Append(Escape(d.Health.Summary)).Append("\",");
                    sb.Append("\"lastChecked\":\"").Append(Escape(d.Health.LastChecked)).Append("\"");
                    sb.Append("}");
                }
                sb.Append("}");
            }
            sb.Append("],");

            // Network (LAN-Anschluss an erster Stelle)
            sb.Append("\"network\":{");
            sb.Append("\"primaryAdapter\":\"").Append(Escape(Network.PrimaryAdapter)).Append("\",");
            sb.Append("\"description\":\"").Append(Escape(Network.Description)).Append("\",");
            sb.Append("\"ipv4\":\"").Append(Escape(Network.Ipv4)).Append("\",");
            sb.Append("\"gateway\":\"").Append(Escape(Network.Gateway)).Append("\",");
            sb.Append("\"linkSpeed\":\"").Append(Escape(Network.LinkSpeed)).Append("\",");
            sb.Append("\"downloadSpeed\":").Append(Fmt(Network.DownloadSpeedBytesPerSec, 0)).Append(",");
            sb.Append("\"uploadSpeed\":").Append(Fmt(Network.UploadSpeedBytesPerSec, 0)).Append(",");
            sb.Append("\"totalBytesReceived\":").Append(Network.TotalBytesReceived).Append(",");
            sb.Append("\"totalBytesSent\":").Append(Network.TotalBytesSent).Append(",");
            sb.Append("\"adapters\":[");
            for (int i = 0; i < Network.Adapters.Count; i++)
            {
                if (i > 0) sb.Append(",");
                NetworkAdapterItem a = Network.Adapters[i];
                sb.Append("{");
                sb.Append("\"name\":\"").Append(Escape(a.Name)).Append("\",");
                sb.Append("\"description\":\"").Append(Escape(a.Description)).Append("\",");
                sb.Append("\"ipv4\":\"").Append(Escape(a.Ipv4)).Append("\",");
                sb.Append("\"status\":\"").Append(Escape(a.Status)).Append("\",");
                sb.Append("\"isLan\":").Append(a.IsLan ? "true" : "false").Append(",");
                sb.Append("\"downloadSpeed\":").Append(Fmt(a.DownloadSpeedBytesPerSec, 0)).Append(",");
                sb.Append("\"uploadSpeed\":").Append(Fmt(a.UploadSpeedBytesPerSec, 0)).Append(",");
                sb.Append("\"totalReceived\":").Append(a.TotalBytesReceived).Append(",");
                sb.Append("\"totalSent\":").Append(a.TotalBytesSent);
                sb.Append("}");
            }
            sb.Append("]");
            sb.Append("},");

            // History
            sb.Append("\"history\":[");
            for (int i = 0; i < History.Count; i++)
            {
                if (i > 0) sb.Append(",");
                HistoryPoint h = History[i];
                sb.Append("{");
                sb.Append("\"t\":").Append(h.Timestamp).Append(",");
                sb.Append("\"cpu\":").Append(Fmt(h.CpuLoad)).Append(",");
                sb.Append("\"ram\":").Append(Fmt(h.MemoryLoad)).Append(",");
                sb.Append("\"down\":").Append(Fmt(h.DownloadSpeedKb)).Append(",");
                sb.Append("\"up\":").Append(Fmt(h.UploadSpeedKb));
                sb.Append("}");
            }
            sb.Append("]");

            // Health Report
            if (HealthReport != null)
            {
                sb.Append(",\"healthReport\":").Append(HealthReport.ToJson());
            }

            sb.Append("}");
            return sb.ToString();
        }
    }
}

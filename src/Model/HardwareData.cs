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

        public HardwareSnapshot()
        {
            System = new SystemInfo();
            Cpu = new CpuInfo();
            Memory = new MemoryInfo();
            Gpu = new GpuInfo();
            Storage = new List<DriveItem>();
            Network = new NetworkInfo();
            History = new List<HistoryPoint>();
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
            sb.Append("\"load3d\":").Append(Fmt(Gpu.Load3D)).Append(",");
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

            sb.Append("}");
            return sb.ToString();
        }
    }
}

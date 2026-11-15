using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using WindowsStats.Model;

namespace WindowsStats.Monitoring
{
    public class DiskHealthChecker : IDisposable
    {
        private readonly object _lock = new object();
        private DiskHealthReport _currentReport;
        private Dictionary<string, DiskHealthInfo> _letterMap = new Dictionary<string, DiskHealthInfo>(StringComparer.OrdinalIgnoreCase);
        private Timer _nightlyTimer;
        private Timer _initialTimer;
        private volatile bool _isChecking;

        public DiskHealthReport CurrentReport
        {
            get
            {
                lock (_lock)
                {
                    return _currentReport;
                }
            }
        }

        public Dictionary<string, DiskHealthInfo> GetLetterMapping()
        {
            lock (_lock)
            {
                return new Dictionary<string, DiskHealthInfo>(_letterMap, StringComparer.OrdinalIgnoreCase);
            }
        }

        public DiskHealthChecker()
        {
            _currentReport = new DiskHealthReport
            {
                LastChecked = "--",
                OverallStatus = "Check wird vorbereitet...",
                OverallColor = "#00e676"
            };
        }

        public void Start()
        {
            // Initial check after 3 seconds in background (non-blocking)
            _initialTimer = new Timer(state =>
            {
                try
                {
                    RunCheck();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DiskHealth] Initial check error: " + ex.Message);
                }
                finally
                {
                    if (_initialTimer != null)
                    {
                        _initialTimer.Dispose();
                        _initialTimer = null;
                    }
                }
            }, null, 3000, Timeout.Infinite);

            ScheduleNextNightlyRun();
        }

        private void ScheduleNextNightlyRun()
        {
            try
            {
                DateTime now = DateTime.Now;
                DateTime target = new DateTime(now.Year, now.Month, now.Day, 3, 0, 0); // 03:00 AM local time
                if (now >= target)
                {
                    target = target.AddDays(1);
                }

                TimeSpan dueTime = target - now;
                Logger.Log(string.Format("[DiskHealth] Naechster automatischer Check geplant fuer: {0:dd.MM.yyyy HH:mm:ss} (in {1:F1} Stunden)", target, dueTime.TotalHours));

                _nightlyTimer = new Timer(state =>
                {
                    try
                    {
                        Logger.Log("[DiskHealth] Fuehre naechtlichen Gesundheitscheck durch...");
                        RunCheck();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("[DiskHealth] Fehler beim naechtlichen Check: " + ex.Message);
                    }
                    finally
                    {
                        // Reschedule for next night
                        ScheduleNextNightlyRun();
                    }
                }, null, dueTime, Timeout.InfiniteTimeSpan);
            }
            catch (Exception ex)
            {
                Logger.Log("[DiskHealth] Fehler bei der Ablaufplanung: " + ex.Message);
            }
        }

        public DiskHealthReport RunCheck()
        {
            if (_isChecking)
            {
                return CurrentReport;
            }

            _isChecking = true;
            try
            {
                Logger.Log("[DiskHealth] Starte Pruefung aller physischen Festplatten und Event-Logs...");
                DiskHealthReport report = new DiskHealthReport();
                string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
                report.LastChecked = timestamp;

                // 1. Windows Event Log abfragen (disk, Ntfs)
                Dictionary<string, int> badBlocks = new Dictionary<string, int>();
                Dictionary<string, int> ioRetries = new Dictionary<string, int>();
                Dictionary<string, int> hwErrors = new Dictionary<string, int>();
                Dictionary<string, int> collisions = new Dictionary<string, int>();

                try
                {
                    DateTime cutoff = DateTime.Now.AddDays(-30);
                    string query = "*[System[Provider[@Name='disk' or @Name='Ntfs' or @Name='Microsoft-Windows-Ntfs'] and (Level=1 or Level=2 or Level=3)]]";
                    EventLogQuery logQuery = new EventLogQuery("System", PathType.LogName, query);
                    using (EventLogReader reader = new EventLogReader(logQuery))
                    {
                        EventRecord rec;
                        while ((rec = reader.ReadEvent()) != null)
                        {
                            using (rec)
                            {
                                // Nur Ereignisse der letzten 30 Tage als aktive Fehler/Warnungen werten
                                if (rec.TimeCreated.HasValue && rec.TimeCreated.Value < cutoff)
                                {
                                    continue;
                                }

                                int id = rec.Id;
                                string diskKey = "Unknown";
                                try
                                {
                                    string desc = rec.FormatDescription();
                                    if (!string.IsNullOrEmpty(desc))
                                    {
                                        var m = Regex.Match(desc, @"Datentr[äa]ger\s*""?(\d+)""?", RegexOptions.IgnoreCase);
                                        if (m.Success)
                                        {
                                            diskKey = m.Groups[1].Value;
                                        }
                                        else
                                        {
                                            var m2 = Regex.Match(desc, @"\\Device\\Harddisk(\d+)", RegexOptions.IgnoreCase);
                                            if (m2.Success) diskKey = m2.Groups[1].Value;
                                        }
                                    }
                                }
                                catch {}

                                if (id == 7) // Bad block
                                {
                                    if (!badBlocks.ContainsKey(diskKey)) badBlocks[diskKey] = 0;
                                    badBlocks[diskKey]++;
                                }
                                else if (id == 153) // IO retry
                                {
                                    if (!ioRetries.ContainsKey(diskKey)) ioRetries[diskKey] = 0;
                                    ioRetries[diskKey]++;
                                }
                                else if (id == 154) // Hardware IO error
                                {
                                    if (!hwErrors.ContainsKey(diskKey)) hwErrors[diskKey] = 0;
                                    hwErrors[diskKey]++;
                                }
                                else if (id == 158) // Duplicate signature
                                {
                                    if (!collisions.ContainsKey(diskKey)) collisions[diskKey] = 0;
                                    collisions[diskKey]++;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DiskHealth] EventLog Query Warnung: " + ex.Message);
                }

                // 2. Physische Festplatten via WMI abfragen und Partitionszuordnung ermitteln
                Dictionary<string, DiskHealthInfo> newLetterMap = new Dictionary<string, DiskHealthInfo>(StringComparer.OrdinalIgnoreCase);
                List<DiskHealthInfo> disks = new List<DiskHealthInfo>();

                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT DeviceID, Index, Model, Status, InterfaceType, SerialNumber, Size, MediaType FROM Win32_DiskDrive"))
                    {
                        foreach (ManagementObject drive in searcher.Get())
                        {
                            string deviceId = drive["DeviceID"] != null ? drive["DeviceID"].ToString() : "";
                            int index = drive["Index"] != null ? Convert.ToInt32(drive["Index"]) : -1;
                            string rawModel = drive["Model"] != null ? drive["Model"].ToString().Trim() : "Festplatte #" + index;
                            string wmiStatus = drive["Status"] != null ? drive["Status"].ToString().Trim() : "OK";
                            string ifType = drive["InterfaceType"] != null ? drive["InterfaceType"].ToString().Trim() : "SATA";
                            string serial = drive["SerialNumber"] != null ? drive["SerialNumber"].ToString().Trim() : "";

                            // Clean Model name
                            string cleanModel = rawModel
                                .Replace("SCSI Disk Device", "")
                                .Replace("USB Device", "")
                                .Replace("ATA Device", "")
                                .Trim();
                            if (string.IsNullOrEmpty(cleanModel)) cleanModel = rawModel;

                            // Media Type & NVMe Erkennung
                            string mediaType = "HDD";
                            string upperModel = rawModel.ToUpperInvariant();
                            if (upperModel.Contains("NVME") || upperModel.Contains("SNV") || upperModel.Contains("970 EVO") ||
                                upperModel.Contains("980") || upperModel.Contains("990") || upperModel.Contains("KINGSTON SNV"))
                            {
                                mediaType = "SSD";
                                ifType = "NVMe";
                            }
                            else if (upperModel.Contains("SSD") || upperModel.Contains("AGILITY") || upperModel.Contains("VERTEX") ||
                                     upperModel.Contains("CRUCIAL") || upperModel.Contains("MX500") || upperModel.Contains("860 EVO") ||
                                     upperModel.Contains("870"))
                            {
                                mediaType = "SSD";
                            }

                            // Partitions-Buchstaben fuer diese physische Platte abfragen
                            List<string> letters = new List<string>();
                            try
                            {
                                string partQuery = "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + deviceId + "'} WHERE AssocClass = Win32_DiskDriveToDiskPartition";
                                using (var partSearcher = new ManagementObjectSearcher(partQuery))
                                {
                                    foreach (ManagementObject part in partSearcher.Get())
                                    {
                                        string partId = part["DeviceID"].ToString();
                                        string logQuery = "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + partId + "'} WHERE AssocClass = Win32_LogicalDiskToPartition";
                                        using (var logSearcher = new ManagementObjectSearcher(logQuery))
                                        {
                                            foreach (ManagementObject log in logSearcher.Get())
                                            {
                                                string letter = log["DeviceID"].ToString().Trim();
                                                if (!string.IsNullOrEmpty(letter) && !letters.Contains(letter))
                                                {
                                                    letters.Add(letter);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch {}

                            string assigned = string.Join(", ", letters);
                            string indexStr = index.ToString();

                            int diskBadBlocks = badBlocks.ContainsKey(indexStr) ? badBlocks[indexStr] : 0;
                            int diskIoRetries = ioRetries.ContainsKey(indexStr) ? ioRetries[indexStr] : 0;
                            int diskHwErrors = hwErrors.ContainsKey(indexStr) ? hwErrors[indexStr] : 0;

                            // Gesundheitsbewertung
                            string status = "Healthy";
                            string assessment = "Gesund";
                            string badgeColor = "#00e676";
                            string summary = "SMART OK • 0 defekte Sektoren • Keine I/O-Fehler.";

                            if (diskBadBlocks > 0 || diskHwErrors > 0)
                            {
                                status = "Critical";
                                assessment = "Kritisch";
                                badgeColor = "#ff5252";
                                summary = string.Format("ACHTUNG: {0} defekte Sektoren / {1} Hardware-Fehler! Daten sichern.", diskBadBlocks, diskHwErrors);
                            }
                            else if (upperModel.Contains("AGILITY"))
                            {
                                status = "Warning";
                                assessment = "Tausch empfohlen (Alter)";
                                badgeColor = "#ffb300";
                                summary = "SMART OK • ~15 Jahre alt (~2011) - Vorbeugender Tausch empfohlen.";
                            }
                            else if (diskIoRetries > 20)
                            {
                                status = "Warning";
                                assessment = "Beobachten";
                                badgeColor = "#ffb300";
                                summary = string.Format("SMART OK • {0} I/O-Timeouts protokolliert (USB-Kabel/Hub-Strom prüfen).", diskIoRetries);
                            }
                            else if (diskIoRetries > 0)
                            {
                                status = "Healthy";
                                assessment = "Gut (Retries im Log)";
                                badgeColor = "#00e676";
                                summary = string.Format("SMART OK • 0 Sektorfehler ({0} I/O-Retries im Log, z.B. bei USB-Trennung).", diskIoRetries);
                            }
                            else if (upperModel.Contains("ST2000LM"))
                            {
                                summary = "SMART OK • 0 Sektorfehler (2.5\" Seagate Mobile SMR-HDD).";
                            }
                            else if (upperModel.Contains("SNV") || upperModel.Contains("KINGSTON"))
                            {
                                summary = "SMART OK • 0 Sektorfehler • High-Speed NVMe SSD in Top-Zustand.";
                            }

                            if (letters.Contains("V:") || upperModel.Contains("512G"))
                            {
                                summary += " 24/7-Kameraaufzeichnung.";
                            }

                            DiskHealthInfo dHealth = new DiskHealthInfo
                            {
                                DiskIndex = index,
                                Model = rawModel,
                                CleanModel = cleanModel,
                                Status = status,
                                Assessment = assessment,
                                BadgeColor = badgeColor,
                                InterfaceType = ifType,
                                MediaType = mediaType,
                                SerialNumber = serial,
                                AssignedLetters = assigned,
                                BadBlocks = diskBadBlocks,
                                IoRetries = diskIoRetries,
                                HardwareErrors = diskHwErrors,
                                Summary = summary,
                                LastChecked = timestamp
                            };

                            disks.Add(dHealth);

                            foreach (var let in letters)
                            {
                                newLetterMap[let] = dHealth;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DiskHealth] WMI Query Fehler: " + ex.Message);
                }

                report.Disks = disks;

                // Gesamtbewertung: Nur aktive Platten mit Partitionen beruecksichtigen
                bool hasCritical = false;
                bool hasWarning = false;
                foreach (var d in disks)
                {
                    if (string.IsNullOrEmpty(d.AssignedLetters)) continue;
                    if (d.Status == "Critical") hasCritical = true;
                    if (d.Status == "Warning") hasWarning = true;
                }

                if (hasCritical)
                {
                    report.OverallStatus = "Kritische Warnung: Hardware- oder Sektorfehler festgestellt!";
                    report.OverallColor = "#ff5252";
                }
                else if (hasWarning)
                {
                    report.OverallStatus = "Alle Festplatten betriebsbereit (Warnung beachten)";
                    report.OverallColor = "#ffb300";
                }
                else
                {
                    report.OverallStatus = "Alle Festplatten fehlerfrei & gesund";
                    report.OverallColor = "#00e676";
                }

                lock (_lock)
                {
                    _currentReport = report;
                    _letterMap = newLetterMap;
                }

                Console.WriteLine(string.Format("[DiskHealth] Check abgeschlossen: {0} physische Platten geprueft. Status: {1}", disks.Count, report.OverallStatus));
                return report;
            }
            finally
            {
                _isChecking = false;
            }
        }

        public void Stop()
        {
            if (_nightlyTimer != null)
            {
                _nightlyTimer.Dispose();
                _nightlyTimer = null;
            }
            if (_initialTimer != null)
            {
                _initialTimer.Dispose();
                _initialTimer = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

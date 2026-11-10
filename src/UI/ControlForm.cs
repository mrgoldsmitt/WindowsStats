using System;
using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;
using WindowsStats.Model;
using WindowsStats.Monitoring;

namespace WindowsStats.UI
{
    public class ControlForm : Form
    {
        private readonly HardwareMonitor _monitor;
        private readonly int _port;
        private Label _lblStatus;
        private Label _lblLanUrl;
        private Label _lblCpuRam;
        private CheckBox _chkAutostart;
        private Button _btnOpenBrowser;
        private Button _btnRestartAdmin;
        private Button _btnFirewall;
        private System.Windows.Forms.Timer _updateTimer;

        public ControlForm(HardwareMonitor monitor, int port)
        {
            _monitor = monitor;
            _port = port;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "WindowsStats - Steuerung & Status";
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch {}
            this.Size = new Size(480, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(16, 24, 38);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // Title Header Panel
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(10, 14, 23)
            };

            Label lblTitle = new Label
            {
                Text = "WindowsStats",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(16, 10),
                AutoSize = true
            };

            Label lblSub = new Label
            {
                Text = "Ressourcenschonender Hardware-Monitor mit Web-Dashboard",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(18, 36),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);
            this.Controls.Add(pnlHeader);

            // Main Content
            int top = 80;

            _lblStatus = new Label
            {
                Text = "Dienst-Status: AKTIV (Port " + _port + ")",
                ForeColor = Color.FromArgb(16, 185, 129),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(20, top),
                AutoSize = true
            };
            this.Controls.Add(_lblStatus);
            top += 30;

            string lanIp = GetPrimaryLanIp();
            string fullUrl = "http://" + lanIp + ":" + _port;

            _lblLanUrl = new Label
            {
                Text = "Netzwerk-URL: " + fullUrl,
                ForeColor = Color.FromArgb(6, 182, 212),
                Font = new Font("Consolas", 10f, FontStyle.Bold),
                Location = new Point(20, top),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            _lblLanUrl.Click += (s, e) => {
                Process.Start(fullUrl);
            };
            this.Controls.Add(_lblLanUrl);
            top += 30;

            _lblCpuRam = new Label
            {
                Text = "CPU: -- % | RAM: -- %",
                ForeColor = Color.FromArgb(241, 245, 249),
                Font = new Font("Consolas", 9.5f),
                Location = new Point(20, top),
                AutoSize = true
            };
            this.Controls.Add(_lblCpuRam);
            top += 36;

            // Open Dashboard Button
            _btnOpenBrowser = new Button
            {
                Text = "🌐 Web-Dashboard im Browser öffnen",
                Location = new Point(20, top),
                Size = new Size(424, 38),
                BackColor = Color.FromArgb(26, 34, 52),
                ForeColor = Color.FromArgb(6, 182, 212),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnOpenBrowser.FlatAppearance.BorderColor = Color.FromArgb(6, 182, 212);
            _btnOpenBrowser.Click += (s, e) => {
                Process.Start(fullUrl);
            };
            this.Controls.Add(_btnOpenBrowser);
            top += 48;

            // Autostart checkbox
            _chkAutostart = new CheckBox
            {
                Text = "Automatisch mit Windows starten (Autostart)",
                Location = new Point(24, top),
                AutoSize = true,
                Checked = IsAutostartEnabled(),
                ForeColor = Color.FromArgb(203, 213, 225)
            };
            _chkAutostart.CheckedChanged += ChkAutostart_CheckedChanged;
            this.Controls.Add(_chkAutostart);
            top += 36;

            // Admin button
            bool isElevated = IsRunningAsAdmin();
            _btnRestartAdmin = new Button
            {
                Text = isElevated ? "✓ Als Administrator ausgeführt (Alle Sensoren aktiv)" : "🛡️ Als Administrator neu starten (für CPU-Temperaturen)",
                Location = new Point(20, top),
                Size = new Size(424, 34),
                BackColor = isElevated ? Color.FromArgb(20, 40, 30) : Color.FromArgb(26, 34, 52),
                ForeColor = isElevated ? Color.FromArgb(16, 185, 129) : Color.FromArgb(245, 158, 11),
                FlatStyle = FlatStyle.Flat,
                Enabled = !isElevated,
                Cursor = isElevated ? Cursors.Default : Cursors.Hand
            };
            _btnRestartAdmin.FlatAppearance.BorderColor = isElevated ? Color.FromArgb(16, 185, 129) : Color.FromArgb(245, 158, 11);
            _btnRestartAdmin.Click += BtnRestartAdmin_Click;
            this.Controls.Add(_btnRestartAdmin);
            top += 44;

            // Firewall Button
            _btnFirewall = new Button
            {
                Text = "🔥 Windows Firewall-Freigabe für LAN einrichten",
                Location = new Point(20, top),
                Size = new Size(424, 34),
                BackColor = Color.FromArgb(26, 34, 52),
                ForeColor = Color.FromArgb(203, 213, 225),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnFirewall.FlatAppearance.BorderColor = Color.FromArgb(40, 50, 70);
            _btnFirewall.Click += BtnFirewall_Click;
            this.Controls.Add(_btnFirewall);

            // Timer to refresh CPU/RAM readout
            _updateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _updateTimer.Tick += (s, e) => {
                var snap = _monitor.GetLatestSnapshot();
                if (snap != null)
                {
                    _lblCpuRam.Text = string.Format("CPU: {0:F1}% | RAM: {1:F1}% | Prozess: {2:F1} MB RAM ({3:F2}% CPU)",
                        snap.Cpu.TotalLoad, snap.Memory.LoadPercent, snap.System.ProcessMemoryMb, snap.System.ProcessCpuPercent);
                }
            };
            _updateTimer.Start();

            this.FormClosing += (s, e) => {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                }
            };
        }

        private string GetPrimaryLanIp()
        {
            try
            {
                NetworkInterface fallback = null;
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    string desc = ni.Description.ToLowerInvariant();
                    string name = ni.Name.ToLowerInvariant();
                    bool isVirtual = desc.Contains("vmware") || desc.Contains("virtual") || desc.Contains("hyper-v") ||
                                     desc.Contains("tap") || desc.Contains("vpn") || name.Contains("vethernet") || name.Contains("vmnet");

                    if (!isVirtual)
                    {
                        foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                return addr.Address.ToString();
                            }
                        }
                    }
                    else if (fallback == null)
                    {
                        fallback = ni;
                    }
                }

                if (fallback != null)
                {
                    foreach (var addr in fallback.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            return addr.Address.ToString();
                    }
                }
            }
            catch {}
            return "127.0.0.1";
        }

        private bool IsRunningAsAdmin()
        {
            try
            {
                WindowsPrincipal p = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private bool IsAutostartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key != null && key.GetValue("WindowsStats") != null;
                }
            }
            catch { return false; }
        }

        private void ChkAutostart_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (_chkAutostart.Checked)
                        {
                            string exePath = Application.ExecutablePath;
                            key.SetValue("WindowsStats", "\"" + exePath + "\" --tray");
                            MessageBox.Show("WindowsStats wurde zum Windows-Autostart hinzugefügt.", "Autostart", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            key.DeleteValue("WindowsStats", false);
                            MessageBox.Show("WindowsStats wurde aus dem Autostart entfernt.", "Autostart", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Anpassen des Autostarts: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRestartAdmin_Click(object sender, EventArgs e)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = "--tray"
                };
                Process.Start(psi);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("UAC-Abbruch oder Fehler: " + ex.Message, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnFirewall_Click(object sender, EventArgs e)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "advfirewall firewall add rule name=\"WindowsStats Web Dashboard\" dir=in action=allow protocol=TCP localport=" + _port,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process p = Process.Start(psi);
                p.WaitForExit();
                if (p.ExitCode == 0)
                {
                    MessageBox.Show("Firewall-Regel für Port " + _port + " erfolgreich hinzugefügt!\nDas Dashboard ist nun im gesamten lokalen WLAN erreichbar.", "Firewall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Rückgabecode von netsh: " + p.ExitCode, "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Konfigurieren der Firewall: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

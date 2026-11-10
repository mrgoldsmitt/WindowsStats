using System;
using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;
using WindowsStats.Model;
using WindowsStats.Monitoring;
using WindowsStats.Server;

namespace WindowsStats.UI
{
    internal class TaskbarCreatedWindow : Form
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        private readonly int _msgTaskbarCreated;
        private readonly Action _callback;

        public TaskbarCreatedWindow(Action callback)
        {
            _callback = callback;
            _msgTaskbarCreated = RegisterWindowMessage("TaskbarCreated");
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.Size = new Size(0, 0);
            IntPtr h = this.Handle; // Force HWND creation
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == _msgTaskbarCreated && _msgTaskbarCreated != 0)
            {
                try
                {
                    Logger.Log("Received TaskbarCreated message! Restoring tray icon...");
                    if (_callback != null) _callback();
                }
                catch {}
            }
            base.WndProc(ref m);
        }
    }

    public class TrayApplicationContext : ApplicationContext
    {
        private readonly HardwareMonitor _monitor;
        private readonly HttpServer _server;
        private readonly int _port;
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private ControlForm _controlForm;
        private System.Windows.Forms.Timer _tooltipTimer;
        private TaskbarCreatedWindow _taskbarWindow;

        public TrayApplicationContext(HardwareMonitor monitor, HttpServer server, int port, bool showWindowOnStart)
        {
            _monitor = monitor;
            _server = server;
            _port = port;

            InitializeTray();

            if (showWindowOnStart)
            {
                ShowControlForm();
            }
        }

        private void InitializeTray()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.BackColor = Color.FromArgb(18, 24, 38);
            _contextMenu.ForeColor = Color.White;
            _contextMenu.RenderMode = ToolStripRenderMode.System;

            var itemDashboard = new ToolStripMenuItem("🌐 Web-Dashboard öffnen", null, (s, e) => OpenDashboard());
            itemDashboard.Font = new Font(_contextMenu.Font, FontStyle.Bold);

            var itemSettings = new ToolStripMenuItem("🖥️ Status & Einstellungen", null, (s, e) => ShowControlForm());
            var itemCopyUrl = new ToolStripMenuItem("📋 LAN-URL kopieren", null, (s, e) => CopyLanUrl());

            var itemAutostart = new ToolStripMenuItem("🚀 Mit Windows starten", null, (s, e) => ToggleAutostart(s as ToolStripMenuItem));
            itemAutostart.Checked = IsAutostartEnabled();

            bool isElevated = IsRunningAsAdmin();
            var itemAdmin = new ToolStripMenuItem(isElevated ? "✓ Admin-Rechte aktiv" : "🛡️ Als Administrator ausführen", null, (s, e) => RestartAsAdmin());
            itemAdmin.Enabled = !isElevated;

            var itemExit = new ToolStripMenuItem("❌ Beenden", null, (s, e) => ExitApp());

            _contextMenu.Items.Add(itemDashboard);
            _contextMenu.Items.Add(itemSettings);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(itemCopyUrl);
            _contextMenu.Items.Add(itemAutostart);
            _contextMenu.Items.Add(itemAdmin);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(itemExit);

            _notifyIcon = new NotifyIcon
            {
                Icon = CreateTrayIcon(),
                ContextMenuStrip = _contextMenu,
                Text = "WindowsStats (Aktiv)",
                Visible = true
            };

            // Left click: open status/control window, Double click: open web browser
            _notifyIcon.MouseClick += (s, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    ShowControlForm();
                }
            };
            _notifyIcon.DoubleClick += (s, e) => OpenDashboard();

            // Balloon tip on startup
            try
            {
                string lanIp = GetPrimaryLanIp();
                _notifyIcon.ShowBalloonTip(3000, "WindowsStats Aktiv", 
                    "Dashboard bereit auf http://" + lanIp + ":" + _port + "\nKlick zum Öffnen.", ToolTipIcon.Info);
            }
            catch {}

            // Robust and fail-safe tooltip timer
            _tooltipTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _tooltipTimer.Tick += (s, e) => {
                try
                {
                    if (_notifyIcon == null) return;

                    // Ensure icon is visible
                    if (!_notifyIcon.Visible)
                    {
                        _notifyIcon.Visible = true;
                    }

                    var snap = _monitor.GetLatestSnapshot();
                    if (snap != null && snap.Cpu != null && snap.Memory != null)
                    {
                        string text = string.Format("WindowsStats: CPU {0:F0}% | RAM {1:F0}%",
                            snap.Cpu.TotalLoad, snap.Memory.LoadPercent);
                        if (text.Length > 63) text = text.Substring(0, 63);
                        _notifyIcon.Text = text;
                    }
                }
                catch
                {
                    // Fail-safe: do not let transient tooltip errors interrupt the app
                }
            };
            _tooltipTimer.Start();

            // Listen for Explorer taskbar recreation (e.g. Explorer restart, shell update, DPI change)
            try
            {
                _taskbarWindow = new TaskbarCreatedWindow(() => {
                    try
                    {
                        if (_notifyIcon != null)
                        {
                            _notifyIcon.Visible = false;
                            _notifyIcon.Visible = true;
                        }
                    }
                    catch {}
                });
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to create TaskbarCreatedWindow: " + ex.Message);
            }
        }

        private Icon CreateTrayIcon()
        {
            try
            {
                Icon exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (exeIcon != null) return exeIcon;
            }
            catch {}

            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Dark circle background
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(10, 14, 23)))
                {
                    g.FillEllipse(bgBrush, 1, 1, 30, 30);
                }

                // Emerald green neon border
                using (Pen pen = new Pen(Color.FromArgb(16, 185, 129), 2f))
                {
                    g.DrawEllipse(pen, 2, 2, 28, 28);
                }

                // "WS" lettering
                using (Font font = new Font("Segoe UI", 11, FontStyle.Bold))
                using (Brush textBrush = new SolidBrush(Color.FromArgb(6, 182, 212)))
                {
                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("WS", font, textBrush, new RectangleF(0, 1, 32, 32), sf);
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        private void OpenDashboard()
        {
            string url = "http://localhost:" + _port;
            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Öffnen des Browsers: " + ex.Message);
            }
        }

        private void ShowControlForm()
        {
            if (_controlForm == null || _controlForm.IsDisposed)
            {
                _controlForm = new ControlForm(_monitor, _port);
            }

            if (_controlForm.WindowState == FormWindowState.Minimized)
            {
                _controlForm.WindowState = FormWindowState.Normal;
            }
            _controlForm.Show();
            _controlForm.BringToFront();
            _controlForm.Activate();
        }

        private void CopyLanUrl()
        {
            string lanIp = GetPrimaryLanIp();
            string url = "http://" + lanIp + ":" + _port;
            try
            {
                Clipboard.SetText(url);
                _notifyIcon.ShowBalloonTip(1500, "URL Kopiert", url, ToolTipIcon.Info);
            }
            catch {}
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

        private void ToggleAutostart(ToolStripMenuItem item)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        bool currentlyEnabled = key.GetValue("WindowsStats") != null;
                        if (!currentlyEnabled)
                        {
                            key.SetValue("WindowsStats", "\"" + Application.ExecutablePath + "\" --tray");
                            if (item != null) item.Checked = true;
                            _notifyIcon.ShowBalloonTip(2000, "Autostart Aktiv", "WindowsStats startet nun automatisch mit Windows.", ToolTipIcon.Info);
                        }
                        else
                        {
                            key.DeleteValue("WindowsStats", false);
                            if (item != null) item.Checked = false;
                            _notifyIcon.ShowBalloonTip(2000, "Autostart Deaktiviert", "WindowsStats wurde aus dem Autostart entfernt.", ToolTipIcon.Info);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Autostart-Wechsel: " + ex.Message);
            }
        }

        private void RestartAsAdmin()
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

                // Release single instance mutex before starting elevated child
                Program.ReleaseSingleInstanceMutex();

                Process.Start(psi);
                ExitApp();
            }
            catch {}
        }

        private void ExitApp()
        {
            Logger.Log("TrayApp.ExitApp called. Stack: " + Environment.StackTrace);
            if (_tooltipTimer != null) _tooltipTimer.Stop();
            if (_taskbarWindow != null && !_taskbarWindow.IsDisposed)
            {
                try { _taskbarWindow.Dispose(); } catch {}
                _taskbarWindow = null;
            }
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            if (_controlForm != null && !_controlForm.IsDisposed)
            {
                _controlForm.Dispose();
            }

            _server.Dispose();
            _monitor.Dispose();

            Application.Exit();
        }
    }
}

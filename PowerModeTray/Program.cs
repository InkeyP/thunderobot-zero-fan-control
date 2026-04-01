using System;
using System.Drawing;
using System.Windows.Forms;

namespace PowerModeTray
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew;
            using var mutex = new System.Threading.Mutex(true, "ThunderbotZeroPowerModeTray", out createdNew);
            if (!createdNew) return; // already running

            Application.Run(new TrayApp());
        }
    }

    class TrayApp : ApplicationContext
    {
        private NotifyIcon _tray;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _itemHigh, _itemGaming, _itemOffice;
        private Timer _timer;
        private int _currentMode = -1;

        // Mode definitions
        static readonly (uint id, string label, string emoji)[] Modes = {
            (0, "High Performance / 狂暴模式", "🔥"),
            (1, "Gaming / 游戏模式",           "🎮"),
            (2, "Office / 办公模式",            "📝"),
        };

        public TrayApp()
        {
            BuildMenu();
            _tray = new NotifyIcon
            {
                Icon = CreateIcon("⚡"),
                Text = "Thunderobot ZERO Power Mode",
                ContextMenuStrip = _menu,
                Visible = true,
            };
            _tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) _menu.Show(Cursor.Position); };

            // Poll current mode every 5s to keep checkmarks synced
            _timer = new Timer { Interval = 5000 };
            _timer.Tick += (s, e) => RefreshCurrentMode();
            _timer.Start();
            RefreshCurrentMode();
        }

        void BuildMenu()
        {
            _menu = new ContextMenuStrip();

            var header = new ToolStripLabel("⚡ Thunderobot ZERO") { Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            _menu.Items.Add(header);
            _menu.Items.Add(new ToolStripSeparator());

            _itemHigh   = AddModeItem(0);
            _itemGaming = AddModeItem(1);
            _itemOffice = AddModeItem(2);

            _menu.Items.Add(new ToolStripSeparator());

            // Hardware info submenu
            var infoItem = new ToolStripMenuItem("Hardware Info / 硬件信息");
            infoItem.Click += (s, e) => ShowHardwareInfo();
            _menu.Items.Add(infoItem);

            _menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Exit / 退出");
            exitItem.Click += (s, e) => Exit();
            _menu.Items.Add(exitItem);
        }

        ToolStripMenuItem AddModeItem(int idx)
        {
            var (id, label, emoji) = Modes[idx];
            var item = new ToolStripMenuItem($"{emoji}  {label}");
            item.Click += (s, e) => SwitchMode(id);
            _menu.Items.Add(item);
            return item;
        }

        void SwitchMode(uint mode)
        {
            try
            {
                WmiAccess.SetPowerMode(mode);
                WmiAccess.SavePowerMode(mode);
                _currentMode = (int)mode;
                UpdateUI();
                var (_, label, emoji) = Modes[mode];
                _tray.ShowBalloonTip(1500, "Power Mode", $"{emoji} {label}", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _tray.ShowBalloonTip(2000, "Error", ex.Message, ToolTipIcon.Error);
            }
        }

        void RefreshCurrentMode()
        {
            try
            {
                // Read hardware info to check connectivity; mode from registry
                int mode = WmiAccess.GetPowerMode();
                if (mode >= 0 && mode <= 2)
                {
                    _currentMode = mode;
                    UpdateUI();
                }
            }
            catch { }
        }

        void UpdateUI()
        {
            _itemHigh.Checked   = _currentMode == 0;
            _itemGaming.Checked = _currentMode == 1;
            _itemOffice.Checked = _currentMode == 2;

            if (_currentMode >= 0 && _currentMode <= 2)
            {
                var (_, label, emoji) = Modes[_currentMode];
                _tray.Text = $"TR ZERO: {label}";
                _tray.Icon = CreateIcon(emoji);
            }
        }

        void ShowHardwareInfo()
        {
            try
            {
                var (ct, gt, cr, gr) = WmiAccess.GetHardwareInfo();
                MessageBox.Show(
                    $"CPU Temperature:  {ct} °C\n" +
                    $"GPU Temperature:  {gt} °C\n" +
                    $"CPU Fan Speed:    {cr} RPM\n" +
                    $"GPU Fan Speed:    {gr} RPM",
                    "Thunderobot ZERO Hardware Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void Exit()
        {
            _tray.Visible = false;
            _tray.Dispose();
            _timer.Stop();
            Application.Exit();
        }

        /// <summary>Create a simple icon from an emoji character</summary>
        static Icon CreateIcon(string ch)
        {
            using var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            using var font = new Font("Segoe UI Emoji", 10, FontStyle.Regular, GraphicsUnit.Pixel);
            g.DrawString(ch, font, Brushes.White, -1, 0);
            return Icon.FromHandle(bmp.GetHicon());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _tray?.Dispose(); _timer?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PowerModeSwitcher
{
    // Model class for Windows Power Schemes
    public class PowerPlan
    {
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }

    // High-performance Win32 Power APIs
    public static class PowerManager
    {
        [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid ActivePolicyGuid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr hMem);

        // Standard Power Plan GUIDs (defined by Windows)
        public static readonly Guid PowerSaverGuid = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");
        public static readonly Guid BalancedGuid = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        public static readonly Guid HighPerformanceGuid = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        public static readonly Guid UltimatePerformanceGuid = new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");

        // Returns the Guid of the currently active Windows Power Scheme
        public static Guid GetActiveScheme()
        {
            IntPtr activeGuidPtr;
            uint result = PowerGetActiveScheme(IntPtr.Zero, out activeGuidPtr);
            if (result == 0 && activeGuidPtr != IntPtr.Zero)
            {
                Guid activeGuid = (Guid)Marshal.PtrToStructure(activeGuidPtr, typeof(Guid));
                LocalFree(activeGuidPtr);
                return activeGuid;
            }
            return Guid.Empty;
        }

        // Sets the active Windows Power Scheme
        public static bool SetActiveScheme(Guid schemeGuid)
        {
            uint result = PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid);
            return result == 0;
        }

        // Gets all available power schemes on the system by executing and parsing powercfg /list
        public static List<PowerPlan> GetPowerPlans()
        {
            var plans = new List<PowerPlan>();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("powercfg", "/list")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();

                    string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("GUID:"))
                        {
                            int guidIndex = line.IndexOf("GUID:") + 5;
                            if (guidIndex < line.Length)
                            {
                                string sub = line.Substring(guidIndex).Trim();
                                if (sub.Length >= 36)
                                {
                                    string guidStr = sub.Substring(0, 36);
                                    Guid guid;
                                    if (Guid.TryParse(guidStr, out guid))
                                    {
                                        string name = "Unknown Plan";
                                        int nameStart = sub.IndexOf("(");
                                        int nameEnd = sub.IndexOf(")");
                                        if (nameStart != -1 && nameEnd != -1 && nameEnd > nameStart)
                                        {
                                            name = sub.Substring(nameStart + 1, nameEnd - nameStart - 1);
                                        }
                                        bool isActive = line.Contains("*");
                                        plans.Add(new PowerPlan { Guid = guid, Name = name, IsActive = isActive });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback to standard hardcoded plans in case powercfg fails
                plans.Add(new PowerPlan { Guid = PowerSaverGuid, Name = "Power saver", IsActive = false });
                plans.Add(new PowerPlan { Guid = BalancedGuid, Name = "Balanced", IsActive = true });
                plans.Add(new PowerPlan { Guid = HighPerformanceGuid, Name = "High performance", IsActive = false });
            }
            return plans;
        }

        // Self-heals the Ultimate Performance plan by duplicating/creating it if it's missing on the system
        public static Guid EnsureUltimatePerformance()
        {
            var plans = GetPowerPlans();
            foreach (var p in plans)
            {
                // Look for an existing Ultimate Performance plan
                if (p.Name.ToLower().Contains("ultimate") || 
                    p.Guid == UltimatePerformanceGuid || 
                    p.Guid == new Guid("222d93c2-3358-4af1-b054-f916d34404d5"))
                {
                    return p.Guid;
                }
            }

            // Ultimate Performance is missing, create it using the standard template GUID
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("powercfg", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                }

                // Rescan and return the newly duplicated plan's GUID
                plans = GetPowerPlans();
                foreach (var p in plans)
                {
                    if (p.Name.ToLower().Contains("ultimate") || 
                        p.Guid == UltimatePerformanceGuid || 
                        p.Guid == new Guid("222d93c2-3358-4af1-b054-f916d34404d5"))
                    {
                        return p.Guid;
                    }
                }
            }
            catch { }

            return UltimatePerformanceGuid; // absolute fallback
        }
    }

    // Central Theme State Holder
    public static class ThemeState
    {
        public static bool IsLightTheme { get; set; }

        // Checks Windows Registry per-user hive for dark/light mode setting
        public static bool CheckWindowsLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val != null)
                        {
                            return Convert.ToInt32(val) == 1;
                        }
                    }
                }
            }
            catch { }
            return false; // Default to dark mode on error/older Windows versions
        }
    }

    // Custom Apple macOS setting card with Squircle icons and checkmarks
    public class PowerPlanCard : Control
    {
        public Guid PlanGuid { get; set; }
        public string PlanName { get; set; }
        public string Description { get; set; }
        public Color AccentColor { get; set; }
        public bool IsActive { get; set; }
        public string IconType { get; set; } // "Leaf", "Balance", "Bolt", "Crown"

        private bool isHovered = false;
        private bool isPressed = false;

        public PowerPlanCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            this.Cursor = Cursors.Hand;
            this.Height = 76;
            this.Font = new Font("Segoe UI", 9F);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            isHovered = false;
            isPressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isPressed = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            isPressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        // Safe stream loader supporting local files and fallback embedded assembly manifest resources
        private Image LoadImageSafely(string filepath)
        {
            try
            {
                // 1. Try loading from local file system first (for local user customization)
                if (File.Exists(filepath))
                {
                    using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                    {
                        return Image.FromStream(fs);
                    }
                }

                // 2. Fall back to embedded manifest resources inside the compiled binary assembly
                string resourceName = "PowerModeSwitcher." + filepath.Replace('/', '.').Replace('\\', '.');
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        return Image.FromStream(stream);
                    }
                }
            }
            catch { }
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Render backgrounds and borders based on Light/Dark themes
            Color bgColor;
            Color borderColor;

            if (ThemeState.IsLightTheme)
            {
                // Apple Light Theme Colors
                bgColor = Color.FromArgb(255, 255, 255); // Crisp white card
                borderColor = Color.FromArgb(229, 229, 234); // Slate Light Gray (#E5E5EA)

                if (IsActive)
                {
                    bgColor = isPressed ? Color.FromArgb(242, 242, 247) : Color.FromArgb(248, 249, 250);
                    borderColor = Color.FromArgb(199, 199, 204); // #C7C7CC
                }
                else if (isHovered)
                {
                    bgColor = isPressed ? Color.FromArgb(235, 235, 240) : Color.FromArgb(242, 242, 247); // Lighter gray
                    borderColor = Color.FromArgb(209, 209, 214);
                }
            }
            else
            {
                // Apple Dark Theme Colors
                bgColor = Color.FromArgb(37, 37, 38); // #252526 (Mac System Settings gray)
                borderColor = Color.FromArgb(50, 50, 52); // #323234

                if (IsActive)
                {
                    bgColor = isPressed ? Color.FromArgb(55, 55, 57) : Color.FromArgb(46, 46, 48); // Slight highlighting
                    borderColor = Color.FromArgb(68, 68, 70); // #444446
                }
                else if (isHovered)
                {
                    bgColor = isPressed ? Color.FromArgb(42, 42, 44) : Color.FromArgb(48, 48, 50); // Lighter gray
                    borderColor = Color.FromArgb(63, 63, 65);
                }
            }

            // Fill card body
            using (GraphicsPath path = GetRoundedRectPath(new Rectangle(1, 1, Width - 3, Height - 3), 8f))
            {
                using (SolidBrush brush = new SolidBrush(bgColor))
                {
                    g.FillPath(brush, path);
                }
                
                // Draw card border
                using (Pen pen = new Pen(borderColor, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Draw crisp solid Apple squircle icon container on the left
            Rectangle rectIcon = new Rectangle(16, 18, 40, 40);

            // Determine custom PNG image path
            string customImagePath = null;
            if (IconType == "Leaf") customImagePath = "icons/power-saver.png";
            else if (IconType == "Balance") customImagePath = "icons/balanced.png";
            else if (IconType == "Bolt") customImagePath = "icons/high-performance.png";
            else if (IconType == "Crown") customImagePath = "icons/ultimate-performance.png";

            Image customImg = LoadImageSafely(customImagePath);

            if (customImg != null)
            {
                // Programmatically clip the custom PNG inside a beautiful anti-aliased squircle!
                using (GraphicsPath squircle = GetRoundedRectPath(rectIcon, 10f))
                {
                    // Draw a soft tinted glassmorphic container (12% opacity of AccentColor) behind the transparent PNG
                    using (SolidBrush tintBrush = new SolidBrush(Color.FromArgb(30, AccentColor.R, AccentColor.G, AccentColor.B)))
                    {
                        g.FillPath(tintBrush, squircle);
                    }

                    var oldClip = g.Clip;
                    using (Region reg = new Region(squircle))
                    {
                        g.Clip = reg;
                        
                        // Zoom in by 1.55x and center inside rectIcon to crop out the transparent padding
                        float zoom = 1.55f;
                        int zoomedWidth = (int)(rectIcon.Width * zoom);
                        int zoomedHeight = (int)(rectIcon.Height * zoom);
                        int offsetX = (zoomedWidth - rectIcon.Width) / 2;
                        int offsetY = (zoomedHeight - rectIcon.Height) / 2;
                        
                        Rectangle rectZoom = new Rectangle(rectIcon.Left - offsetX, rectIcon.Top - offsetY, zoomedWidth, zoomedHeight);
                        g.DrawImage(customImg, rectZoom);
                    }
                    g.Clip = oldClip;

                    // Draw subtle border around the custom image squircle
                    Color borderThemeColor = ThemeState.IsLightTheme ? Color.FromArgb(229, 229, 234) : Color.FromArgb(50, 50, 52);
                    using (Pen p = new Pen(borderThemeColor, 1.5f))
                    {
                        g.DrawPath(p, squircle);
                    }
                }
                customImg.Dispose(); // Release handle immediately
            }
            else
            {
                // Absolute clean glassmorphic fallback container if the image is somehow missing
                using (GraphicsPath squircle = GetRoundedRectPath(rectIcon, 10f))
                {
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(30, AccentColor.R, AccentColor.G, AccentColor.B)))
                    {
                        g.FillPath(brush, squircle);
                    }
                    using (Pen p = new Pen(AccentColor, 1.5f))
                    {
                        g.DrawPath(p, squircle);
                    }
                }
            }

            // Draw Apple Typography with safe spacing (No cutoffs!)
            int textX = rectIcon.Right + 16;
            
            // Mode Title
            Color titleColor = ThemeState.IsLightTheme ? Color.FromArgb(28, 28, 30) : Color.White;
            using (SolidBrush titleBrush = new SolidBrush(titleColor))
            {
                using (Font titleFont = new Font("Segoe UI", 10F, FontStyle.Bold))
                {
                    g.DrawString(PlanName, titleFont, titleBrush, textX, 15);
                }
            }

            // Description with Dynamic Wrap Rect (guarantees text will wrap automatically without cutting off!)
            Color descColor = ThemeState.IsLightTheme ? Color.FromArgb(99, 99, 102) : Color.FromArgb(142, 142, 147);
            using (SolidBrush descBrush = new SolidBrush(descColor))
            {
                RectangleF descRect = new RectangleF(textX, 36, Width - textX - 48, 34);
                g.DrawString(Description, this.Font, descBrush, descRect);
            }

            // Elegant Apple Circular Checkmark
            if (IsActive)
            {
                int checkSize = 18;
                Rectangle rectCheck = new Rectangle(Width - checkSize - 20, (Height - checkSize) / 2, checkSize, checkSize);
                
                using (SolidBrush checkBg = new SolidBrush(Color.FromArgb(0, 122, 255))) // Apple Blue (#007AFF)
                {
                    g.FillEllipse(checkBg, rectCheck);
                }
                
                using (Pen checkPen = new Pen(Color.White, 2f))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    g.DrawLine(checkPen, rectCheck.Left + 5, rectCheck.Top + 9, rectCheck.Left + 8, rectCheck.Top + 12);
                    g.DrawLine(checkPen, rectCheck.Left + 8, rectCheck.Top + 12, rectCheck.Left + 13, rectCheck.Top + 5);
                }
            }
        }

        private GraphicsPath GetRoundedRectPath(Rectangle rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float size = radius * 2;
            path.StartFigure();
            path.AddArc(rect.X, rect.Y, size, size, 180, 90);
            path.AddArc(rect.Right - size, rect.Y, size, size, 270, 90);
            path.AddArc(rect.Right - size, rect.Bottom - size, size, size, 0, 90);
            path.AddArc(rect.X, rect.Bottom - size, size, size, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // Beautiful Apple macOS-style Pill Toggle Switch (sliding switch)
    public class MacToggleSwitch : Control
    {
        private bool _checked = false;
        public bool Checked
        {
            get { return _checked; }
            set { _checked = value; Invalidate(); }
        }

        public string DisplayText { get; set; }
        private bool isHovered = false;

        public event EventHandler CheckedChanged;

        public MacToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            this.Cursor = Cursors.Hand;
            this.Height = 24;
            this.Font = new Font("Segoe UI", 9F);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            isHovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Checked = !Checked;
                var handler = CheckedChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Pill container dimensions
            int pillWidth = 36;
            int pillHeight = 20;
            Rectangle pillRect = new Rectangle(2, 2, pillWidth, pillHeight);
            
            Color pillColor;
            if (Checked)
            {
                pillColor = Color.FromArgb(0, 122, 255); // Apple Blue
            }
            else
            {
                pillColor = ThemeState.IsLightTheme ? Color.FromArgb(229, 229, 234) : Color.FromArgb(72, 72, 74); // Light vs Dark Grey
            }

            // Draw rounded pill path
            using (GraphicsPath path = GetPillPath(pillRect))
            {
                using (SolidBrush b = new SolidBrush(pillColor))
                {
                    g.FillPath(b, path);
                }
            }

            // Draw white sliding thumb
            int thumbSize = 16;
            int thumbY = pillRect.Y + 2;
            int thumbX = Checked ? pillRect.Right - thumbSize - 2 : pillRect.Left + 2;
            
            Rectangle thumbRect = new Rectangle(thumbX, thumbY, thumbSize, thumbSize);
            using (SolidBrush b = new SolidBrush(Color.White))
            {
                g.FillEllipse(b, thumbRect);
            }

            // Draw label text next to it
            Color textColor;
            if (ThemeState.IsLightTheme)
            {
                textColor = isHovered ? Color.Black : Color.FromArgb(60, 60, 67); // Apple Dark Text
            }
            else
            {
                textColor = isHovered ? Color.White : Color.FromArgb(220, 220, 224); // Apple Light Text
            }

            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                g.DrawString(DisplayText, this.Font, textBrush, pillRect.Right + 10, 2);
            }
        }

        private GraphicsPath GetPillPath(Rectangle rect)
        {
            GraphicsPath path = new GraphicsPath();
            float r = rect.Height / 2f;
            float size = r * 2;
            path.StartFigure();
            path.AddArc(rect.X, rect.Y, size, size, 90, 180);
            path.AddArc(rect.Right - size, rect.Y, size, size, 270, 180);
            path.CloseFigure();
            return path;
        }
    }

    // Premium Dark-themed ToolStrip Renderer for tray context menu
    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(20, 20, 22); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(20, 20, 22); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(20, 20, 22); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(20, 20, 22); } }
        public override Color MenuBorder { get { return Color.FromArgb(39, 39, 42); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(63, 63, 70); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(39, 39, 42); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(39, 39, 42); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(39, 39, 42); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(20, 20, 22); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(20, 20, 22); } }
        public override Color SeparatorDark { get { return Color.FromArgb(39, 39, 42); } }
    }

    // Premium Light-themed ToolStrip Renderer for tray context menu
    public class LightColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(242, 242, 247); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(242, 242, 247); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(242, 242, 247); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(242, 242, 247); } }
        public override Color MenuBorder { get { return Color.FromArgb(209, 209, 214); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(199, 199, 204); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(229, 229, 234); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(229, 229, 234); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(229, 229, 234); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(242, 242, 247); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(242, 242, 247); } }
        public override Color SeparatorDark { get { return Color.FromArgb(209, 209, 214); } }
    }

    // Main Form using native Windows Title Bar & Frame
    public class MainWindow : Form
    {
        // Windows 11 Rounded Corners and Title Bar Theme API
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Native user32 call to release GDI handle leaks
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_SETICON = 0x0080;
        private const int ICON_BIG = 1;

        private IntPtr activeTaskbarHIcon = IntPtr.Zero;
        private IntPtr activeTrayHIcon = IntPtr.Zero;

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private FlowLayoutPanel cardContainer;
        private Label lblActivePlan;
        private Label lblActiveHeader;
        private Label lblSelect;
        private Panel activeBanner;
        private Panel activeStatusBadge;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private MacToggleSwitch chkMinimizeToTray;
        private MacToggleSwitch chkStartWithWindows;
        private Timer backgroundSyncTimer;

        private List<PowerPlanCard> cards = new List<PowerPlanCard>();

        public MainWindow()
        {
            InitializeComponent();
            
            // Force Rounded Corners on Windows 11 Frame
            try
            {
                int cornerPreference = DWMWCP_ROUND;
                DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
            }
            catch { }
        }

        private void InitializeComponent()
        {
            // Set client size instead of Form size (gives exact inner dimensions regardless of native title bar size)
            this.ClientSize = new Size(420, 530); 
            this.FormBorderStyle = FormBorderStyle.FixedSingle; // Default native Windows frame (non-resizable)
            this.MaximizeBox = false; // Disable native maximize box
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;

            // Load custom application icons
            try
            {
                // 1. Try loading from local file system first (for local user customization)
                if (File.Exists("app.ico"))
                {
                    this.Icon = new Icon("app.ico");
                }
                else
                {
                    // 2. Fall back to embedded manifest resource inside the compiled binary
                    string resourceName = "PowerModeSwitcher.app.ico";
                    System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            this.Icon = new Icon(stream);
                        }
                        else
                        {
                            // 3. Absolute fallback to associated executable icon (from win32icon metadata)
                            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                        }
                    }
                }
            }
            catch
            {
                try
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch
                {
                    this.Icon = SystemIcons.Application;
                }
            }

            // 1. Active Mode Display Banner (Apple System panel)
            activeBanner = new Panel
            {
                Top = 16,
                Left = 20,
                Width = ClientSize.Width - 40,
                Height = 64
            };
            
            // Sub-borders
            activeBanner.Paint += (s, e) =>
            {
                Color borderThemeColor = ThemeState.IsLightTheme ? Color.FromArgb(229, 229, 234) : Color.FromArgb(50, 50, 52);
                using (Pen p = new Pen(borderThemeColor, 1f))
                {
                    using (GraphicsPath path = GetRoundedRectPath(new Rectangle(0, 0, activeBanner.Width - 1, activeBanner.Height - 1), 6f))
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.DrawPath(p, path);
                    }
                }
            };

            lblActiveHeader = new Label
            {
                Text = "CURRENT ACTIVE PLAN",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(142, 142, 147), // Apple Gray (#8E8E93)
                AutoSize = true,
                Left = 16,
                Top = 12
            };
            activeBanner.Controls.Add(lblActiveHeader);

            lblActivePlan = new Label
            {
                Text = "Scanning...",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Left = 15,
                Top = 28
            };
            activeBanner.Controls.Add(lblActivePlan);

            // Glowing Indicator Dot
            activeStatusBadge = new Panel
            {
                Size = new Size(10, 10),
                Left = activeBanner.Width - 24,
                Top = 27
            };
            activeStatusBadge.Paint += ActiveStatusBadge_Paint;
            activeBanner.Controls.Add(activeStatusBadge);

            this.Controls.Add(activeBanner);

            // 2. Selection Title Label
            lblSelect = new Label
            {
                Text = "SELECT POWER PLAN",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(142, 142, 147),
                AutoSize = true,
                Left = 22,
                Top = activeBanner.Bottom + 16
            };
            this.Controls.Add(lblSelect);

            // 3. Power Plan Cards Container (flow layout)
            cardContainer = new FlowLayoutPanel
            {
                Top = lblSelect.Bottom + 6,
                Left = 20,
                Width = ClientSize.Width - 40,
                Height = 336,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };
            this.Controls.Add(cardContainer);

            // Setup Custom Cards
            SetupPowerPlanCards();

            // 4. Tray Options / macOS pill toggles
            chkMinimizeToTray = new MacToggleSwitch
            {
                DisplayText = "Minimize to System Tray on Close",
                Checked = LoadConfigBool("MinimizeToTray", true),
                Left = 24,
                Top = cardContainer.Bottom + 12,
                Width = ClientSize.Width - 48
            };
            chkMinimizeToTray.CheckedChanged += (s, e) => SaveConfigBool("MinimizeToTray", chkMinimizeToTray.Checked);
            this.Controls.Add(chkMinimizeToTray);

            chkStartWithWindows = new MacToggleSwitch
            {
                DisplayText = "Start with Windows (Minimized)",
                Checked = CheckStartupEnabled(),
                Left = 24,
                Top = chkMinimizeToTray.Bottom + 8,
                Width = ClientSize.Width - 48
            };
            chkStartWithWindows.CheckedChanged += ToggleStartup;
            this.Controls.Add(chkStartWithWindows);

            // Set initial theme based on Windows Registry
            ThemeState.IsLightTheme = ThemeState.CheckWindowsLightTheme();
            ApplyTheme();

            // 5. System Tray setup
            SetupSystemTray();

            // 6. Background Auto-Sync Timer (syncs active plan + detects OS theme changes in real-time)
            backgroundSyncTimer = new Timer();
            backgroundSyncTimer.Interval = 2000; // 2 seconds
            backgroundSyncTimer.Tick += (s, e) => {
                SyncActivePlan(false);
                
                // Real-time OS Theme Detection
                bool currentTheme = ThemeState.CheckWindowsLightTheme();
                if (ThemeState.IsLightTheme != currentTheme)
                {
                    ThemeState.IsLightTheme = currentTheme;
                    ApplyTheme();
                }
            };
            backgroundSyncTimer.Start();

            // Initial Sync
            SyncActivePlan(true);
        }

        // Programmatic native Title Bar theme toggler
        private void SetTitleBarTheme(bool lightMode)
        {
            try
            {
                // DWMWA_USE_IMMERSIVE_DARK_MODE: 1 = dark mode, 0 = light mode
                int useDark = lightMode ? 0 : 1;
                
                // Attribute 20 controls dark theme on Win 10 20H1+ and Windows 11
                int result = DwmSetWindowAttribute(this.Handle, 20, ref useDark, sizeof(int));
                if (result != 0)
                {
                    // Fallback to attribute 19 on older Win 10 builds
                    DwmSetWindowAttribute(this.Handle, 19, ref useDark, sizeof(int));
                }
            }
            catch { }
        }

        // Safe stream loader supporting local files and fallback embedded assembly manifest resources
        private static Image LoadImageSafely(string filepath)
        {
            try
            {
                // 1. Try loading from local file system first (for local user customization)
                if (File.Exists(filepath))
                {
                    using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                    {
                        return Image.FromStream(fs);
                    }
                }

                // 2. Fall back to embedded manifest resources inside the compiled binary assembly
                string resourceName = "PowerModeSwitcher." + filepath.Replace('/', '.').Replace('\\', '.');
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        return Image.FromStream(stream);
                    }
                }
            }
            catch { }
            return null;
        }

        // Programmatic Real-Time theme applicator
        private void ApplyTheme()
        {
            bool lightMode = ThemeState.IsLightTheme;

            // Set native title bar background, close/minimize buttons, and text theme!
            SetTitleBarTheme(lightMode);

            // Form Background Colors
            this.BackColor = lightMode ? Color.FromArgb(242, 242, 247) : Color.FromArgb(30, 30, 30);
            
            // Active banner panel colors
            if (activeBanner != null)
            {
                activeBanner.BackColor = lightMode ? Color.FromArgb(255, 255, 255) : Color.FromArgb(37, 37, 38);
                activeBanner.Invalidate();
            }

            // Update typography
            Color labelColor = lightMode ? Color.FromArgb(100, 100, 102) : Color.FromArgb(142, 142, 147);
            if (lblActiveHeader != null)
            {
                lblActiveHeader.ForeColor = labelColor;
            }
            if (lblSelect != null)
            {
                lblSelect.ForeColor = labelColor;
            }

            // Update System Tray Context Menu Theme dynamically!
            if (trayMenu != null)
            {
                trayMenu.Renderer = new ToolStripProfessionalRenderer(lightMode ? (ProfessionalColorTable)new LightColorTable() : new DarkColorTable());
                trayMenu.ForeColor = lightMode ? Color.FromArgb(28, 28, 30) : Color.White;
                
                // Update specific item colors
                foreach (ToolStripItem toolItem in trayMenu.Items)
                {
                    ToolStripMenuItem item = toolItem as ToolStripMenuItem;
                    if (item != null)
                    {
                        item.ForeColor = lightMode ? Color.FromArgb(28, 28, 30) : Color.White;
                        if (item.Text == "Exit")
                        {
                            item.ForeColor = Color.FromArgb(255, 69, 58); // Always Apple Red
                        }
                    }
                }
            }

            // Sync main label text colors based on theme if plan matches
            SyncActivePlan(true);

            // Force complete recursive invalidation of the entire window and child elements
            this.Invalidate(true);
        }

        private void ActiveStatusBadge_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color accent = Color.FromArgb(0, 122, 255); // Apple Blue
            
            // Get active card's accent
            foreach (var card in cards)
            {
                if (card.IsActive)
                {
                    accent = card.AccentColor;
                    break;
                }
            }

            // Glow ring
            using (SolidBrush sbGlow = new SolidBrush(Color.FromArgb(40, accent.R, accent.G, accent.B)))
            {
                e.Graphics.FillEllipse(sbGlow, new Rectangle(0, 0, 9, 9));
            }
            // Center solid dot
            using (SolidBrush sbSolid = new SolidBrush(accent))
            {
                e.Graphics.FillEllipse(sbSolid, new Rectangle(2, 2, 5, 5));
            }
        }

        private void SetupPowerPlanCards()
        {
            // Cards declarations
            var cardSaver = new PowerPlanCard
            {
                PlanGuid = PowerManager.PowerSaverGuid,
                PlanName = "Power Saver",
                Description = "Maximizes battery life & energy savings by limiting system performance.",
                AccentColor = Color.FromArgb(52, 199, 89), // #34C759 (Apple Green)
                IconType = "Leaf",
                Width = cardContainer.Width
            };
            cardSaver.Click += (s, e) => SetActivePlan(cardSaver.PlanGuid);
            cards.Add(cardSaver);

            var cardBalanced = new PowerPlanCard
            {
                PlanGuid = PowerManager.BalancedGuid,
                PlanName = "Balanced",
                Description = "Optimizes energy usage and performance dynamically based on system load.",
                AccentColor = Color.FromArgb(0, 122, 255), // #007AFF (Apple Blue)
                IconType = "Balance",
                Width = cardContainer.Width
            };
            cardBalanced.Click += (s, e) => SetActivePlan(cardBalanced.PlanGuid);
            cards.Add(cardBalanced);

            var cardHigh = new PowerPlanCard
            {
                PlanGuid = PowerManager.HighPerformanceGuid,
                PlanName = "High Performance",
                Description = "Favors raw CPU speed and system responsiveness over energy efficiency.",
                AccentColor = Color.FromArgb(255, 149, 0), // #FF9500 (Apple Orange)
                IconType = "Bolt",
                Width = cardContainer.Width
            };
            cardHigh.Click += (s, e) => SetActivePlan(cardHigh.PlanGuid);
            cards.Add(cardHigh);

            // Self-healed Ultimate Performance card
            var cardUltimate = new PowerPlanCard
            {
                PlanGuid = Guid.Empty, // resolved dynamically
                PlanName = "Ultimate Performance",
                Description = "Delivers absolute maximum response speeds and raw power for high-end systems.",
                AccentColor = Color.FromArgb(255, 69, 58), // #FF453A (Apple Red)
                IconType = "Crown",
                Width = cardContainer.Width
            };
            cardUltimate.Click += (s, e) => {
                if (cardUltimate.PlanGuid == Guid.Empty)
                {
                    cardUltimate.PlanGuid = PowerManager.EnsureUltimatePerformance();
                }
                SetActivePlan(cardUltimate.PlanGuid);
            };
            cards.Add(cardUltimate);

            // Add all cards to FlowLayoutPanel
            foreach (var card in cards)
            {
                card.Margin = new Padding(0, 0, 0, 8);
                cardContainer.Controls.Add(card);
            }
        }

        private void SetActivePlan(Guid planGuid)
        {
            if (planGuid == Guid.Empty) return;

            bool success = PowerManager.SetActiveScheme(planGuid);
            if (success)
            {
                SyncActivePlan(true);
            }
        }

        private void SyncActivePlan(bool forceRedraw)
        {
            Guid activeGuid = PowerManager.GetActiveScheme();
            
            // Ultimate Performance Guid checking (look dynamically if GUID matches a scheme with ultimate in name)
            Guid resolvedUltimate = Guid.Empty;
            var plans = PowerManager.GetPowerPlans();
            foreach (var p in plans)
            {
                if (p.Name.ToLower().Contains("ultimate"))
                {
                    resolvedUltimate = p.Guid;
                    break;
                }
            }

            // Sync card active states
            bool activeChanged = false;
            foreach (var card in cards)
            {
                bool shouldBeActive = false;
                
                if (card.PlanGuid == PowerManager.PowerSaverGuid)
                {
                    shouldBeActive = (activeGuid == PowerManager.PowerSaverGuid);
                }
                else if (card.PlanGuid == PowerManager.BalancedGuid)
                {
                    shouldBeActive = (activeGuid == PowerManager.BalancedGuid);
                }
                else if (card.PlanGuid == PowerManager.HighPerformanceGuid)
                {
                    shouldBeActive = (activeGuid == PowerManager.HighPerformanceGuid);
                }
                else // Ultimate card
                {
                    // Resolve dynamically
                    if (resolvedUltimate != Guid.Empty)
                    {
                        card.PlanGuid = resolvedUltimate;
                    }
                    shouldBeActive = (activeGuid == resolvedUltimate && resolvedUltimate != Guid.Empty);
                }

                if (card.IsActive != shouldBeActive)
                {
                    card.IsActive = shouldBeActive;
                    card.Invalidate();
                    activeChanged = true;
                }
            }

            // Update main label and status ring
            if (activeChanged || forceRedraw)
            {
                string activeName = "Unknown Scheme";
                Color accentColor = Color.FromArgb(0, 122, 255); // default Blue

                foreach (var card in cards)
                {
                    if (card.IsActive)
                    {
                        activeName = card.PlanName;
                        accentColor = card.AccentColor;
                        break;
                    }
                }

                // If no card is active (e.g. customized user scheme), query system name
                if (activeName == "Unknown Scheme")
                {
                    foreach (var p in plans)
                    {
                        if (p.Guid == activeGuid)
                        {
                            activeName = p.Name;
                            break;
                        }
                    }
                }

                lblActivePlan.Text = activeName;
                
                // Color mapping depending on Light/Dark active text
                if (ThemeState.IsLightTheme)
                {
                    // Slightly darken the accent color in light mode for proper contrast and legibility!
                    if (accentColor.R == 52 && accentColor.G == 199 && accentColor.B == 89) // Green
                        lblActivePlan.ForeColor = Color.FromArgb(34, 139, 34); // Forest Green
                    else if (accentColor.R == 255 && accentColor.G == 149 && accentColor.B == 0) // Orange
                        lblActivePlan.ForeColor = Color.FromArgb(204, 102, 0); // Darker Orange
                    else if (accentColor.R == 255 && accentColor.G == 69 && accentColor.B == 58) // Red
                        lblActivePlan.ForeColor = Color.FromArgb(200, 0, 0); // Darker Red for legibility
                    else
                        lblActivePlan.ForeColor = accentColor;
                }
                else
                {
                    lblActivePlan.ForeColor = accentColor;
                }

                activeStatusBadge.Invalidate();

                // Re-sync Tray Menu checkmarks
                UpdateTrayCheckmarks(activeGuid, resolvedUltimate);

                // Update Application & Tray Icons in Real-Time!
                UpdateApplicationIcons(activeGuid, resolvedUltimate);
            }
        }

        private void SetupSystemTray()
        {
            trayMenu = new ContextMenuStrip
            {
                Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()),
                ShowImageMargin = true
            };
            trayMenu.Font = new Font("Segoe UI", 9F);
            trayMenu.ForeColor = Color.White;

            // Generate beautifully customized tray context menu items
            AddTrayMenuItem("Power Saver", Color.FromArgb(52, 199, 89), PowerManager.PowerSaverGuid);
            AddTrayMenuItem("Balanced", Color.FromArgb(0, 122, 255), PowerManager.BalancedGuid);
            AddTrayMenuItem("High Performance", Color.FromArgb(255, 149, 0), PowerManager.HighPerformanceGuid);
            
            // Ultimate item (uses tag so we can dynamically query or active)
            ToolStripMenuItem itemUlt = new ToolStripMenuItem("Ultimate Performance");
            itemUlt.ForeColor = Color.White;
            itemUlt.Image = GetMenuImage(PowerManager.UltimatePerformanceGuid, false, PowerManager.UltimatePerformanceGuid);
            itemUlt.Click += (s, e) =>
            {
                Guid ult = PowerManager.EnsureUltimatePerformance();
                SetActivePlan(ult);
            };
            trayMenu.Items.Add(itemUlt);

            trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemOpen = new ToolStripMenuItem("Open Switcher");
            itemOpen.ForeColor = Color.White;
            itemOpen.Font = new Font(trayMenu.Font, FontStyle.Bold);
            itemOpen.Click += (s, e) => ShowForm();
            trayMenu.Items.Add(itemOpen);

            ToolStripMenuItem itemExit = new ToolStripMenuItem("Exit");
            itemExit.ForeColor = Color.FromArgb(255, 69, 58); // Apple Red (#FF453A)
            itemExit.Click += (s, e) => ExitApplication();
            trayMenu.Items.Add(itemExit);

            // Construct tray icon
            trayIcon = new NotifyIcon
            {
                Icon = this.Icon,
                ContextMenuStrip = trayMenu,
                Text = "Power Mode Switcher",
                Visible = true
            };
            
            // Double click tray icon opens main window
            trayIcon.DoubleClick += (s, e) => ShowForm();
        }

        private void AddTrayMenuItem(string name, Color accent, Guid guid)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(name)
            {
                ForeColor = Color.White,
                Image = GetMenuImage(guid, false, guid),
                Tag = guid
            };
            item.Click += (s, e) => SetActivePlan(guid);
            trayMenu.Items.Add(item);
        }

        private Color GetPlanAccentColor(Guid guid)
        {
            if (guid == PowerManager.PowerSaverGuid)
                return Color.FromArgb(52, 199, 89);
            if (guid == PowerManager.BalancedGuid)
                return Color.FromArgb(0, 122, 255);
            if (guid == PowerManager.HighPerformanceGuid)
                return Color.FromArgb(255, 149, 0);
            return Color.FromArgb(255, 69, 58);
        }

        private void UpdateTrayCheckmarks(Guid activeGuid, Guid resolvedUltimate)
        {
            if (trayMenu == null) return;

            foreach (ToolStripItem toolItem in trayMenu.Items)
            {
                ToolStripMenuItem item = toolItem as ToolStripMenuItem;
                if (item != null)
                {
                    if (item.Tag is Guid)
                    {
                        Guid itemGuid = (Guid)item.Tag;
                        bool isActive = (itemGuid == activeGuid);
                        
                        // Dynamically update image to custom JPEG or fallback circle
                        item.Image = GetMenuImage(itemGuid, isActive, resolvedUltimate);
                    }
                    else if (item.Text == "Ultimate Performance")
                    {
                        bool isActive = (resolvedUltimate != Guid.Empty && activeGuid == resolvedUltimate);
                        item.Image = GetMenuImage(resolvedUltimate, isActive, resolvedUltimate);
                    }
                }
            }
        }

        private void HandleCloseButton()
        {
            if (chkMinimizeToTray.Checked)
            {
                this.Hide();
                trayIcon.ShowBalloonTip(1500, "Power Mode Switcher", "App minimized to tray. Right-click icon to quick-switch plans!", ToolTipIcon.Info);
            }
            else
            {
                ExitApplication();
            }
        }

        private void ShowForm()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
            SyncActivePlan(true);
        }

        private void ExitApplication()
        {
            backgroundSyncTimer.Stop();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            
            // Clean up GDI handles on exit
            if (activeTaskbarHIcon != IntPtr.Zero) DestroyIcon(activeTaskbarHIcon);
            if (activeTrayHIcon != IntPtr.Zero) DestroyIcon(activeTrayHIcon);
            
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && chkMinimizeToTray.Checked)
            {
                e.Cancel = true;
                this.Hide();
            }
            base.OnFormClosing(e);
        }



        // Custom dynamic menu circles (Active circular checkmark vs Inactive simple circle)
        private Bitmap CreateMenuCircle(Color color, bool isActive)
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                if (isActive)
                {
                    // Draw beautiful solid active circle with a small white vector checkmark inside!
                    using (SolidBrush b = new SolidBrush(color))
                    {
                        g.FillEllipse(b, 1, 1, 14, 14);
                    }
                    using (Pen p = new Pen(Color.White, 1.8f))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        g.DrawLine(p, 5, 8, 7, 10);
                        g.DrawLine(p, 7, 10, 11, 5);
                    }
                }
                else
                {
                    // Draw a simple, clean inactive circle
                    using (SolidBrush b = new SolidBrush(color))
                    {
                        g.FillEllipse(b, 3, 3, 10, 10);
                    }
                }
            }
            return bmp;
        }

        // Render custom circular menu checkmarks from custom PNGs (with fallback to solid circles)
        private Image GetMenuImage(Guid guid, bool isActive, Guid resolvedUltimate)
        {
            try
            {
                string pngPath = null;
                
                if (guid == PowerManager.PowerSaverGuid) pngPath = "icons/power-saver.png";
                else if (guid == PowerManager.BalancedGuid) pngPath = "icons/balanced.png";
                else if (guid == PowerManager.HighPerformanceGuid) pngPath = "icons/high-performance.png";
                else if (resolvedUltimate != Guid.Empty && guid == resolvedUltimate) pngPath = "icons/ultimate-performance.png";

                Image customImg = LoadImageSafely(pngPath);
                if (customImg != null)
                {
                    Bitmap menuBmp = new Bitmap(16, 16);
                    using (Graphics g = Graphics.FromImage(menuBmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.Clear(Color.Transparent);

                        // Draw the custom-shaped PNG directly at full 16x16 size with a 1.55x zoom to crop margins
                        float zoom = 1.55f;
                        int zoomedSize = (int)(16 * zoom);
                        int offset = (zoomedSize - 16) / 2;
                        g.DrawImage(customImg, new Rectangle(-offset, -offset, zoomedSize, zoomedSize));

                        // If active, overlay a tiny, crisp Apple-style blue circular checkmark in the bottom right corner!
                        if (isActive)
                        {
                            int checkSize = 9;
                            Rectangle rectCheck = new Rectangle(16 - checkSize, 16 - checkSize, checkSize, checkSize);
                            using (SolidBrush checkBg = new SolidBrush(Color.FromArgb(0, 122, 255))) // Apple Blue
                            {
                                g.FillEllipse(checkBg, rectCheck);
                            }
                            using (Pen checkPen = new Pen(Color.White, 1.2f))
                            {
                                checkPen.StartCap = LineCap.Round;
                                checkPen.EndCap = LineCap.Round;
                                g.DrawLine(checkPen, rectCheck.Left + 2f, rectCheck.Top + 4.5f, rectCheck.Left + 4f, rectCheck.Top + 6.5f);
                                g.DrawLine(checkPen, rectCheck.Left + 4f, rectCheck.Top + 6.5f, rectCheck.Left + 7.5f, rectCheck.Top + 2.5f);
                            }
                        }
                    }
                    customImg.Dispose();
                    return menuBmp;
                }
            }
            catch { }

            // Dynamic vector circle fallback if JPEG is missing
            Color accent = GetPlanAccentColor(guid);
            return CreateMenuCircle(accent, isActive);
        }

        private Bitmap CreateRoundedIconBitmap(Image image, int size, float cornerRadius)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.Clear(Color.Transparent);

                // Zoom in by 1.55x and center to crop out the transparent padding around the reactor sphere,
                // making it fill the entire canvas bounding box and display much larger in the taskbar/system tray!
                float zoom = 1.55f;
                int zoomedSize = (int)(size * zoom);
                int offset = (zoomedSize - size) / 2;
                g.DrawImage(image, new Rectangle(-offset, -offset, zoomedSize, zoomedSize));
            }
            return bmp;
        }

        // Dynamic taskbar, system tray, and main window icon loader supporting custom PNGs
        private void UpdateApplicationIcons(Guid activeGuid, Guid resolvedUltimate)
        {
            try
            {
                string pngPath = null;

                if (activeGuid == PowerManager.PowerSaverGuid)
                {
                    pngPath = "icons/power-saver.png";
                }
                else if (activeGuid == PowerManager.BalancedGuid)
                {
                    pngPath = "icons/balanced.png";
                }
                else if (activeGuid == PowerManager.HighPerformanceGuid)
                {
                    pngPath = "icons/high-performance.png";
                }
                else if (resolvedUltimate != Guid.Empty && activeGuid == resolvedUltimate)
                {
                    pngPath = "icons/ultimate-performance.png";
                }

                // 1. Try loading from custom icons folder (PNG)
                Image customImg = LoadImageSafely(pngPath);
                if (customImg != null)
                {
                    // Create high-quality 256x256 icon for the form (taskbar) to support high-DPI scaling perfectly
                    using (Bitmap bmp256 = CreateRoundedIconBitmap(customImg, 256, 0f))
                    {
                        IntPtr oldTaskbar = activeTaskbarHIcon;
                        activeTaskbarHIcon = bmp256.GetHicon();

                        // Force native WM_SETICON big icon update
                        SendMessage(this.Handle, WM_SETICON, (IntPtr)ICON_BIG, activeTaskbarHIcon);

                        using (Icon tempIcon256 = Icon.FromHandle(activeTaskbarHIcon))
                        {
                            this.Icon = (Icon)tempIcon256.Clone();
                        }

                        // Destroy old handle to prevent GDI leak
                        if (oldTaskbar != IntPtr.Zero)
                        {
                            DestroyIcon(oldTaskbar);
                        }
                    }

                    // Create high-quality rounded 16x16 icon for the system tray
                    using (Bitmap bmp16 = CreateRoundedIconBitmap(customImg, 16, 3.5f))
                    {
                        IntPtr oldTray = activeTrayHIcon;
                        activeTrayHIcon = bmp16.GetHicon();

                        using (Icon tempIcon16 = Icon.FromHandle(activeTrayHIcon))
                        {
                            if (trayIcon != null)
                            {
                                trayIcon.Icon = (Icon)tempIcon16.Clone();
                            }
                        }

                        // Destroy old handle to prevent GDI leak
                        if (oldTray != IntPtr.Zero)
                        {
                            DestroyIcon(oldTray);
                        }
                    }
                    
                    customImg.Dispose();
                }
                else
                {
                    // Absolute safety fallback: use the main application window's icon
                    if (trayIcon != null)
                    {
                        trayIcon.Icon = this.Icon;
                    }
                }
            }
            catch { }
        }

        private GraphicsPath GetRoundedRectPath(Rectangle rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float size = radius * 2;
            path.StartFigure();
            path.AddArc(rect.X, rect.Y, size, size, 180, 90);
            path.AddArc(rect.Right - size, rect.Y, size, size, 270, 90);
            path.AddArc(rect.Right - size, rect.Bottom - size, size, size, 0, 90);
            path.AddArc(rect.X, rect.Bottom - size, size, size, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Startup Registry Utilities (HKCU, no admin rights required)
        private bool CheckStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null)
                    {
                        string val = key.GetValue("PowerModeSwitcher") as string;
                        return val != null && val.Contains(Application.ExecutablePath);
                    }
                }
            }
            catch { }
            return false;
        }

        private void ToggleStartup(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (chkStartWithWindows.Checked)
                        {
                            // Launch minimized to system tray
                            key.SetValue("PowerModeSwitcher", "\"" + Application.ExecutablePath + "\" --minimized");
                        }
                        else
                        {
                            key.DeleteValue("PowerModeSwitcher", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not toggle Startup state: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Configuration Helpers
        private bool LoadConfigBool(string keyName, bool defaultVal)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\PowerModeSwitcher"))
                {
                    object val = key.GetValue(keyName);
                    if (val != null)
                    {
                        return Convert.ToInt32(val) == 1;
                    }
                }
            }
            catch { }
            return defaultVal;
        }

        private void SaveConfigBool(string keyName, bool val)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\PowerModeSwitcher"))
                {
                    key.SetValue(keyName, val ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch { }
        }
    }

    // Main Startup Handler
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Single instance lock
            bool createdNew;
            using (var mutex = new System.Threading.Mutex(true, "PowerModeSwitcherSingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("Power Mode Switcher is already running in your system tray!", "Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                MainWindow mainWin = new MainWindow();
                
                // Read start parameters
                bool startMinimized = false;
                foreach (string arg in args)
                {
                    if (arg.ToLower() == "--minimized" || arg.ToLower() == "-m")
                    {
                        startMinimized = true;
                        break;
                    }
                }

                if (startMinimized)
                {
                    // Hide the form on start to load directly into the tray
                    mainWin.WindowState = FormWindowState.Minimized;
                    mainWin.ShowInTaskbar = false;
                    
                    // Run the message pump with an invisible form
                    Application.Run();
                }
                else
                {
                    Application.Run(mainWin);
                }
            }
        }
    }
}

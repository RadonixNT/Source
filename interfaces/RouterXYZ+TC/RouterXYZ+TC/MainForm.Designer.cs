namespace RouterXYZTC;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeDiagnosticWindows();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        R_ThemeManager = new NT.Tools.sTheme(components);
        R_BrandLabel = new NT.Tools.sLabel();
        R_Connected = new NT.Tools.sSignal();
        R_XAxis = new NT.Tools.sSignal();
        R_YAxis = new NT.Tools.sSignal();
        R_ZAxis = new NT.Tools.sSignal();
        R_AxisAlarm = new NT.Tools.sSignal();
        R_Emergency = new NT.Tools.sSignal();
        R_ThemeToggle = new NT.Tools.sThemeButton();
        R_Minimize = new NT.Tools.sButton();
        R_Exit = new NT.Tools.sButton();
        R_Configuration = new NT.Tools.sButton();
        R_ProgramPath = new NT.Tools.sLabel();
        R_MachineFeedValue = new NT.Tools.sProgressBar();
        R_ProgramView3D = new NT.Tools.s3DView();
        R_MachinePositionPanel = new NT.Tools.sPanel();
        R_MachinePositionTitle = new NT.Tools.sLabel();
        R_CommandPositionX = new NT.Tools.sLabel();
        R_CommandPositionY = new NT.Tools.sLabel();
        R_CommandPositionZ = new NT.Tools.sLabel();
        R_WorkPositionPanel = new NT.Tools.sPanel();
        R_WorkPositionTitle = new NT.Tools.sLabel();
        R_WorkPositionX = new NT.Tools.sLabel();
        R_WorkPositionY = new NT.Tools.sLabel();
        R_WorkPositionZ = new NT.Tools.sLabel();
        R_JogPanel = new NT.Tools.sPanel();
        R_GoToZ = new NT.Tools.sButton();
        R_GoToY = new NT.Tools.sButton();
        R_JogPositionHeader = new NT.Tools.sLabel();
        R_JogStepHeader = new NT.Tools.sLabel();
        R_PositionX = new NT.Tools.sTextBox();
        R_PositionY = new NT.Tools.sTextBox();
        R_GoToX = new NT.Tools.sButton();
        R_JogAxisXLabel = new NT.Tools.sLabel();
        R_PositionZ = new NT.Tools.sTextBox();
        R_JogAxisYLabel = new NT.Tools.sLabel();
        R_JogVelocityHeader = new NT.Tools.sLabel();
        R_JogAxisZLabel = new NT.Tools.sLabel();
        R_JogVelocityX = new NT.Tools.sTextBox();
        R_JogVelocityY = new NT.Tools.sTextBox();
        R_JogVelocityZ = new NT.Tools.sTextBox();
        R_StepX = new NT.Tools.sTextBox();
        R_StepY = new NT.Tools.sTextBox();
        R_StepZ = new NT.Tools.sTextBox();
        R_JogNXPY = new NT.Tools.sButton();
        R_JogPY = new NT.Tools.sButton();
        R_JogPXPY = new NT.Tools.sButton();
        R_JogPZ = new NT.Tools.sButton();
        R_JogNX = new NT.Tools.sButton();
        R_JogMode = new NT.Tools.sToggle();
        R_JogPX = new NT.Tools.sButton();
        R_JogPause = new NT.Tools.sButton();
        R_JogNXNY = new NT.Tools.sButton();
        R_JogNY = new NT.Tools.sButton();
        R_JogPXNY = new NT.Tools.sButton();
        R_JogNZ = new NT.Tools.sButton();
        R_JogFeedOverride = new NT.Tools.sSlider();
        R_Home = new NT.Tools.sButton();
        R_GoHome = new NT.Tools.sButton();
        R_GoToReference = new NT.Tools.sButton();
        R_ReferencePause = new NT.Tools.sButton();
        R_RefZZero = new NT.Tools.sButton();
        R_RefYZero = new NT.Tools.sButton();
        R_RefXZero = new NT.Tools.sButton();
        R_ProgramPanel = new NT.Tools.sPanel();
        R_GCodeView = new NT.Tools.sGCodeView();
        R_FeedOverride = new NT.Tools.sSlider();
        R_RunProgram = new NT.Tools.sButton();
        R_PauseProgram = new NT.Tools.sButton();
        R_ResetProgram = new NT.Tools.sButton();
        R_EditGCode = new NT.Tools.sButton();
        R_SelectStartLine = new NT.Tools.sButton();
        R_OpenFile = new NT.Tools.sButton();
        R_SpindlePanel = new NT.Tools.sPanel();
        R_SpindlePanelTitle = new NT.Tools.sLabel();
        R_ToolSelectorLabel = new NT.Tools.sLabel();
        R_Spindle = new NT.Tools.sToggle();
        R_ToolSelector = new NT.Tools.sParameterBox();
        R_MeasureToolOffset = new NT.Tools.sButton();
        R_SpindleSpeed = new NT.Tools.sSlider();
        R_ProgramProgress = new NT.Tools.sProgressBar();
        R_ReferenceX = new NT.Tools.sTextBox();
        R_ReferenceY = new NT.Tools.sTextBox();
        R_ReferenceZ = new NT.Tools.sTextBox();
        R_ReferenceSelector = new NT.Tools.sComboBox();
        R_ReferencePanel = new NT.Tools.sPanel();
        R_ReferencePanelTitle = new NT.Tools.sLabel();
        R_ReferenceAxisXLabel = new NT.Tools.sLabel();
        R_ReferenceAxisYLabel = new NT.Tools.sLabel();
        R_ReferenceAxisZLabel = new NT.Tools.sLabel();
        R_Homed = new NT.Tools.sSignal();
        (R_ThemeManager).BeginInit();
        R_MachinePositionPanel.SuspendLayout();
        R_WorkPositionPanel.SuspendLayout();
        R_JogPanel.SuspendLayout();
        R_ProgramPanel.SuspendLayout();
        R_SpindlePanel.SuspendLayout();
        R_ReferencePanel.SuspendLayout();
        SuspendLayout();
        // 
        // R_ThemeManager
        // 
        R_ThemeManager.RootControl = this;
        R_ThemeManager.RootDpiMode = NT.Tools.SThemeDpiMode.FixedPixels;
        // 
        // R_BrandLabel
        // 
        R_BrandLabel.AccessibleRole = AccessibleRole.StaticText;
        R_BrandLabel.CornerRadius = 10;
        R_BrandLabel.Font = new Font("Segoe UI", 28.125F, FontStyle.Bold, GraphicsUnit.Point, 0);
        R_BrandLabel.ForeColor = Color.SteelBlue;
        R_BrandLabel.Location = new Point(9, 9);
        R_BrandLabel.Name = "R_BrandLabel";
        R_BrandLabel.Padding = new Padding(8, 0, 0, 0);
        R_BrandLabel.Size = new Size(204, 56);
        R_BrandLabel.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_BrandLabel.TabIndex = 1;
        R_BrandLabel.TabStop = false;
        R_BrandLabel.Text = "RADONIX";
        R_BrandLabel.TextAlign = ContentAlignment.MiddleCenter;
        R_BrandLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_Connected
        // 
        R_Connected.AccessibleRole = AccessibleRole.Indicator;
        R_Connected.CornerRadius = 10;
        R_Connected.IndicatorSize = new Size(30, 30);
        R_Connected.Location = new Point(221, 9);
        R_Connected.Name = "R_Connected";
        R_Connected.Size = new Size(168, 56);
        R_Connected.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_Connected.TabIndex = 3;
        R_Connected.TabStop = false;
        R_Connected.Text = "Online";
        // 
        // R_XAxis
        // 
        R_XAxis.AccessibleRole = AccessibleRole.Indicator;
        R_XAxis.CornerRadius = 10;
        R_XAxis.IndicatorSize = new Size(30, 30);
        R_XAxis.Location = new Point(397, 9);
        R_XAxis.Name = "R_XAxis";
        R_XAxis.Size = new Size(168, 56);
        R_XAxis.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_XAxis.TabIndex = 4;
        R_XAxis.TabStop = false;
        R_XAxis.Text = "X Axis";
        // 
        // R_YAxis
        // 
        R_YAxis.AccessibleRole = AccessibleRole.Indicator;
        R_YAxis.CornerRadius = 10;
        R_YAxis.IndicatorSize = new Size(30, 30);
        R_YAxis.Location = new Point(573, 9);
        R_YAxis.Name = "R_YAxis";
        R_YAxis.Size = new Size(168, 56);
        R_YAxis.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_YAxis.TabIndex = 5;
        R_YAxis.TabStop = false;
        R_YAxis.Text = "Y Axis";
        // 
        // R_ZAxis
        // 
        R_ZAxis.AccessibleRole = AccessibleRole.Indicator;
        R_ZAxis.CornerRadius = 10;
        R_ZAxis.IndicatorSize = new Size(30, 30);
        R_ZAxis.Location = new Point(749, 9);
        R_ZAxis.Name = "R_ZAxis";
        R_ZAxis.Size = new Size(168, 56);
        R_ZAxis.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_ZAxis.TabIndex = 6;
        R_ZAxis.TabStop = false;
        R_ZAxis.Text = "Z Axis";
        // 
        // R_AxisAlarm
        // 
        R_AxisAlarm.AccessibleRole = AccessibleRole.Indicator;
        R_AxisAlarm.CornerRadius = 10;
        R_AxisAlarm.IndicatorColor = Color.Red;
        R_AxisAlarm.IndicatorSize = new Size(30, 30);
        R_AxisAlarm.Location = new Point(1101, 9);
        R_AxisAlarm.Name = "R_AxisAlarm";
        R_AxisAlarm.Size = new Size(168, 56);
        R_AxisAlarm.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_AxisAlarm.TabIndex = 7;
        R_AxisAlarm.TabStop = false;
        R_AxisAlarm.Text = "Alarm";
        R_AxisAlarm.Tone = NT.Tools.SButtonTone.Danger;
        // 
        // R_Emergency
        // 
        R_Emergency.AccessibleRole = AccessibleRole.Indicator;
        R_Emergency.CornerRadius = 10;
        R_Emergency.IndicatorColor = Color.Red;
        R_Emergency.IndicatorSize = new Size(30, 30);
        R_Emergency.Location = new Point(1277, 9);
        R_Emergency.Name = "R_Emergency";
        R_Emergency.Size = new Size(168, 56);
        R_Emergency.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_Emergency.TabIndex = 8;
        R_Emergency.TabStop = false;
        R_Emergency.Text = "Emergency";
        R_Emergency.Tone = NT.Tools.SButtonTone.Danger;
        // 
        // R_ThemeToggle
        // 
        R_ThemeToggle.AccessibleName = "Switch to light theme";
        R_ThemeToggle.AutoEllipsis = true;
        R_ThemeToggle.AutoToggleTheme = false;
        R_ThemeToggle.CornerRadius = 10;
        R_ThemeToggle.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_ThemeToggle.GradientStrength = 0.14F;
        R_ThemeToggle.IconSize = new Size(32, 32);
        R_ThemeToggle.Location = new Point(1727, 9);
        R_ThemeToggle.Name = "R_ThemeToggle";
        R_ThemeToggle.Padding = new Padding(4);
        R_ThemeToggle.Size = new Size(56, 56);
        R_ThemeToggle.TabIndex = 11;
        R_ThemeToggle.TabStop = false;
        R_ThemeToggle.TextImageRelation = TextImageRelation.Overlay;
        R_ThemeToggle.ThemeManager = R_ThemeManager;
        R_ThemeToggle.UseTextColorForImage = true;
        // 
        // R_Minimize
        // 
        R_Minimize.AutoEllipsis = true;
        R_Minimize.CornerRadius = 10;
        R_Minimize.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_Minimize.GradientStrength = 0.14F;
        R_Minimize.IconSize = new Size(48, 48);
        R_Minimize.Image = Properties.Resources.ST__10_b;
        R_Minimize.Location = new Point(1791, 9);
        R_Minimize.Name = "R_Minimize";
        R_Minimize.Padding = new Padding(10);
        R_Minimize.Size = new Size(56, 56);
        R_Minimize.TabIndex = 12;
        R_Minimize.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_Exit
        // 
        R_Exit.AutoEllipsis = true;
        R_Exit.CornerRadius = 10;
        R_Exit.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_Exit.GradientStrength = 0.14F;
        R_Exit.IconSize = new Size(48, 48);
        R_Exit.Image = Properties.Resources.ST__10_;
        R_Exit.Location = new Point(1855, 9);
        R_Exit.Margin = new Padding(0, 0, 0, 8);
        R_Exit.Name = "R_Exit";
        R_Exit.Padding = new Padding(10);
        R_Exit.Size = new Size(56, 56);
        R_Exit.TabIndex = 13;
        R_Exit.Tone = NT.Tools.SButtonTone.Danger;
        // 
        // R_Configuration
        // 
        R_Configuration.AutoEllipsis = true;
        R_Configuration.CornerRadius = 10;
        R_Configuration.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_Configuration.Font = new Font("Segoe UI Semibold", 15F);
        R_Configuration.GradientStrength = 0.14F;
        R_Configuration.IconSize = new Size(48, 48);
        R_Configuration.Image = Properties.Resources.ST__90_;
        R_Configuration.Location = new Point(1687, 1015);
        R_Configuration.Margin = new Padding(0);
        R_Configuration.Name = "R_Configuration";
        R_Configuration.Padding = new Padding(12, 6, 6, 6);
        R_Configuration.Size = new Size(224, 56);
        R_Configuration.TabIndex = 10;
        R_Configuration.Text = "Configuration";
        R_Configuration.TextAlign = ContentAlignment.MiddleLeft;
        R_Configuration.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_ProgramPath
        // 
        R_ProgramPath.AccessibleRole = AccessibleRole.StaticText;
        R_ProgramPath.CornerRadius = 10;
        R_ProgramPath.Location = new Point(8, 17);
        R_ProgramPath.Margin = new Padding(8, 16, 0, 8);
        R_ProgramPath.Name = "R_ProgramPath";
        R_ProgramPath.Padding = new Padding(8, 0, 0, 0);
        R_ProgramPath.Size = new Size(951, 34);
        R_ProgramPath.TabIndex = 2;
        R_ProgramPath.TabStop = false;
        R_ProgramPath.Text = "No program loaded";
        // 
        // R_MachineFeedValue
        // 
        R_MachineFeedValue.AccessibleRole = AccessibleRole.ProgressBar;
        R_MachineFeedValue.CornerRadius = 10;
        R_MachineFeedValue.Font = new Font("Courier New", 22.125F, FontStyle.Regular, GraphicsUnit.Point, 0);
        R_MachineFeedValue.Location = new Point(1453, 9);
        R_MachineFeedValue.Margin = new Padding(0, 0, 8, 8);
        R_MachineFeedValue.Name = "R_MachineFeedValue";
        R_MachineFeedValue.Padding = new Padding(12, 0, 0, 0);
        R_MachineFeedValue.ProgressInset = 8;
        R_MachineFeedValue.Size = new Size(266, 56);
        R_MachineFeedValue.TabIndex = 2;
        R_MachineFeedValue.TabStop = false;
        R_MachineFeedValue.Text = "V: 000.0 mm/s";
        // 
        // R_ProgramView3D
        // 
        R_ProgramView3D.BackColor = Color.Transparent;
        R_ProgramView3D.CornerRadius = 10;
        R_ProgramView3D.LightColor = Color.White;
        R_ProgramView3D.Location = new Point(9, 73);
        R_ProgramView3D.Margin = new Padding(0, 0, 8, 8);
        R_ProgramView3D.MinimumSize = new Size(180, 140);
        R_ProgramView3D.Name = "R_ProgramView3D";
        R_ProgramView3D.Size = new Size(1283, 672);
        R_ProgramView3D.TabIndex = 7;
        // 
        // R_MachinePositionPanel
        // 
        R_MachinePositionPanel.Controls.Add(R_MachinePositionTitle);
        R_MachinePositionPanel.Controls.Add(R_CommandPositionX);
        R_MachinePositionPanel.Controls.Add(R_CommandPositionY);
        R_MachinePositionPanel.Controls.Add(R_CommandPositionZ);
        R_MachinePositionPanel.CornerRadius = 10;
        R_MachinePositionPanel.Location = new Point(1300, 73);
        R_MachinePositionPanel.Name = "R_MachinePositionPanel";
        R_MachinePositionPanel.Padding = new Padding(0);
        R_MachinePositionPanel.Size = new Size(302, 221);
        R_MachinePositionPanel.TabIndex = 2;
        // 
        // R_MachinePositionTitle
        // 
        R_MachinePositionTitle.AccessibleRole = AccessibleRole.StaticText;
        R_MachinePositionTitle.CornerRadius = 10;
        R_MachinePositionTitle.Location = new Point(9, 17);
        R_MachinePositionTitle.Margin = new Padding(8, 16, 8, 8);
        R_MachinePositionTitle.Name = "R_MachinePositionTitle";
        R_MachinePositionTitle.Padding = new Padding(12, 0, 12, 0);
        R_MachinePositionTitle.Size = new Size(284, 34);
        R_MachinePositionTitle.TabIndex = 1;
        R_MachinePositionTitle.TabStop = false;
        R_MachinePositionTitle.Text = "Machine position";
        // 
        // R_CommandPositionX
        // 
        R_CommandPositionX.AccessibleRole = AccessibleRole.StaticText;
        R_CommandPositionX.CornerRadius = 10;
        R_CommandPositionX.Font = new Font("Courier New", 22.2F);
        R_CommandPositionX.Location = new Point(9, 59);
        R_CommandPositionX.Margin = new Padding(8, 0, 8, 8);
        R_CommandPositionX.Name = "R_CommandPositionX";
        R_CommandPositionX.Padding = new Padding(12, 4, 0, 4);
        R_CommandPositionX.Size = new Size(282, 40);
        R_CommandPositionX.TabIndex = 2;
        R_CommandPositionX.TabStop = false;
        R_CommandPositionX.Text = "X  0000.000 mm";
        // 
        // R_CommandPositionY
        // 
        R_CommandPositionY.AccessibleRole = AccessibleRole.StaticText;
        R_CommandPositionY.CornerRadius = 10;
        R_CommandPositionY.Font = new Font("Courier New", 22.2F);
        R_CommandPositionY.Location = new Point(9, 107);
        R_CommandPositionY.Margin = new Padding(8, 0, 8, 8);
        R_CommandPositionY.Name = "R_CommandPositionY";
        R_CommandPositionY.Padding = new Padding(12, 4, 0, 4);
        R_CommandPositionY.Size = new Size(282, 40);
        R_CommandPositionY.TabIndex = 3;
        R_CommandPositionY.TabStop = false;
        R_CommandPositionY.Text = "Y  0000.000 mm";
        // 
        // R_CommandPositionZ
        // 
        R_CommandPositionZ.AccessibleRole = AccessibleRole.StaticText;
        R_CommandPositionZ.CornerRadius = 10;
        R_CommandPositionZ.Font = new Font("Courier New", 22.2F);
        R_CommandPositionZ.Location = new Point(9, 155);
        R_CommandPositionZ.Margin = new Padding(8, 0, 8, 8);
        R_CommandPositionZ.Name = "R_CommandPositionZ";
        R_CommandPositionZ.Padding = new Padding(12, 4, 0, 4);
        R_CommandPositionZ.Size = new Size(282, 40);
        R_CommandPositionZ.TabIndex = 4;
        R_CommandPositionZ.TabStop = false;
        R_CommandPositionZ.Text = "Z  0000.000 mm";
        // 
        // R_WorkPositionPanel
        // 
        R_WorkPositionPanel.Controls.Add(R_WorkPositionTitle);
        R_WorkPositionPanel.Controls.Add(R_WorkPositionX);
        R_WorkPositionPanel.Controls.Add(R_WorkPositionY);
        R_WorkPositionPanel.Controls.Add(R_WorkPositionZ);
        R_WorkPositionPanel.CornerRadius = 10;
        R_WorkPositionPanel.Location = new Point(1610, 73);
        R_WorkPositionPanel.Name = "R_WorkPositionPanel";
        R_WorkPositionPanel.Padding = new Padding(0);
        R_WorkPositionPanel.Size = new Size(301, 221);
        R_WorkPositionPanel.TabIndex = 3;
        // 
        // R_WorkPositionTitle
        // 
        R_WorkPositionTitle.AccessibleRole = AccessibleRole.StaticText;
        R_WorkPositionTitle.CornerRadius = 10;
        R_WorkPositionTitle.Location = new Point(9, 17);
        R_WorkPositionTitle.Margin = new Padding(8, 16, 8, 8);
        R_WorkPositionTitle.Name = "R_WorkPositionTitle";
        R_WorkPositionTitle.Padding = new Padding(12, 0, 12, 0);
        R_WorkPositionTitle.Size = new Size(284, 34);
        R_WorkPositionTitle.TabIndex = 1;
        R_WorkPositionTitle.TabStop = false;
        R_WorkPositionTitle.Text = "Work position";
        // 
        // R_WorkPositionX
        // 
        R_WorkPositionX.AccessibleRole = AccessibleRole.StaticText;
        R_WorkPositionX.CornerRadius = 10;
        R_WorkPositionX.Font = new Font("Courier New", 22.2F);
        R_WorkPositionX.Location = new Point(9, 59);
        R_WorkPositionX.Margin = new Padding(8, 0, 8, 8);
        R_WorkPositionX.Name = "R_WorkPositionX";
        R_WorkPositionX.Padding = new Padding(12, 4, 0, 4);
        R_WorkPositionX.Size = new Size(282, 40);
        R_WorkPositionX.TabIndex = 2;
        R_WorkPositionX.TabStop = false;
        R_WorkPositionX.Text = "X  0000.000 mm";
        // 
        // R_WorkPositionY
        // 
        R_WorkPositionY.AccessibleRole = AccessibleRole.StaticText;
        R_WorkPositionY.CornerRadius = 10;
        R_WorkPositionY.Font = new Font("Courier New", 22.2F);
        R_WorkPositionY.Location = new Point(10, 107);
        R_WorkPositionY.Margin = new Padding(8, 0, 8, 8);
        R_WorkPositionY.Name = "R_WorkPositionY";
        R_WorkPositionY.Padding = new Padding(12, 4, 0, 4);
        R_WorkPositionY.Size = new Size(282, 40);
        R_WorkPositionY.TabIndex = 3;
        R_WorkPositionY.TabStop = false;
        R_WorkPositionY.Text = "Y  0000.000 mm";
        // 
        // R_WorkPositionZ
        // 
        R_WorkPositionZ.AccessibleRole = AccessibleRole.StaticText;
        R_WorkPositionZ.CornerRadius = 10;
        R_WorkPositionZ.Font = new Font("Courier New", 22.2F);
        R_WorkPositionZ.Location = new Point(10, 155);
        R_WorkPositionZ.Margin = new Padding(8, 0, 8, 8);
        R_WorkPositionZ.Name = "R_WorkPositionZ";
        R_WorkPositionZ.Padding = new Padding(12, 4, 0, 4);
        R_WorkPositionZ.Size = new Size(282, 40);
        R_WorkPositionZ.TabIndex = 4;
        R_WorkPositionZ.TabStop = false;
        R_WorkPositionZ.Text = "Z  0000.000 mm";
        // 
        // R_JogPanel
        // 
        R_JogPanel.Controls.Add(R_GoToZ);
        R_JogPanel.Controls.Add(R_GoToY);
        R_JogPanel.Controls.Add(R_JogPositionHeader);
        R_JogPanel.Controls.Add(R_JogStepHeader);
        R_JogPanel.Controls.Add(R_PositionX);
        R_JogPanel.Controls.Add(R_PositionY);
        R_JogPanel.Controls.Add(R_GoToX);
        R_JogPanel.Controls.Add(R_JogAxisXLabel);
        R_JogPanel.Controls.Add(R_PositionZ);
        R_JogPanel.Controls.Add(R_JogAxisYLabel);
        R_JogPanel.Controls.Add(R_JogVelocityHeader);
        R_JogPanel.Controls.Add(R_JogAxisZLabel);
        R_JogPanel.Controls.Add(R_JogVelocityX);
        R_JogPanel.Controls.Add(R_JogVelocityY);
        R_JogPanel.Controls.Add(R_JogVelocityZ);
        R_JogPanel.Controls.Add(R_StepX);
        R_JogPanel.Controls.Add(R_StepY);
        R_JogPanel.Controls.Add(R_StepZ);
        R_JogPanel.Controls.Add(R_JogNXPY);
        R_JogPanel.Controls.Add(R_JogPY);
        R_JogPanel.Controls.Add(R_JogPXPY);
        R_JogPanel.Controls.Add(R_JogPZ);
        R_JogPanel.Controls.Add(R_JogNX);
        R_JogPanel.Controls.Add(R_JogMode);
        R_JogPanel.Controls.Add(R_JogPX);
        R_JogPanel.Controls.Add(R_JogPause);
        R_JogPanel.Controls.Add(R_JogNXNY);
        R_JogPanel.Controls.Add(R_JogNY);
        R_JogPanel.Controls.Add(R_JogPXNY);
        R_JogPanel.Controls.Add(R_JogNZ);
        R_JogPanel.Controls.Add(R_JogFeedOverride);
        R_JogPanel.CornerRadius = 10;
        R_JogPanel.Location = new Point(1300, 562);
        R_JogPanel.Margin = new Padding(0, 0, 0, 8);
        R_JogPanel.Name = "R_JogPanel";
        R_JogPanel.Padding = new Padding(0);
        R_JogPanel.Size = new Size(611, 445);
        R_JogPanel.TabIndex = 4;
        // 
        // R_GoToZ
        // 
        R_GoToZ.AutoEllipsis = true;
        R_GoToZ.CornerRadius = 10;
        R_GoToZ.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToZ.GradientStrength = 0.14F;
        R_GoToZ.Location = new Point(369, 189);
        R_GoToZ.Margin = new Padding(8, 0, 8, 8);
        R_GoToZ.Name = "R_GoToZ";
        R_GoToZ.Size = new Size(112, 56);
        R_GoToZ.TabIndex = 48;
        R_GoToZ.Text = "Goto Z";
        R_GoToZ.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_GoToY
        // 
        R_GoToY.AutoEllipsis = true;
        R_GoToY.CornerRadius = 10;
        R_GoToY.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToY.GradientStrength = 0.14F;
        R_GoToY.Location = new Point(369, 126);
        R_GoToY.Margin = new Padding(8, 0, 8, 8);
        R_GoToY.Name = "R_GoToY";
        R_GoToY.Size = new Size(112, 56);
        R_GoToY.TabIndex = 47;
        R_GoToY.Text = "Goto Y";
        R_GoToY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPositionHeader
        // 
        R_JogPositionHeader.AccessibleRole = AccessibleRole.StaticText;
        R_JogPositionHeader.CornerRadius = 10;
        R_JogPositionHeader.Location = new Point(368, 18);
        R_JogPositionHeader.Margin = new Padding(0, 16, 0, 8);
        R_JogPositionHeader.Name = "R_JogPositionHeader";
        R_JogPositionHeader.Padding = new Padding(0);
        R_JogPositionHeader.Size = new Size(233, 34);
        R_JogPositionHeader.TabIndex = 45;
        R_JogPositionHeader.TabStop = false;
        R_JogPositionHeader.Text = "Position(mm)";
        R_JogPositionHeader.TextAlign = ContentAlignment.MiddleRight;
        // 
        // R_JogStepHeader
        // 
        R_JogStepHeader.AccessibleRole = AccessibleRole.StaticText;
        R_JogStepHeader.CornerRadius = 10;
        R_JogStepHeader.Location = new Point(189, 19);
        R_JogStepHeader.Margin = new Padding(0, 16, 0, 8);
        R_JogStepHeader.Name = "R_JogStepHeader";
        R_JogStepHeader.Padding = new Padding(0);
        R_JogStepHeader.Size = new Size(112, 34);
        R_JogStepHeader.TabIndex = 41;
        R_JogStepHeader.TabStop = false;
        R_JogStepHeader.Text = "Step(mm)";
        // 
        // R_PositionX
        // 
        R_PositionX.BackColor = Color.Transparent;
        R_PositionX.CornerRadius = 10;
        R_PositionX.Location = new Point(490, 60);
        R_PositionX.Name = "R_PositionX";
        R_PositionX.Size = new Size(112, 56);
        R_PositionX.TabIndex = 42;
        R_PositionX.TabStop = false;
        R_PositionX.TextAlign = HorizontalAlignment.Center;
        // 
        // R_PositionY
        // 
        R_PositionY.BackColor = Color.Transparent;
        R_PositionY.CornerRadius = 10;
        R_PositionY.Location = new Point(489, 124);
        R_PositionY.Name = "R_PositionY";
        R_PositionY.Size = new Size(112, 56);
        R_PositionY.TabIndex = 43;
        R_PositionY.TabStop = false;
        R_PositionY.TextAlign = HorizontalAlignment.Center;
        // 
        // R_GoToX
        // 
        R_GoToX.AutoEllipsis = true;
        R_GoToX.CornerRadius = 10;
        R_GoToX.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToX.GradientStrength = 0.14F;
        R_GoToX.Location = new Point(368, 62);
        R_GoToX.Margin = new Padding(8, 0, 8, 8);
        R_GoToX.Name = "R_GoToX";
        R_GoToX.Size = new Size(112, 56);
        R_GoToX.TabIndex = 46;
        R_GoToX.Text = "Goto X";
        R_GoToX.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogAxisXLabel
        // 
        R_JogAxisXLabel.AccessibleRole = AccessibleRole.StaticText;
        R_JogAxisXLabel.CornerRadius = 10;
        R_JogAxisXLabel.Location = new Point(9, 60);
        R_JogAxisXLabel.Margin = new Padding(8, 0, 8, 8);
        R_JogAxisXLabel.Name = "R_JogAxisXLabel";
        R_JogAxisXLabel.Padding = new Padding(0, 0, 8, 0);
        R_JogAxisXLabel.Size = new Size(52, 56);
        R_JogAxisXLabel.TabIndex = 5;
        R_JogAxisXLabel.TabStop = false;
        R_JogAxisXLabel.Text = "X";
        R_JogAxisXLabel.TextAlign = ContentAlignment.MiddleRight;
        R_JogAxisXLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_PositionZ
        // 
        R_PositionZ.BackColor = Color.Transparent;
        R_PositionZ.CornerRadius = 10;
        R_PositionZ.Location = new Point(490, 188);
        R_PositionZ.Name = "R_PositionZ";
        R_PositionZ.Size = new Size(112, 56);
        R_PositionZ.TabIndex = 44;
        R_PositionZ.TabStop = false;
        R_PositionZ.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogAxisYLabel
        // 
        R_JogAxisYLabel.AccessibleRole = AccessibleRole.StaticText;
        R_JogAxisYLabel.CornerRadius = 10;
        R_JogAxisYLabel.Location = new Point(9, 124);
        R_JogAxisYLabel.Margin = new Padding(8, 0, 8, 8);
        R_JogAxisYLabel.Name = "R_JogAxisYLabel";
        R_JogAxisYLabel.Padding = new Padding(0, 0, 8, 0);
        R_JogAxisYLabel.Size = new Size(52, 56);
        R_JogAxisYLabel.TabIndex = 6;
        R_JogAxisYLabel.TabStop = false;
        R_JogAxisYLabel.Text = "Y";
        R_JogAxisYLabel.TextAlign = ContentAlignment.MiddleRight;
        R_JogAxisYLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_JogVelocityHeader
        // 
        R_JogVelocityHeader.AccessibleRole = AccessibleRole.StaticText;
        R_JogVelocityHeader.CornerRadius = 10;
        R_JogVelocityHeader.Location = new Point(69, 19);
        R_JogVelocityHeader.Margin = new Padding(0, 16, 0, 8);
        R_JogVelocityHeader.Name = "R_JogVelocityHeader";
        R_JogVelocityHeader.Padding = new Padding(0);
        R_JogVelocityHeader.Size = new Size(112, 34);
        R_JogVelocityHeader.TabIndex = 2;
        R_JogVelocityHeader.TabStop = false;
        R_JogVelocityHeader.Text = "V(mm/s)";
        // 
        // R_JogAxisZLabel
        // 
        R_JogAxisZLabel.AccessibleRole = AccessibleRole.StaticText;
        R_JogAxisZLabel.CornerRadius = 10;
        R_JogAxisZLabel.Location = new Point(9, 188);
        R_JogAxisZLabel.Margin = new Padding(8, 0, 8, 8);
        R_JogAxisZLabel.Name = "R_JogAxisZLabel";
        R_JogAxisZLabel.Padding = new Padding(0, 0, 8, 0);
        R_JogAxisZLabel.Size = new Size(52, 56);
        R_JogAxisZLabel.TabIndex = 7;
        R_JogAxisZLabel.TabStop = false;
        R_JogAxisZLabel.Text = "Z";
        R_JogAxisZLabel.TextAlign = ContentAlignment.MiddleRight;
        R_JogAxisZLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_JogVelocityX
        // 
        R_JogVelocityX.BackColor = Color.Transparent;
        R_JogVelocityX.CornerRadius = 10;
        R_JogVelocityX.Location = new Point(69, 61);
        R_JogVelocityX.Name = "R_JogVelocityX";
        R_JogVelocityX.Size = new Size(112, 56);
        R_JogVelocityX.TabIndex = 15;
        R_JogVelocityX.TabStop = false;
        R_JogVelocityX.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogVelocityY
        // 
        R_JogVelocityY.BackColor = Color.Transparent;
        R_JogVelocityY.CornerRadius = 10;
        R_JogVelocityY.Location = new Point(69, 125);
        R_JogVelocityY.Name = "R_JogVelocityY";
        R_JogVelocityY.Size = new Size(112, 56);
        R_JogVelocityY.TabIndex = 16;
        R_JogVelocityY.TabStop = false;
        R_JogVelocityY.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogVelocityZ
        // 
        R_JogVelocityZ.BackColor = Color.Transparent;
        R_JogVelocityZ.CornerRadius = 10;
        R_JogVelocityZ.Location = new Point(69, 189);
        R_JogVelocityZ.Name = "R_JogVelocityZ";
        R_JogVelocityZ.Size = new Size(112, 56);
        R_JogVelocityZ.TabIndex = 17;
        R_JogVelocityZ.TabStop = false;
        R_JogVelocityZ.TextAlign = HorizontalAlignment.Center;
        // 
        // R_StepX
        // 
        R_StepX.BackColor = Color.Transparent;
        R_StepX.CornerRadius = 10;
        R_StepX.Location = new Point(189, 61);
        R_StepX.Name = "R_StepX";
        R_StepX.Size = new Size(112, 56);
        R_StepX.TabIndex = 18;
        R_StepX.TabStop = false;
        R_StepX.TextAlign = HorizontalAlignment.Center;
        // 
        // R_StepY
        // 
        R_StepY.BackColor = Color.Transparent;
        R_StepY.CornerRadius = 10;
        R_StepY.Location = new Point(189, 125);
        R_StepY.Name = "R_StepY";
        R_StepY.Size = new Size(112, 56);
        R_StepY.TabIndex = 19;
        R_StepY.TabStop = false;
        R_StepY.TextAlign = HorizontalAlignment.Center;
        // 
        // R_StepZ
        // 
        R_StepZ.BackColor = Color.Transparent;
        R_StepZ.CornerRadius = 10;
        R_StepZ.Location = new Point(189, 189);
        R_StepZ.Name = "R_StepZ";
        R_StepZ.Size = new Size(112, 56);
        R_StepZ.TabIndex = 20;
        R_StepZ.TabStop = false;
        R_StepZ.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogNXPY
        // 
        R_JogNXPY.AutoEllipsis = true;
        R_JogNXPY.CornerRadius = 10;
        R_JogNXPY.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNXPY.GradientStrength = 0.14F;
        R_JogNXPY.IconSize = new Size(48, 48);
        R_JogNXPY.Image = Properties.Resources.AR__14_;
        R_JogNXPY.Location = new Point(9, 252);
        R_JogNXPY.Margin = new Padding(8, 0, 8, 8);
        R_JogNXPY.Name = "R_JogNXPY";
        R_JogNXPY.Size = new Size(112, 56);
        R_JogNXPY.TabIndex = 28;
        R_JogNXPY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPY
        // 
        R_JogPY.AutoEllipsis = true;
        R_JogPY.CornerRadius = 10;
        R_JogPY.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPY.GradientStrength = 0.14F;
        R_JogPY.Location = new Point(129, 252);
        R_JogPY.Name = "R_JogPY";
        R_JogPY.Size = new Size(112, 56);
        R_JogPY.TabIndex = 29;
        R_JogPY.Text = "Y+";
        R_JogPY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPXPY
        // 
        R_JogPXPY.AutoEllipsis = true;
        R_JogPXPY.CornerRadius = 10;
        R_JogPXPY.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPXPY.GradientStrength = 0.14F;
        R_JogPXPY.IconSize = new Size(48, 48);
        R_JogPXPY.Image = Properties.Resources.AR__15_;
        R_JogPXPY.Location = new Point(249, 252);
        R_JogPXPY.Name = "R_JogPXPY";
        R_JogPXPY.Padding = new Padding(0);
        R_JogPXPY.Size = new Size(112, 56);
        R_JogPXPY.TabIndex = 30;
        R_JogPXPY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPZ
        // 
        R_JogPZ.AutoEllipsis = true;
        R_JogPZ.CornerRadius = 10;
        R_JogPZ.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPZ.GradientStrength = 0.14F;
        R_JogPZ.Location = new Point(489, 252);
        R_JogPZ.Name = "R_JogPZ";
        R_JogPZ.Size = new Size(112, 56);
        R_JogPZ.TabIndex = 31;
        R_JogPZ.Text = "Z+";
        // 
        // R_JogNX
        // 
        R_JogNX.AutoEllipsis = true;
        R_JogNX.CornerRadius = 10;
        R_JogNX.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNX.GradientStrength = 0.14F;
        R_JogNX.Location = new Point(9, 316);
        R_JogNX.Margin = new Padding(8, 0, 8, 8);
        R_JogNX.Name = "R_JogNX";
        R_JogNX.Size = new Size(112, 56);
        R_JogNX.TabIndex = 32;
        R_JogNX.Text = "X−";
        R_JogNX.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogMode
        // 
        R_JogMode.AutoEllipsis = true;
        R_JogMode.CheckedFillColor = Color.FromArgb(90, 0, 255, 255);
        R_JogMode.CornerRadius = 10;
        R_JogMode.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogMode.GradientStrength = 0.14F;
        R_JogMode.IndicatorPlacement = NT.Tools.SIndicatorPlacement.Bottom;
        R_JogMode.IndicatorStyle = NT.Tools.SIndicatorStyle.None;
        R_JogMode.Location = new Point(129, 316);
        R_JogMode.Name = "R_JogMode";
        R_JogMode.Padding = new Padding(0, 4, 0, 4);
        R_JogMode.Size = new Size(112, 56);
        R_JogMode.TabIndex = 33;
        R_JogMode.Text = "Step Jog";
        R_JogMode.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // R_JogPX
        // 
        R_JogPX.AutoEllipsis = true;
        R_JogPX.CornerRadius = 10;
        R_JogPX.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPX.GradientStrength = 0.14F;
        R_JogPX.Location = new Point(249, 316);
        R_JogPX.Name = "R_JogPX";
        R_JogPX.Size = new Size(112, 56);
        R_JogPX.TabIndex = 34;
        R_JogPX.Text = "X+";
        R_JogPX.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPause
        // 
        R_JogPause.AutoEllipsis = true;
        R_JogPause.CornerRadius = 10;
        R_JogPause.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPause.GradientStrength = 0.14F;
        R_JogPause.Location = new Point(489, 316);
        R_JogPause.Name = "R_JogPause";
        R_JogPause.Size = new Size(112, 56);
        R_JogPause.TabIndex = 35;
        R_JogPause.Text = "Pause";
        R_JogPause.Tone = NT.Tools.SButtonTone.Warning;
        // 
        // R_JogNXNY
        // 
        R_JogNXNY.AutoEllipsis = true;
        R_JogNXNY.CornerRadius = 10;
        R_JogNXNY.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNXNY.GradientStrength = 0.14F;
        R_JogNXNY.IconSize = new Size(48, 48);
        R_JogNXNY.Image = Properties.Resources.AR__13_;
        R_JogNXNY.Location = new Point(9, 380);
        R_JogNXNY.Margin = new Padding(8, 0, 8, 8);
        R_JogNXNY.Name = "R_JogNXNY";
        R_JogNXNY.Size = new Size(112, 56);
        R_JogNXNY.TabIndex = 36;
        R_JogNXNY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogNY
        // 
        R_JogNY.AutoEllipsis = true;
        R_JogNY.CornerRadius = 10;
        R_JogNY.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNY.GradientStrength = 0.14F;
        R_JogNY.Location = new Point(129, 380);
        R_JogNY.Name = "R_JogNY";
        R_JogNY.Size = new Size(112, 56);
        R_JogNY.TabIndex = 37;
        R_JogNY.Text = "Y−";
        R_JogNY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPXNY
        // 
        R_JogPXNY.AutoEllipsis = true;
        R_JogPXNY.CornerRadius = 10;
        R_JogPXNY.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPXNY.GradientStrength = 0.14F;
        R_JogPXNY.IconSize = new Size(48, 48);
        R_JogPXNY.Image = Properties.Resources.AR__16_;
        R_JogPXNY.Location = new Point(249, 380);
        R_JogPXNY.Name = "R_JogPXNY";
        R_JogPXNY.Size = new Size(112, 56);
        R_JogPXNY.TabIndex = 38;
        R_JogPXNY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogNZ
        // 
        R_JogNZ.AutoEllipsis = true;
        R_JogNZ.CornerRadius = 10;
        R_JogNZ.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNZ.GradientStrength = 0.14F;
        R_JogNZ.Location = new Point(489, 380);
        R_JogNZ.Name = "R_JogNZ";
        R_JogNZ.Size = new Size(112, 56);
        R_JogNZ.TabIndex = 39;
        R_JogNZ.Text = "Z−";
        // 
        // R_JogFeedOverride
        // 
        R_JogFeedOverride.AccessibleRole = AccessibleRole.Slider;
        R_JogFeedOverride.CornerRadius = 10;
        R_JogFeedOverride.Location = new Point(369, 253);
        R_JogFeedOverride.Name = "R_JogFeedOverride";
        R_JogFeedOverride.Minimum = 5D;
        R_JogFeedOverride.Maximum = 100D;
        R_JogFeedOverride.Orientation = NT.Tools.SSliderOrientation.Vertical;
        R_JogFeedOverride.ShowSteps = true;
        R_JogFeedOverride.Size = new Size(112, 184);
        R_JogFeedOverride.SliderStyle = NT.Tools.SSliderStyle.Fader;
        R_JogFeedOverride.Step = 5D;
        R_JogFeedOverride.SmallChange = 5D;
        R_JogFeedOverride.LargeChange = 20D;
        R_JogFeedOverride.TabIndex = 40;
        R_JogFeedOverride.Value = 100D;
        R_JogFeedOverride.ValueSuffix = "%";
        // 
        // R_Home
        // 
        R_Home.AutoEllipsis = true;
        R_Home.CornerRadius = 10;
        R_Home.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_Home.GradientStrength = 0.14F;
        R_Home.IconSize = new Size(48, 48);
        R_Home.Image = Properties.Resources.R_Home;
        R_Home.ImageAlign = ContentAlignment.MiddleLeft;
        R_Home.Location = new Point(9, 59);
        R_Home.Name = "R_Home";
        R_Home.Padding = new Padding(6);
        R_Home.Size = new Size(168, 56);
        R_Home.TabIndex = 8;
        R_Home.Text = "Home";
        R_Home.TextImageRelation = TextImageRelation.Overlay;
        // 
        // R_GoHome
        // 
        R_GoHome.AutoEllipsis = true;
        R_GoHome.CornerRadius = 10;
        R_GoHome.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoHome.GradientStrength = 0.14F;
        R_GoHome.Location = new Point(9, 187);
        R_GoHome.Name = "R_GoHome";
        R_GoHome.Padding = new Padding(0, 4, 0, 4);
        R_GoHome.Size = new Size(168, 56);
        R_GoHome.TabIndex = 9;
        R_GoHome.Text = "Go Home";
        R_GoHome.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_GoToReference
        // 
        R_GoToReference.AutoEllipsis = true;
        R_GoToReference.CornerRadius = 10;
        R_GoToReference.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToReference.GradientStrength = 0.14F;
        R_GoToReference.Location = new Point(9, 123);
        R_GoToReference.Name = "R_GoToReference";
        R_GoToReference.Padding = new Padding(0, 4, 0, 4);
        R_GoToReference.Size = new Size(168, 56);
        R_GoToReference.TabIndex = 10;
        R_GoToReference.Text = "Go Reference";
        R_GoToReference.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_ReferencePause
        // 
        R_ReferencePause.AutoEllipsis = true;
        R_ReferencePause.CornerRadius = 10;
        R_ReferencePause.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_ReferencePause.GradientStrength = 0.14F;
        R_ReferencePause.Location = new Point(185, 123);
        R_ReferencePause.Name = "R_ReferencePause";
        R_ReferencePause.Padding = new Padding(0, 4, 0, 4);
        R_ReferencePause.Size = new Size(112, 56);
        R_ReferencePause.TabIndex = 11;
        R_ReferencePause.Text = "Pause";
        R_ReferencePause.Tone = NT.Tools.SButtonTone.Warning;
        // 
        // R_RefZZero
        // 
        R_RefZZero.AutoEllipsis = true;
        R_RefZZero.CornerRadius = 10;
        R_RefZZero.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_RefZZero.GradientStrength = 0.14F;
        R_RefZZero.Location = new Point(489, 187);
        R_RefZZero.Name = "R_RefZZero";
        R_RefZZero.Padding = new Padding(0, 4, 0, 4);
        R_RefZZero.Size = new Size(112, 56);
        R_RefZZero.TabIndex = 23;
        R_RefZZero.Text = "Set Z0";
        R_RefZZero.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_RefYZero
        // 
        R_RefYZero.AutoEllipsis = true;
        R_RefYZero.CornerRadius = 10;
        R_RefYZero.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_RefYZero.GradientStrength = 0.14F;
        R_RefYZero.Location = new Point(489, 123);
        R_RefYZero.Name = "R_RefYZero";
        R_RefYZero.Padding = new Padding(0, 4, 0, 4);
        R_RefYZero.Size = new Size(112, 56);
        R_RefYZero.TabIndex = 22;
        R_RefYZero.Text = "Set Y0";
        R_RefYZero.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_RefXZero
        // 
        R_RefXZero.AutoEllipsis = true;
        R_RefXZero.CornerRadius = 10;
        R_RefXZero.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_RefXZero.GradientStrength = 0.14F;
        R_RefXZero.Location = new Point(489, 59);
        R_RefXZero.Name = "R_RefXZero";
        R_RefXZero.Padding = new Padding(0, 4, 0, 4);
        R_RefXZero.Size = new Size(112, 56);
        R_RefXZero.TabIndex = 21;
        R_RefXZero.Text = "Set X0";
        R_RefXZero.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_ProgramPanel
        // 
        R_ProgramPanel.Controls.Add(R_ProgramPath);
        R_ProgramPanel.Controls.Add(R_GCodeView);
        R_ProgramPanel.Controls.Add(R_FeedOverride);
        R_ProgramPanel.Controls.Add(R_RunProgram);
        R_ProgramPanel.Controls.Add(R_PauseProgram);
        R_ProgramPanel.Controls.Add(R_ResetProgram);
        R_ProgramPanel.CornerRadius = 10;
        R_ProgramPanel.Location = new Point(9, 753);
        R_ProgramPanel.Name = "R_ProgramPanel";
        R_ProgramPanel.Padding = new Padding(0);
        R_ProgramPanel.Size = new Size(968, 254);
        R_ProgramPanel.TabIndex = 5;
        // 
        // R_GCodeView
        // 
        R_GCodeView.AccessibleRole = AccessibleRole.List;
        R_GCodeView.CornerRadius = 10;
        R_GCodeView.FirstVisibleLine = 0;
        R_GCodeView.Location = new Point(8, 59);
        R_GCodeView.Margin = new Padding(0, 0, 8, 8);
        R_GCodeView.Name = "R_GCodeView";
        R_GCodeView.Size = new Size(655, 185);
        R_GCodeView.TabIndex = 3;
        // 
        // R_FeedOverride
        // 
        R_FeedOverride.AccessibleRole = AccessibleRole.Slider;
        R_FeedOverride.CornerRadius = 10;
        R_FeedOverride.Location = new Point(847, 59);
        R_FeedOverride.Margin = new Padding(0, 8, 8, 8);
        R_FeedOverride.Minimum = 0D;
        R_FeedOverride.Maximum = 120D;
        R_FeedOverride.Name = "R_FeedOverride";
        R_FeedOverride.Orientation = NT.Tools.SSliderOrientation.Vertical;
        R_FeedOverride.ShowSteps = true;
        R_FeedOverride.Size = new Size(112, 186);
        R_FeedOverride.SliderStyle = NT.Tools.SSliderStyle.Fader;
        R_FeedOverride.Step = 10D;
        R_FeedOverride.SmallChange = 10D;
        R_FeedOverride.LargeChange = 20D;
        R_FeedOverride.TabIndex = 4;
        R_FeedOverride.Value = 100D;
        R_FeedOverride.ValueFormat = "0";
        R_FeedOverride.ValueSuffix = "%";
        // 
        // R_RunProgram
        // 
        R_RunProgram.AutoEllipsis = true;
        R_RunProgram.CornerRadius = 10;
        R_RunProgram.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_RunProgram.GradientStrength = 0.14F;
        R_RunProgram.IconSize = new Size(48, 48);
        R_RunProgram.Image = Properties.Resources.R_NewPlayForeward;
        R_RunProgram.ImageAlign = ContentAlignment.MiddleLeft;
        R_RunProgram.Location = new Point(671, 59);
        R_RunProgram.Margin = new Padding(0, 8, 8, 8);
        R_RunProgram.Name = "R_RunProgram";
        R_RunProgram.Size = new Size(168, 56);
        R_RunProgram.TabIndex = 6;
        R_RunProgram.Text = "Run";
        R_RunProgram.TextAlign = ContentAlignment.MiddleLeft;
        R_RunProgram.Tone = NT.Tools.SButtonTone.Success;
        // 
        // R_PauseProgram
        // 
        R_PauseProgram.AutoEllipsis = true;
        R_PauseProgram.CornerRadius = 10;
        R_PauseProgram.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_PauseProgram.GradientStrength = 0.14F;
        R_PauseProgram.IconSize = new Size(48, 48);
        R_PauseProgram.Image = Properties.Resources.R_NewPause;
        R_PauseProgram.Location = new Point(671, 125);
        R_PauseProgram.Name = "R_PauseProgram";
        R_PauseProgram.Size = new Size(168, 56);
        R_PauseProgram.TabIndex = 7;
        R_PauseProgram.Text = "Pause";
        R_PauseProgram.TextAlign = ContentAlignment.MiddleLeft;
        R_PauseProgram.Tone = NT.Tools.SButtonTone.Warning;
        // 
        // R_ResetProgram
        // 
        R_ResetProgram.AutoEllipsis = true;
        R_ResetProgram.CornerRadius = 10;
        R_ResetProgram.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_ResetProgram.GradientStrength = 0.14F;
        R_ResetProgram.IconSize = new Size(48, 48);
        R_ResetProgram.Image = Properties.Resources.R_NewStop;
        R_ResetProgram.ImageAlign = ContentAlignment.MiddleLeft;
        R_ResetProgram.Location = new Point(671, 189);
        R_ResetProgram.Name = "R_ResetProgram";
        R_ResetProgram.Size = new Size(168, 56);
        R_ResetProgram.TabIndex = 8;
        R_ResetProgram.Text = "Reset";
        R_ResetProgram.TextAlign = ContentAlignment.MiddleLeft;
        R_ResetProgram.Tone = NT.Tools.SButtonTone.Danger;
        // 
        // R_EditGCode
        // 
        R_EditGCode.AutoEllipsis = true;
        R_EditGCode.CornerRadius = 10;
        R_EditGCode.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_EditGCode.GradientStrength = 0.14F;
        R_EditGCode.Location = new Point(185, 1015);
        R_EditGCode.Margin = new Padding(0, 0, 8, 0);
        R_EditGCode.Name = "R_EditGCode";
        R_EditGCode.Size = new Size(168, 56);
        R_EditGCode.TabIndex = 34;
        R_EditGCode.Text = "Edit GCode";
        R_EditGCode.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_SelectStartLine
        // 
        R_SelectStartLine.AutoEllipsis = true;
        R_SelectStartLine.CornerRadius = 10;
        R_SelectStartLine.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_SelectStartLine.GradientStrength = 0.14F;
        R_SelectStartLine.Location = new Point(361, 1015);
        R_SelectStartLine.Margin = new Padding(0, 0, 8, 0);
        R_SelectStartLine.Name = "R_SelectStartLine";
        R_SelectStartLine.Size = new Size(168, 56);
        R_SelectStartLine.TabIndex = 33;
        R_SelectStartLine.Text = "Start Line";
        R_SelectStartLine.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_OpenFile
        // 
        R_OpenFile.AutoEllipsis = true;
        R_OpenFile.CornerRadius = 10;
        R_OpenFile.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_OpenFile.Font = new Font("Segoe UI Semibold", 15F);
        R_OpenFile.GradientStrength = 0.14F;
        R_OpenFile.IconSize = new Size(48, 48);
        R_OpenFile.Image = Properties.Resources.ST__33_;
        R_OpenFile.Location = new Point(9, 1015);
        R_OpenFile.Margin = new Padding(0, 0, 8, 0);
        R_OpenFile.Name = "R_OpenFile";
        R_OpenFile.Padding = new Padding(12, 8, 0, 8);
        R_OpenFile.Size = new Size(168, 56);
        R_OpenFile.TabIndex = 1;
        R_OpenFile.Text = "Open file";
        R_OpenFile.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // R_SpindlePanel
        // 
        R_SpindlePanel.Controls.Add(R_SpindlePanelTitle);
        R_SpindlePanel.Controls.Add(R_ToolSelectorLabel);
        R_SpindlePanel.Controls.Add(R_Spindle);
        R_SpindlePanel.Controls.Add(R_ToolSelector);
        R_SpindlePanel.Controls.Add(R_MeasureToolOffset);
        R_SpindlePanel.Controls.Add(R_SpindleSpeed);
        R_SpindlePanel.CornerRadius = 10;
        R_SpindlePanel.Location = new Point(985, 753);
        R_SpindlePanel.Name = "R_SpindlePanel";
        R_SpindlePanel.Padding = new Padding(0);
        R_SpindlePanel.Size = new Size(307, 254);
        R_SpindlePanel.TabIndex = 6;
        // 
        // R_SpindlePanelTitle
        // 
        R_SpindlePanelTitle.AccessibleRole = AccessibleRole.StaticText;
        R_SpindlePanelTitle.CornerRadius = 10;
        R_SpindlePanelTitle.Location = new Point(9, 17);
        R_SpindlePanelTitle.Margin = new Padding(8, 16, 0, 8);
        R_SpindlePanelTitle.Name = "R_SpindlePanelTitle";
        R_SpindlePanelTitle.Padding = new Padding(0);
        R_SpindlePanelTitle.Size = new Size(288, 34);
        R_SpindlePanelTitle.TabIndex = 43;
        R_SpindlePanelTitle.TabStop = false;
        R_SpindlePanelTitle.Text = "Spindle properties";
        // 
        // R_ToolSelectorLabel
        // 
        R_ToolSelectorLabel.AccessibleRole = AccessibleRole.StaticText;
        R_ToolSelectorLabel.CornerRadius = 10;
        R_ToolSelectorLabel.Location = new Point(9, 124);
        R_ToolSelectorLabel.Name = "R_ToolSelectorLabel";
        R_ToolSelectorLabel.Padding = new Padding(8, 0, 0, 0);
        R_ToolSelectorLabel.Size = new Size(64, 56);
        R_ToolSelectorLabel.TabIndex = 1;
        R_ToolSelectorLabel.TabStop = false;
        R_ToolSelectorLabel.Text = "Tool";
        // 
        // R_Spindle
        // 
        R_Spindle.AutoEllipsis = true;
        R_Spindle.CheckedFillColor = Color.FromArgb(90, 0, 255, 0);
        R_Spindle.CornerRadius = 10;
        R_Spindle.DimIndicatorWhenDisabled = false;
        R_Spindle.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_Spindle.GradientStrength = 0.14F;
        R_Spindle.IndicatorSize = new Size(30, 30);
        R_Spindle.IndicatorStyle = NT.Tools.SIndicatorStyle.None;
        R_Spindle.Location = new Point(9, 59);
        R_Spindle.Margin = new Padding(8, 0, 8, 8);
        R_Spindle.Name = "R_Spindle";
        R_Spindle.Size = new Size(168, 56);
        R_Spindle.TabIndex = 3;
        R_Spindle.Text = "Spindle";
        R_Spindle.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // R_ToolSelector
        // 
        R_ToolSelector.CornerRadius = 10;
        R_ToolSelector.InputMode = NT.Tools.SParameterInputMode.DropDownList;
        R_ToolSelector.Location = new Point(81, 124);
        R_ToolSelector.Name = "R_ToolSelector";
        R_ToolSelector.Padding = new Padding(12, 4, 12, 4);
        R_ToolSelector.ShowStatus = false;
        R_ToolSelector.Size = new Size(96, 56);
        R_ToolSelector.TabIndex = 4;
        R_ToolSelector.TabStop = false;
        // 
        // R_MeasureToolOffset
        // 
        R_MeasureToolOffset.AutoEllipsis = true;
        R_MeasureToolOffset.CornerRadius = 10;
        R_MeasureToolOffset.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_MeasureToolOffset.GradientStrength = 0.14F;
        R_MeasureToolOffset.Location = new Point(9, 188);
        R_MeasureToolOffset.Margin = new Padding(8, 0, 8, 8);
        R_MeasureToolOffset.Name = "R_MeasureToolOffset";
        R_MeasureToolOffset.Padding = new Padding(0);
        R_MeasureToolOffset.Size = new Size(168, 56);
        R_MeasureToolOffset.TabIndex = 5;
        R_MeasureToolOffset.Text = "Measure Tool";
        R_MeasureToolOffset.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_SpindleSpeed
        // 
        R_SpindleSpeed.AccessibleRole = AccessibleRole.Slider;
        R_SpindleSpeed.CornerRadius = 10;
        R_SpindleSpeed.Location = new Point(185, 59);
        R_SpindleSpeed.Name = "R_SpindleSpeed";
        R_SpindleSpeed.Orientation = NT.Tools.SSliderOrientation.Vertical;
        R_SpindleSpeed.ShowSteps = true;
        R_SpindleSpeed.Size = new Size(112, 185);
        R_SpindleSpeed.SliderStyle = NT.Tools.SSliderStyle.Fader;
        R_SpindleSpeed.TabIndex = 6;
        // 
        // R_ProgramProgress
        // 
        R_ProgramProgress.AccessibleRole = AccessibleRole.ProgressBar;
        R_ProgramProgress.CornerRadius = 10;
        R_ProgramProgress.Location = new Point(537, 1015);
        R_ProgramProgress.Margin = new Padding(0, 0, 8, 0);
        R_ProgramProgress.Name = "R_ProgramProgress";
        R_ProgramProgress.Padding = new Padding(12, 0, 12, 0);
        R_ProgramProgress.ProgressInset = 8;
        R_ProgramProgress.Size = new Size(1142, 56);
        R_ProgramProgress.TabIndex = 11;
        R_ProgramProgress.TabStop = false;
        R_ProgramProgress.Text = "DESIGN PREVIEW  /  Machine commands are not connected";
        // 
        // R_ReferenceX
        // 
        R_ReferenceX.BackColor = Color.Transparent;
        R_ReferenceX.CornerRadius = 10;
        R_ReferenceX.Location = new Point(369, 59);
        R_ReferenceX.Name = "R_ReferenceX";
        R_ReferenceX.Size = new Size(112, 56);
        R_ReferenceX.TabIndex = 8;
        R_ReferenceX.TabStop = false;
        R_ReferenceX.Visible = false;
        // 
        // R_ReferenceY
        // 
        R_ReferenceY.BackColor = Color.Transparent;
        R_ReferenceY.CornerRadius = 10;
        R_ReferenceY.Location = new Point(369, 123);
        R_ReferenceY.Name = "R_ReferenceY";
        R_ReferenceY.Size = new Size(112, 56);
        R_ReferenceY.TabIndex = 9;
        R_ReferenceY.TabStop = false;
        R_ReferenceY.Visible = false;
        // 
        // R_ReferenceZ
        // 
        R_ReferenceZ.BackColor = Color.Transparent;
        R_ReferenceZ.CornerRadius = 10;
        R_ReferenceZ.Location = new Point(369, 187);
        R_ReferenceZ.Name = "R_ReferenceZ";
        R_ReferenceZ.Size = new Size(112, 56);
        R_ReferenceZ.TabIndex = 10;
        R_ReferenceZ.TabStop = false;
        R_ReferenceZ.Visible = false;
        // 
        // R_ReferenceSelector
        // 
        R_ReferenceSelector.BackColor = Color.Transparent;
        R_ReferenceSelector.CornerRadius = 10;
        R_ReferenceSelector.Location = new Point(185, 59);
        R_ReferenceSelector.Name = "R_ReferenceSelector";
        R_ReferenceSelector.Size = new Size(112, 56);
        R_ReferenceSelector.TabIndex = 7;
        R_ReferenceSelector.TabStop = false;
        R_ReferenceSelector.Visible = false;
        // 
        // R_ReferencePanel
        // 
        R_ReferencePanel.Controls.Add(R_ReferencePanelTitle);
        R_ReferencePanel.Controls.Add(R_ReferenceAxisXLabel);
        R_ReferencePanel.Controls.Add(R_ReferenceAxisYLabel);
        R_ReferencePanel.Controls.Add(R_ReferenceAxisZLabel);
        R_ReferencePanel.Controls.Add(R_ReferenceX);
        R_ReferencePanel.Controls.Add(R_ReferenceZ);
        R_ReferencePanel.Controls.Add(R_ReferenceY);
        R_ReferencePanel.Controls.Add(R_RefXZero);
        R_ReferencePanel.Controls.Add(R_RefYZero);
        R_ReferencePanel.Controls.Add(R_RefZZero);
        R_ReferencePanel.Controls.Add(R_Home);
        R_ReferencePanel.Controls.Add(R_ReferenceSelector);
        R_ReferencePanel.Controls.Add(R_GoToReference);
        R_ReferencePanel.Controls.Add(R_GoHome);
        R_ReferencePanel.Controls.Add(R_ReferencePause);
        R_ReferencePanel.CornerRadius = 10;
        R_ReferencePanel.Location = new Point(1300, 302);
        R_ReferencePanel.Margin = new Padding(0, 0, 0, 8);
        R_ReferencePanel.Name = "R_ReferencePanel";
        R_ReferencePanel.Padding = new Padding(0);
        R_ReferencePanel.Size = new Size(611, 253);
        R_ReferencePanel.TabIndex = 46;
        // 
        // R_ReferencePanelTitle
        // 
        R_ReferencePanelTitle.AccessibleRole = AccessibleRole.StaticText;
        R_ReferencePanelTitle.CornerRadius = 10;
        R_ReferencePanelTitle.Location = new Point(369, 17);
        R_ReferencePanelTitle.Margin = new Padding(0, 16, 0, 8);
        R_ReferencePanelTitle.Name = "R_ReferencePanelTitle";
        R_ReferencePanelTitle.Padding = new Padding(0);
        R_ReferencePanelTitle.Size = new Size(234, 34);
        R_ReferencePanelTitle.TabIndex = 42;
        R_ReferencePanelTitle.TabStop = false;
        R_ReferencePanelTitle.Text = "Reference(mm)";
        // 
        // R_ReferenceAxisXLabel
        // 
        R_ReferenceAxisXLabel.AccessibleRole = AccessibleRole.StaticText;
        R_ReferenceAxisXLabel.CornerRadius = 10;
        R_ReferenceAxisXLabel.Location = new Point(305, 59);
        R_ReferenceAxisXLabel.Margin = new Padding(8, 0, 8, 8);
        R_ReferenceAxisXLabel.Name = "R_ReferenceAxisXLabel";
        R_ReferenceAxisXLabel.Padding = new Padding(0, 0, 8, 0);
        R_ReferenceAxisXLabel.Size = new Size(56, 56);
        R_ReferenceAxisXLabel.TabIndex = 5;
        R_ReferenceAxisXLabel.TabStop = false;
        R_ReferenceAxisXLabel.Text = "X";
        R_ReferenceAxisXLabel.TextAlign = ContentAlignment.MiddleRight;
        R_ReferenceAxisXLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_ReferenceAxisYLabel
        // 
        R_ReferenceAxisYLabel.AccessibleRole = AccessibleRole.StaticText;
        R_ReferenceAxisYLabel.CornerRadius = 10;
        R_ReferenceAxisYLabel.Location = new Point(305, 123);
        R_ReferenceAxisYLabel.Margin = new Padding(8, 0, 8, 8);
        R_ReferenceAxisYLabel.Name = "R_ReferenceAxisYLabel";
        R_ReferenceAxisYLabel.Padding = new Padding(0, 0, 8, 0);
        R_ReferenceAxisYLabel.Size = new Size(56, 56);
        R_ReferenceAxisYLabel.TabIndex = 6;
        R_ReferenceAxisYLabel.TabStop = false;
        R_ReferenceAxisYLabel.Text = "Y";
        R_ReferenceAxisYLabel.TextAlign = ContentAlignment.MiddleRight;
        R_ReferenceAxisYLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_ReferenceAxisZLabel
        // 
        R_ReferenceAxisZLabel.AccessibleRole = AccessibleRole.StaticText;
        R_ReferenceAxisZLabel.CornerRadius = 10;
        R_ReferenceAxisZLabel.Location = new Point(305, 187);
        R_ReferenceAxisZLabel.Margin = new Padding(8, 0, 8, 8);
        R_ReferenceAxisZLabel.Name = "R_ReferenceAxisZLabel";
        R_ReferenceAxisZLabel.Padding = new Padding(0, 0, 8, 0);
        R_ReferenceAxisZLabel.Size = new Size(56, 56);
        R_ReferenceAxisZLabel.TabIndex = 7;
        R_ReferenceAxisZLabel.TabStop = false;
        R_ReferenceAxisZLabel.Text = "Z";
        R_ReferenceAxisZLabel.TextAlign = ContentAlignment.MiddleRight;
        R_ReferenceAxisZLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_Homed
        // 
        R_Homed.AccessibleRole = AccessibleRole.Indicator;
        R_Homed.CornerRadius = 10;
        R_Homed.IndicatorSize = new Size(30, 30);
        R_Homed.Location = new Point(925, 9);
        R_Homed.Name = "R_Homed";
        R_Homed.Size = new Size(168, 56);
        R_Homed.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_Homed.TabIndex = 47;
        R_Homed.TabStop = false;
        R_Homed.Text = "Homed";
        // 
        // MainForm
        // 
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(1920, 1080);
        Controls.Add(R_Homed);
        Controls.Add(R_SelectStartLine);
        Controls.Add(R_EditGCode);
        Controls.Add(R_ProgramView3D);
        Controls.Add(R_ReferencePanel);
        Controls.Add(R_MachineFeedValue);
        Controls.Add(R_BrandLabel);
        Controls.Add(R_Exit);
        Controls.Add(R_Minimize);
        Controls.Add(R_ThemeToggle);
        Controls.Add(R_Emergency);
        Controls.Add(R_AxisAlarm);
        Controls.Add(R_ZAxis);
        Controls.Add(R_YAxis);
        Controls.Add(R_XAxis);
        Controls.Add(R_Connected);
        Controls.Add(R_OpenFile);
        Controls.Add(R_MachinePositionPanel);
        Controls.Add(R_WorkPositionPanel);
        Controls.Add(R_JogPanel);
        Controls.Add(R_ProgramPanel);
        Controls.Add(R_SpindlePanel);
        Controls.Add(R_Configuration);
        Controls.Add(R_ProgramProgress);
        R_ThemeManager.SetDpiMode(this, NT.Tools.SThemeDpiMode.FixedPixels);
        Font = new Font("Segoe UI", 16.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
        ForeColor = Color.FromArgb(224, 229, 235);
        FormBorderStyle = FormBorderStyle.None;
        Margin = new Padding(4, 4, 4, 4);
        MinimumSize = new Size(1280, 720);
        Name = "R_MainForm";
        R_ThemeManager.SetScaleMode(this, NT.Tools.SFormScaleMode.BoundsAndFonts);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RouterXYZ+TC";
        Load += MainForm_Load;
        (R_ThemeManager).EndInit();
        R_MachinePositionPanel.ResumeLayout(false);
        R_WorkPositionPanel.ResumeLayout(false);
        R_JogPanel.ResumeLayout(false);
        R_ProgramPanel.ResumeLayout(false);
        R_SpindlePanel.ResumeLayout(false);
        R_ReferencePanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    private NT.Tools.sTheme R_ThemeManager = null!;
    private NT.Tools.sPanel R_MachinePositionPanel = null!;
    private NT.Tools.sPanel R_WorkPositionPanel = null!;
    private NT.Tools.sPanel R_JogPanel = null!;
    private NT.Tools.sPanel R_ProgramPanel = null!;
    private NT.Tools.sPanel R_SpindlePanel = null!;
    private NT.Tools.s3DView R_ProgramView3D = null!;
    private NT.Tools.sGCodeView R_GCodeView = null!;
    private NT.Tools.sProgressBar R_ProgramProgress = null!;
    private NT.Tools.sSignal R_Connected = null!;
    private NT.Tools.sSignal R_XAxis = null!;
    private NT.Tools.sSignal R_YAxis = null!;
    private NT.Tools.sSignal R_ZAxis = null!;
    private NT.Tools.sSignal R_AxisAlarm = null!;
    private NT.Tools.sSignal R_Emergency = null!;
    private NT.Tools.sButton R_Configuration = null!;
    private NT.Tools.sThemeButton R_ThemeToggle = null!;
    private NT.Tools.sButton R_Minimize = null!;
    private NT.Tools.sButton R_Exit = null!;
    private NT.Tools.sButton R_RunProgram = null!;
    private NT.Tools.sButton R_PauseProgram = null!;
    private NT.Tools.sButton R_ResetProgram = null!;
    private NT.Tools.sButton R_OpenFile = null!;
    private NT.Tools.sButton R_Home = null!;
    private NT.Tools.sButton R_GoHome = null!;
    private NT.Tools.sButton R_GoToReference = null!;
    private NT.Tools.sButton R_ReferencePause = null!;
    private NT.Tools.sButton R_JogPause = null!;
    private NT.Tools.sButton R_RefXZero = null!;
    private NT.Tools.sButton R_RefYZero = null!;
    private NT.Tools.sButton R_RefZZero = null!;
    private NT.Tools.sButton R_JogNX = null!;
    private NT.Tools.sButton R_JogPX = null!;
    private NT.Tools.sButton R_JogNY = null!;
    private NT.Tools.sButton R_JogPY = null!;
    private NT.Tools.sButton R_JogNZ = null!;
    private NT.Tools.sButton R_JogPZ = null!;
    private NT.Tools.sButton R_JogNXPY = null!;
    private NT.Tools.sButton R_JogPXPY = null!;
    private NT.Tools.sButton R_JogNXNY = null!;
    private NT.Tools.sButton R_JogPXNY = null!;
    private NT.Tools.sToggle R_JogMode = null!;
    private NT.Tools.sToggle R_Spindle = null!;
    private NT.Tools.sButton R_MeasureToolOffset = null!;
    private NT.Tools.sLabel R_CommandPositionX = null!;
    private NT.Tools.sLabel R_CommandPositionY = null!;
    private NT.Tools.sLabel R_CommandPositionZ = null!;
    private NT.Tools.sLabel R_WorkPositionX = null!;
    private NT.Tools.sLabel R_WorkPositionY = null!;
    private NT.Tools.sLabel R_WorkPositionZ = null!;
    private NT.Tools.sLabel R_ProgramPath = null!;
    private NT.Tools.sTextBox R_ReferenceX = null!;
    private NT.Tools.sTextBox R_ReferenceY = null!;
    private NT.Tools.sTextBox R_ReferenceZ = null!;
    private NT.Tools.sTextBox R_JogVelocityX = null!;
    private NT.Tools.sTextBox R_JogVelocityY = null!;
    private NT.Tools.sTextBox R_JogVelocityZ = null!;
    private NT.Tools.sTextBox R_StepX = null!;
    private NT.Tools.sTextBox R_StepY = null!;
    private NT.Tools.sTextBox R_StepZ = null!;
    private NT.Tools.sParameterBox R_ToolSelector = null!;
    private NT.Tools.sSlider R_FeedOverride = null!;
    private NT.Tools.sSlider R_JogFeedOverride = null!;
    private NT.Tools.sSlider R_SpindleSpeed = null!;
    private NT.Tools.sLabel R_BrandLabel = null!;
    private NT.Tools.sProgressBar R_MachineFeedValue = null!;
    private NT.Tools.sLabel R_MachinePositionTitle = null!;
    private NT.Tools.sLabel R_WorkPositionTitle = null!;
    private NT.Tools.sLabel R_JogVelocityHeader = null!;
    private NT.Tools.sLabel R_JogAxisXLabel = null!;
    private NT.Tools.sLabel R_JogAxisYLabel = null!;
    private NT.Tools.sLabel R_JogAxisZLabel = null!;
    private NT.Tools.sLabel R_ToolSelectorLabel = null!;
    private NT.Tools.sComboBox R_ReferenceSelector = null!;
    private NT.Tools.sLabel R_JogPositionHeader;
    private NT.Tools.sLabel R_JogStepHeader;
    private NT.Tools.sPanel R_ReferencePanel;
    private NT.Tools.sLabel R_ReferencePanelTitle;
    private NT.Tools.sLabel R_ReferenceAxisXLabel;
    private NT.Tools.sLabel R_ReferenceAxisYLabel;
    private NT.Tools.sLabel R_ReferenceAxisZLabel;
    private NT.Tools.sLabel R_SpindlePanelTitle;
    private NT.Tools.sButton R_EditGCode;
    private NT.Tools.sButton R_SelectStartLine;
    private NT.Tools.sSignal R_Homed;
    private NT.Tools.sButton R_GoToZ;
    private NT.Tools.sButton R_GoToY;
    private NT.Tools.sButton R_GoToX;
    private NT.Tools.sTextBox R_PositionX;
    private NT.Tools.sTextBox R_PositionY;
    private NT.Tools.sTextBox R_PositionZ;
}

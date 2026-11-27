namespace RouterXYZACRTCP;

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
        R_CommandPositionC = new NT.Tools.sLabel();
        R_CommandPositionA = new NT.Tools.sLabel();
        R_MachinePositionTitle = new NT.Tools.sLabel();
        R_CommandPositionX = new NT.Tools.sLabel();
        R_CommandPositionY = new NT.Tools.sLabel();
        R_CommandPositionZ = new NT.Tools.sLabel();
        R_WorkPositionPanel = new NT.Tools.sPanel();
        R_WorkPositionC = new NT.Tools.sLabel();
        R_WorkPositionA = new NT.Tools.sLabel();
        R_WorkPositionTitle = new NT.Tools.sLabel();
        R_WorkPositionX = new NT.Tools.sLabel();
        R_WorkPositionY = new NT.Tools.sLabel();
        R_WorkPositionZ = new NT.Tools.sLabel();
        R_JogStepHeader = new NT.Tools.sLabel();
        R_JogAxisXLabel = new NT.Tools.sLabel();
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
        R_GCodeView = new NT.Tools.sGCodeView();
        R_FeedOverride = new NT.Tools.sSlider();
        R_RunProgram = new NT.Tools.sButton();
        R_PauseProgram = new NT.Tools.sButton();
        R_ResetProgram = new NT.Tools.sButton();
        R_EditGCode = new NT.Tools.sButton();
        R_SelectStartLine = new NT.Tools.sButton();
        R_OpenFile = new NT.Tools.sButton();
        R_ToolLength = new NT.Tools.sTextBox();
        R_SpindlePageTitle = new NT.Tools.sLabel();
        R_Spindle = new NT.Tools.sToggle();
        R_SpindleSpeed = new NT.Tools.sSlider();
        R_ToolLengthLabel = new NT.Tools.sLabel();
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
        R_JogPagePanel = new NT.Tools.sPagePanel();
        R_JogControlsPage = new NT.Tools.sPage();
        R_JogControlsTitle = new NT.Tools.sLabel();
        R_JogNZT = new NT.Tools.sButton();
        R_JogPZT = new NT.Tools.sButton();
        R_JogNCP = new NT.Tools.sButton();
        R_JogNAP = new NT.Tools.sButton();
        R_JogPAP = new NT.Tools.sButton();
        R_JogPCP = new NT.Tools.sButton();
        R_GotoJogProperties = new NT.Tools.sButton();
        R_JogNC = new NT.Tools.sButton();
        R_JogNA = new NT.Tools.sButton();
        R_JogPA = new NT.Tools.sButton();
        R_JogPC = new NT.Tools.sButton();
        R_JogPropertiesPage = new NT.Tools.sPage();
        R_JogPropertiesTitle = new NT.Tools.sLabel();
        R_RtcpJogStepHeader = new NT.Tools.sLabel();
        R_RtcpJogVelocityHeader = new NT.Tools.sLabel();
        R_JogAxisZTLabel = new NT.Tools.sLabel();
        R_StepZT = new NT.Tools.sTextBox();
        R_JogAxisCPLabel = new NT.Tools.sLabel();
        R_StepCP = new NT.Tools.sTextBox();
        R_JogVelocityZT = new NT.Tools.sTextBox();
        R_JogVelocityCP = new NT.Tools.sTextBox();
        R_JogAxisAPLabel = new NT.Tools.sLabel();
        R_StepAP = new NT.Tools.sTextBox();
        R_JogVelocityAP = new NT.Tools.sTextBox();
        R_GotoJogPositions = new NT.Tools.sButton();
        R_JogAxisCLabel = new NT.Tools.sLabel();
        R_StepC = new NT.Tools.sTextBox();
        R_JogAxisALabel = new NT.Tools.sLabel();
        R_StepA = new NT.Tools.sTextBox();
        R_JogVelocityC = new NT.Tools.sTextBox();
        R_JogVelocityA = new NT.Tools.sTextBox();
        R_JogPositionsPage = new NT.Tools.sPage();
        R_JogPositionsTitle = new NT.Tools.sLabel();
        R_RtcpJogPositionHeader = new NT.Tools.sLabel();
        R_GoToCP = new NT.Tools.sButton();
        R_GoToAP = new NT.Tools.sButton();
        R_PositionCP = new NT.Tools.sTextBox();
        R_PositionAP = new NT.Tools.sTextBox();
        R_GoToZT = new NT.Tools.sButton();
        R_PositionZT = new NT.Tools.sTextBox();
        R_GotoJog = new NT.Tools.sButton();
        R_GoToC = new NT.Tools.sButton();
        R_GoToA = new NT.Tools.sButton();
        R_PositionC = new NT.Tools.sTextBox();
        R_PositionA = new NT.Tools.sTextBox();
        R_GoToZ = new NT.Tools.sButton();
        R_JogPositionHeader = new NT.Tools.sLabel();
        R_GoToY = new NT.Tools.sButton();
        R_PositionX = new NT.Tools.sTextBox();
        R_PositionZ = new NT.Tools.sTextBox();
        R_GoToX = new NT.Tools.sButton();
        R_PositionY = new NT.Tools.sTextBox();
        R_AAxis = new NT.Tools.sSignal();
        R_CAxis = new NT.Tools.sSignal();
        R_RtcpEnabled = new NT.Tools.sToggle();
        R_HeadPagePanel = new NT.Tools.sPagePanel();
        R_HeadRtcpPage = new NT.Tools.sPage();
        R_RtcpPageTitle = new NT.Tools.sLabel();
        R_GotoSpindlePage = new NT.Tools.sButton();
        R_HeadSpindlePage = new NT.Tools.sPage();
        R_GotoRtcpPage = new NT.Tools.sButton();
        R_NavigatorPanel = new NT.Tools.sPagePanel();
        R_NavigatorProgramPage = new NT.Tools.sPage();
        R_GotoNavigatorGCodePage = new NT.Tools.sButton();
        R_NavigatorGCodePage = new NT.Tools.sPage();
        R_GotoNavigatorProgramPage = new NT.Tools.sButton();
        R_GCodeExpandedView = new NT.Tools.sGCodeView();
        R_ExpandedProgramPath = new NT.Tools.sLabel();
        (R_ThemeManager).BeginInit();
        R_MachinePositionPanel.SuspendLayout();
        R_WorkPositionPanel.SuspendLayout();
        R_ReferencePanel.SuspendLayout();
        R_JogPagePanel.SuspendLayout();
        R_JogControlsPage.SuspendLayout();
        R_JogPropertiesPage.SuspendLayout();
        R_JogPositionsPage.SuspendLayout();
        R_HeadPagePanel.SuspendLayout();
        R_HeadRtcpPage.SuspendLayout();
        R_HeadSpindlePage.SuspendLayout();
        R_NavigatorPanel.SuspendLayout();
        R_NavigatorProgramPage.SuspendLayout();
        R_NavigatorGCodePage.SuspendLayout();
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
        R_BrandLabel.Size = new Size(242, 56);
        R_BrandLabel.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_BrandLabel.TabIndex = 0;
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
        R_Connected.Location = new Point(259, 9);
        R_Connected.Name = "R_Connected";
        R_Connected.Size = new Size(168, 56);
        R_Connected.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_Connected.TabIndex = 1;
        R_Connected.TabStop = false;
        R_Connected.Text = "Online";
        // 
        // R_XAxis
        // 
        R_XAxis.AccessibleRole = AccessibleRole.Indicator;
        R_XAxis.CornerRadius = 10;
        R_XAxis.IndicatorSize = new Size(30, 30);
        R_XAxis.Location = new Point(435, 9);
        R_XAxis.Name = "R_XAxis";
        R_XAxis.Padding = new Padding(16, 4, 12, 4);
        R_XAxis.Size = new Size(90, 56);
        R_XAxis.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_XAxis.TabIndex = 2;
        R_XAxis.TabStop = false;
        R_XAxis.Text = "X";
        // 
        // R_YAxis
        // 
        R_YAxis.AccessibleRole = AccessibleRole.Indicator;
        R_YAxis.CornerRadius = 10;
        R_YAxis.IndicatorSize = new Size(30, 30);
        R_YAxis.Location = new Point(533, 9);
        R_YAxis.Name = "R_YAxis";
        R_YAxis.Padding = new Padding(16, 4, 12, 4);
        R_YAxis.Size = new Size(90, 56);
        R_YAxis.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_YAxis.TabIndex = 3;
        R_YAxis.TabStop = false;
        R_YAxis.Text = "Y";
        // 
        // R_ZAxis
        // 
        R_ZAxis.AccessibleRole = AccessibleRole.Indicator;
        R_ZAxis.CornerRadius = 10;
        R_ZAxis.IndicatorSize = new Size(30, 30);
        R_ZAxis.Location = new Point(631, 9);
        R_ZAxis.Name = "R_ZAxis";
        R_ZAxis.Padding = new Padding(16, 4, 12, 4);
        R_ZAxis.Size = new Size(90, 56);
        R_ZAxis.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_ZAxis.TabIndex = 4;
        R_ZAxis.TabStop = false;
        R_ZAxis.Text = "Z";
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
        R_AxisAlarm.TabIndex = 8;
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
        R_Emergency.TabIndex = 9;
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
        R_Configuration.TabIndex = 25;
        R_Configuration.Text = "Configuration";
        R_Configuration.TextAlign = ContentAlignment.MiddleLeft;
        R_Configuration.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_ProgramPath
        // 
        R_ProgramPath.AccessibleRole = AccessibleRole.StaticText;
        R_ProgramPath.CornerRadius = 10;
        R_ProgramPath.Location = new Point(14, 17);
        R_ProgramPath.Margin = new Padding(8, 16, 0, 8);
        R_ProgramPath.Name = "R_ProgramPath";
        R_ProgramPath.Padding = new Padding(0);
        R_ProgramPath.Size = new Size(879, 34);
        R_ProgramPath.TabIndex = 0;
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
        R_MachineFeedValue.TabIndex = 10;
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
        R_ProgramView3D.Size = new Size(1260, 672);
        R_ProgramView3D.TabIndex = 14;
        // 
        // R_MachinePositionPanel
        // 
        R_MachinePositionPanel.Controls.Add(R_CommandPositionC);
        R_MachinePositionPanel.Controls.Add(R_CommandPositionA);
        R_MachinePositionPanel.Controls.Add(R_MachinePositionTitle);
        R_MachinePositionPanel.Controls.Add(R_CommandPositionX);
        R_MachinePositionPanel.Controls.Add(R_CommandPositionY);
        R_MachinePositionPanel.Controls.Add(R_CommandPositionZ);
        R_MachinePositionPanel.CornerRadius = 10;
        R_MachinePositionPanel.Location = new Point(1277, 73);
        R_MachinePositionPanel.Name = "R_MachinePositionPanel";
        R_MachinePositionPanel.Padding = new Padding(0);
        R_MachinePositionPanel.Size = new Size(314, 251);
        R_MachinePositionPanel.TabIndex = 15;
        // 
        // R_CommandPositionC
        // 
        R_CommandPositionC.AccessibleRole = AccessibleRole.StaticText;
        R_CommandPositionC.CornerRadius = 10;
        R_CommandPositionC.Font = new Font("Courier New", 22.2F);
        R_CommandPositionC.Location = new Point(11, 203);
        R_CommandPositionC.Margin = new Padding(8, 0, 8, 0);
        R_CommandPositionC.Name = "R_CommandPositionC";
        R_CommandPositionC.Padding = new Padding(12, 4, 0, 4);
        R_CommandPositionC.Size = new Size(282, 36);
        R_CommandPositionC.TabIndex = 5;
        R_CommandPositionC.TabStop = false;
        R_CommandPositionC.Text = "C  0000.000 °";
        // 
        // R_CommandPositionA
        // 
        R_CommandPositionA.AccessibleRole = AccessibleRole.StaticText;
        R_CommandPositionA.CornerRadius = 10;
        R_CommandPositionA.Font = new Font("Courier New", 22.2F);
        R_CommandPositionA.Location = new Point(11, 167);
        R_CommandPositionA.Margin = new Padding(8, 0, 8, 0);
        R_CommandPositionA.Name = "R_CommandPositionA";
        R_CommandPositionA.Padding = new Padding(12, 4, 0, 4);
        R_CommandPositionA.Size = new Size(282, 36);
        R_CommandPositionA.TabIndex = 4;
        R_CommandPositionA.TabStop = false;
        R_CommandPositionA.Text = "A  0000.000 °";
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
        R_MachinePositionTitle.TabIndex = 0;
        R_MachinePositionTitle.TabStop = false;
        R_MachinePositionTitle.Text = "Machine position";
        // 
        // R_CommandPositionX
        // 
        R_CommandPositionX.AccessibleRole = AccessibleRole.StaticText;
        R_CommandPositionX.CornerRadius = 10;
        R_CommandPositionX.Font = new Font("Courier New", 22.2F);
        R_CommandPositionX.Location = new Point(9, 59);
        R_CommandPositionX.Margin = new Padding(8, 0, 8, 0);
        R_CommandPositionX.Name = "R_CommandPositionX";
        R_CommandPositionX.Padding = new Padding(12, 4, 0, 4);
        R_CommandPositionX.Size = new Size(282, 36);
        R_CommandPositionX.TabIndex = 1;
        R_CommandPositionX.TabStop = false;
        R_CommandPositionX.Text = "X  0000.000 mm";
        // 
        // R_CommandPositionY
        // 
        R_CommandPositionY.AccessibleRole = AccessibleRole.StaticText;
        R_CommandPositionY.CornerRadius = 10;
        R_CommandPositionY.Font = new Font("Courier New", 22.2F);
        R_CommandPositionY.Location = new Point(9, 95);
        R_CommandPositionY.Margin = new Padding(8, 0, 8, 0);
        R_CommandPositionY.Name = "R_CommandPositionY";
        R_CommandPositionY.Padding = new Padding(12, 4, 0, 4);
        R_CommandPositionY.Size = new Size(282, 36);
        R_CommandPositionY.TabIndex = 2;
        R_CommandPositionY.TabStop = false;
        R_CommandPositionY.Text = "Y  0000.000 mm";
        // 
        // R_CommandPositionZ
        // 
        R_CommandPositionZ.AccessibleRole = AccessibleRole.StaticText;
        R_CommandPositionZ.CornerRadius = 10;
        R_CommandPositionZ.Font = new Font("Courier New", 22.2F);
        R_CommandPositionZ.Location = new Point(9, 131);
        R_CommandPositionZ.Margin = new Padding(8, 0, 8, 0);
        R_CommandPositionZ.Name = "R_CommandPositionZ";
        R_CommandPositionZ.Padding = new Padding(12, 4, 0, 4);
        R_CommandPositionZ.Size = new Size(282, 36);
        R_CommandPositionZ.TabIndex = 3;
        R_CommandPositionZ.TabStop = false;
        R_CommandPositionZ.Text = "Z  0000.000 mm";
        // 
        // R_WorkPositionPanel
        // 
        R_WorkPositionPanel.Controls.Add(R_WorkPositionC);
        R_WorkPositionPanel.Controls.Add(R_WorkPositionA);
        R_WorkPositionPanel.Controls.Add(R_WorkPositionTitle);
        R_WorkPositionPanel.Controls.Add(R_WorkPositionX);
        R_WorkPositionPanel.Controls.Add(R_WorkPositionY);
        R_WorkPositionPanel.Controls.Add(R_WorkPositionZ);
        R_WorkPositionPanel.CornerRadius = 10;
        R_WorkPositionPanel.Location = new Point(1598, 73);
        R_WorkPositionPanel.Name = "R_WorkPositionPanel";
        R_WorkPositionPanel.Padding = new Padding(0);
        R_WorkPositionPanel.Size = new Size(313, 251);
        R_WorkPositionPanel.TabIndex = 16;
        // 
        // R_WorkPositionC
        // 
        R_WorkPositionC.AccessibleRole = AccessibleRole.StaticText;
        R_WorkPositionC.CornerRadius = 10;
        R_WorkPositionC.Font = new Font("Courier New", 22.2F);
        R_WorkPositionC.Location = new Point(9, 203);
        R_WorkPositionC.Margin = new Padding(8, 0, 8, 0);
        R_WorkPositionC.Name = "R_WorkPositionC";
        R_WorkPositionC.Padding = new Padding(12, 4, 0, 4);
        R_WorkPositionC.Size = new Size(282, 36);
        R_WorkPositionC.TabIndex = 5;
        R_WorkPositionC.TabStop = false;
        R_WorkPositionC.Text = "C  0000.000 °";
        // 
        // R_WorkPositionA
        // 
        R_WorkPositionA.AccessibleRole = AccessibleRole.StaticText;
        R_WorkPositionA.CornerRadius = 10;
        R_WorkPositionA.Font = new Font("Courier New", 22.2F);
        R_WorkPositionA.Location = new Point(9, 167);
        R_WorkPositionA.Margin = new Padding(8, 0, 8, 0);
        R_WorkPositionA.Name = "R_WorkPositionA";
        R_WorkPositionA.Padding = new Padding(12, 4, 0, 4);
        R_WorkPositionA.Size = new Size(282, 36);
        R_WorkPositionA.TabIndex = 4;
        R_WorkPositionA.TabStop = false;
        R_WorkPositionA.Text = "A  0000.000 °";
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
        R_WorkPositionTitle.TabIndex = 0;
        R_WorkPositionTitle.TabStop = false;
        R_WorkPositionTitle.Text = "Work position";
        // 
        // R_WorkPositionX
        // 
        R_WorkPositionX.AccessibleRole = AccessibleRole.StaticText;
        R_WorkPositionX.CornerRadius = 10;
        R_WorkPositionX.Font = new Font("Courier New", 22.2F);
        R_WorkPositionX.Location = new Point(9, 59);
        R_WorkPositionX.Margin = new Padding(8, 0, 8, 0);
        R_WorkPositionX.Name = "R_WorkPositionX";
        R_WorkPositionX.Padding = new Padding(12, 4, 0, 4);
        R_WorkPositionX.Size = new Size(282, 36);
        R_WorkPositionX.TabIndex = 1;
        R_WorkPositionX.TabStop = false;
        R_WorkPositionX.Text = "X  0000.000 mm";
        // 
        // R_WorkPositionY
        // 
        R_WorkPositionY.AccessibleRole = AccessibleRole.StaticText;
        R_WorkPositionY.CornerRadius = 10;
        R_WorkPositionY.Font = new Font("Courier New", 22.2F);
        R_WorkPositionY.Location = new Point(9, 95);
        R_WorkPositionY.Margin = new Padding(8, 0, 8, 0);
        R_WorkPositionY.Name = "R_WorkPositionY";
        R_WorkPositionY.Padding = new Padding(12, 4, 0, 4);
        R_WorkPositionY.Size = new Size(282, 36);
        R_WorkPositionY.TabIndex = 2;
        R_WorkPositionY.TabStop = false;
        R_WorkPositionY.Text = "Y  0000.000 mm";
        // 
        // R_WorkPositionZ
        // 
        R_WorkPositionZ.AccessibleRole = AccessibleRole.StaticText;
        R_WorkPositionZ.CornerRadius = 10;
        R_WorkPositionZ.Font = new Font("Courier New", 22.2F);
        R_WorkPositionZ.Location = new Point(9, 131);
        R_WorkPositionZ.Margin = new Padding(8, 0, 8, 0);
        R_WorkPositionZ.Name = "R_WorkPositionZ";
        R_WorkPositionZ.Padding = new Padding(12, 4, 0, 4);
        R_WorkPositionZ.Size = new Size(282, 36);
        R_WorkPositionZ.TabIndex = 3;
        R_WorkPositionZ.TabStop = false;
        R_WorkPositionZ.Text = "Z  0000.000 mm";
        // 
        // R_JogStepHeader
        // 
        R_JogStepHeader.AccessibleRole = AccessibleRole.StaticText;
        R_JogStepHeader.CornerRadius = 10;
        R_JogStepHeader.Location = new Point(189, 114);
        R_JogStepHeader.Margin = new Padding(0, 16, 0, 8);
        R_JogStepHeader.Name = "R_JogStepHeader";
        R_JogStepHeader.Padding = new Padding(0);
        R_JogStepHeader.Size = new Size(112, 34);
        R_JogStepHeader.TabIndex = 2;
        R_JogStepHeader.TabStop = false;
        R_JogStepHeader.Text = "Step (mm)";
        // 
        // R_JogAxisXLabel
        // 
        R_JogAxisXLabel.AccessibleRole = AccessibleRole.StaticText;
        R_JogAxisXLabel.CornerRadius = 10;
        R_JogAxisXLabel.Location = new Point(9, 155);
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
        // R_JogAxisYLabel
        // 
        R_JogAxisYLabel.AccessibleRole = AccessibleRole.StaticText;
        R_JogAxisYLabel.CornerRadius = 10;
        R_JogAxisYLabel.Location = new Point(9, 219);
        R_JogAxisYLabel.Margin = new Padding(8, 0, 8, 8);
        R_JogAxisYLabel.Name = "R_JogAxisYLabel";
        R_JogAxisYLabel.Padding = new Padding(0, 0, 8, 0);
        R_JogAxisYLabel.Size = new Size(52, 56);
        R_JogAxisYLabel.TabIndex = 8;
        R_JogAxisYLabel.TabStop = false;
        R_JogAxisYLabel.Text = "Y";
        R_JogAxisYLabel.TextAlign = ContentAlignment.MiddleRight;
        R_JogAxisYLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_JogVelocityHeader
        // 
        R_JogVelocityHeader.AccessibleRole = AccessibleRole.StaticText;
        R_JogVelocityHeader.CornerRadius = 10;
        R_JogVelocityHeader.Location = new Point(69, 114);
        R_JogVelocityHeader.Margin = new Padding(0, 16, 0, 8);
        R_JogVelocityHeader.Name = "R_JogVelocityHeader";
        R_JogVelocityHeader.Padding = new Padding(0);
        R_JogVelocityHeader.Size = new Size(112, 34);
        R_JogVelocityHeader.TabIndex = 1;
        R_JogVelocityHeader.TabStop = false;
        R_JogVelocityHeader.Text = "V (mm/s)";
        // 
        // R_JogAxisZLabel
        // 
        R_JogAxisZLabel.AccessibleRole = AccessibleRole.StaticText;
        R_JogAxisZLabel.CornerRadius = 10;
        R_JogAxisZLabel.Location = new Point(9, 283);
        R_JogAxisZLabel.Margin = new Padding(8, 0, 8, 8);
        R_JogAxisZLabel.Name = "R_JogAxisZLabel";
        R_JogAxisZLabel.Padding = new Padding(0, 0, 8, 0);
        R_JogAxisZLabel.Size = new Size(52, 56);
        R_JogAxisZLabel.TabIndex = 11;
        R_JogAxisZLabel.TabStop = false;
        R_JogAxisZLabel.Text = "Z";
        R_JogAxisZLabel.TextAlign = ContentAlignment.MiddleRight;
        R_JogAxisZLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_JogVelocityX
        // 
        R_JogVelocityX.BackColor = Color.Transparent;
        R_JogVelocityX.CornerRadius = 10;
        R_JogVelocityX.Location = new Point(69, 156);
        R_JogVelocityX.Name = "R_JogVelocityX";
        R_JogVelocityX.Size = new Size(112, 56);
        R_JogVelocityX.TabIndex = 6;
        R_JogVelocityX.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogVelocityY
        // 
        R_JogVelocityY.BackColor = Color.Transparent;
        R_JogVelocityY.CornerRadius = 10;
        R_JogVelocityY.Location = new Point(69, 220);
        R_JogVelocityY.Name = "R_JogVelocityY";
        R_JogVelocityY.Size = new Size(112, 56);
        R_JogVelocityY.TabIndex = 9;
        R_JogVelocityY.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogVelocityZ
        // 
        R_JogVelocityZ.BackColor = Color.Transparent;
        R_JogVelocityZ.CornerRadius = 10;
        R_JogVelocityZ.Location = new Point(69, 284);
        R_JogVelocityZ.Name = "R_JogVelocityZ";
        R_JogVelocityZ.Size = new Size(112, 56);
        R_JogVelocityZ.TabIndex = 12;
        R_JogVelocityZ.TextAlign = HorizontalAlignment.Center;
        // 
        // R_StepX
        // 
        R_StepX.BackColor = Color.Transparent;
        R_StepX.CornerRadius = 10;
        R_StepX.Location = new Point(189, 156);
        R_StepX.Name = "R_StepX";
        R_StepX.Size = new Size(112, 56);
        R_StepX.TabIndex = 7;
        R_StepX.TextAlign = HorizontalAlignment.Center;
        // 
        // R_StepY
        // 
        R_StepY.BackColor = Color.Transparent;
        R_StepY.CornerRadius = 10;
        R_StepY.Location = new Point(189, 220);
        R_StepY.Name = "R_StepY";
        R_StepY.Size = new Size(112, 56);
        R_StepY.TabIndex = 10;
        R_StepY.TextAlign = HorizontalAlignment.Center;
        // 
        // R_StepZ
        // 
        R_StepZ.BackColor = Color.Transparent;
        R_StepZ.CornerRadius = 10;
        R_StepZ.Location = new Point(189, 284);
        R_StepZ.Name = "R_StepZ";
        R_StepZ.Size = new Size(112, 56);
        R_StepZ.TabIndex = 13;
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
        R_JogNXPY.Location = new Point(9, 221);
        R_JogNXPY.Margin = new Padding(8, 0, 8, 8);
        R_JogNXPY.Name = "R_JogNXPY";
        R_JogNXPY.Size = new Size(112, 56);
        R_JogNXPY.TabIndex = 11;
        R_JogNXPY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPY
        // 
        R_JogPY.AutoEllipsis = true;
        R_JogPY.CornerRadius = 10;
        R_JogPY.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPY.GradientStrength = 0.14F;
        R_JogPY.Location = new Point(129, 221);
        R_JogPY.Name = "R_JogPY";
        R_JogPY.Size = new Size(112, 56);
        R_JogPY.TabIndex = 12;
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
        R_JogPXPY.Location = new Point(249, 221);
        R_JogPXPY.Name = "R_JogPXPY";
        R_JogPXPY.Padding = new Padding(0);
        R_JogPXPY.Size = new Size(112, 56);
        R_JogPXPY.TabIndex = 13;
        R_JogPXPY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPZ
        // 
        R_JogPZ.AutoEllipsis = true;
        R_JogPZ.CornerRadius = 10;
        R_JogPZ.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPZ.GradientStrength = 0.14F;
        R_JogPZ.Location = new Point(369, 220);
        R_JogPZ.Name = "R_JogPZ";
        R_JogPZ.Size = new Size(112, 56);
        R_JogPZ.TabIndex = 14;
        R_JogPZ.Text = "Z+";
        // 
        // R_JogNX
        // 
        R_JogNX.AutoEllipsis = true;
        R_JogNX.CornerRadius = 10;
        R_JogNX.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNX.GradientStrength = 0.14F;
        R_JogNX.Location = new Point(9, 285);
        R_JogNX.Margin = new Padding(8, 0, 8, 8);
        R_JogNX.Name = "R_JogNX";
        R_JogNX.Size = new Size(112, 56);
        R_JogNX.TabIndex = 16;
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
        R_JogMode.Location = new Point(129, 285);
        R_JogMode.Name = "R_JogMode";
        R_JogMode.Padding = new Padding(0, 4, 0, 4);
        R_JogMode.Size = new Size(112, 56);
        R_JogMode.TabIndex = 17;
        R_JogMode.Text = "Step Jog";
        R_JogMode.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // R_JogPX
        // 
        R_JogPX.AutoEllipsis = true;
        R_JogPX.CornerRadius = 10;
        R_JogPX.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPX.GradientStrength = 0.14F;
        R_JogPX.Location = new Point(249, 285);
        R_JogPX.Name = "R_JogPX";
        R_JogPX.Size = new Size(112, 56);
        R_JogPX.TabIndex = 18;
        R_JogPX.Text = "X+";
        R_JogPX.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPause
        // 
        R_JogPause.AutoEllipsis = true;
        R_JogPause.CornerRadius = 10;
        R_JogPause.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPause.GradientStrength = 0.14F;
        R_JogPause.Location = new Point(369, 284);
        R_JogPause.Name = "R_JogPause";
        R_JogPause.Size = new Size(112, 56);
        R_JogPause.TabIndex = 19;
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
        R_JogNXNY.Location = new Point(9, 349);
        R_JogNXNY.Margin = new Padding(8, 0, 8, 8);
        R_JogNXNY.Name = "R_JogNXNY";
        R_JogNXNY.Size = new Size(112, 56);
        R_JogNXNY.TabIndex = 20;
        R_JogNXNY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogNY
        // 
        R_JogNY.AutoEllipsis = true;
        R_JogNY.CornerRadius = 10;
        R_JogNY.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNY.GradientStrength = 0.14F;
        R_JogNY.Location = new Point(129, 349);
        R_JogNY.Name = "R_JogNY";
        R_JogNY.Size = new Size(112, 56);
        R_JogNY.TabIndex = 21;
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
        R_JogPXNY.Location = new Point(249, 349);
        R_JogPXNY.Name = "R_JogPXNY";
        R_JogPXNY.Size = new Size(112, 56);
        R_JogPXNY.TabIndex = 22;
        R_JogPXNY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogNZ
        // 
        R_JogNZ.AutoEllipsis = true;
        R_JogNZ.CornerRadius = 10;
        R_JogNZ.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNZ.GradientStrength = 0.14F;
        R_JogNZ.Location = new Point(369, 348);
        R_JogNZ.Name = "R_JogNZ";
        R_JogNZ.Size = new Size(112, 56);
        R_JogNZ.TabIndex = 23;
        R_JogNZ.Text = "Z−";
        // 
        // R_JogFeedOverride
        // 
        R_JogFeedOverride.AccessibleRole = AccessibleRole.Slider;
        R_JogFeedOverride.CornerRadius = 10;
        R_JogFeedOverride.LargeChange = 20D;
        R_JogFeedOverride.Location = new Point(512, 221);
        R_JogFeedOverride.Minimum = 5D;
        R_JogFeedOverride.Name = "R_JogFeedOverride";
        R_JogFeedOverride.Orientation = NT.Tools.SSliderOrientation.Vertical;
        R_JogFeedOverride.ShowSteps = true;
        R_JogFeedOverride.Size = new Size(112, 184);
        R_JogFeedOverride.SliderStyle = NT.Tools.SSliderStyle.Fader;
        R_JogFeedOverride.SmallChange = 5D;
        R_JogFeedOverride.Step = 5D;
        R_JogFeedOverride.TabIndex = 15;
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
        R_Home.TabIndex = 1;
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
        R_GoHome.TabIndex = 11;
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
        R_GoToReference.TabIndex = 6;
        R_GoToReference.Text = "Go Reference";
        R_GoToReference.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_ReferencePause
        // 
        R_ReferencePause.AutoEllipsis = true;
        R_ReferencePause.CornerRadius = 10;
        R_ReferencePause.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_ReferencePause.GradientStrength = 0.14F;
        R_ReferencePause.Location = new Point(185, 188);
        R_ReferencePause.Name = "R_ReferencePause";
        R_ReferencePause.Padding = new Padding(0, 4, 0, 4);
        R_ReferencePause.Size = new Size(112, 56);
        R_ReferencePause.TabIndex = 7;
        R_ReferencePause.Text = "Pause";
        R_ReferencePause.Tone = NT.Tools.SButtonTone.Warning;
        // 
        // R_RefZZero
        // 
        R_RefZZero.AutoEllipsis = true;
        R_RefZZero.CornerRadius = 10;
        R_RefZZero.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_RefZZero.GradientStrength = 0.14F;
        R_RefZZero.Location = new Point(513, 187);
        R_RefZZero.Name = "R_RefZZero";
        R_RefZZero.Padding = new Padding(0, 4, 0, 4);
        R_RefZZero.Size = new Size(112, 56);
        R_RefZZero.TabIndex = 14;
        R_RefZZero.Text = "Set Z0";
        R_RefZZero.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_RefYZero
        // 
        R_RefYZero.AutoEllipsis = true;
        R_RefYZero.CornerRadius = 10;
        R_RefYZero.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_RefYZero.GradientStrength = 0.14F;
        R_RefYZero.Location = new Point(513, 123);
        R_RefYZero.Name = "R_RefYZero";
        R_RefYZero.Padding = new Padding(0, 4, 0, 4);
        R_RefYZero.Size = new Size(112, 56);
        R_RefYZero.TabIndex = 10;
        R_RefYZero.Text = "Set Y0";
        R_RefYZero.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_RefXZero
        // 
        R_RefXZero.AutoEllipsis = true;
        R_RefXZero.CornerRadius = 10;
        R_RefXZero.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_RefXZero.GradientStrength = 0.14F;
        R_RefXZero.Location = new Point(513, 59);
        R_RefXZero.Name = "R_RefXZero";
        R_RefXZero.Padding = new Padding(0, 4, 0, 4);
        R_RefXZero.Size = new Size(112, 56);
        R_RefXZero.TabIndex = 5;
        R_RefXZero.Text = "Set X0";
        R_RefXZero.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_GCodeView
        // 
        R_GCodeView.AccessibleRole = AccessibleRole.List;
        R_GCodeView.CornerRadius = 10;
        R_GCodeView.FirstVisibleLine = 0;
        R_GCodeView.Location = new Point(9, 60);
        R_GCodeView.Margin = new Padding(8);
        R_GCodeView.Name = "R_GCodeView";
        R_GCodeView.Size = new Size(631, 185);
        R_GCodeView.TabIndex = 2;
        // 
        // R_FeedOverride
        // 
        R_FeedOverride.AccessibleRole = AccessibleRole.Slider;
        R_FeedOverride.CornerRadius = 10;
        R_FeedOverride.LargeChange = 20D;
        R_FeedOverride.Location = new Point(824, 59);
        R_FeedOverride.Margin = new Padding(0, 8, 8, 8);
        R_FeedOverride.Maximum = 120D;
        R_FeedOverride.Name = "R_FeedOverride";
        R_FeedOverride.Orientation = NT.Tools.SSliderOrientation.Vertical;
        R_FeedOverride.ShowSteps = true;
        R_FeedOverride.Size = new Size(112, 186);
        R_FeedOverride.SliderStyle = NT.Tools.SSliderStyle.Fader;
        R_FeedOverride.SmallChange = 10D;
        R_FeedOverride.Step = 10D;
        R_FeedOverride.TabIndex = 6;
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
        R_RunProgram.Location = new Point(648, 59);
        R_RunProgram.Margin = new Padding(0, 8, 8, 8);
        R_RunProgram.Name = "R_RunProgram";
        R_RunProgram.Size = new Size(168, 56);
        R_RunProgram.TabIndex = 3;
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
        R_PauseProgram.Location = new Point(648, 125);
        R_PauseProgram.Name = "R_PauseProgram";
        R_PauseProgram.Size = new Size(168, 56);
        R_PauseProgram.TabIndex = 4;
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
        R_ResetProgram.Location = new Point(648, 189);
        R_ResetProgram.Name = "R_ResetProgram";
        R_ResetProgram.Size = new Size(168, 56);
        R_ResetProgram.TabIndex = 5;
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
        R_EditGCode.TabIndex = 22;
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
        R_SelectStartLine.TabIndex = 23;
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
        R_OpenFile.TabIndex = 21;
        R_OpenFile.Text = "Open file";
        R_OpenFile.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // R_ToolLength
        // 
        R_ToolLength.BackColor = Color.Transparent;
        R_ToolLength.CornerRadius = 10;
        R_ToolLength.Location = new Point(193, 189);
        R_ToolLength.Name = "R_ToolLength";
        R_ToolLength.Size = new Size(105, 56);
        R_ToolLength.TabIndex = 4;
        // 
        // R_SpindlePageTitle
        // 
        R_SpindlePageTitle.AccessibleRole = AccessibleRole.StaticText;
        R_SpindlePageTitle.CornerRadius = 10;
        R_SpindlePageTitle.Location = new Point(10, 18);
        R_SpindlePageTitle.Margin = new Padding(8, 16, 0, 8);
        R_SpindlePageTitle.Name = "R_SpindlePageTitle";
        R_SpindlePageTitle.Padding = new Padding(0);
        R_SpindlePageTitle.Size = new Size(288, 34);
        R_SpindlePageTitle.TabIndex = 0;
        R_SpindlePageTitle.TabStop = false;
        R_SpindlePageTitle.Text = "Spindle Part";
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
        R_Spindle.Location = new Point(9, 58);
        R_Spindle.Margin = new Padding(8, 0, 8, 8);
        R_Spindle.Name = "R_Spindle";
        R_Spindle.Size = new Size(168, 56);
        R_Spindle.TabIndex = 2;
        R_Spindle.Text = "Spindle";
        R_Spindle.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // R_SpindleSpeed
        // 
        R_SpindleSpeed.AccessibleRole = AccessibleRole.Slider;
        R_SpindleSpeed.CornerRadius = 10;
        R_SpindleSpeed.Location = new Point(186, 60);
        R_SpindleSpeed.Name = "R_SpindleSpeed";
        R_SpindleSpeed.Orientation = NT.Tools.SSliderOrientation.Vertical;
        R_SpindleSpeed.ShowSteps = true;
        R_SpindleSpeed.Size = new Size(112, 185);
        R_SpindleSpeed.SliderStyle = NT.Tools.SSliderStyle.Fader;
        R_SpindleSpeed.TabIndex = 3;
        // 
        // R_ToolLengthLabel
        // 
        R_ToolLengthLabel.AccessibleRole = AccessibleRole.StaticText;
        R_ToolLengthLabel.CornerRadius = 10;
        R_ToolLengthLabel.Location = new Point(9, 203);
        R_ToolLengthLabel.Margin = new Padding(0, 16, 8, 16);
        R_ToolLengthLabel.Name = "R_ToolLengthLabel";
        R_ToolLengthLabel.Padding = new Padding(0);
        R_ToolLengthLabel.Size = new Size(176, 34);
        R_ToolLengthLabel.TabIndex = 3;
        R_ToolLengthLabel.TabStop = false;
        R_ToolLengthLabel.Text = "Tool Length (mm)";
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
        R_ProgramProgress.TabIndex = 24;
        R_ProgramProgress.TabStop = false;
        R_ProgramProgress.Text = "DESIGN PREVIEW  /  Machine commands are not connected";
        // 
        // R_ReferenceX
        // 
        R_ReferenceX.BackColor = Color.Transparent;
        R_ReferenceX.CornerRadius = 10;
        R_ReferenceX.Location = new Point(393, 59);
        R_ReferenceX.Name = "R_ReferenceX";
        R_ReferenceX.Size = new Size(112, 56);
        R_ReferenceX.TabIndex = 4;
        R_ReferenceX.Visible = false;
        // 
        // R_ReferenceY
        // 
        R_ReferenceY.BackColor = Color.Transparent;
        R_ReferenceY.CornerRadius = 10;
        R_ReferenceY.Location = new Point(393, 123);
        R_ReferenceY.Name = "R_ReferenceY";
        R_ReferenceY.Size = new Size(112, 56);
        R_ReferenceY.TabIndex = 9;
        R_ReferenceY.Visible = false;
        // 
        // R_ReferenceZ
        // 
        R_ReferenceZ.BackColor = Color.Transparent;
        R_ReferenceZ.CornerRadius = 10;
        R_ReferenceZ.Location = new Point(393, 187);
        R_ReferenceZ.Name = "R_ReferenceZ";
        R_ReferenceZ.Size = new Size(112, 56);
        R_ReferenceZ.TabIndex = 13;
        R_ReferenceZ.Visible = false;
        // 
        // R_ReferenceSelector
        // 
        R_ReferenceSelector.BackColor = Color.Transparent;
        R_ReferenceSelector.CornerRadius = 10;
        R_ReferenceSelector.Location = new Point(217, 59);
        R_ReferenceSelector.Margin = new Padding(0, 0, 0, 8);
        R_ReferenceSelector.Name = "R_ReferenceSelector";
        R_ReferenceSelector.Size = new Size(112, 56);
        R_ReferenceSelector.TabIndex = 2;
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
        R_ReferencePanel.Location = new Point(1277, 332);
        R_ReferencePanel.Margin = new Padding(0, 0, 0, 8);
        R_ReferencePanel.Name = "R_ReferencePanel";
        R_ReferencePanel.Padding = new Padding(0);
        R_ReferencePanel.Size = new Size(634, 253);
        R_ReferencePanel.TabIndex = 17;
        // 
        // R_ReferencePanelTitle
        // 
        R_ReferencePanelTitle.AccessibleRole = AccessibleRole.StaticText;
        R_ReferencePanelTitle.CornerRadius = 10;
        R_ReferencePanelTitle.Location = new Point(393, 17);
        R_ReferencePanelTitle.Margin = new Padding(0, 16, 0, 8);
        R_ReferencePanelTitle.Name = "R_ReferencePanelTitle";
        R_ReferencePanelTitle.Padding = new Padding(0);
        R_ReferencePanelTitle.Size = new Size(234, 34);
        R_ReferencePanelTitle.TabIndex = 0;
        R_ReferencePanelTitle.TabStop = false;
        R_ReferencePanelTitle.Text = "Reference(mm)";
        // 
        // R_ReferenceAxisXLabel
        // 
        R_ReferenceAxisXLabel.AccessibleRole = AccessibleRole.StaticText;
        R_ReferenceAxisXLabel.CornerRadius = 10;
        R_ReferenceAxisXLabel.Location = new Point(329, 59);
        R_ReferenceAxisXLabel.Name = "R_ReferenceAxisXLabel";
        R_ReferenceAxisXLabel.Padding = new Padding(0, 0, 8, 0);
        R_ReferenceAxisXLabel.Size = new Size(56, 56);
        R_ReferenceAxisXLabel.TabIndex = 3;
        R_ReferenceAxisXLabel.TabStop = false;
        R_ReferenceAxisXLabel.Text = "X";
        R_ReferenceAxisXLabel.TextAlign = ContentAlignment.MiddleRight;
        R_ReferenceAxisXLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_ReferenceAxisYLabel
        // 
        R_ReferenceAxisYLabel.AccessibleRole = AccessibleRole.StaticText;
        R_ReferenceAxisYLabel.CornerRadius = 10;
        R_ReferenceAxisYLabel.Location = new Point(329, 123);
        R_ReferenceAxisYLabel.Margin = new Padding(8, 0, 8, 8);
        R_ReferenceAxisYLabel.Name = "R_ReferenceAxisYLabel";
        R_ReferenceAxisYLabel.Padding = new Padding(0, 0, 8, 0);
        R_ReferenceAxisYLabel.Size = new Size(56, 56);
        R_ReferenceAxisYLabel.TabIndex = 8;
        R_ReferenceAxisYLabel.TabStop = false;
        R_ReferenceAxisYLabel.Text = "Y";
        R_ReferenceAxisYLabel.TextAlign = ContentAlignment.MiddleRight;
        R_ReferenceAxisYLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_ReferenceAxisZLabel
        // 
        R_ReferenceAxisZLabel.AccessibleRole = AccessibleRole.StaticText;
        R_ReferenceAxisZLabel.CornerRadius = 10;
        R_ReferenceAxisZLabel.Location = new Point(329, 187);
        R_ReferenceAxisZLabel.Margin = new Padding(8, 0, 8, 8);
        R_ReferenceAxisZLabel.Name = "R_ReferenceAxisZLabel";
        R_ReferenceAxisZLabel.Padding = new Padding(0, 0, 8, 0);
        R_ReferenceAxisZLabel.Size = new Size(56, 56);
        R_ReferenceAxisZLabel.TabIndex = 12;
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
        R_Homed.TabIndex = 7;
        R_Homed.TabStop = false;
        R_Homed.Text = "Homed";
        // 
        // R_JogPagePanel
        // 
        R_JogPagePanel.CornerRadius = 10;
        R_JogPagePanel.Location = new Point(1277, 593);
        R_JogPagePanel.Margin = new Padding(0, 0, 0, 8);
        R_JogPagePanel.Name = "R_JogPagePanel";
        R_JogPagePanel.Padding = new Padding(0);
        R_JogPagePanel.Pages.AddRange(new NT.Tools.sPage[] { R_JogControlsPage, R_JogPropertiesPage, R_JogPositionsPage });
        R_JogPagePanel.Size = new Size(634, 414);
        R_JogPagePanel.TabIndex = 18;
        // 
        // R_JogControlsPage
        // 
        R_JogControlsPage.Controls.Add(R_JogControlsTitle);
        R_JogControlsPage.Controls.Add(R_JogNZT);
        R_JogControlsPage.Controls.Add(R_JogPZT);
        R_JogControlsPage.Controls.Add(R_JogNCP);
        R_JogControlsPage.Controls.Add(R_JogNAP);
        R_JogControlsPage.Controls.Add(R_JogPAP);
        R_JogControlsPage.Controls.Add(R_JogPCP);
        R_JogControlsPage.Controls.Add(R_GotoJogProperties);
        R_JogControlsPage.Controls.Add(R_JogNC);
        R_JogControlsPage.Controls.Add(R_JogNA);
        R_JogControlsPage.Controls.Add(R_JogPA);
        R_JogControlsPage.Controls.Add(R_JogPC);
        R_JogControlsPage.Controls.Add(R_JogNXNY);
        R_JogControlsPage.Controls.Add(R_JogFeedOverride);
        R_JogControlsPage.Controls.Add(R_JogNZ);
        R_JogControlsPage.Controls.Add(R_JogPXNY);
        R_JogControlsPage.Controls.Add(R_JogNY);
        R_JogControlsPage.Controls.Add(R_JogPause);
        R_JogControlsPage.Controls.Add(R_JogPX);
        R_JogControlsPage.Controls.Add(R_JogMode);
        R_JogControlsPage.Controls.Add(R_JogNX);
        R_JogControlsPage.Controls.Add(R_JogPZ);
        R_JogControlsPage.Controls.Add(R_JogPXPY);
        R_JogControlsPage.Controls.Add(R_JogPY);
        R_JogControlsPage.Controls.Add(R_JogNXPY);
        R_JogControlsPage.ForeColor = Color.FromArgb(225, 228, 232);
        R_JogControlsPage.Name = "R_JogControlsPage";
        // 
        // R_JogControlsTitle
        // 
        R_JogControlsTitle.AccessibleRole = AccessibleRole.StaticText;
        R_JogControlsTitle.CornerRadius = 10;
        R_JogControlsTitle.Location = new Point(11, 17);
        R_JogControlsTitle.Margin = new Padding(0, 16, 0, 8);
        R_JogControlsTitle.Name = "R_JogControlsTitle";
        R_JogControlsTitle.Padding = new Padding(0);
        R_JogControlsTitle.Size = new Size(230, 34);
        R_JogControlsTitle.TabIndex = 24;
        R_JogControlsTitle.TabStop = false;
        R_JogControlsTitle.Text = "Jog";
        // 
        // R_JogNZT
        // 
        R_JogNZT.AutoEllipsis = true;
        R_JogNZT.CornerRadius = 10;
        R_JogNZT.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNZT.GradientStrength = 0.14F;
        R_JogNZT.Location = new Point(512, 123);
        R_JogNZT.Name = "R_JogNZT";
        R_JogNZT.Padding = new Padding(0);
        R_JogNZT.Size = new Size(112, 56);
        R_JogNZT.TabIndex = 10;
        R_JogNZT.Text = "ZT-";
        R_JogNZT.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPZT
        // 
        R_JogPZT.AutoEllipsis = true;
        R_JogPZT.CornerRadius = 10;
        R_JogPZT.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPZT.GradientStrength = 0.14F;
        R_JogPZT.Location = new Point(512, 59);
        R_JogPZT.Name = "R_JogPZT";
        R_JogPZT.Padding = new Padding(0);
        R_JogPZT.Size = new Size(112, 56);
        R_JogPZT.TabIndex = 5;
        R_JogPZT.Text = "ZT+";
        R_JogPZT.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogNCP
        // 
        R_JogNCP.AutoEllipsis = true;
        R_JogNCP.CornerRadius = 10;
        R_JogNCP.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNCP.GradientStrength = 0.14F;
        R_JogNCP.Location = new Point(392, 123);
        R_JogNCP.Name = "R_JogNCP";
        R_JogNCP.Padding = new Padding(0);
        R_JogNCP.Size = new Size(112, 56);
        R_JogNCP.TabIndex = 9;
        R_JogNCP.Text = "CP-";
        R_JogNCP.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogNAP
        // 
        R_JogNAP.AutoEllipsis = true;
        R_JogNAP.CornerRadius = 10;
        R_JogNAP.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNAP.GradientStrength = 0.14F;
        R_JogNAP.Location = new Point(272, 123);
        R_JogNAP.Name = "R_JogNAP";
        R_JogNAP.Padding = new Padding(0);
        R_JogNAP.Size = new Size(112, 56);
        R_JogNAP.TabIndex = 8;
        R_JogNAP.Text = "AP-";
        R_JogNAP.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPAP
        // 
        R_JogPAP.AutoEllipsis = true;
        R_JogPAP.CornerRadius = 10;
        R_JogPAP.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPAP.GradientStrength = 0.14F;
        R_JogPAP.Location = new Point(272, 59);
        R_JogPAP.Margin = new Padding(8, 0, 8, 8);
        R_JogPAP.Name = "R_JogPAP";
        R_JogPAP.Padding = new Padding(0);
        R_JogPAP.Size = new Size(112, 56);
        R_JogPAP.TabIndex = 3;
        R_JogPAP.Text = "AP+";
        R_JogPAP.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPCP
        // 
        R_JogPCP.AutoEllipsis = true;
        R_JogPCP.CornerRadius = 10;
        R_JogPCP.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPCP.GradientStrength = 0.14F;
        R_JogPCP.Location = new Point(392, 59);
        R_JogPCP.Name = "R_JogPCP";
        R_JogPCP.Padding = new Padding(0);
        R_JogPCP.Size = new Size(112, 56);
        R_JogPCP.TabIndex = 4;
        R_JogPCP.Text = "CP+";
        R_JogPCP.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_GotoJogProperties
        // 
        R_GotoJogProperties.AutoEllipsis = true;
        R_GotoJogProperties.CornerRadius = 10;
        R_GotoJogProperties.GradientStrength = 0.14F;
        R_GotoJogProperties.IconSize = new Size(36, 36);
        R_GotoJogProperties.Image = Properties.Resources.AR__35_;
        R_GotoJogProperties.Location = new Point(585, 9);
        R_GotoJogProperties.Margin = new Padding(8);
        R_GotoJogProperties.Name = "R_GotoJogProperties";
        R_GotoJogProperties.Padding = new Padding(0);
        R_GotoJogProperties.Size = new Size(40, 40);
        R_GotoJogProperties.SurfaceStyle = NT.Tools.SSurfaceStyle.Text;
        R_GotoJogProperties.TabIndex = 0;
        R_GotoJogProperties.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogNC
        // 
        R_JogNC.AutoEllipsis = true;
        R_JogNC.CornerRadius = 10;
        R_JogNC.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNC.GradientStrength = 0.14F;
        R_JogNC.Location = new Point(129, 123);
        R_JogNC.Name = "R_JogNC";
        R_JogNC.Padding = new Padding(0);
        R_JogNC.Size = new Size(112, 56);
        R_JogNC.TabIndex = 7;
        R_JogNC.Text = "C-";
        R_JogNC.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogNA
        // 
        R_JogNA.AutoEllipsis = true;
        R_JogNA.CornerRadius = 10;
        R_JogNA.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogNA.GradientStrength = 0.14F;
        R_JogNA.Location = new Point(9, 123);
        R_JogNA.Name = "R_JogNA";
        R_JogNA.Padding = new Padding(0);
        R_JogNA.Size = new Size(112, 56);
        R_JogNA.TabIndex = 6;
        R_JogNA.Text = "A-";
        R_JogNA.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPA
        // 
        R_JogPA.AutoEllipsis = true;
        R_JogPA.CornerRadius = 10;
        R_JogPA.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPA.GradientStrength = 0.14F;
        R_JogPA.Location = new Point(9, 59);
        R_JogPA.Margin = new Padding(8, 0, 8, 8);
        R_JogPA.Name = "R_JogPA";
        R_JogPA.Padding = new Padding(0);
        R_JogPA.Size = new Size(112, 56);
        R_JogPA.TabIndex = 1;
        R_JogPA.Text = "A+";
        R_JogPA.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPC
        // 
        R_JogPC.AutoEllipsis = true;
        R_JogPC.CornerRadius = 10;
        R_JogPC.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_JogPC.GradientStrength = 0.14F;
        R_JogPC.Location = new Point(129, 59);
        R_JogPC.Name = "R_JogPC";
        R_JogPC.Padding = new Padding(0);
        R_JogPC.Size = new Size(112, 56);
        R_JogPC.TabIndex = 2;
        R_JogPC.Text = "C+";
        R_JogPC.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPropertiesPage
        // 
        R_JogPropertiesPage.Controls.Add(R_JogPropertiesTitle);
        R_JogPropertiesPage.Controls.Add(R_RtcpJogStepHeader);
        R_JogPropertiesPage.Controls.Add(R_RtcpJogVelocityHeader);
        R_JogPropertiesPage.Controls.Add(R_JogAxisZTLabel);
        R_JogPropertiesPage.Controls.Add(R_StepZT);
        R_JogPropertiesPage.Controls.Add(R_JogAxisCPLabel);
        R_JogPropertiesPage.Controls.Add(R_StepCP);
        R_JogPropertiesPage.Controls.Add(R_JogVelocityZT);
        R_JogPropertiesPage.Controls.Add(R_JogVelocityCP);
        R_JogPropertiesPage.Controls.Add(R_JogAxisAPLabel);
        R_JogPropertiesPage.Controls.Add(R_StepAP);
        R_JogPropertiesPage.Controls.Add(R_JogVelocityAP);
        R_JogPropertiesPage.Controls.Add(R_GotoJogPositions);
        R_JogPropertiesPage.Controls.Add(R_JogAxisCLabel);
        R_JogPropertiesPage.Controls.Add(R_StepC);
        R_JogPropertiesPage.Controls.Add(R_JogAxisALabel);
        R_JogPropertiesPage.Controls.Add(R_StepA);
        R_JogPropertiesPage.Controls.Add(R_JogVelocityC);
        R_JogPropertiesPage.Controls.Add(R_JogVelocityA);
        R_JogPropertiesPage.Controls.Add(R_JogStepHeader);
        R_JogPropertiesPage.Controls.Add(R_JogAxisZLabel);
        R_JogPropertiesPage.Controls.Add(R_JogAxisXLabel);
        R_JogPropertiesPage.Controls.Add(R_StepZ);
        R_JogPropertiesPage.Controls.Add(R_JogAxisYLabel);
        R_JogPropertiesPage.Controls.Add(R_StepY);
        R_JogPropertiesPage.Controls.Add(R_JogVelocityHeader);
        R_JogPropertiesPage.Controls.Add(R_StepX);
        R_JogPropertiesPage.Controls.Add(R_JogVelocityZ);
        R_JogPropertiesPage.Controls.Add(R_JogVelocityY);
        R_JogPropertiesPage.Controls.Add(R_JogVelocityX);
        R_JogPropertiesPage.ForeColor = Color.FromArgb(225, 228, 232);
        R_JogPropertiesPage.Name = "R_JogPropertiesPage";
        // 
        // R_JogPropertiesTitle
        // 
        R_JogPropertiesTitle.AccessibleRole = AccessibleRole.StaticText;
        R_JogPropertiesTitle.CornerRadius = 10;
        R_JogPropertiesTitle.Location = new Point(9, 17);
        R_JogPropertiesTitle.Margin = new Padding(0, 16, 0, 8);
        R_JogPropertiesTitle.Name = "R_JogPropertiesTitle";
        R_JogPropertiesTitle.Padding = new Padding(0);
        R_JogPropertiesTitle.Size = new Size(292, 34);
        R_JogPropertiesTitle.TabIndex = 29;
        R_JogPropertiesTitle.TabStop = false;
        R_JogPropertiesTitle.Text = "Jog Properties";
        // 
        // R_RtcpJogStepHeader
        // 
        R_RtcpJogStepHeader.AccessibleRole = AccessibleRole.StaticText;
        R_RtcpJogStepHeader.CornerRadius = 10;
        R_RtcpJogStepHeader.Location = new Point(514, 114);
        R_RtcpJogStepHeader.Margin = new Padding(0, 16, 0, 8);
        R_RtcpJogStepHeader.Name = "R_RtcpJogStepHeader";
        R_RtcpJogStepHeader.Padding = new Padding(0);
        R_RtcpJogStepHeader.Size = new Size(112, 34);
        R_RtcpJogStepHeader.TabIndex = 4;
        R_RtcpJogStepHeader.TabStop = false;
        R_RtcpJogStepHeader.Text = "Step";
        // 
        // R_RtcpJogVelocityHeader
        // 
        R_RtcpJogVelocityHeader.AccessibleRole = AccessibleRole.StaticText;
        R_RtcpJogVelocityHeader.CornerRadius = 10;
        R_RtcpJogVelocityHeader.Location = new Point(394, 114);
        R_RtcpJogVelocityHeader.Margin = new Padding(0, 16, 0, 8);
        R_RtcpJogVelocityHeader.Name = "R_RtcpJogVelocityHeader";
        R_RtcpJogVelocityHeader.Padding = new Padding(0);
        R_RtcpJogVelocityHeader.Size = new Size(112, 34);
        R_RtcpJogVelocityHeader.TabIndex = 3;
        R_RtcpJogVelocityHeader.TabStop = false;
        R_RtcpJogVelocityHeader.Text = "V (/s)";
        // 
        // R_JogAxisZTLabel
        // 
        R_JogAxisZTLabel.AccessibleRole = AccessibleRole.StaticText;
        R_JogAxisZTLabel.CornerRadius = 10;
        R_JogAxisZTLabel.Location = new Point(9, 347);
        R_JogAxisZTLabel.Margin = new Padding(8, 0, 8, 8);
        R_JogAxisZTLabel.Name = "R_JogAxisZTLabel";
        R_JogAxisZTLabel.Padding = new Padding(0, 0, 8, 0);
        R_JogAxisZTLabel.Size = new Size(52, 56);
        R_JogAxisZTLabel.TabIndex = 26;
        R_JogAxisZTLabel.TabStop = false;
        R_JogAxisZTLabel.Text = "ZT";
        R_JogAxisZTLabel.TextAlign = ContentAlignment.MiddleRight;
        R_JogAxisZTLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_StepZT
        // 
        R_StepZT.BackColor = Color.Transparent;
        R_StepZT.CornerRadius = 10;
        R_StepZT.Location = new Point(189, 348);
        R_StepZT.Name = "R_StepZT";
        R_StepZT.Size = new Size(112, 56);
        R_StepZT.TabIndex = 28;
        R_StepZT.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogAxisCPLabel
        // 
        R_JogAxisCPLabel.AccessibleRole = AccessibleRole.StaticText;
        R_JogAxisCPLabel.CornerRadius = 10;
        R_JogAxisCPLabel.Location = new Point(334, 347);
        R_JogAxisCPLabel.Margin = new Padding(8, 0, 8, 8);
        R_JogAxisCPLabel.Name = "R_JogAxisCPLabel";
        R_JogAxisCPLabel.Padding = new Padding(0, 0, 8, 0);
        R_JogAxisCPLabel.Size = new Size(52, 56);
        R_JogAxisCPLabel.TabIndex = 23;
        R_JogAxisCPLabel.TabStop = false;
        R_JogAxisCPLabel.Text = "CP";
        R_JogAxisCPLabel.TextAlign = ContentAlignment.MiddleRight;
        R_JogAxisCPLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_StepCP
        // 
        R_StepCP.BackColor = Color.Transparent;
        R_StepCP.CornerRadius = 10;
        R_StepCP.Location = new Point(514, 348);
        R_StepCP.Name = "R_StepCP";
        R_StepCP.Size = new Size(112, 56);
        R_StepCP.TabIndex = 25;
        R_StepCP.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogVelocityZT
        // 
        R_JogVelocityZT.BackColor = Color.Transparent;
        R_JogVelocityZT.CornerRadius = 10;
        R_JogVelocityZT.Location = new Point(69, 348);
        R_JogVelocityZT.Name = "R_JogVelocityZT";
        R_JogVelocityZT.Size = new Size(112, 56);
        R_JogVelocityZT.TabIndex = 27;
        R_JogVelocityZT.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogVelocityCP
        // 
        R_JogVelocityCP.BackColor = Color.Transparent;
        R_JogVelocityCP.CornerRadius = 10;
        R_JogVelocityCP.Location = new Point(394, 348);
        R_JogVelocityCP.Name = "R_JogVelocityCP";
        R_JogVelocityCP.Size = new Size(112, 56);
        R_JogVelocityCP.TabIndex = 24;
        R_JogVelocityCP.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogAxisAPLabel
        // 
        R_JogAxisAPLabel.AccessibleRole = AccessibleRole.StaticText;
        R_JogAxisAPLabel.CornerRadius = 10;
        R_JogAxisAPLabel.Location = new Point(334, 284);
        R_JogAxisAPLabel.Margin = new Padding(8, 0, 8, 8);
        R_JogAxisAPLabel.Name = "R_JogAxisAPLabel";
        R_JogAxisAPLabel.Padding = new Padding(0, 0, 8, 0);
        R_JogAxisAPLabel.Size = new Size(52, 56);
        R_JogAxisAPLabel.TabIndex = 20;
        R_JogAxisAPLabel.TabStop = false;
        R_JogAxisAPLabel.Text = "AP";
        R_JogAxisAPLabel.TextAlign = ContentAlignment.MiddleRight;
        R_JogAxisAPLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_StepAP
        // 
        R_StepAP.BackColor = Color.Transparent;
        R_StepAP.CornerRadius = 10;
        R_StepAP.Location = new Point(514, 285);
        R_StepAP.Name = "R_StepAP";
        R_StepAP.Size = new Size(112, 56);
        R_StepAP.TabIndex = 22;
        R_StepAP.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogVelocityAP
        // 
        R_JogVelocityAP.BackColor = Color.Transparent;
        R_JogVelocityAP.CornerRadius = 10;
        R_JogVelocityAP.Location = new Point(394, 285);
        R_JogVelocityAP.Name = "R_JogVelocityAP";
        R_JogVelocityAP.Size = new Size(112, 56);
        R_JogVelocityAP.TabIndex = 21;
        R_JogVelocityAP.TextAlign = HorizontalAlignment.Center;
        // 
        // R_GotoJogPositions
        // 
        R_GotoJogPositions.AutoEllipsis = true;
        R_GotoJogPositions.CornerRadius = 10;
        R_GotoJogPositions.GradientStrength = 0.14F;
        R_GotoJogPositions.IconSize = new Size(36, 36);
        R_GotoJogPositions.Image = Properties.Resources.AR__35_;
        R_GotoJogPositions.Location = new Point(585, 9);
        R_GotoJogPositions.Name = "R_GotoJogPositions";
        R_GotoJogPositions.Padding = new Padding(0);
        R_GotoJogPositions.Size = new Size(40, 40);
        R_GotoJogPositions.SurfaceStyle = NT.Tools.SSurfaceStyle.Text;
        R_GotoJogPositions.TabIndex = 0;
        R_GotoJogPositions.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogAxisCLabel
        // 
        R_JogAxisCLabel.AccessibleRole = AccessibleRole.StaticText;
        R_JogAxisCLabel.CornerRadius = 10;
        R_JogAxisCLabel.Location = new Point(334, 220);
        R_JogAxisCLabel.Margin = new Padding(8, 0, 8, 8);
        R_JogAxisCLabel.Name = "R_JogAxisCLabel";
        R_JogAxisCLabel.Padding = new Padding(0, 0, 8, 0);
        R_JogAxisCLabel.Size = new Size(52, 56);
        R_JogAxisCLabel.TabIndex = 17;
        R_JogAxisCLabel.TabStop = false;
        R_JogAxisCLabel.Text = "C";
        R_JogAxisCLabel.TextAlign = ContentAlignment.MiddleRight;
        R_JogAxisCLabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_StepC
        // 
        R_StepC.BackColor = Color.Transparent;
        R_StepC.CornerRadius = 10;
        R_StepC.Location = new Point(514, 221);
        R_StepC.Name = "R_StepC";
        R_StepC.Size = new Size(112, 56);
        R_StepC.TabIndex = 19;
        R_StepC.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogAxisALabel
        // 
        R_JogAxisALabel.AccessibleRole = AccessibleRole.StaticText;
        R_JogAxisALabel.CornerRadius = 10;
        R_JogAxisALabel.Location = new Point(334, 156);
        R_JogAxisALabel.Margin = new Padding(8, 0, 8, 8);
        R_JogAxisALabel.Name = "R_JogAxisALabel";
        R_JogAxisALabel.Padding = new Padding(0, 0, 8, 0);
        R_JogAxisALabel.Size = new Size(52, 56);
        R_JogAxisALabel.TabIndex = 14;
        R_JogAxisALabel.TabStop = false;
        R_JogAxisALabel.Text = "A";
        R_JogAxisALabel.TextAlign = ContentAlignment.MiddleRight;
        R_JogAxisALabel.Tone = NT.Tools.SButtonTone.Accent;
        // 
        // R_StepA
        // 
        R_StepA.BackColor = Color.Transparent;
        R_StepA.CornerRadius = 10;
        R_StepA.Location = new Point(514, 157);
        R_StepA.Name = "R_StepA";
        R_StepA.Size = new Size(112, 56);
        R_StepA.TabIndex = 16;
        R_StepA.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogVelocityC
        // 
        R_JogVelocityC.BackColor = Color.Transparent;
        R_JogVelocityC.CornerRadius = 10;
        R_JogVelocityC.Location = new Point(394, 221);
        R_JogVelocityC.Name = "R_JogVelocityC";
        R_JogVelocityC.Size = new Size(112, 56);
        R_JogVelocityC.TabIndex = 18;
        R_JogVelocityC.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogVelocityA
        // 
        R_JogVelocityA.BackColor = Color.Transparent;
        R_JogVelocityA.CornerRadius = 10;
        R_JogVelocityA.Location = new Point(394, 157);
        R_JogVelocityA.Name = "R_JogVelocityA";
        R_JogVelocityA.Size = new Size(112, 56);
        R_JogVelocityA.TabIndex = 15;
        R_JogVelocityA.TextAlign = HorizontalAlignment.Center;
        // 
        // R_JogPositionsPage
        // 
        R_JogPositionsPage.Controls.Add(R_JogPositionsTitle);
        R_JogPositionsPage.Controls.Add(R_RtcpJogPositionHeader);
        R_JogPositionsPage.Controls.Add(R_GoToCP);
        R_JogPositionsPage.Controls.Add(R_GoToAP);
        R_JogPositionsPage.Controls.Add(R_PositionCP);
        R_JogPositionsPage.Controls.Add(R_PositionAP);
        R_JogPositionsPage.Controls.Add(R_GoToZT);
        R_JogPositionsPage.Controls.Add(R_PositionZT);
        R_JogPositionsPage.Controls.Add(R_GotoJog);
        R_JogPositionsPage.Controls.Add(R_GoToC);
        R_JogPositionsPage.Controls.Add(R_GoToA);
        R_JogPositionsPage.Controls.Add(R_PositionC);
        R_JogPositionsPage.Controls.Add(R_PositionA);
        R_JogPositionsPage.Controls.Add(R_GoToZ);
        R_JogPositionsPage.Controls.Add(R_JogPositionHeader);
        R_JogPositionsPage.Controls.Add(R_GoToY);
        R_JogPositionsPage.Controls.Add(R_PositionX);
        R_JogPositionsPage.Controls.Add(R_PositionZ);
        R_JogPositionsPage.Controls.Add(R_GoToX);
        R_JogPositionsPage.Controls.Add(R_PositionY);
        R_JogPositionsPage.ForeColor = Color.FromArgb(225, 228, 232);
        R_JogPositionsPage.Name = "R_JogPositionsPage";
        // 
        // R_JogPositionsTitle
        // 
        R_JogPositionsTitle.AccessibleRole = AccessibleRole.StaticText;
        R_JogPositionsTitle.CornerRadius = 10;
        R_JogPositionsTitle.Location = new Point(9, 17);
        R_JogPositionsTitle.Margin = new Padding(0, 16, 0, 8);
        R_JogPositionsTitle.Name = "R_JogPositionsTitle";
        R_JogPositionsTitle.Padding = new Padding(0);
        R_JogPositionsTitle.Size = new Size(289, 34);
        R_JogPositionsTitle.TabIndex = 30;
        R_JogPositionsTitle.TabStop = false;
        R_JogPositionsTitle.Text = "Go to ...";
        // 
        // R_RtcpJogPositionHeader
        // 
        R_RtcpJogPositionHeader.AccessibleRole = AccessibleRole.StaticText;
        R_RtcpJogPositionHeader.CornerRadius = 10;
        R_RtcpJogPositionHeader.Location = new Point(458, 115);
        R_RtcpJogPositionHeader.Margin = new Padding(0, 16, 0, 8);
        R_RtcpJogPositionHeader.Name = "R_RtcpJogPositionHeader";
        R_RtcpJogPositionHeader.Padding = new Padding(0);
        R_RtcpJogPositionHeader.Size = new Size(169, 34);
        R_RtcpJogPositionHeader.TabIndex = 2;
        R_RtcpJogPositionHeader.TabStop = false;
        R_RtcpJogPositionHeader.Text = "Position";
        // 
        // R_GoToCP
        // 
        R_GoToCP.AutoEllipsis = true;
        R_GoToCP.CornerRadius = 10;
        R_GoToCP.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToCP.GradientStrength = 0.14F;
        R_GoToCP.Location = new Point(337, 349);
        R_GoToCP.Margin = new Padding(8, 0, 8, 8);
        R_GoToCP.Name = "R_GoToCP";
        R_GoToCP.Size = new Size(112, 56);
        R_GoToCP.TabIndex = 16;
        R_GoToCP.Text = "Goto CP";
        R_GoToCP.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_GoToAP
        // 
        R_GoToAP.AutoEllipsis = true;
        R_GoToAP.CornerRadius = 10;
        R_GoToAP.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToAP.GradientStrength = 0.14F;
        R_GoToAP.Location = new Point(337, 286);
        R_GoToAP.Margin = new Padding(8, 0, 8, 8);
        R_GoToAP.Name = "R_GoToAP";
        R_GoToAP.Size = new Size(112, 56);
        R_GoToAP.TabIndex = 14;
        R_GoToAP.Text = "Goto AP";
        R_GoToAP.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_PositionCP
        // 
        R_PositionCP.BackColor = Color.Transparent;
        R_PositionCP.CornerRadius = 10;
        R_PositionCP.Location = new Point(458, 348);
        R_PositionCP.Name = "R_PositionCP";
        R_PositionCP.Size = new Size(168, 56);
        R_PositionCP.TabIndex = 15;
        R_PositionCP.TextAlign = HorizontalAlignment.Center;
        // 
        // R_PositionAP
        // 
        R_PositionAP.BackColor = Color.Transparent;
        R_PositionAP.CornerRadius = 10;
        R_PositionAP.Location = new Point(457, 284);
        R_PositionAP.Name = "R_PositionAP";
        R_PositionAP.Size = new Size(168, 56);
        R_PositionAP.TabIndex = 13;
        R_PositionAP.TextAlign = HorizontalAlignment.Center;
        // 
        // R_GoToZT
        // 
        R_GoToZT.AutoEllipsis = true;
        R_GoToZT.CornerRadius = 10;
        R_GoToZT.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToZT.GradientStrength = 0.14F;
        R_GoToZT.Location = new Point(9, 349);
        R_GoToZT.Margin = new Padding(8, 0, 8, 8);
        R_GoToZT.Name = "R_GoToZT";
        R_GoToZT.Size = new Size(112, 56);
        R_GoToZT.TabIndex = 18;
        R_GoToZT.Text = "Goto ZT";
        R_GoToZT.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_PositionZT
        // 
        R_PositionZT.BackColor = Color.Transparent;
        R_PositionZT.CornerRadius = 10;
        R_PositionZT.Location = new Point(130, 348);
        R_PositionZT.Name = "R_PositionZT";
        R_PositionZT.Size = new Size(168, 56);
        R_PositionZT.TabIndex = 17;
        R_PositionZT.TextAlign = HorizontalAlignment.Center;
        // 
        // R_GotoJog
        // 
        R_GotoJog.AutoEllipsis = true;
        R_GotoJog.CornerRadius = 10;
        R_GotoJog.GradientStrength = 0.14F;
        R_GotoJog.IconSize = new Size(36, 36);
        R_GotoJog.Image = Properties.Resources.AR__35_;
        R_GotoJog.Location = new Point(585, 9);
        R_GotoJog.Name = "R_GotoJog";
        R_GotoJog.Padding = new Padding(0);
        R_GotoJog.Size = new Size(40, 40);
        R_GotoJog.SurfaceStyle = NT.Tools.SSurfaceStyle.Text;
        R_GotoJog.TabIndex = 0;
        R_GotoJog.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_GoToC
        // 
        R_GoToC.AutoEllipsis = true;
        R_GoToC.CornerRadius = 10;
        R_GoToC.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToC.GradientStrength = 0.14F;
        R_GoToC.Location = new Point(337, 222);
        R_GoToC.Margin = new Padding(8, 0, 8, 8);
        R_GoToC.Name = "R_GoToC";
        R_GoToC.Size = new Size(112, 56);
        R_GoToC.TabIndex = 12;
        R_GoToC.Text = "Goto C";
        R_GoToC.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_GoToA
        // 
        R_GoToA.AutoEllipsis = true;
        R_GoToA.CornerRadius = 10;
        R_GoToA.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToA.GradientStrength = 0.14F;
        R_GoToA.Location = new Point(337, 159);
        R_GoToA.Margin = new Padding(8, 0, 8, 8);
        R_GoToA.Name = "R_GoToA";
        R_GoToA.Size = new Size(112, 56);
        R_GoToA.TabIndex = 10;
        R_GoToA.Text = "Goto A";
        R_GoToA.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_PositionC
        // 
        R_PositionC.BackColor = Color.Transparent;
        R_PositionC.CornerRadius = 10;
        R_PositionC.Location = new Point(458, 221);
        R_PositionC.Name = "R_PositionC";
        R_PositionC.Size = new Size(168, 56);
        R_PositionC.TabIndex = 11;
        R_PositionC.TextAlign = HorizontalAlignment.Center;
        // 
        // R_PositionA
        // 
        R_PositionA.BackColor = Color.Transparent;
        R_PositionA.CornerRadius = 10;
        R_PositionA.Location = new Point(457, 157);
        R_PositionA.Name = "R_PositionA";
        R_PositionA.Size = new Size(168, 56);
        R_PositionA.TabIndex = 9;
        R_PositionA.TextAlign = HorizontalAlignment.Center;
        // 
        // R_GoToZ
        // 
        R_GoToZ.AutoEllipsis = true;
        R_GoToZ.CornerRadius = 10;
        R_GoToZ.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToZ.GradientStrength = 0.14F;
        R_GoToZ.Location = new Point(10, 285);
        R_GoToZ.Margin = new Padding(8, 0, 8, 8);
        R_GoToZ.Name = "R_GoToZ";
        R_GoToZ.Size = new Size(112, 56);
        R_GoToZ.TabIndex = 8;
        R_GoToZ.Text = "Goto Z";
        R_GoToZ.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_JogPositionHeader
        // 
        R_JogPositionHeader.AccessibleRole = AccessibleRole.StaticText;
        R_JogPositionHeader.CornerRadius = 10;
        R_JogPositionHeader.Location = new Point(130, 114);
        R_JogPositionHeader.Margin = new Padding(0, 16, 0, 8);
        R_JogPositionHeader.Name = "R_JogPositionHeader";
        R_JogPositionHeader.Padding = new Padding(0);
        R_JogPositionHeader.Size = new Size(169, 34);
        R_JogPositionHeader.TabIndex = 1;
        R_JogPositionHeader.TabStop = false;
        R_JogPositionHeader.Text = "Position (mm)";
        // 
        // R_GoToY
        // 
        R_GoToY.AutoEllipsis = true;
        R_GoToY.CornerRadius = 10;
        R_GoToY.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToY.GradientStrength = 0.14F;
        R_GoToY.Location = new Point(10, 222);
        R_GoToY.Margin = new Padding(8, 0, 8, 8);
        R_GoToY.Name = "R_GoToY";
        R_GoToY.Size = new Size(112, 56);
        R_GoToY.TabIndex = 6;
        R_GoToY.Text = "Goto Y";
        R_GoToY.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_PositionX
        // 
        R_PositionX.BackColor = Color.Transparent;
        R_PositionX.CornerRadius = 10;
        R_PositionX.Location = new Point(131, 156);
        R_PositionX.Name = "R_PositionX";
        R_PositionX.Size = new Size(168, 56);
        R_PositionX.TabIndex = 3;
        R_PositionX.TextAlign = HorizontalAlignment.Center;
        // 
        // R_PositionZ
        // 
        R_PositionZ.BackColor = Color.Transparent;
        R_PositionZ.CornerRadius = 10;
        R_PositionZ.Location = new Point(131, 284);
        R_PositionZ.Name = "R_PositionZ";
        R_PositionZ.Size = new Size(168, 56);
        R_PositionZ.TabIndex = 7;
        R_PositionZ.TextAlign = HorizontalAlignment.Center;
        // 
        // R_GoToX
        // 
        R_GoToX.AutoEllipsis = true;
        R_GoToX.CornerRadius = 10;
        R_GoToX.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_GoToX.GradientStrength = 0.14F;
        R_GoToX.Location = new Point(9, 158);
        R_GoToX.Margin = new Padding(8, 0, 8, 8);
        R_GoToX.Name = "R_GoToX";
        R_GoToX.Size = new Size(112, 56);
        R_GoToX.TabIndex = 4;
        R_GoToX.Text = "Goto X";
        R_GoToX.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_PositionY
        // 
        R_PositionY.BackColor = Color.Transparent;
        R_PositionY.CornerRadius = 10;
        R_PositionY.Location = new Point(130, 220);
        R_PositionY.Name = "R_PositionY";
        R_PositionY.Size = new Size(168, 56);
        R_PositionY.TabIndex = 5;
        R_PositionY.TextAlign = HorizontalAlignment.Center;
        // 
        // R_AAxis
        // 
        R_AAxis.AccessibleRole = AccessibleRole.Indicator;
        R_AAxis.CornerRadius = 10;
        R_AAxis.IndicatorSize = new Size(30, 30);
        R_AAxis.Location = new Point(729, 9);
        R_AAxis.Name = "R_AAxis";
        R_AAxis.Padding = new Padding(16, 4, 12, 4);
        R_AAxis.Size = new Size(90, 56);
        R_AAxis.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_AAxis.TabIndex = 5;
        R_AAxis.TabStop = false;
        R_AAxis.Text = "A";
        // 
        // R_CAxis
        // 
        R_CAxis.AccessibleRole = AccessibleRole.Indicator;
        R_CAxis.CornerRadius = 10;
        R_CAxis.IndicatorSize = new Size(30, 30);
        R_CAxis.Location = new Point(827, 9);
        R_CAxis.Name = "R_CAxis";
        R_CAxis.Padding = new Padding(16, 4, 12, 4);
        R_CAxis.Size = new Size(90, 56);
        R_CAxis.SurfaceStyle = NT.Tools.SSurfaceStyle.Tonal;
        R_CAxis.TabIndex = 6;
        R_CAxis.TabStop = false;
        R_CAxis.Text = "C";
        // 
        // R_RtcpEnabled
        // 
        R_RtcpEnabled.AutoEllipsis = true;
        R_RtcpEnabled.CheckedFillColor = Color.FromArgb(90, 255, 0, 0);
        R_RtcpEnabled.CornerRadius = 10;
        R_RtcpEnabled.FillMode = NT.Tools.SButtonFillMode.LinearGradient;
        R_RtcpEnabled.GradientStrength = 0.14F;
        R_RtcpEnabled.IndicatorPlacement = NT.Tools.SIndicatorPlacement.Bottom;
        R_RtcpEnabled.IndicatorStyle = NT.Tools.SIndicatorStyle.None;
        R_RtcpEnabled.Location = new Point(9, 58);
        R_RtcpEnabled.Margin = new Padding(8);
        R_RtcpEnabled.Name = "R_RtcpEnabled";
        R_RtcpEnabled.Padding = new Padding(0, 4, 0, 4);
        R_RtcpEnabled.Size = new Size(168, 56);
        R_RtcpEnabled.TabIndex = 2;
        R_RtcpEnabled.Text = "RTCP Enabled";
        R_RtcpEnabled.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // R_HeadPagePanel
        // 
        R_HeadPagePanel.Location = new Point(962, 753);
        R_HeadPagePanel.Name = "R_HeadPagePanel";
        R_HeadPagePanel.Padding = new Padding(0);
        R_HeadPagePanel.Pages.AddRange(new NT.Tools.sPage[] { R_HeadRtcpPage, R_HeadSpindlePage });
        R_HeadPagePanel.Size = new Size(307, 254);
        R_HeadPagePanel.TabIndex = 20;
        // 
        // R_HeadRtcpPage
        // 
        R_HeadRtcpPage.Controls.Add(R_RtcpPageTitle);
        R_HeadRtcpPage.Controls.Add(R_GotoSpindlePage);
        R_HeadRtcpPage.Controls.Add(R_ToolLength);
        R_HeadRtcpPage.Controls.Add(R_RtcpEnabled);
        R_HeadRtcpPage.Controls.Add(R_ToolLengthLabel);
        R_HeadRtcpPage.ForeColor = Color.FromArgb(225, 228, 232);
        R_HeadRtcpPage.Name = "R_HeadRtcpPage";
        // 
        // R_RtcpPageTitle
        // 
        R_RtcpPageTitle.AccessibleRole = AccessibleRole.StaticText;
        R_RtcpPageTitle.CornerRadius = 10;
        R_RtcpPageTitle.Location = new Point(14, 17);
        R_RtcpPageTitle.Margin = new Padding(8, 16, 0, 8);
        R_RtcpPageTitle.Name = "R_RtcpPageTitle";
        R_RtcpPageTitle.Padding = new Padding(0);
        R_RtcpPageTitle.Size = new Size(221, 34);
        R_RtcpPageTitle.TabIndex = 0;
        R_RtcpPageTitle.TabStop = false;
        R_RtcpPageTitle.Text = "RTCP Part";
        // 
        // R_GotoSpindlePage
        // 
        R_GotoSpindlePage.AutoEllipsis = true;
        R_GotoSpindlePage.CornerRadius = 10;
        R_GotoSpindlePage.GradientStrength = 0.14F;
        R_GotoSpindlePage.IconSize = new Size(36, 36);
        R_GotoSpindlePage.Image = Properties.Resources.AR__35_;
        R_GotoSpindlePage.Location = new Point(258, 9);
        R_GotoSpindlePage.Margin = new Padding(8);
        R_GotoSpindlePage.Name = "R_GotoSpindlePage";
        R_GotoSpindlePage.Padding = new Padding(0);
        R_GotoSpindlePage.Size = new Size(40, 40);
        R_GotoSpindlePage.SurfaceStyle = NT.Tools.SSurfaceStyle.Text;
        R_GotoSpindlePage.TabIndex = 1;
        R_GotoSpindlePage.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_HeadSpindlePage
        // 
        R_HeadSpindlePage.Controls.Add(R_GotoRtcpPage);
        R_HeadSpindlePage.Controls.Add(R_SpindleSpeed);
        R_HeadSpindlePage.Controls.Add(R_SpindlePageTitle);
        R_HeadSpindlePage.Controls.Add(R_Spindle);
        R_HeadSpindlePage.ForeColor = Color.FromArgb(225, 228, 232);
        R_HeadSpindlePage.Name = "R_HeadSpindlePage";
        // 
        // R_GotoRtcpPage
        // 
        R_GotoRtcpPage.AutoEllipsis = true;
        R_GotoRtcpPage.CornerRadius = 10;
        R_GotoRtcpPage.GradientStrength = 0.14F;
        R_GotoRtcpPage.IconSize = new Size(36, 36);
        R_GotoRtcpPage.Image = Properties.Resources.AR__35_;
        R_GotoRtcpPage.Location = new Point(258, 9);
        R_GotoRtcpPage.Margin = new Padding(8);
        R_GotoRtcpPage.Name = "R_GotoRtcpPage";
        R_GotoRtcpPage.Padding = new Padding(0);
        R_GotoRtcpPage.Size = new Size(40, 40);
        R_GotoRtcpPage.SurfaceStyle = NT.Tools.SSurfaceStyle.Text;
        R_GotoRtcpPage.TabIndex = 1;
        R_GotoRtcpPage.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_NavigatorPanel
        // 
        R_NavigatorPanel.Location = new Point(9, 753);
        R_NavigatorPanel.Name = "R_NavigatorPanel";
        R_NavigatorPanel.Padding = new Padding(0);
        R_NavigatorPanel.Pages.AddRange(new NT.Tools.sPage[] { R_NavigatorProgramPage, R_NavigatorGCodePage });
        R_NavigatorPanel.Size = new Size(945, 254);
        R_NavigatorPanel.TabIndex = 19;
        // 
        // R_NavigatorProgramPage
        // 
        R_NavigatorProgramPage.Controls.Add(R_GotoNavigatorGCodePage);
        R_NavigatorProgramPage.Controls.Add(R_FeedOverride);
        R_NavigatorProgramPage.Controls.Add(R_ProgramPath);
        R_NavigatorProgramPage.Controls.Add(R_ResetProgram);
        R_NavigatorProgramPage.Controls.Add(R_GCodeView);
        R_NavigatorProgramPage.Controls.Add(R_PauseProgram);
        R_NavigatorProgramPage.Controls.Add(R_RunProgram);
        R_NavigatorProgramPage.ForeColor = Color.FromArgb(225, 228, 232);
        R_NavigatorProgramPage.Name = "R_NavigatorProgramPage";
        // 
        // R_GotoNavigatorGCodePage
        // 
        R_GotoNavigatorGCodePage.AutoEllipsis = true;
        R_GotoNavigatorGCodePage.CornerRadius = 10;
        R_GotoNavigatorGCodePage.GradientStrength = 0.14F;
        R_GotoNavigatorGCodePage.IconSize = new Size(36, 36);
        R_GotoNavigatorGCodePage.Image = Properties.Resources.AR__35_;
        R_GotoNavigatorGCodePage.Location = new Point(896, 10);
        R_GotoNavigatorGCodePage.Margin = new Padding(8);
        R_GotoNavigatorGCodePage.Name = "R_GotoNavigatorGCodePage";
        R_GotoNavigatorGCodePage.Padding = new Padding(0);
        R_GotoNavigatorGCodePage.Size = new Size(40, 40);
        R_GotoNavigatorGCodePage.SurfaceStyle = NT.Tools.SSurfaceStyle.Text;
        R_GotoNavigatorGCodePage.TabIndex = 1;
        R_GotoNavigatorGCodePage.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_NavigatorGCodePage
        // 
        R_NavigatorGCodePage.Controls.Add(R_GotoNavigatorProgramPage);
        R_NavigatorGCodePage.Controls.Add(R_GCodeExpandedView);
        R_NavigatorGCodePage.Controls.Add(R_ExpandedProgramPath);
        R_NavigatorGCodePage.ForeColor = Color.FromArgb(225, 228, 232);
        R_NavigatorGCodePage.Name = "R_NavigatorGCodePage";
        // 
        // R_GotoNavigatorProgramPage
        // 
        R_GotoNavigatorProgramPage.AutoEllipsis = true;
        R_GotoNavigatorProgramPage.CornerRadius = 10;
        R_GotoNavigatorProgramPage.GradientStrength = 0.14F;
        R_GotoNavigatorProgramPage.IconSize = new Size(36, 36);
        R_GotoNavigatorProgramPage.Image = Properties.Resources.AR__35_;
        R_GotoNavigatorProgramPage.Location = new Point(896, 10);
        R_GotoNavigatorProgramPage.Margin = new Padding(8);
        R_GotoNavigatorProgramPage.Name = "R_GotoNavigatorProgramPage";
        R_GotoNavigatorProgramPage.Padding = new Padding(0);
        R_GotoNavigatorProgramPage.Size = new Size(40, 40);
        R_GotoNavigatorProgramPage.SurfaceStyle = NT.Tools.SSurfaceStyle.Text;
        R_GotoNavigatorProgramPage.TabIndex = 1;
        R_GotoNavigatorProgramPage.Tone = NT.Tools.SButtonTone.Neutral;
        // 
        // R_GCodeExpandedView
        // 
        R_GCodeExpandedView.AccessibleRole = AccessibleRole.List;
        R_GCodeExpandedView.CornerRadius = 10;
        R_GCodeExpandedView.FirstVisibleLine = 0;
        R_GCodeExpandedView.Location = new Point(9, 60);
        R_GCodeExpandedView.Margin = new Padding(8);
        R_GCodeExpandedView.Name = "R_GCodeExpandedView";
        R_GCodeExpandedView.Size = new Size(927, 185);
        R_GCodeExpandedView.TabIndex = 2;
        // 
        // R_ExpandedProgramPath
        // 
        R_ExpandedProgramPath.AccessibleRole = AccessibleRole.StaticText;
        R_ExpandedProgramPath.CornerRadius = 10;
        R_ExpandedProgramPath.Location = new Point(14, 17);
        R_ExpandedProgramPath.Margin = new Padding(8, 16, 0, 8);
        R_ExpandedProgramPath.Name = "R_ExpandedProgramPath";
        R_ExpandedProgramPath.Padding = new Padding(0);
        R_ExpandedProgramPath.Size = new Size(874, 34);
        R_ExpandedProgramPath.TabIndex = 0;
        R_ExpandedProgramPath.TabStop = false;
        R_ExpandedProgramPath.Text = "No program loaded";
        // 
        // MainForm
        // 
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(1920, 1080);
        Controls.Add(R_NavigatorPanel);
        Controls.Add(R_HeadPagePanel);
        Controls.Add(R_CAxis);
        Controls.Add(R_AAxis);
        Controls.Add(R_JogPagePanel);
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
        Controls.Add(R_Configuration);
        Controls.Add(R_ProgramProgress);
        R_ThemeManager.SetDpiMode(this, NT.Tools.SThemeDpiMode.FixedPixels);
        Font = new Font("Segoe UI", 16.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
        ForeColor = Color.FromArgb(224, 229, 235);
        FormBorderStyle = FormBorderStyle.None;
        Margin = new Padding(4);
        MinimumSize = new Size(1280, 720);
        Name = "R_MainForm";
        R_ThemeManager.SetScaleMode(this, NT.Tools.SFormScaleMode.BoundsAndFonts);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RouterXYZAC+RTCP";
        Load += MainForm_Load;
        (R_ThemeManager).EndInit();
        R_MachinePositionPanel.ResumeLayout(false);
        R_WorkPositionPanel.ResumeLayout(false);
        R_ReferencePanel.ResumeLayout(false);
        R_JogPagePanel.ResumeLayout(false);
        R_JogControlsPage.ResumeLayout(false);
        R_JogPropertiesPage.ResumeLayout(false);
        R_JogPositionsPage.ResumeLayout(false);
        R_HeadPagePanel.ResumeLayout(false);
        R_HeadRtcpPage.ResumeLayout(false);
        R_HeadSpindlePage.ResumeLayout(false);
        R_NavigatorPanel.ResumeLayout(false);
        R_NavigatorProgramPage.ResumeLayout(false);
        R_NavigatorGCodePage.ResumeLayout(false);
        ResumeLayout(false);
    }

    private NT.Tools.sTheme R_ThemeManager = null!;
    private NT.Tools.sPanel R_MachinePositionPanel = null!;
    private NT.Tools.sPanel R_WorkPositionPanel = null!;
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
    private NT.Tools.sComboBox R_ReferenceSelector = null!;
    private NT.Tools.sLabel R_JogStepHeader;
    private NT.Tools.sPanel R_ReferencePanel;
    private NT.Tools.sLabel R_ReferencePanelTitle;
    private NT.Tools.sLabel R_ReferenceAxisXLabel;
    private NT.Tools.sLabel R_ReferenceAxisYLabel;
    private NT.Tools.sLabel R_ReferenceAxisZLabel;
    private NT.Tools.sLabel R_SpindlePageTitle;
    private NT.Tools.sButton R_EditGCode;
    private NT.Tools.sButton R_SelectStartLine;
    private NT.Tools.sSignal R_Homed;
    private NT.Tools.sPagePanel R_JogPagePanel;
    private NT.Tools.sPage R_JogControlsPage;
    private NT.Tools.sPage R_JogPropertiesPage;
    private NT.Tools.sButton R_JogNC;
    private NT.Tools.sButton R_JogNA;
    private NT.Tools.sButton R_JogPA;
    private NT.Tools.sButton R_JogPC;
    private NT.Tools.sButton R_GotoJogProperties;
    private NT.Tools.sLabel R_JogAxisCLabel;
    private NT.Tools.sTextBox R_StepC;
    private NT.Tools.sLabel R_JogAxisALabel;
    private NT.Tools.sTextBox R_StepA;
    private NT.Tools.sTextBox R_JogVelocityC;
    private NT.Tools.sTextBox R_JogVelocityA;
    private NT.Tools.sButton R_GotoJogPositions;
    private NT.Tools.sSignal R_CAxis;
    private NT.Tools.sSignal R_AAxis;
    private NT.Tools.sLabel R_CommandPositionC;
    private NT.Tools.sLabel R_CommandPositionA;
    private NT.Tools.sLabel R_WorkPositionC;
    private NT.Tools.sLabel R_WorkPositionA;
    private NT.Tools.sButton R_JogNZT;
    private NT.Tools.sButton R_JogPZT;
    private NT.Tools.sButton R_JogNCP;
    private NT.Tools.sButton R_JogNAP;
    private NT.Tools.sButton R_JogPAP;
    private NT.Tools.sButton R_JogPCP;
    private NT.Tools.sPage R_JogPositionsPage;
    private NT.Tools.sButton R_GoToC;
    private NT.Tools.sButton R_GoToA;
    private NT.Tools.sTextBox R_PositionC;
    private NT.Tools.sTextBox R_PositionA;
    private NT.Tools.sButton R_GoToZ;
    private NT.Tools.sLabel R_JogPositionHeader;
    private NT.Tools.sButton R_GoToY;
    private NT.Tools.sTextBox R_PositionX;
    private NT.Tools.sTextBox R_PositionZ;
    private NT.Tools.sButton R_GoToX;
    private NT.Tools.sTextBox R_PositionY;
    private NT.Tools.sLabel R_JogAxisZTLabel;
    private NT.Tools.sTextBox R_StepZT;
    private NT.Tools.sLabel R_JogAxisCPLabel;
    private NT.Tools.sTextBox R_StepCP;
    private NT.Tools.sTextBox R_JogVelocityZT;
    private NT.Tools.sTextBox R_JogVelocityCP;
    private NT.Tools.sLabel R_JogAxisAPLabel;
    private NT.Tools.sTextBox R_StepAP;
    private NT.Tools.sTextBox R_JogVelocityAP;
    private NT.Tools.sButton R_GotoJog;
    private NT.Tools.sLabel R_RtcpJogStepHeader;
    private NT.Tools.sLabel R_RtcpJogVelocityHeader;
    private NT.Tools.sButton R_GoToZT;
    private NT.Tools.sTextBox R_PositionZT;
    private NT.Tools.sLabel R_RtcpJogPositionHeader;
    private NT.Tools.sButton R_GoToCP;
    private NT.Tools.sButton R_GoToAP;
    private NT.Tools.sTextBox R_PositionCP;
    private NT.Tools.sTextBox R_PositionAP;
    private NT.Tools.sToggle R_RtcpEnabled;
    private NT.Tools.sTextBox R_ToolLength;
    private NT.Tools.sLabel R_ToolLengthLabel;
    private NT.Tools.sPagePanel R_HeadPagePanel;
    private NT.Tools.sPage R_HeadRtcpPage;
    private NT.Tools.sPage R_HeadSpindlePage;
    private NT.Tools.sLabel R_RtcpPageTitle;
    private NT.Tools.sButton R_GotoSpindlePage;
    private NT.Tools.sButton R_GotoRtcpPage;
    private NT.Tools.sPagePanel R_NavigatorPanel;
    private NT.Tools.sPage R_NavigatorProgramPage;
    private NT.Tools.sButton R_GotoNavigatorGCodePage;
    private NT.Tools.sLabel R_ExpandedProgramPath;
    private NT.Tools.sPage R_NavigatorGCodePage;
    private NT.Tools.sGCodeView R_GCodeExpandedView;
    private NT.Tools.sButton R_GotoNavigatorProgramPage;
    private NT.Tools.sLabel R_JogControlsTitle;
    private NT.Tools.sLabel R_JogPropertiesTitle;
    private NT.Tools.sLabel R_JogPositionsTitle;
}

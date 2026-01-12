using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SyncthingRun
{
    public partial class MainForm : Form
    {
        private NotifyIcon trayIcon;
        private Config config;
        private bool isSyncthingRunning;
        private string currentSyncthingVersion = "";

        public MainForm()
        {
            InitializeComponent();
            
            // 先初始化配置，再进行其他操作
            config = Config.Load();
            CheckSyncthingPath();
            CheckSyncthingStatus();
            InitializeTrayIcon();
            AutoStartSyncthingIfEnabled();
            UpdateMenu();
            UpdateTrayIcon(); // 确保初始状态下的托盘文字正确
            CheckSyncthingVersion(); // 检测版本
            UpdateMenu(); // 确保版本信息显示在菜单中
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(0, 0);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "MainForm";
            this.ShowInTaskbar = false;
            this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            this.ResumeLayout(false);
        }

        private void InitializeTrayIcon()
        {
            try
            {
                // 使用对象初始化器创建NotifyIcon，标准WinForms组件
                trayIcon = new NotifyIcon
                {
                    Icon = GetEmbeddedIcon(!isSyncthingRunning),  // 根据状态使用内嵌的图标
                    Text = "syncthing-run",                        // 设置鼠标悬停提示文字
                    Visible = true                                // 立即显示在系统托盘
                };

                // 设置菜单
                UpdateMenu();

                // 双击事件
                trayIcon.DoubleClick += TrayIcon_DoubleClick;

                // 添加退出事件处理
                this.FormClosing += MainForm_FormClosing;
            }
            catch (Exception ex)
            {
                Log.Error("初始化托盘图标失败: " + ex.Message);
                MessageBox.Show("初始化托盘图标失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTrayIcon()
        {
            try
            {
                // 根据Syncthing状态使用不同的图标
                trayIcon.Icon = GetEmbeddedIcon(!isSyncthingRunning);
                
                // 更新托盘图标文字
                string tooltipText = "syncthing-run";
                if (!isSyncthingRunning)
                {
                    if (!File.Exists(config.SyncthingExePath))
                    {
                        tooltipText += " (请设置路径)";
                    }
                    else
                    {
                        tooltipText += " (未启动)";
                    }
                }
                else
                {
                    tooltipText += " (运行中)";
                }
                
                trayIcon.Text = tooltipText;
                trayIcon.Visible = true;
            }
            catch (Exception ex)
            {
                Log.Error("更新托盘图标失败: " + ex.Message);
            }
        }

        private System.Drawing.Icon GetEmbeddedIcon(bool grayscale = false)
        {
            try
            {
                System.Drawing.Icon originalIcon;
                
                // 尝试从内嵌资源获取图标
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var stream = assembly.GetManifestResourceStream("SyncthingRun.app.ico");
                if (stream != null)
                {
                    originalIcon = new System.Drawing.Icon(stream);
                }
                else
                {
                    // 备用方案：使用应用程序图标
                    try
                    {
                        originalIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    }
                    catch
                    {
                        // 如果提取失败，返回默认系统图标
                        originalIcon = System.Drawing.SystemIcons.Application;
                    }
                }
                
                // 如果不需要灰度，直接返回原图标
                if (!grayscale)
                {
                    return originalIcon;
                }

                // 创建灰度版本的图标
                return CreateGrayscaleIcon(originalIcon);
            }
            catch
            {
                return System.Drawing.SystemIcons.Application;
            }
        }

        private System.Drawing.Icon CreateGrayscaleIcon(System.Drawing.Icon originalIcon)
        {
            try
            {
                // 将图标转换为位图进行处理
                using (Bitmap bitmap = originalIcon.ToBitmap())
                {
                    // 创建新的位图
                    using (Bitmap grayBitmap = new Bitmap(bitmap.Width, bitmap.Height))
                    {
                        // 使用Graphics和ColorMatrix进行更高效的灰度转换
                        using (Graphics g = Graphics.FromImage(grayBitmap))
                        {
                            // 创建灰度颜色矩阵
                            System.Drawing.Imaging.ColorMatrix colorMatrix = new System.Drawing.Imaging.ColorMatrix(
                                new float[][] 
                                {
                                    new float[] {0.299f, 0.299f, 0.299f, 0, 0},      // 红色通道
                                    new float[] {0.587f, 0.587f, 0.587f, 0, 0},      // 绿色通道
                                    new float[] {0.114f, 0.114f, 0.114f, 0, 0},       // 蓝色通道
                                    new float[] {0, 0, 0, 1, 0},
                                    new float[] {0, 0, 0, 0, 1}
                                });

                            // 创建ImageAttributes并应用颜色矩阵
                            using (ImageAttributes attributes = new ImageAttributes())
                            {
                                attributes.SetColorMatrix(colorMatrix);
                                
                                // 绘制灰度图像
                                g.DrawImage(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
                            }
                        }

                        // 从处理后的位图创建新图标
                        return System.Drawing.Icon.FromHandle(grayBitmap.GetHicon());
                    }
                }
            }
            catch
            {
                // 如果创建灰度图标失败，返回原图标
                return originalIcon;
            }
        }

        private void UpdateMenu()
        {
            var menu = new ContextMenuStrip();

            // 1. WEBGUI设置
            menu.Items.Add("打开WebGui设置", null, OpenSettings_Click);
            
            // 2. 设置路径
            menu.Items.Add("设置Syncthing.exe路径", null, SetSyncthingPath_Click);
            
            // 2.5. 查看当前托盘配置
            menu.Items.Add("查看当前托盘配置", null, ViewCurrentSettings_Click);
            menu.Items.Add(new ToolStripSeparator());
            
            // 3. 启动
            menu.Items.Add("▶ 启动Syncthing", null, StartSyncthing_Click)
                .Enabled = !isSyncthingRunning;
            
            // 4. 停止
            menu.Items.Add("■ 停止Syncthing", null, StopSyncthing_Click)
                .Enabled = isSyncthingRunning;
            menu.Items.Add(new ToolStripSeparator());
            
            // 5. 启动托盘时启动syncthing
            var autoStartItem = new ToolStripMenuItem("启动托盘程序时自动启动Syncthing", null, ToggleAutoStartSyncthing_Click);
            autoStartItem.Checked = config.AutoStartSyncthing;
            autoStartItem.CheckOnClick = true;
            menu.Items.Add(autoStartItem);
            
            // 6. 开机启动
            var startupItem = new ToolStripMenuItem("开机启动托盘程序", null, ToggleStartup_Click);
            startupItem.Checked = IsStartupEnabled();
            startupItem.CheckOnClick = true;
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            
            // 7. 版本号
            string versionDisplay = $"Syncthing {currentSyncthingVersion}";
            menu.Items.Add(versionDisplay, null).Enabled = false;
            
            // 8. 更新
            menu.Items.Add("更新Syncthing", null, SyncthingUpgrade_Click);
            menu.Items.Add(new ToolStripSeparator());
            
            // 9. 退出并停止
            menu.Items.Add("退出并停止Syncthing", null, ExitAndCloseSyncthing_Click);
            
            // 10. 仅退出
            menu.Items.Add("仅退出", null, ExitWithoutClosingSyncthing_Click);
            menu.Items.Add(new ToolStripSeparator());
            
            // 11. 关于
            menu.Items.Add("关于", null, About_Click);

            trayIcon.ContextMenuStrip = menu;
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            OpenSettings_Click(sender, e);
        }

        private void OpenSettings_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("http://127.0.0.1:8384");
            }
            catch (Exception ex)
            {
                Log.Error("设置Syncthing失败: " + ex.Message);
            }
        }

        private void StartSyncthing_Click(object sender, EventArgs e)
        {
                            if (!StartSyncthingProcess())
                            {
                                Log.ShowError("启动Syncthing失败");
                            }
        }

        // 统一的启动Syncthing方法
        private bool StartSyncthingProcess()
        {
            try
            {
                if (string.IsNullOrEmpty(config?.SyncthingExePath) || !File.Exists(config.SyncthingExePath))
                {
                    return false;
                }

                if (isSyncthingRunning)
                {
                    return true; // 已经运行
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = config.SyncthingExePath,
                    Arguments = "--no-browser --gui-address=127.0.0.1:8384",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                
                try
                {
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    // 只记录真正的启动失败
                    Log.Info("启动命令执行失败: " + ex.Message);
                    return false;
                }
                
                // 简化检测：延迟后直接检查进程是否存在
                System.Threading.Thread.Sleep(1000);
                CheckSyncthingStatus(); // 使用与菜单相同的检测方式
                
                if (isSyncthingRunning)
                {
                    // 只在确认启动成功后才更新UI
                    try
                    {
                        UpdateMenu();
                        UpdateTrayIcon();
                        Log.Info("Syncthing已启动 (仅限本地访问)");
                    }
                    catch
                    {
                        // UI更新失败，但启动成功，不影响结果
                    }
                    return true;
                }
                else
                {
                    Log.Info("启动命令已执行，但未检测到进程");
                    return false;
                }
            }
            catch (Exception ex)
            {
                // 记录但不弹窗，由调用方处理
                Log.Info("启动过程异常: " + ex.Message);
                return false;
            }
        }

        // 统一的停止Syncthing方法
        private bool StopSyncthingProcess()
        {
            try
            {
                var processes = Process.GetProcessesByName("syncthing");
                if (processes.Length == 0)
                {
                    return true; // 已经停止
                }

                foreach (var process in processes)
                {
                    process.Kill();
                }
                
                // 延迟检查状态
                System.Threading.Thread.Sleep(1000);
                CheckSyncthingStatus();
                UpdateMenu();
                UpdateTrayIcon();
                
                Log.Info("Syncthing已停止");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("停止Syncthing失败: " + ex.Message);
                return false;
            }
        }

        private void StopSyncthing_Click(object sender, EventArgs e)
        {
            if (!StopSyncthingProcess())
            {
                Log.ShowError("停止Syncthing失败");
            }
        }

        private void ToggleAutoStartSyncthing_Click(object sender, EventArgs e)
        {
            config.AutoStartSyncthing = !config.AutoStartSyncthing;
            config.Save();
            UpdateMenu();
        }

        private void ToggleStartup_Click(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    bool isCurrentlyEnabled = IsStartupEnabled();
                    if (isCurrentlyEnabled)
                    {
                        key.DeleteValue("SyncthingRun", false);
                    }
                    else
                    {
                        key.SetValue("SyncthingRun", Application.ExecutablePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("切换开机启动失败: " + ex.Message);
                MessageBox.Show("切换开机启动失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("SyncthingRun");
                        if (value != null && value.ToString() == Application.ExecutablePath)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // 注册表访问失败，返回false
            }
            return false;
        }

        private void SyncthingUpgrade_Click(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(config.SyncthingExePath))
                {
                    MessageBox.Show("请先设置正确的Syncthing.exe路径。", "找不到文件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 直接打开cmd窗口运行upgrade命令
                var cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                var command = $"\"{config.SyncthingExePath}\" upgrade";

                var startInfo = new ProcessStartInfo
                {
                    FileName = cmdPath,
                    Arguments = $"/c {command} & pause",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Process.Start(startInfo);
                
                Log.Info("已启动Syncthing更新进程");
            }
            catch (Exception ex)
            {
                Log.Error("启动Syncthing更新失败: " + ex.Message);
                MessageBox.Show("启动Syncthing更新失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetSyncthingPath_Click(object sender, EventArgs e)
        {
            try
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Title = "选择Syncthing.exe文件";
                    openFileDialog.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                    string initialDir = string.IsNullOrEmpty(config.SyncthingExePath) 
                        ? AppDomain.CurrentDomain.BaseDirectory 
                        : Path.GetDirectoryName(config.SyncthingExePath);
                    openFileDialog.InitialDirectory = initialDir;
                    
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string newPath = openFileDialog.FileName;
                        config.SyncthingExePath = newPath;
                        config.Save();
                        CheckSyncthingVersion(); // 重新检测版本
                        UpdateMenu();
                        UpdateTrayIcon(); // 更新托盘图标文字
                        Log.Info("Syncthing.exe路径已更新为: " + newPath);
                        
                        // 询问是否启动Syncthing
                        if (!isSyncthingRunning && File.Exists(config.SyncthingExePath))
                        {
                            var startResult = MessageBox.Show(
                                "Syncthing.exe路径设置成功！\n\n是否立即启动Syncthing？",
                                "启动Syncthing",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);
                                
                        if (startResult == DialogResult.Yes)
                        {
                            if (!StartSyncthingProcess())
                            {
                                MessageBox.Show("启动Syncthing失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        }
                    }
                    else
                    {
                        // 检查当前路径是否有效
                        if (!File.Exists(config.SyncthingExePath))
                        {
                            MessageBox.Show("当前Syncthing.exe路径无效，请设置有效的路径。", "路径无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("设置Syncthing路径失败: " + ex.Message);
            }
        }

        private void ExitWithoutClosingSyncthing_Click(object sender, EventArgs e)
        {
            try
            {
                Log.Info("退出应用，保留Syncthing运行...");
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
                Application.Exit();
            }
            catch (Exception ex)
            {
                Log.Error("退出失败: " + ex.Message);
            }
        }

        private void ExitAndCloseSyncthing_Click(object sender, EventArgs e)
        {
            try
            {
                Log.Info("退出应用并关闭Syncthing...");
                // 关闭Syncthing进程
                if (!StopSyncthingProcess())
                {
                    Log.Error("关闭Syncthing失败");
                }
                else
                {
                    Log.Info("已关闭Syncthing进程");
                }
                
                // 退出应用
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
                Application.Exit();
            }
            catch (Exception ex)
            {
                Log.Error("退出失败: " + ex.Message);
            }
        }

        private void About_Click(object sender, EventArgs e)
        {
            MessageBox.Show("本工具仅为syncthing的辅助程序，用于便捷启动使用。", "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ViewCurrentSettings_Click(object sender, EventArgs e)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syncthing-run.json");
                
                if (!File.Exists(configPath))
                {
                    MessageBox.Show("未找到配置文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 直接读取JSON文件并解析
                var json = File.ReadAllText(configPath);
                var serializer = new JavaScriptSerializer();
                var configData = serializer.Deserialize<Dictionary<string, object>>(json);
                
                // 获取配置值
                string syncthingPath = configData.ContainsKey("SyncthingExePath") 
                    ? configData["SyncthingExePath"].ToString() 
                    : "未设置";
                
                bool autoStart = configData.ContainsKey("AutoStartSyncthing") 
                    && Convert.ToBoolean(configData["AutoStartSyncthing"]);
                
                string autoStartText = autoStart ? "是" : "否";
                
                // 如果路径为空，显示"未设置"
                if (string.IsNullOrWhiteSpace(syncthingPath))
                {
                    syncthingPath = "未设置";
                }
                
                string message = $"当前托盘配置：\n\n" +
                                $"Syncthing.exe路径：{syncthingPath}\n" +
                                $"启动时自动运行：{autoStartText}";
                
                MessageBox.Show(message, "当前托盘配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取配置文件失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 保留原方法以避免编译错误
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void CheckSyncthingStatus()
        {
            isSyncthingRunning = Process.GetProcessesByName("syncthing").Length > 0;
        }

        private void AutoStartSyncthingIfEnabled()
        {
            if (config.AutoStartSyncthing && !isSyncthingRunning)
            {
                if (StartSyncthingProcess())
                {
                    Log.Info("自动启动Syncthing成功");
                }
                else
                {
                    Log.Error("自动启动Syncthing失败");
                }
            }
        }

        private void CheckSyncthingVersion()
        {
            try
            {
                currentSyncthingVersion = GetCurrentSyncthingVersion();
                if (!string.IsNullOrEmpty(currentSyncthingVersion))
                {
                    Log.Info($"当前Syncthing版本: {currentSyncthingVersion}");
                }
            }
            catch (Exception ex)
            {
                Log.Error("获取Syncthing版本失败: " + ex.Message);
            }
        }



        private string GetCurrentSyncthingVersion()
        {
            try
            {
                if (!File.Exists(config.SyncthingExePath))
                {
                    return "找不到文件";
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = config.SyncthingExePath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    string output = process.StandardOutput.ReadToEnd();
                    
                    // 查找版本号，支持更多格式，如 v2.0.13 或 1.27.0 等
                    var match = Regex.Match(output, @"(v?\d+\.\d+(?:\.\d+)+)");
                    if (match.Success)
                    {
                        // 确保返回的版本号包含 'v' 前缀（如果原输出中有）
                        string version = match.Groups[1].Value;
                        if (!version.StartsWith("v"))
                        {
                            version = "v" + version;
                        }
                        return version;
                    }
                }
            }
            catch
            {
                // 静默处理错误
            }
            return "未检测到";
        }





        private void CheckSyncthingPath()
        {
            // 如果当前路径有效，直接返回
            if (File.Exists(config.SyncthingExePath))
            {
                return;
            }

            // 尝试检测同目录下的syncthing.exe
            string detectedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syncthing.exe");
            if (File.Exists(detectedPath))
            {
                var result = MessageBox.Show(
                    $"检测到同级目录下有Syncthing程序：{detectedPath}\n\n是否直接使用此路径？",
                    "检测到Syncthing程序",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                    
                if (result == DialogResult.Yes)
                {
                    config.SyncthingExePath = detectedPath;
                    config.Save();
                    currentSyncthingVersion = GetCurrentSyncthingVersion();
                    Log.Info($"已使用检测到的路径: {detectedPath}");
                    
                    // 询问是否启动Syncthing
                    if (!isSyncthingRunning)
                    {
                        var startResult = MessageBox.Show(
                            "Syncthing.exe路径设置成功！\n\n是否立即启动Syncthing？",
                            "启动Syncthing",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                            
                        if (startResult == DialogResult.Yes)
                        {
                            if (!StartSyncthingProcess())
                            {
                                Log.ShowError("启动Syncthing失败");
                            }
                        }
                    }
                }
                else
                {
                    // 用户选择否，手动选择路径
                    SetSyncthingPathManually();
                }
                return;
            }
        }

        // 添加手动选择路径的方法
        private void SetSyncthingPathManually()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "选择Syncthing.exe文件";
                openFileDialog.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                openFileDialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string newPath = openFileDialog.FileName;
                    config.SyncthingExePath = newPath;
                    config.Save();
                    currentSyncthingVersion = GetCurrentSyncthingVersion();
                    UpdateMenu();
                    UpdateTrayIcon();
                    Log.Info("手动设置Syncthing.exe路径为: " + newPath);
                    
                    // 询问是否启动Syncthing
                    if (!isSyncthingRunning)
                    {
                        var startResult = MessageBox.Show(
                            "Syncthing.exe路径设置成功！\n\n是否立即启动Syncthing？",
                            "启动Syncthing",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                            
                        if (startResult == DialogResult.Yes)
                        {
                            if (!StartSyncthingProcess())
                            {
                                MessageBox.Show("启动Syncthing失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            }
        }

    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SyncthingRun
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show("程序启动失败：" + ex.Message + "\n\n详细信息：" + ex.StackTrace, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log.Error("程序崩溃：" + ex.Message + "\n" + ex.StackTrace);
            }
        }
    }

    public class Config
    {
        public string SyncthingExePath { get; set; } = "";
        public bool AutoStartSyncthing { get; set; } = false;

        public static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syncthing-run.json");
        private static readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public static Config Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var config = serializer.Deserialize<Config>(json);
                    
                    // 清理旧的StartupEnabled配置项
                    CleanOldConfig(config);
                    
                    return config;
                }
            }
            catch
            {
                // 忽略配置加载错误，使用默认配置
            }

            // 使用默认配置
            var defaultConfig = new Config();
            CleanOldConfig(defaultConfig);
            defaultConfig.Save();
            return defaultConfig;
        }

        private static void CleanOldConfig(Config config)
        {
            try
            {
                // 读取现有配置文件
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        // 检查是否有StartupEnabled属性
                        var tempConfig = serializer.Deserialize<Dictionary<string, object>>(json);
                        if (tempConfig.ContainsKey("StartupEnabled"))
                        {
                            tempConfig.Remove("StartupEnabled");
                            var cleanedJson = serializer.Serialize(tempConfig);
                            File.WriteAllText(ConfigFilePath, cleanedJson);
                            Log.Info("已清理旧的StartupEnabled配置项");
                        }
                    }
                }
            }
            catch
            {
                // 清理失败也不影响，下次保存时会正常
            }
        }

        public void Save()
        {
            try
            {
                var json = serializer.Serialize(this);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error("保存配置失败: " + ex.Message);
            }
        }
    }

    public static class Log
    {
        public static void Info(string message)
        {
            // 信息日志不再记录，直接忽略
        }

        public static void Error(string message)
        {
            // 改为静默记录，不再弹窗
            // 调用方根据业务逻辑决定是否提示用户
            try
            {
                System.Diagnostics.Debug.WriteLine("ERROR: " + message);
            }
            catch { }
        }

        // 新增用户提示方法
        public static void ShowError(string message, string title = "错误")
        {
            try
            {
                MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        public static void Warning(string message)
        {
            // 警告信息通过弹窗显示
            try
            {
                MessageBox.Show(message, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch { }
        }
    }
}
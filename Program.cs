using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SyncthingRun
{
    public class IniFile
    {
        private string path;

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public IniFile(string filePath)
        {
            path = filePath;
        }

        public void Write(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, path);
        }

        public string Read(string section, string key, string defaultValue = "")
        {
            StringBuilder retVal = new StringBuilder(255);
            GetPrivateProfileString(section, key, defaultValue, retVal, 255, path);
            return retVal.ToString();
        }
    }
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

        public static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syncthing-run.ini");

        public static Config Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var ini = new IniFile(ConfigFilePath);
                    var config = new Config();
                    
                    config.SyncthingExePath = ini.Read("Settings", "SyncthingExePath", "");
                    var autoStartValue = ini.Read("Settings", "AutoStartSyncthing", "False");
                    config.AutoStartSyncthing = autoStartValue.ToLower() == "true";
                    
                    return config;
                }
            }
            catch
            {
                // 忽略配置加载错误，使用默认配置
            }

            // 使用默认配置
            var defaultConfig = new Config();
            defaultConfig.Save();
            return defaultConfig;
        }

        public void Save()
        {
            try
            {
                var ini = new IniFile(ConfigFilePath);
                ini.Write("Settings", "SyncthingExePath", SyncthingExePath ?? "");
                ini.Write("Settings", "AutoStartSyncthing", AutoStartSyncthing.ToString());
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
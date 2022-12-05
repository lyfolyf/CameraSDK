using CameraSDK.Models;
using GL.Kit.Log;
using System;
using System.IO;
using System.Text;
using static CameraSDK.CameraAction;
using static GL.Kit.Log.ActionResult;

namespace CameraSDK.DaHua
{
    public class DaHuaCameraSDKLog : ICameraSDKLog
    {
        string PATH = $@"C:\Users\{Environment.UserName}\MVSDK\HyEye";
        const string BrandModel = CameraAlias.DaHua + " 相机";

        IGLog log;

        public string ConfigFile { get; set; }

        public DaHuaCameraSDKLog(IGLog log)
        {
            this.log = log;
        }

        public void OpenDebugLevel()
        {
            if (string.IsNullOrEmpty(ConfigFile))
            {
                log?.Error(new CameraLogMessage(BrandModel, A_OpenSDKLog, R_Fail, "配置文件不存在"));
                return;
            }

            try
            {
                StreamReader reader = new StreamReader(ConfigFile, Encoding.Default);
                string tempstr = reader.ReadToEnd();
                tempstr = tempstr.Replace("= FATAL", "= DEBUG");
                reader.Close();
                StreamWriter writer = new StreamWriter(ConfigFile, false, Encoding.Default);
                writer.Write(tempstr);
                writer.Flush();
                writer.Close();
                log?.Info(new CameraLogMessage(BrandModel, A_OpenSDKLog, R_Success));
            }
            catch (Exception ex)
            {
                log?.Error(new CameraLogMessage(BrandModel, A_OpenSDKLog, R_Fail, ex.Message));
            }
        }

        public void CloseLog()
        {
            if (string.IsNullOrEmpty(ConfigFile))
            {
                log?.Error(new CameraLogMessage(BrandModel, A_CloseSDKLog, R_Fail, "配置文件不存在"));
                return;
            }

            try
            {
                StreamReader reader = new StreamReader(ConfigFile, Encoding.Default);
                string tempstr = reader.ReadToEnd();
                tempstr = tempstr.Replace("= DEBUG", "= FATAL");
                reader.Close();
                StreamWriter writer = new StreamWriter(ConfigFile, false, Encoding.Default);
                writer.Write(tempstr);
                writer.Flush();
                writer.Close();
                log?.Info(new CameraLogMessage(BrandModel, A_CloseSDKLog, R_Success));
            }
            catch (Exception ex)
            {
                log?.Error(new CameraLogMessage(BrandModel, A_CloseSDKLog, R_Fail, ex.Message));
            }
        }

        public void CopyLog()
        {
            if (!Directory.Exists(PATH))
            {
                log?.Error(new CameraLogMessage(BrandModel, A_GetSDKLog, R_Fail, "路径不存在"));
                return;
            }

            string[] logFilenames = Directory.GetFiles(PATH, "*.log");

            if (logFilenames.Length == 0)
            {
                log?.Info(new CameraLogMessage(BrandModel, A_GetSDKLog, R_Fail, "未发现 SDK 日志"));
                return;
            }

            foreach (string fn in logFilenames)
            {
                File.Copy(fn, $"{PathUtils.CurrentDirectory}Log\\{Path.GetFileName(fn)}", true);
            }
            log?.Info(new CameraLogMessage(BrandModel, A_GetSDKLog, R_Success));
        }

    }
}

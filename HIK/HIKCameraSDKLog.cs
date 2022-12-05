using System;
using CameraSDK.Models;
using GL.Kit.Log;
using System.IO;
using static GL.Kit.Log.ActionResult;
using static CameraSDK.CameraAction;


namespace CameraSDK.HIK
{
    class HIKCameraSDKLog : ICameraSDKLog
    {
        const string PATH = @"C:\Windows\Temp\MvSDKLog";
        const string BrandModel = CameraAlias.HIK + " 相机";

        IGLog log;

        public string ConfigFile { get; set; }

        public HIKCameraSDKLog(IGLog log)
        {
            this.log = log;
        }

        //public bool DebugLevelOpened { get; set; }

        public void OpenDebugLevel()
        {
            try
            {
                string fileName = PATH + @"\LogServer.ini";
                DeleteOld(fileName);
                FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine("[File]");
                sw.WriteLine("FileSize=50");
                sw.WriteLine("[Level]");
                sw.WriteLine("LogLevel=7");
                sw.WriteLine("LogDebugLevel=2");
                sw.Flush();
                sw.Close();
                fs.Close();
                log?.Info(new CameraLogMessage(BrandModel, A_OpenSDKLog, R_Success));
                //DebugLevelOpened = true;
            }
            catch (Exception ex)
            {
                log?.Error(new CameraLogMessage(BrandModel, A_OpenSDKLog, R_Fail, ex.Message));
            }
        }

        public void CloseLog()
        {
            try
            {
                string fileName = PATH + @"\LogServer.ini";
                DeleteOld(fileName);
                FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine("[File]");
                sw.WriteLine("FileSize=1");
                sw.WriteLine("[Level]");
                sw.WriteLine("LogLevel=0");
                sw.Flush();
                sw.Close();
                fs.Close();
                log?.Info(new CameraLogMessage(BrandModel, A_CloseSDKLog, R_Success));
                //DebugLevelOpened = false;
            }
            catch (Exception ex)
            {
                log?.Error(new CameraLogMessage(BrandModel, A_CloseSDKLog, R_Fail, ex.Message));
            }
        }

        public void DeleteOld(string _fileName)
        {
            try
            {
                if (File.Exists(_fileName))
                    File.Delete(_fileName);
            }
            catch (Exception ex)
            {
                throw new CameraSDKException("删除SDK日志配置文件异常" + ex.Message);
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

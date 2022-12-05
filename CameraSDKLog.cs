using CameraSDK.DaHua;
using CameraSDK.HIK;
using CameraSDK.Models;
using GL.Kit.Log;

namespace CameraSDK
{
    public interface ICameraSDKLog
    {
        //bool DebugLevelOpened { get; }
        string ConfigFile { get; set; }

        void OpenDebugLevel();

        void CloseLog();

        void CopyLog();
    }

    public class CameraSDKLog : ICameraSDKLog
    {
        ICameraSDKLog cameraSDKLog;

        public string ConfigFile
        {
            get { return cameraSDKLog?.ConfigFile; }
            set
            {
                if (cameraSDKLog != null)
                    cameraSDKLog.ConfigFile = value;
            }
        }

        public CameraSDKLog(CameraBrand brand, IGLog log)
        {
            switch (brand)
            {
                case CameraBrand.HIK_Gige:
                case CameraBrand.HIK_GenTL:
                    cameraSDKLog = new HIKCameraSDKLog(log);
                    break;
                case CameraBrand.DaHua:
                    cameraSDKLog = new DaHuaCameraSDKLog(log);
                    break;
            }
        }

        //public bool DebugLevelOpened
        //{
        //    get { return cameraSDKLog?.DebugLevelOpened ?? false; }
        //}

        public void OpenDebugLevel()
        {
            if (cameraSDKLog == null)
                throw new CameraSDKException("该品牌相机不支持设置 SDK 日志");

            cameraSDKLog.OpenDebugLevel();
        }

        public void CloseLog()
        {
            if (cameraSDKLog == null)
                throw new CameraSDKException("该品牌相机不支持设置 SDK 日志");

            cameraSDKLog.CloseLog();
        }

        public void CopyLog()
        {
            if (cameraSDKLog == null)
                throw new CameraSDKException("该品牌相机不支持设置 SDK 日志");

            cameraSDKLog.CopyLog();
        }

    }
}

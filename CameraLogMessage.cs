using CameraSDK.Models;
using GL.Kit.Log;

namespace CameraSDK
{
    public class CameraLogMessage : LogMessage
    {
        public override string Module => "CameraSDK";

        /// <summary>
        /// 相机名称
        /// </summary>
        public string CameraName { get; set; }

        public double? Time { get; set; }

        public override string ToString(LogFormat format)
        {
            if (format == LogFormat.CSV)
                return $"{Module},{CameraName},{Action},{ActionResult},{Message},{Time}";
            else
                return $"[{CameraName}] {Action}{ActionResult}{(Message != null ? $"，{Message}" : string.Empty)}";
        }

        public CameraLogMessage() { }

        public CameraLogMessage(ComCameraInfo cameraInfo, string action, string actionResult, string message = null, double? time = null)
        {
            CameraName = cameraInfo.ToString();
            Action = action;
            ActionResult = actionResult;
            Message = message;
            Time = time;
        }

        public CameraLogMessage(string cameraName, string action, string actionResult, string message = null, double? time = null)
        {
            CameraName = cameraName;
            Action = action;
            ActionResult = actionResult;
            Message = message;
            Time = time;
        }
    }

    public static class CameraAction
    {
        public const string A_Search        = "搜索";

        public const string A_GetCamera     = "获取相机";

        public const string A_Open          = "打开";

        public const string A_Close         = "关闭";

        public const string A_Rename        = "重命名";

        public const string A_SoftTrigger   = "软触发";

        public const string A_StartGrabbing = "开始采集";

        public const string A_StopGrabbing  = "停止采集";

        public const string A_AcqImage      = "取到图像";

        public const string A_GetParam      = "读取参数";

        public const string A_SetParam      = "设置参数";

        public const string A_Register      = "注册";

        public const string A_Connect       = "连接";

        public const string A_Other         = "其他";

        public const string A_Copy          = "复制";

        public const string A_ConvertPixel = "像素转化";

        public const string A_DiscardImage  = "丢弃图像";

        public const string A_GetSDKLog     = "获取 SDK 日志";

        public const string A_OpenSDKLog    = "打开SDK日志";

        public const string A_CloseSDKLog   = "关闭SDK日志";
    }
}

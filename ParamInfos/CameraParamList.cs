namespace CameraSDK.Models
{
    /// <summary>
    /// 相机参数列表
    /// <para>包含所有品牌、型号的相机</para>
    /// </summary>
    public class CameraParamList
    {
        public const string C_DeviceUserID         = "DeviceUserID";         // 相机名称
        public const string C_AcquisitionMode      = "AcquisitionMode";      // 采集模式，单帧、多帧、连续
        public const string C_TriggerMode          = "TriggerMode";          // 触发模式：连续模式/触发一次模式
        public const string C_TriggerSource        = "TriggerSource";        // 触发源：软触发/硬触发

        public const string C_Exposure             = "ExposureTime";         // 曝光
        public const string C_Gain                 = "Gain";                 // 增益
        public const string C_PreampGain           = "PreampGain";           // 增益
        public const string C_ExposureAuto         = "ExposureAuto";         // 自动曝光
        public const string C_GainAuto             = "GainAuto";             // 自动增益

        public const string C_ImageWidth           = "Width";                // 图像宽度
        public const string C_ImageHeight          = "Height";               // 图像高度
        public const string C_PixelFormat          = "PixelFormat";
        public const string C_TriggerDelay         = "TriggerDelay";         // 拍照延时

        public const string C_HeartbeatTimeout     = "GevHeartbeatTimeout";  // 心跳超时时间 500-60000

        public const string C_ImageCacheCount      = "ImageCacheCount";      // 图像缓存

        public const string C_GevSCPSPacketSize    = "GevSCPSPacketSize";    // 最佳网络包大小
        public const string C_ResultingFrameRate   = "ResultingFrameRate";   // 读取帧率
        public const string C_AcquisitionFrameRate = "AcquisitionFrameRate"; // 设置帧率
        public const string C_PayloadSize          = "PayloadSize";          // 有效载荷
    }
}

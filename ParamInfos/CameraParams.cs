using CameraSDK.HIK;
using System.ComponentModel;
using System.Text;

namespace CameraSDK.Models
{
    /// <summary>
    /// 相机参数
    /// </summary>
    public struct CameraParams
    {
        /// <summary>
        /// 曝光
        /// </summary>
        [Description("曝光")]
        public float? ExposureTime { get; set; }

        /// <summary>
        /// 增益
        /// </summary>
        [Description("增益")]
        public float? Gain { get; set; }

        /// <summary>
        /// 增益
        /// <para>目前为海康独有</para>
        /// </summary>
        [Description("增益")]
        public PreampGainEnum? PreampGain { get; set; }

        /// <summary>
        /// 帧率
        /// </summary>
        [Description("帧率")]
        public float? FrameRate { get; set; }

        /// <summary>
        /// 图像宽度
        /// </summary>
        [Description("图像宽度")]
        public int? ImageWidth { get; set; }

        /// <summary>
        /// 图像高度
        /// </summary>
        [Description("图像高度")]
        public int? ImageHeight { get; set; }

        /// <summary>
        /// 拍照延时
        /// </summary>
        [Description("拍照延时")]
        public float? TriggerDelay { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(32);
            if (ExposureTime.HasValue)
                sb.Append($"曝光 = {ExposureTime},");
            if (Gain.HasValue)
                sb.Append($"增益 = {Gain},");
            if (FrameRate.HasValue)
                sb.Append($"帧率 = {FrameRate},");
            if (ImageWidth.HasValue)
                sb.Append($"图像宽度 = {ImageWidth},");
            if (ImageHeight.HasValue)
                sb.Append($"图像高度 = {ImageHeight}");
            if (TriggerDelay.HasValue)
                sb.Append($"拍照延时 = {TriggerDelay}");

            return sb.ToString();
        }
    }
}

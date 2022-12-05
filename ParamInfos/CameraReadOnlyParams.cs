using System.ComponentModel;

namespace CameraSDK.Models
{
    /// <summary>
    /// 只读参数
    /// </summary>
    public struct CameraReadOnlyParams
    {
        /// <summary>
        /// 图像最大宽度
        /// </summary>
        [Description("图像最大宽度")]
        public int? ImageMaxWidth { get; set; }

        /// <summary>
        /// 图像最小宽度
        /// </summary>
        [Description("图像最小宽度")]
        public int? ImageMinWidth { get; set; }

        /// <summary>
        /// 图像最大高度
        /// </summary>
        [Description("图像最大高度")]
        public int? ImageMaxHeight { get; set; }

        /// <summary>
        /// 图像最小高度
        /// </summary>
        [Description("图像最小高度")]
        public int? ImageMinHeight { get; set; }

        /// <summary>
        /// 最大曝光
        /// </summary>
        [Description("最大曝光")]
        public float? MaxExposureTime { get; set; }

        /// <summary>
        /// 最小曝光
        /// </summary>
        [Description("最小曝光")]
        public float? MinExposureTime { get; set; }

        /// <summary>
        /// 最大增益
        /// </summary>
        [Description("最大增益")]
        public float? MaxGain { get; set; }

        /// <summary>
        /// 最小增益
        /// </summary>
        [Description("最小增益")]
        public float? MinGain { get; set; }
    }
}

using CameraSDK.Models;

namespace CameraSDK.Basler
{
    public sealed class BaslerParamInfoCollection : CameraParamInfoCollection
    {
        public BaslerParamInfoCollection()
        {
            ExposureRaw = new CameraParamInfo2 { Name = "ExposureTimeRaw", Description = "曝光", Type = FloatType };

            GainRaw = new CameraParamInfo2 { Name = "GainRaw", Description = "增益", Type = FloatType };

            TriggerDelayAbs = new CameraParamInfo2 { Name = "TriggerDelayAbs", Description = "拍照延时", Type = FloatType };

            ImageCacheCount = new CameraParamInfo2 { Name = "MaxNumBuffer", Description = "图片缓存", Type = IntType };

            paramInfoDict.Add(ExposureRaw.Name, ExposureRaw);
            paramInfoDict.Add(GainRaw.Name, GainRaw);
            paramInfoDict.Add(CameraParamList.C_ImageCacheCount, ImageCacheCount);
        }

        /// <summary>
        /// 曝光
        /// <para>Gige</para>
        /// </summary>
        public CameraParamInfo2 ExposureRaw { get; }

        /// <summary>
        /// 增益
        /// <para>Gige</para>
        /// </summary>
        public CameraParamInfo2 GainRaw { get; }

        /// <summary>
        /// 拍照延时
        /// <para>Gige</para>
        /// </summary>
        public CameraParamInfo2 TriggerDelayAbs { get; }

        public CameraParamInfo2 ImageCacheCount { get; }

    }
}

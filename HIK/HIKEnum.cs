using MvCamCtrl.NET;
using System.ComponentModel;

namespace CameraSDK.HIK
{
    /// <summary>
    /// 海康 CXP 相机增益枚举值
    /// </summary>
    public enum PreampGainEnum
    {
        [Description("1.25x")]
        Gain_1250X = 1250,

        [Description("1.50x")]
        Gain_1500X = 1500,

        [Description("1.75x")]
        Gain_1750X = 1750,

        [Description("2.00x")]
        Gain_2000X = 2000,

        [Description("2.25x")]
        Gain_2250X = 2250,

        [Description("2.50x")]
        Gain_2500X = 2500,

        [Description("2.75x")]
        Gain_2750X = 2750,

        [Description("3.00x")]
        Gain_3000X = 3000,

        [Description("3.25x")]
        Gain_3250X = 3250,

        [Description("3.50x")]
        Gain_3500X = 3500,

        [Description("3.75x")]
        Gain_3750X = 3750,

        [Description("4.00x")]
        Gain_4000X = 4000,

        [Description("4.25x")]
        Gain_4250X = 4250,

        [Description("4.50x")]
        Gain_4500X = 4500,

        [Description("4.75x")]
        Gain_4750X = 4750,

        [Description("5.00x")]
        Gain_5000X = 5000,

        [Description("5.25x")]
        Gain_5250X = 5250,

        [Description("5.50x")]
        Gain_5500X = 5500,

        [Description("5.75x")]
        Gain_5750X = 5750,

        [Description("6.00x")]
        Gain_6000X = 6000
    }

    enum AcquisitionMode_HIK
    {
        [Description("连续")]
        Continuous = MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS,
    }

    enum TriggerMode_HIK
    {
        Off = MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF,

        On = MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON
    }

    enum TriggerSource_HIK
    {
        Software = MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE,

        Extern = MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE0
    }

    enum ExposureAuto_HIK
    {
        Off = MyCamera.MV_CAM_EXPOSURE_AUTO_MODE.MV_EXPOSURE_AUTO_MODE_OFF,

        Once = MyCamera.MV_CAM_EXPOSURE_AUTO_MODE.MV_EXPOSURE_AUTO_MODE_ONCE,

        Continuous = MyCamera.MV_CAM_EXPOSURE_AUTO_MODE.MV_EXPOSURE_AUTO_MODE_CONTINUOUS
    }

    enum GainAuto_HIK
    {
        Off = MyCamera.MV_CAM_GAIN_MODE.MV_GAIN_MODE_OFF,

        Once = MyCamera.MV_CAM_GAIN_MODE.MV_GAIN_MODE_ONCE,

        Continuous = MyCamera.MV_CAM_GAIN_MODE.MV_GAIN_MODE_CONTINUOUS
    }
}

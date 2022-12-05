using System;
using System.Collections.Generic;
using System.Linq;

namespace CameraSDK.Models
{
    public class CameraParamInfo2 : CameraParamInfo
    {
        public string Description { get; set; }

        public Type Type { get; set; }
    }

    public class CameraParamInfoCollection
    {
        protected static readonly Type IntType = typeof(int);
        protected static readonly Type FloatType = typeof(float);
        protected static readonly Type StringType = typeof(string);
        protected static readonly Type EnumType = typeof(Enum);

        public CameraParamInfoCollection()
        {
            mDeviceUserID = new CameraParamInfo2 { Name = CameraParamList.C_DeviceUserID, Description = "相机名称", Type = StringType };

            mAcquisitionMode = new CameraParamInfo2 { Name = CameraParamList.C_AcquisitionMode, Description = "采集模式", Type = EnumType };

            mTriggerMode = new CameraParamInfo2 { Name = CameraParamList.C_TriggerMode, Description = "触发模式", Type = EnumType };

            mTriggerSource = new CameraParamInfo2 { Name = CameraParamList.C_TriggerSource, Description = "触发源", Type = EnumType };

            mExposure = new CameraParamInfo2 { Name = CameraParamList.C_Exposure, Description = "曝光", Type = FloatType };

            mGain = new CameraParamInfo2 { Name = CameraParamList.C_Gain, Description = "增益", Type = FloatType };

            mExposureAuto = new CameraParamInfo2 { Name = CameraParamList.C_ExposureAuto, Description = "自动曝光", Type = EnumType };

            mGainAuto = new CameraParamInfo2 { Name = CameraParamList.C_GainAuto, Description = "自动增益", Type = EnumType };

            mWidth = new CameraParamInfo2 { Name = CameraParamList.C_ImageWidth, Description = "图像宽度", Type = IntType };

            mHeight = new CameraParamInfo2 { Name = CameraParamList.C_ImageHeight, Description = "图像高度", Type = IntType };

            mTriggerDelay = new CameraParamInfo2 { Name = CameraParamList.C_TriggerDelay, Description = "拍照延时", Type = FloatType };

            mPixelFormat = new CameraParamInfo2 { Name = "PixelFormat", Description = "像素格式", Type = EnumType };

            mHeartbeatTimeout = new CameraParamInfo2 { Name = CameraParamList.C_HeartbeatTimeout, Description = "心跳时间", Type = IntType };

            paramInfoDict = new Dictionary<string, CameraParamInfo2>
            {
                { CameraParamList.C_DeviceUserID    , DeviceUserID     },
                { CameraParamList.C_AcquisitionMode , AcquisitionMode  },
                { CameraParamList.C_TriggerMode     , TriggerMode      },
                { CameraParamList.C_TriggerSource   , TriggerSource    },
                { CameraParamList.C_Exposure        , Exposure         },
                { CameraParamList.C_Gain            , Gain             },
                { CameraParamList.C_ExposureAuto    , ExposureAuto     },
                { CameraParamList.C_GainAuto        , GainAuto         },
                { CameraParamList.C_ImageWidth      , Width            },
                { CameraParamList.C_ImageHeight     , Height           },
                { CameraParamList.C_TriggerDelay    , TriggerDelay     },
                { CameraParamList.C_PixelFormat     , PixelFormat      },
                { CameraParamList.C_HeartbeatTimeout, HeartbeatTimeout }
            };
        }

        CameraParamInfo2 mDeviceUserID;
        /// <summary>
        /// 相机名称
        /// </summary>
        public virtual CameraParamInfo2 DeviceUserID
        {
            get { return mDeviceUserID; }
        }

        CameraParamInfo2 mAcquisitionMode;
        /// <summary>
        /// 采集模式
        /// </summary>
        public virtual CameraParamInfo2 AcquisitionMode
        {
            get { return mAcquisitionMode; }
        }

        CameraParamInfo2 mTriggerMode;
        /// <summary>
        /// 触发模式
        /// </summary>
        public virtual CameraParamInfo2 TriggerMode
        {
            get { return mTriggerMode; }
        }

        CameraParamInfo2 mTriggerSource;
        /// <summary>
        /// 触发源
        /// </summary>
        public virtual CameraParamInfo2 TriggerSource
        {
            get { return mTriggerSource; }
        }

        CameraParamInfo2 mExposure;
        /// <summary>
        /// 曝光
        /// </summary>
        public virtual CameraParamInfo2 Exposure
        {
            get { return mExposure; }
        }

        CameraParamInfo2 mGain;
        /// <summary>
        /// 增益
        /// </summary>
        public virtual CameraParamInfo2 Gain
        {
            get { return mGain; }
        }

        CameraParamInfo2 mExposureAuto;
        /// <summary>
        /// 自动曝光
        /// </summary>
        public virtual CameraParamInfo2 ExposureAuto
        {
            get { return mExposureAuto; }
        }

        CameraParamInfo2 mGainAuto;
        /// <summary>
        /// 自动增益
        /// </summary>
        public virtual CameraParamInfo2 GainAuto
        {
            get { return mGainAuto; }
        }

        CameraParamInfo2 mWidth;
        /// <summary>
        /// 图像宽度
        /// </summary>
        public virtual CameraParamInfo2 Width
        {
            get { return mWidth; }
        }

        CameraParamInfo2 mHeight;
        /// <summary>
        /// 图像高度
        /// </summary>
        public virtual CameraParamInfo2 Height
        {
            get { return mHeight; }
        }

        CameraParamInfo2 mTriggerDelay;
        /// <summary>
        /// 拍照延时
        /// </summary>
        public virtual CameraParamInfo2 TriggerDelay
        {
            get { return mTriggerDelay; }
        }

        CameraParamInfo2 mPixelFormat;
        /// <summary>
        /// 像素格式
        /// </summary>
        public virtual CameraParamInfo2 PixelFormat
        {
            get { return mPixelFormat; }
        }

        CameraParamInfo2 mHeartbeatTimeout;
        /// <summary>
        /// 心跳时间
        /// </summary>
        public virtual CameraParamInfo2 HeartbeatTimeout
        {
            get { return mHeartbeatTimeout; }
        }

        // 这里是可变参数
        protected Dictionary<string, CameraParamInfo2> paramInfoDict;

        public CameraParamInfo2 GetParamInfo(string paramName)
        {
            return paramInfoDict[paramName];
        }

        public bool Contains(string paramName)
        {
            return paramInfoDict.ContainsKey(paramName);
        }

        public CameraParamInfo2[] All()
        {
            return paramInfoDict.Values.ToArray();
        }
    }
}

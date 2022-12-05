using CameraSDK.Models;
using GL.Kit.Log;
using MvCamCtrl.NET;
using System;
using System.Runtime.InteropServices;
using static GL.Kit.Log.ActionResult;
using static CameraSDK.CameraAction;

namespace CameraSDK.HIK
{
    /// <summary>
    /// 海康相机（GenTL 口）
    /// </summary>
    public class HIKCameraGenTL : BaseHIKCamera_New
    {
        public HIKCameraGenTL(IntPtr deviceInfo, IGLog log, ComCameraInfo cameraInfo)
            : base(deviceInfo, log, cameraInfo)
        {
            paramInfos.Gain.Enabled = false;
            paramInfos.GainAuto.Enabled = false;
        }

        public override void Open()
        {
            if (IsOpen) return;

            if (m_camera == null)
            {
                m_camera = new MyCamera();
            }

            if (createDevice() == false) return;

            if (OpenDevice() == false) return;
        }

        bool createDevice()
        {
            MyCamera.MV_GENTL_DEV_INFO device = (MyCamera.MV_GENTL_DEV_INFO)Marshal.PtrToStructure(m_deviceInfo, typeof(MyCamera.MV_GENTL_DEV_INFO));

            int nRet = m_camera.MV_CC_CreateDeviceByGenTL_NET(ref device);
            if (MyCamera.MV_OK != nRet)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_Open, R_Fail, HIKErrorCode.ErrorMessage(nRet)));
                throw new CameraSDKException("打开相机失败");
            }

            return true;
        }

        public override bool SetIP(string ip, string subnetMask, string defaultGateway)
        {
            log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "CXP 相机不可以设置 IP"));
            throw new CameraSDKException("CXP 相机不可以设置 IP");
        }
    }
}

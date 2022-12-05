using CameraSDK.Models;
using GL.Kit.Log;
using MvCamCtrl.NET;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static GL.Kit.Log.ActionResult;
using static CameraSDK.CameraAction;

namespace CameraSDK.HIK
{
    class HIKCameraGigEFactory : BaseSingleBrandCameraFac
    {
        readonly IGLog log;

        public HIKCameraGigEFactory(IGLog log)
        {
            this.log = log;
        }

        const string BrandModel = CameraAlias.HIK + "(GigE/U3) 相机";

        public override List<ComCameraInfo> SearchDevice()
        {
            //foreach (HIKCameraGigE camera in cameraDict.Values)
            //{
            //    if (camera.IsOpen)
            //        camera.Close();
            //}

            log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Start));

            MyCamera.MV_CC_DEVICE_INFO_LIST m_stDeviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();

            int nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref m_stDeviceList);
            if (MyCamera.MV_OK != nRet)
            {
                log?.Error(new CameraLogMessage(BrandModel, A_Search, R_Fail, "枚举设备失败"));

                throw new CameraSDKException("搜索相机失败");
            }

            List<ComCameraInfo> cameraInfoList = new List<ComCameraInfo>();

            // 这里还不能用 foreach，因为 nDeviceNum 和 pDeviceInfo.Length 是不一样的
            for (int i = 0; i < m_stDeviceList.nDeviceNum; i++)
            {
                MyCamera.MV_CC_DEVICE_INFO device = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(m_stDeviceList.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO));

                ComCameraInfo cameraInfo = ToCameraInfo(device);
                if (cameraInfo == null) continue;

                cameraInfoList.Add(cameraInfo);

                log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Success, "发现相机：" + cameraInfo.ToString()));

                if (cameraDict.ContainsKey(cameraInfo.SN)) continue;

                HIKCameraGigE camera = new HIKCameraGigE(m_stDeviceList.pDeviceInfo[i], log, cameraInfo);

                camera.Reset += Camera_Reset;

                cameraDict[cameraInfo.SN] = camera;
            }

            if (cameraInfoList.Count == 0)
                log?.Info(new CameraLogMessage(BrandModel, A_Search, R_End, "未发现相机"));

            return cameraInfoList;
        }

        private void Camera_Reset()
        {
            MyCamera.MV_CC_DEVICE_INFO_LIST m_stDeviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();

            MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref m_stDeviceList);
        }

        ComCameraInfo ToCameraInfo(MyCamera.MV_CC_DEVICE_INFO device)
        {
            ComCameraInfo camera = new ComCameraInfo(CameraBrand.HIK_Gige);

            if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
            {
                MyCamera.MV_GIGE_DEVICE_INFO gigeInfo = (MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(device.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO));

                if (!IsHikVision(gigeInfo.chManufacturerName)) return null;

                camera.ConnectionType = ConnectionType.GigE;
                camera.SN = gigeInfo.chSerialNumber;
                camera.UserDefinedName = gigeInfo.chUserDefinedName;
                camera.Model = gigeInfo.chModelName;

                uint nIp1 = (gigeInfo.nCurrentIp & 0xFF000000) >> 24;
                uint nIp2 = (gigeInfo.nCurrentIp & 0x00FF0000) >> 16;
                uint nIp3 = (gigeInfo.nCurrentIp & 0x0000FF00) >> 8;
                uint nIp4 = (gigeInfo.nCurrentIp & 0x000000FF);
                camera.IP = $"{nIp1}.{nIp2}.{nIp3}.{nIp4}";

                nIp1 = (gigeInfo.nCurrentSubNetMask & 0xFF000000) >> 24;
                nIp2 = (gigeInfo.nCurrentSubNetMask & 0x00FF0000) >> 16;
                nIp3 = (gigeInfo.nCurrentSubNetMask & 0x0000FF00) >> 8;
                nIp4 = (gigeInfo.nCurrentSubNetMask & 0x000000FF);
                camera.SubnetMask = $"{nIp1}.{nIp2}.{nIp3}.{nIp4}";

                nIp1 = (gigeInfo.nDefultGateWay & 0xFF000000) >> 24;
                nIp2 = (gigeInfo.nDefultGateWay & 0x00FF0000) >> 16;
                nIp3 = (gigeInfo.nDefultGateWay & 0x0000FF00) >> 8;
                nIp4 = (gigeInfo.nDefultGateWay & 0x000000FF);
                camera.DefaultGateway = $"{nIp1}.{nIp2}.{nIp3}.{nIp4}";
            }
            else if (device.nTLayerType == MyCamera.MV_USB_DEVICE)
            {
                MyCamera.MV_USB3_DEVICE_INFO usbInfo = (MyCamera.MV_USB3_DEVICE_INFO)MyCamera.ByteToStruct(device.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO));

                if (!IsHikVision(usbInfo.chManufacturerName)) return null;

                camera.ConnectionType = ConnectionType.U3;
                camera.SN = usbInfo.chSerialNumber;
                camera.UserDefinedName = usbInfo.chUserDefinedName;
                camera.Model = usbInfo.chModelName;
            }

            return camera;
        }

        // 海康相机分国内标配和中性，中性相机去掉了海康标识，把 chManufacturerName 属性值也改成了GEV 和 U3V
        bool IsHikVision(string manufacturerName)
        {
            if (manufacturerName.StartsWith("Hik", StringComparison.OrdinalIgnoreCase))
                return true;

            if (manufacturerName == "GEV" || manufacturerName == "U3V")
                return true;

            return false;
        }
    }
}

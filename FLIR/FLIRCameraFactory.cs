using CameraSDK.Models;
using GL.Kit.Log;
using SpinnakerNET;
using System;
using System.Collections.Generic;
using static CameraSDK.CameraAction;
using static GL.Kit.Log.ActionResult;

namespace CameraSDK.FLIR
{
    class FLIRCameraFactory : BaseSingleBrandCameraFac
    {
        readonly IGLog log;

        const string BrandModel = CameraAlias.FLIR + " 相机";

        public FLIRCameraFactory(IGLog log)
        {
            this.log = log;
        }

        public override List<ComCameraInfo> SearchDevice()
        {
            log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Start));

            ManagedSystem managed = new ManagedSystem();

            ManagedCameraList camList;

            try
            {
                camList = managed.GetCameras();
            }
            catch (Exception e)
            {
                log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Fail, e.Message));

                return new List<ComCameraInfo>(0);
            }
            finally
            {
                managed.Dispose();
            }

            if (camList.Count == 0)
            {
                log?.Info(new CameraLogMessage(BrandModel, A_Search, R_End, "未发现相机"));

                return new List<ComCameraInfo>(0);
            }

            List<ComCameraInfo> cameraInfoList = new List<ComCameraInfo>();

            foreach (IManagedCamera managedCamera in camList)
            {
                ComCameraInfo cameraInfo = new ComCameraInfo(CameraBrand.FLIR)
                {
                    ConnectionType = FLIRUtils.ToMyType(managedCamera.TLDevice.DeviceType),
                    SN = managedCamera.TLDevice.DeviceSerialNumber,
                    UserDefinedName = managedCamera.TLDevice.DeviceUserID,
                    Model = managedCamera.TLDevice.DeviceModelName
                };
                cameraInfoList.Add(cameraInfo);

                log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Success, "发现相机：" + cameraInfo.ToString()));

                if (cameraDict.ContainsKey(cameraInfo.SN)) continue;

                FLIRCamera camera = new FLIRCamera(managedCamera, log, cameraInfo);

                cameraDict[cameraInfo.SN] = camera;
            }

            return cameraInfoList;
        }
    }
}

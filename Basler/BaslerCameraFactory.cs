using Basler.Pylon;
using CameraSDK.Models;
using GL.Kit.Log;
using System;
using System.Collections.Generic;
using static CameraSDK.CameraAction;
using static GL.Kit.Log.ActionResult;

namespace CameraSDK.Basler
{
    class BaslerCameraFactory : BaseSingleBrandCameraFac
    {
        readonly IGLog log;

        const string BrandModel = "Basler相机";

        public BaslerCameraFactory(IGLog log)
        {
            this.log = log;
        }

        public override List<ComCameraInfo> SearchDevice()
        {
            log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Start));

            try
            {
                List<ICameraInfo> allCameraInfos = CameraFinder.Enumerate();

                if (allCameraInfos.Count == 0)
                {
                    log?.Info(new CameraLogMessage(BrandModel, A_Search, R_End, "未发现相机"));

                    return new List<ComCameraInfo>(0);
                }

                List<ComCameraInfo> cameraInfoList = new List<ComCameraInfo>();

                foreach (ICameraInfo cameraInfo in allCameraInfos)
                {
                    ComCameraInfo comCamera = new ComCameraInfo(CameraBrand.Basler)
                    {
                        SN = cameraInfo[CameraInfoKey.SerialNumber],
                        UserDefinedName = cameraInfo[CameraInfoKey.UserDefinedName],
                        Model = cameraInfo[CameraInfoKey.ModelName],
                        IP = cameraInfo[CameraInfoKey.DeviceIpAddress],
                        SubnetMask = cameraInfo[CameraInfoKey.SubnetMask],
                        DefaultGateway = cameraInfo[CameraInfoKey.DefaultGateway]
                    };

                    cameraInfoList.Add(comCamera);

                    log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Success, "发现相机：" + comCamera.ToString()));

                    if (cameraDict.ContainsKey(comCamera.SN)) continue;

                    BaslerCamera camera = new BaslerCamera(log, comCamera);

                    cameraDict[comCamera.SN] = camera;
                }

                return cameraInfoList;
            }
            catch (Exception)
            {
                log?.Error(new CameraLogMessage(BrandModel, A_Search, R_Fail, "未发现相机，请检查驱动是否已安装"));
                throw;
            }
        }
    }
}

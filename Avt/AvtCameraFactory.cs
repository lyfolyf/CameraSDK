using AVT.VmbAPINET;
using CameraSDK.Models;
using GL.Kit.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CameraSDK.CameraAction;
using static GL.Kit.Log.ActionResult;

namespace CameraSDK.Avt
{
    class AvtCameraFactory : BaseSingleBrandCameraFac
    {
        readonly IGLog log;

        const string BrandModel = "AVT";

        public AvtCameraFactory(IGLog log)
        {
            this.log = log;
        }
        public override List<ComCameraInfo> SearchDevice()
        {
            AvtCamera.Startup();
            log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Start));

            try
            {
                var cameras = AvtHelper.Cameras;

                if (cameras == null || cameras.Count == 0)
                {
                    log?.Info(new CameraLogMessage(BrandModel, A_Search, R_End, "未发现相机"));

                    return new List<ComCameraInfo>(0);
                }

                List<ComCameraInfo> cameraInfoList = new List<ComCameraInfo>();

                foreach (Camera cameraInfo in cameras)
                {
                    ComCameraInfo comCamera = new ComCameraInfo(CameraBrand.Avt)
                    {
                        SN = cameraInfo.Id,//使用ID，使用SN会很不方便，
                        UserDefinedName = cameraInfo.Name,
                        Model = cameraInfo.Model,
                        //IP = cameraInfo.Features["Ip"].StringValue,
                        //SubnetMask = cameraInfo.Features["SubnetMask"].StringValue,,
                        //DefaultGateway = cameraInfo.Features["DefaultGateway"].StringValue,
                    };

                    cameraInfoList.Add(comCamera);

                    log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Success, "发现相机：" + comCamera.ToString()));

                    if (cameraDict.ContainsKey(comCamera.SN)) continue;

                    AvtCamera camera = new AvtCamera(log, comCamera);

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

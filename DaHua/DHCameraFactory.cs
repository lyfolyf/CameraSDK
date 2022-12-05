using CameraSDK.Models;
using GL.Kit.Log;
using System;
using System.Collections.Generic;
using ThridLibray;
using static CameraSDK.CameraAction;
using static GL.Kit.Log.ActionResult;

namespace CameraSDK.DaHua
{
    class DHCameraFactory : BaseSingleBrandCameraFac
    {
        readonly IGLog log;

        const string BrandModel = CameraAlias.DaHua;

        public DHCameraFactory(IGLog log)
        {
            this.log = log;
        }

        public override List<ComCameraInfo> SearchDevice()
        {
            log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Start));

            try
            {
                //大华，先搜索相机信息，再通过Key来获取相机设备
                List<IDeviceInfo> deviceList = Enumerator.EnumerateDevices();

                if (deviceList.Count == 0)
                {
                    log?.Info(new CameraLogMessage(BrandModel, A_Search, R_End, "未发现相机"));
                    return new List<ComCameraInfo>(0);
                }

                List<ComCameraInfo> cameraInfoList = new List<ComCameraInfo>();

                for (int i = 0; i < deviceList.Count; i++)
                {
                    ComCameraInfo cameraInfo = new ComCameraInfo(CameraBrand.DaHua)
                    {
                        ConnectionType = DHUtils.ToMyType(deviceList[i].DeviceTypeEx),
                        SN = deviceList[i].SerialNumber,
                        UserDefinedName = deviceList[i].Name,
                        Model = deviceList[i].Model
                    };

                    IGigeDeviceInfo gigeDevice = null;
                    if (cameraInfo.ConnectionType == ConnectionType.GigE)
                    {
                        gigeDevice = Enumerator.GigeCameraInfo(i);
                        cameraInfo.IP = gigeDevice.IpAddress;
                        cameraInfo.SubnetMask = gigeDevice.SubnetMask;
                        cameraInfo.DefaultGateway = gigeDevice.GateWay;
                    }

                    cameraInfoList.Add(cameraInfo);

                    log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Success, "发现相机：" + cameraInfo.ToString()));

                    if (cameraDict.ContainsKey(cameraInfo.SN))
                    {
                        ((DHCamera)cameraDict[cameraInfo.SN]).index = gigeDevice?.Index ?? -1;
                        continue;
                    }

                    IDevice dev = Enumerator.GetDeviceByIndex(i);

                    DHCamera camera = new DHCamera(dev, log, cameraInfo);
                    camera.index = gigeDevice?.Index ?? -1;
                    cameraDict[cameraInfo.SN] = camera;
                }

                log?.Info(new CameraLogMessage(BrandModel, A_Search, R_End));
                return cameraInfoList;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}

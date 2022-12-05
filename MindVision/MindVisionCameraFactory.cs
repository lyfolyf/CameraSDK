using CameraSDK.Models;
using GL.Kit.Log;
using MVSDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static CameraSDK.CameraAction;
using static CameraSDK.MindVision.MindVisionUtils;
using static GL.Kit.Log.ActionResult;
using CameraHandle = System.Int32;

namespace CameraSDK.MindVision
{
    class MindVisionCameraFactory : BaseSingleBrandCameraFac
    {
        readonly IGLog log;

        const string BrandModel = "MindVision相机";

        public MindVisionCameraFactory(IGLog log)
        {
            this.log = log;
        }

        public override List<ComCameraInfo> SearchDevice()
        {
            log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Start));

            try
            {
                CameraSdkStatus status;
                tSdkCameraDevInfo[] tCameraDevInfoList;
                status = MvApi.CameraEnumerateDevice(out tCameraDevInfoList);
                //此时iCameraCounts返回了实际连接的相机个数。
                if (status == CameraSdkStatus.CAMERA_STATUS_SUCCESS && tCameraDevInfoList != null)
                {
                    List<ComCameraInfo> cameraInfoList = new List<ComCameraInfo>();

                    for (int i = 0; i < tCameraDevInfoList.Count(); i++)
                    {
                        CameraHandle m_hCamera = 0;// 句柄
                        tSdkCameraDevInfo DevInfo = tCameraDevInfoList[i];
                        MvApi.CameraInit(ref DevInfo, -1, -1, ref m_hCamera);

                        //转换信息
                        MDVCameraDevInfo MDV_Info = ConvertCameraInfo(DevInfo);

                        //组织信息
                        byte[] FriendlyNameByte = new byte[32];
                        MvApi.CameraGetFriendlyName(m_hCamera, FriendlyNameByte);
                        string FriendlyName = Encoding.UTF8.GetString(FriendlyNameByte).Substring(0, Encoding.UTF8.GetString(FriendlyNameByte).IndexOf('\0'));
                        ComCameraInfo cameraInfo = new ComCameraInfo(CameraBrand.MindVision)
                        {
                            ConnectionType = MindVisionUtils.ToMyType(MDV_Info.acPortType),
                            SN = MDV_Info.acSn,
                            UserDefinedName = FriendlyName,
                            Model = MDV_Info.acProductSeries
                        };

                        //网口信息
                        if (cameraInfo.ConnectionType == ConnectionType.GigE)
                        {
                            status = MvApi.CameraGigeGetIp(ref DevInfo, out string CamIp, out string CamSubNet, out string CamGateway, out string EIp, out string ESubNet, out string EGateway);
                            if (status == CameraSdkStatus.CAMERA_STATUS_SUCCESS)
                            {
                                cameraInfo.IP = CamIp.ToString();
                                cameraInfo.SubnetMask = CamSubNet.ToString();
                                cameraInfo.DefaultGateway = CamGateway.ToString();
                            }
                        }

                        //添加至列表
                        cameraInfoList.Add(cameraInfo);
                        log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Success, "发现相机：" + cameraInfo.ToString()));

                        //该相机是以Init和UnInit为相机实际的的开启和关闭功能，在搜索的时候Init过了，在相机内再访问相机会AccessDeny
                        //此处UnInit后，即可在相机内再按正常逻辑访问。
                        MvApi.CameraUnInit(m_hCamera);

                        //如果存在的相机，不再创建对象
                        if (cameraDict.ContainsKey(MDV_Info.acSn)) continue;

                        MindVisionCamera camera = new MindVisionCamera(m_hCamera, DevInfo, MDV_Info, log, cameraInfo);
                        cameraDict[cameraInfo.SN] = camera;
                    }

                    log?.Info(new CameraLogMessage(BrandModel, A_Search, R_End));
                    return cameraInfoList;
                }
                else
                {
                    log?.Info(new CameraLogMessage(BrandModel, A_Search, R_End, "未发现相机。没有找到相机，如果已经接上相机，可能是权限不够，请尝试使用管理员权限运行程序。"));
                    return new List<ComCameraInfo>(0);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }




    }
}

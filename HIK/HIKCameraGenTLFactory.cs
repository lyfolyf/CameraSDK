using CameraSDK.Models;
using GL.Kit.Log;
using MvCamCtrl.NET;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using static GL.Kit.Log.ActionResult;
using static CameraSDK.CameraAction;

namespace CameraSDK.HIK
{
    class HIKCameraGenTLFactory : BaseSingleBrandCameraFac
    {
        readonly IGLog log;

        const string defCtiPath = @"C:\Program Files\KAYA Instruments\Common\bin\KYFGLibGenTL_vc141.cti";

        string ctiPath;

        const string BrandModel = CameraAlias.HIK + "(CXP) 相机";

        public HIKCameraGenTLFactory(string ctiPath, IGLog log)
        {
            this.log = log;

            this.ctiPath = string.IsNullOrEmpty(ctiPath) ? defCtiPath : ctiPath;
        }

        MyCamera.MV_GENTL_IF_INFO_LIST m_stIFInfoList = new MyCamera.MV_GENTL_IF_INFO_LIST();

        public override List<ComCameraInfo> SearchDevice()
        {
            if (!File.Exists(ctiPath))
            {
                log?.Error(new CameraLogMessage(BrandModel, A_Search, R_Fail, "cti 文件不存在"));
                throw new CameraSDKException("cti 文件不存在");
            }

            //foreach (HIKCameraGenTL camera in cameraDict.Values)
            //{
            //    if (camera.IsOpen)
            //        camera.Close();
            //}

            log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Start));

            List<ComCameraInfo> cameraInfoList = new List<ComCameraInfo>();

            MyCamera.MV_GENTL_DEV_INFO_LIST m_stDeviceList = new MyCamera.MV_GENTL_DEV_INFO_LIST();

            List<GenTLPort> genTLPorts = GetGenTLPorts();

            foreach (var port in genTLPorts)
            {
                MyCamera.MV_GENTL_IF_INFO stIFInfo = (MyCamera.MV_GENTL_IF_INFO)Marshal.PtrToStructure(
                    m_stIFInfoList.pIFInfo[port.Index], typeof(MyCamera.MV_GENTL_IF_INFO));

                int nRet = MyCamera.MV_CC_EnumDevicesByGenTL_NET(ref stIFInfo, ref m_stDeviceList);
                if (0 != nRet)
                {
                    log?.Error(new CameraLogMessage(BrandModel, A_Search, R_Fail, "枚举设备失败"));
                    throw new CameraSDKException("搜索相机失败");
                }

                for (int i = 0; i < m_stDeviceList.nDeviceNum; i++)
                {
                    MyCamera.MV_GENTL_DEV_INFO device = (MyCamera.MV_GENTL_DEV_INFO)Marshal.PtrToStructure(m_stDeviceList.pDeviceInfo[i], typeof(MyCamera.MV_GENTL_DEV_INFO));

                    ComCameraInfo cameraInfo = new ComCameraInfo(CameraBrand.HIK_GenTL)
                    {
                        ConnectionType = ConnectionType.GenTL,
                        SN = device.chSerialNumber,
                        UserDefinedName = device.chUserDefinedName,
                        Model = device.chModelName
                    };

                    HIKCameraGenTL camera = new HIKCameraGenTL(m_stDeviceList.pDeviceInfo[i], log, cameraInfo);

                    if (cameraDict.ContainsKey(cameraInfo.SN)) continue;

                    cameraDict[cameraInfo.SN] = camera;

                    log?.Info(new CameraLogMessage(BrandModel, A_Search, R_Success, "发现相机：" + cameraInfo.ToString()));

                    cameraInfoList.Add(cameraInfo);
                }
            }

            return cameraInfoList;
        }

        List<GenTLPort> GetGenTLPorts()
        {
            int nRet = MyCamera.MV_CC_EnumInterfacesByGenTL_NET(ref m_stIFInfoList, ctiPath);
            if (0 != nRet)
            {
                log?.Error(new CameraLogMessage(BrandModel, A_Search, R_Fail, HIKErrorCode.ErrorMessage(nRet)));
                return new List<GenTLPort>();
                //throw new Exception("获取 GenTL 接口失败");
            }

            List<GenTLPort> ports = new List<GenTLPort>();

            for (int i = 0; i < m_stIFInfoList.nInterfaceNum; i++)
            {
                MyCamera.MV_GENTL_IF_INFO stIFInfo = (MyCamera.MV_GENTL_IF_INFO)Marshal.PtrToStructure(
                    m_stIFInfoList.pIFInfo[i], typeof(MyCamera.MV_GENTL_IF_INFO));

                GenTLPort port = new GenTLPort
                {
                    Index = i,
                    TLType = stIFInfo.chTLType,
                    Port = stIFInfo.chInterfaceID,
                    DisplayName = stIFInfo.chDisplayName
                };

                ports.Add(port);
            }

            return ports;
        }
    }

    public class GenTLPort
    {
        public int Index { get; set; }

        public string TLType { get; set; }

        public string Port { get; set; }

        public string DisplayName { get; set; }
    }
}

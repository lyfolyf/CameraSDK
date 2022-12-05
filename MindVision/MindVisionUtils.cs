using CameraSDK.Models;
using MVSDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CameraSDK.MindVision
{
    static class MindVisionUtils
    {
        public struct MDVCameraDevInfo
        {
            public string acProductSeries;
            public string acProductName;
            public string acFriendlyName;
            public string acLinkName;
            public string acDriverVersion;
            public string acSensorType;
            public string acPortType;
            public string acSn;
            public uint uInstance;
        }

        public static ConnectionType ToMyType(string deviceType)
        {
            if (deviceType.Contains("USB3.0"))
                return ConnectionType.U3;
            else
                return ConnectionType.GigE;

            //switch (deviceType)
            //{
            //    case "1":
            //        return ConnectionType.GigE;
            //    case "USB3.0":
            //        return ConnectionType.U3;
            //    default:
            //        throw new CameraSDKException("未知的设备类型");
            //}
        }

        public static MDVCameraDevInfo ConvertCameraInfo(tSdkCameraDevInfo info)
        {
            MDVCameraDevInfo res = new MDVCameraDevInfo()
            {
                acProductSeries = Encoding.UTF8.GetString(info.acProductSeries).Substring(0, Encoding.UTF8.GetString(info.acProductSeries).IndexOf('\0')),
                acProductName = Encoding.UTF8.GetString(info.acProductName).Substring(0, Encoding.UTF8.GetString(info.acProductName).IndexOf('\0')),
                acFriendlyName = Encoding.UTF8.GetString(info.acFriendlyName).Substring(0, Encoding.UTF8.GetString(info.acFriendlyName).IndexOf('\0')),
                acLinkName = Encoding.UTF8.GetString(info.acLinkName).Substring(0, Encoding.UTF8.GetString(info.acLinkName).IndexOf('\0')),
                acDriverVersion = Encoding.UTF8.GetString(info.acDriverVersion).Substring(0, Encoding.UTF8.GetString(info.acDriverVersion).IndexOf('\0')),
                acSensorType = Encoding.UTF8.GetString(info.acSensorType).Substring(0, Encoding.UTF8.GetString(info.acSensorType).IndexOf('\0')),
                acPortType = Encoding.UTF8.GetString(info.acPortType).Substring(0, Encoding.UTF8.GetString(info.acPortType).IndexOf('\0')),
                acSn = Encoding.UTF8.GetString(info.acSn).Substring(0, Encoding.UTF8.GetString(info.acSn).IndexOf('\0')),
                uInstance = info.uInstance
            };
            return res;
        }


    }
}

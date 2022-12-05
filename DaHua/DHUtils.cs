using CameraSDK.Models;
using ThridLibray;

namespace CameraSDK.DaHua
{
    static class DHUtils
    {
        public static ConnectionType ToMyType(uint deviceType)
        {
            switch (deviceType)
            {
                case DeviceType.DEVICE_TYPE_GIGE:
                    return ConnectionType.GigE;
                case DeviceType.DEVICE_TYPE_U3V:
                    return ConnectionType.U3;
                default:
                    throw new CameraSDKException("未知的设备类型");
            }
        }
    }

}

using CameraSDK.Models;
using SpinnakerNET.GenApi;

namespace CameraSDK.FLIR
{
    static class FLIRUtils
    {
        public static ConnectionType ToMyType(Enumeration deviceType)
        {
            if (deviceType.Value.String == "USB3Vision")
            {
                return ConnectionType.U3;
            }
            else
                throw new CameraSDKException("未知的设备类型");
        }
    }
}

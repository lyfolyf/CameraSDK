using CameraSDK.Models;
using System;

namespace CameraSDK.Basler
{
    static class BaslerUtils
    {
        /// <summary>
        /// if >= Sfnc2_0_0,说明是 USB3 的相机
        /// </summary>
        static readonly Version Sfnc2_0_0 = new Version(2, 0, 0);

        // 获取连接方式
        public static ConnectionType GetConnectionType(Version version)
        {
            // 这里不一定准确，暂时不会出错
            if (version >= Sfnc2_0_0)
                return ConnectionType.U3;
            else
                return ConnectionType.GigE;
        }
    }
}

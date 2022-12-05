using System;

namespace CameraSDK.Models
{
    /// <summary>
    /// 相机信息
    /// </summary>
    [Serializable]
    public class ComCameraInfo
    {
        /// <summary>
        /// 连接方式（网口/U口）
        /// </summary>
        public ConnectionType ConnectionType { get; internal set; }

        /// <summary>
        /// 相机品牌
        /// </summary>
        public CameraBrand Brand { get; }

        public string SN { get; internal set; }

        public string UserDefinedName { get; set; }

        /// <summary>
        /// 型号
        /// </summary>
        public string Model { get; set; }

        public string IP { get; set; }

        /// <summary>
        /// 子网掩码
        /// </summary>
        public string SubnetMask { get; set; }

        /// <summary>
        /// 默认网关
        /// </summary>
        public string DefaultGateway { get; set; }

        /// <summary>
        /// 图像缓存节点数
        /// </summary>
        public int ImageCacheCount { get; set; } = 1;

        public ComCameraInfo(CameraBrand brand)
        {
            Brand = brand;
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(UserDefinedName))
                return $"{Brand}, {ConnectionType}, {SN}";
            else
                return $"{UserDefinedName}";
        }

        public ComCameraInfo Clone()
        {
            return new ComCameraInfo(Brand)
            {
                ConnectionType = ConnectionType,
                SN = SN,
                UserDefinedName = UserDefinedName,
                Model = Model,
                IP = IP,
                SubnetMask = SubnetMask,
                DefaultGateway = DefaultGateway,
                ImageCacheCount = ImageCacheCount
            };
        }
    }
}

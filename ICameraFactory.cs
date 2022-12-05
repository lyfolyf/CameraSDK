using CameraSDK.Models;
using System.Collections.Generic;

namespace CameraSDK
{
    public interface ICameraFactory
    {
        /// <summary>
        /// 查找相机
        /// </summary>
        /// <returns>相机列表</returns>
        List<ComCameraInfo> SearchDevice();

        ICamera GetCamera(ComCameraInfo cameraInfo);
    }

    abstract class BaseSingleBrandCameraFac : ICameraFactory
    {
        readonly protected Dictionary<string, ICamera> cameraDict = new Dictionary<string, ICamera>();

        public ICamera GetCamera(ComCameraInfo cameraInfo)
        {
            if (!cameraDict.ContainsKey(cameraInfo.SN))
            {
                SearchDevice();
            }

            if (cameraDict.ContainsKey(cameraInfo.SN))
            {
                return cameraDict[cameraInfo.SN];
            }

            return null;
        }

        public abstract List<ComCameraInfo> SearchDevice();
    }
}

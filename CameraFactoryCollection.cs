using CameraSDK.Basler;
using CameraSDK.DaHua;
using CameraSDK.FLIR;
using CameraSDK.HIK;
using CameraSDK.MindVision;
using CameraSDK.Models;
using GL.Kit.Log;
using System.Collections.Generic;
using System.Linq;

namespace CameraSDK
{
    public interface ICameraFactoryCollection : ICameraFactory
    {
        void SetCameraBrands(IEnumerable<CameraBrand> brands);

        void SetCtiPath(string ctiPath);

        /// <summary>
        /// 设置相机参数的启用与否
        /// </summary>
        void SetParamInfos(IEnumerable<CameraParamInfoList> paramInfoList);
    }

    /// <summary>
    /// 包含所有品牌相机
    /// </summary>
    public class CameraFactory : ICameraFactoryCollection
    {
        readonly IGLog log;

        readonly Dictionary<CameraBrand, ICameraFactory> cameraDict = new Dictionary<CameraBrand, ICameraFactory>();

        Dictionary<string, List<CameraParamInfo>> paramInfoDict;

        List<CameraBrand> cameraBrands;

        public IReadOnlyCollection<CameraBrand> CameraBrands
        {
            get { return cameraBrands.AsReadOnly(); }
        }

        string ctiPath;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="log">日志接口，为 null 不输出日志</param>
        /// <param name="cameraBrands">相机品牌</param>
        public CameraFactory(IGLog log)
        {
            this.log = log;
        }

        // 启动任务的时候初始化相机，会并发搜索相机，所以要加个锁
        readonly object sync = new object();

        public ICamera GetCamera(ComCameraInfo cameraInfo)
        {
            if (!cameraBrands.Contains(cameraInfo.Brand))
            {
                log?.Error(new CameraLogMessage(string.Empty, CameraAction.A_GetCamera, ActionResult.R_Fail, "未加载指定品牌的相机"));
                return null;
            }

            ICameraFactory cameraFactory;
            ICamera camera;

            lock (sync)
            {
                cameraFactory = GetCameraFactory(cameraInfo.Brand);
                camera = cameraFactory.GetCamera(cameraInfo);
            }

            if (camera != null)
            {
                camera.ImageCacheCount = cameraInfo.ImageCacheCount;

                if (paramInfoDict != null && paramInfoDict.ContainsKey(cameraInfo.SN))
                {
                    camera.SetParamInfos(paramInfoDict[cameraInfo.SN]);
                }
            }

            return camera;
        }

        public void SetCameraBrands(IEnumerable<CameraBrand> brands)
        {
            cameraBrands = brands?.ToList() ?? new List<CameraBrand>();
        }

        public void SetCtiPath(string ctiPath)
        {
            this.ctiPath = ctiPath;
        }

        public void SetParamInfos(IEnumerable<CameraParamInfoList> paramInfoList)
        {
            paramInfoDict = paramInfoList?.ToDictionary(key => key.SN, value => value.ParamInfos);
        }

        public List<ComCameraInfo> SearchDevice()
        {
            List<ComCameraInfo> cameraInfoList = new List<ComCameraInfo>();

            foreach (var brand in cameraBrands)
            {
                ICameraFactory cameraFactory = GetCameraFactory(brand);

                cameraInfoList.AddRange(cameraFactory.SearchDevice());
            }

            return cameraInfoList;
        }

        ICameraFactory GetCameraFactory(CameraBrand brand)
        {
            ICameraFactory cameraFactory;

            if (cameraDict.ContainsKey(brand))
            {
                cameraFactory = cameraDict[brand];
            }
            else
            {
                if (brand == CameraBrand.HIK_Gige)
                {
                    cameraFactory = new HIKCameraGigEFactory(log);
                }
                else if (brand == CameraBrand.HIK_GenTL)
                {
                    cameraFactory = new HIKCameraGenTLFactory(ctiPath, log);
                }
                else if (brand == CameraBrand.Basler)
                {
                    cameraFactory = new BaslerCameraFactory(log);
                }
                else if (brand == CameraBrand.FLIR)
                {
                    cameraFactory = new FLIRCameraFactory(log);
                }
                else if (brand == CameraBrand.DaHua)
                {
                    cameraFactory = new DHCameraFactory(log);
                }
                else if (brand == CameraBrand.Avt)
                {
                    cameraFactory = new Avt.AvtCameraFactory(log);
                }
                else if (brand == CameraBrand.MindVision)
                {
                    cameraFactory = new MindVisionCameraFactory(log);
                }
                else
                {
                    throw new CameraSDKException("无效的相机品牌");
                }

                cameraDict[brand] = cameraFactory;
            }

            return cameraFactory;
        }

    }
}

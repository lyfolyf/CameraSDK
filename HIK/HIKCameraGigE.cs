using CameraSDK.Models;
using GL.Kit.Log;
using MvCamCtrl.NET;
using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using static GL.Kit.Log.ActionResult;
using static CameraSDK.CameraAction;

namespace CameraSDK.HIK
{
    /// <summary>
    /// HIK 相机（网口和U口）
    /// </summary>
    public class HIKCameraGigE : BaseHIKCamera_New
    {
        /// <summary>
        /// 设置 IP 后触发
        /// </summary>
        internal event Action Reset;

        MyCamera.cbExceptiondelegate pCallBackFunc;

        public HIKCameraGigE(IntPtr deviceInfo, IGLog log, ComCameraInfo cameraInfo)
            : base(deviceInfo, log, cameraInfo)
        {
            pCallBackFunc = new MyCamera.cbExceptiondelegate(cbExceptionDelegate);

            paramInfos.PreampGain.Enabled = false;
        }

        // 异常回调委托
        void cbExceptionDelegate(uint nMsgType, IntPtr pUser)
        {
            if (nMsgType == MyCamera.MV_EXCEPTION_DEV_DISCONNECT)
            {
                Reconnect();
            }
        }

        #region 设置 IP

        public override bool SetIP(string ip, string subnetMask, string defaultGateway)
        {
            if (CameraInfo.ConnectionType == ConnectionType.U3)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "U3 口相机不可以设置 IP"));
                throw new CameraSDKException("U3 口相机不可以设置 IP");
            }

            if (IsOpen)
            {
                throw new CameraSDKException("请先关闭相机");
            }

            if (m_camera == null)
            {
                m_camera = new MyCamera();
            }

            if (CreateDevice() == false) return false;

#pragma warning disable CS0618 // 类型或成员已过时
            long nIp = IPAddress.NetworkToHostOrder(IPAddress.Parse(ip).Address);
            long nSubMask = IPAddress.NetworkToHostOrder(IPAddress.Parse(subnetMask).Address);
            long nDefaultWay = IPAddress.NetworkToHostOrder(IPAddress.Parse(defaultGateway).Address);
#pragma warning restore CS0618 // 类型或成员已过时

            int nRet = m_camera.MV_GIGE_ForceIpEx_NET((uint)(nIp >> 32), (uint)(nSubMask >> 32), (uint)(nDefaultWay >> 32));
            if (MyCamera.MV_OK == nRet)
            {
                CameraInfo.IP = ip;
                CameraInfo.SubnetMask = subnetMask;
                CameraInfo.DefaultGateway = defaultGateway;

                log?.Info(new CameraLogMessage
                {
                    CameraName = CameraInfo.ToString(),
                    Action = A_SetParam,
                    ActionResult = R_Success,
                    Message = $"IP = {CameraInfo.IP}，子网掩码 = {CameraInfo.SubnetMask}"
                });

                Reset?.Invoke();

                return true;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, HIKErrorCode.ErrorMessage(nRet)));
                throw new CameraSDKException("设置相机 IP 失败");
            }
        }

        #endregion

        #region 打开/关闭

        /// <summary>
        /// 打开相机
        /// </summary>
        public override void Open()
        {
            if (IsOpen) return;

            if (m_camera == null)
            {
                m_camera = new MyCamera();
            }

            if (CreateDevice() == false) return;

            if (OpenDevice() == false) return;

            RegisterExceptionCallBack();

            SetGigE();

            SetEnumParam(paramInfos.ExposureAuto, ExposureAuto_HIK.Off);
            // SetEnumParam(paramInfos.GainAuto, GainAuto_HIK.Off);
        }

        bool CreateDevice()
        {
            MyCamera.MV_CC_DEVICE_INFO device = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(m_deviceInfo, typeof(MyCamera.MV_CC_DEVICE_INFO));

            int nRet = m_camera.MV_CC_CreateDevice_NET(ref device);
            if (MyCamera.MV_OK != nRet)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_Open, R_Fail, HIKErrorCode.ErrorMessage(nRet)));
                throw new CameraSDKException("打开相机失败");
            }

            return true;
        }

        void SetGigE()
        {
            if (CameraInfo.ConnectionType == ConnectionType.GigE)
            {
                // 设置心跳超时时间
                SetIntParam(paramInfos.HeartbeatTimeout, 500);

                // 探测网络最佳包大小
                int nPacketSize = m_camera.MV_CC_GetOptimalPacketSize_NET();
                if (nPacketSize > 0)
                {
                    SetIntParam(paramInfos.PacketSize, (uint)nPacketSize);
                }
                else
                {
                    log?.Warn(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "读取最佳网络包大小失败"));
                }
            }
        }

        #endregion

        #region 重连

        const int Interval = 5;

        // 重连
        void Reconnect()
        {
            ForcedStop();

            CloseCamera();

            log?.Error(new CameraLogMessage(CameraInfo, A_Connect, R_Disconnect, "开始自动重连"));

            MyCamera.MV_CC_DEVICE_INFO device = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(m_deviceInfo, typeof(MyCamera.MV_CC_DEVICE_INFO));

            while (IsOpen)
            {
                int nRet = m_camera.MV_CC_CreateDevice_NET(ref device);
                if (MyCamera.MV_OK != nRet)
                {
                    Thread.Sleep(Interval);
                    continue;
                }

                nRet = m_camera.MV_CC_OpenDevice_NET();
                if (MyCamera.MV_OK != nRet)
                {
                    Thread.Sleep(Interval);
                    m_camera.MV_CC_DestroyDevice_NET();
                    continue;
                }
                else
                {
                    offLine = false;

                    log?.Info(new CameraLogMessage(CameraInfo, A_Connect, R_Success));

                    RegisterExceptionCallBack();

                    if (m_running)
                    {
                        m_running = false;
                        Start(TriggerMode, TriggerSource);
                    }

                    break;
                }
            }

            if (!IsOpen)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_Connect, R_Fail, "相机关闭，退出自动重连"));
            }
        }

        // 注册异常回调
        int RegisterExceptionCallBack()
        {
            int nRet = m_camera.MV_CC_RegisterExceptionCallBack_NET(pCallBackFunc, IntPtr.Zero);
            if (MyCamera.MV_OK == nRet)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_Register, R_Success, "开启断线重连机制"));
            }
            else
            {
                log?.Error(new CameraLogMessage
                {
                    CameraName = CameraInfo.ToString(),
                    Action = A_Register,
                    ActionResult = R_Fail,
                    Message = "注册异常回调函数失败，" + HIKErrorCode.ErrorMessage(nRet)
                });
            }
            GC.KeepAlive(pCallBackFunc);

            return nRet;
        }

        #endregion

    }
}

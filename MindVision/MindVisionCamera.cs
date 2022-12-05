using CameraSDK.Models;
using GL.Kit.Log;
using MVSDK;
using System;
using System.Collections.Generic;
using CameraHandle = System.Int32;
using static CameraSDK.CameraAction;
using static GL.Kit.Log.ActionResult;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Diagnostics;
using static CameraSDK.MindVision.MindVisionUtils;

namespace CameraSDK.MindVision
{
    class MindVisionCamera : BaseCamera, ICamera, IDisposable
    {
        CameraHandle m_hCamera;
        //CameraSdkStatus status = 0;
        tSdkCameraDevInfo devInfo;
        MDVCameraDevInfo MDV_devInfo;
        tSdkCameraCapbility cap;
        IntPtr m_Grabber = IntPtr.Zero;
        CAMERA_CONNECTION_STATUS_CALLBACK m_ConStatusCallBack;
        //图像回调函数的上下文参数
        IntPtr m_ConStatusCallbackCtx;

        public MindVisionCamera(CameraHandle m_hCamera, tSdkCameraDevInfo devInfo, MDVCameraDevInfo MDV_devInfo, IGLog log, ComCameraInfo cameraInfo)
            : base(cameraInfo, log)
        {
            this.m_hCamera = m_hCamera;
            this.devInfo = devInfo;
            this.MDV_devInfo = MDV_devInfo;

            paramInfos = new CameraParamInfoCollection();
        }

        #region 打开/关闭

        /// <summary>
        /// 打开
        /// </summary>
        public void Open()
        {
            if (m_hCamera == 0)
                return;
            if (IsOpen)
                return;

            CameraSdkStatus Init_status = MvApi.CameraInit(ref devInfo, -1, -1, ref m_hCamera);

            if (Init_status == 0)
            {
                IsOpen = true;
                log?.Info(new CameraLogMessage(CameraInfo, A_Open, R_Success));

                MvApi.CameraGetCapability(m_hCamera, out cap);
                // 黑白相机设置ISP输出灰度图像。彩色相机ISP默认会输出BGR24图像
                //非MonoSensor设置为CAMERA_MEDIA_TYPE_MONO8
                if (cap.sIspCapacity.bMonoSensor != 0)
                {
                    MvApi.CameraSetIspOutFormat(m_hCamera, (uint)MVSDK.emImageFormat.CAMERA_MEDIA_TYPE_MONO8);
                    isGrey = true;
                }

                m_ConStatusCallBack = new CAMERA_CONNECTION_STATUS_CALLBACK(ConStatusCallBack);
                MvApi.CameraSetConnectionStatusCallback(m_hCamera, m_ConStatusCallBack, m_ConStatusCallbackCtx);
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_Open, R_Fail, $"打开相机失败：{Init_status}"));
                throw new CameraSDKException("相机打开失败");
            }
        }

        /// <summary>
        /// 关闭
        /// </summary>
        public void Close()
        {
            if (m_hCamera == 0)
                return;
            if (!IsOpen)
                return;
            if (m_running)
                Stop();

            CameraSdkStatus SavePara_status = MvApi.CameraSaveParameter(m_hCamera, 0);

            if (SavePara_status == 0)
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, "参数持久化到相机内成功"));

            CameraSdkStatus UnInit_status = MvApi.CameraUnInit(m_hCamera);

            if (UnInit_status == 0)
            {
                IsOpen = false;
                IsLoss = false;
                log?.Info(new CameraLogMessage(CameraInfo, A_Close, R_Success));
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_Close, R_Fail, "关闭相机失败"));
            }
        }

        public void Dispose()
        {
            Close();
        }

        #endregion

        #region 开始/停止

        CancellationTokenSource cts;
        Task acqImageTask;

        /// <summary>
        /// 开始
        /// </summary>
        /// <param name="triggerMode"></param>
        /// <param name="triggerSource"></param>
        protected override void Start2(TriggerMode triggerMode, TriggerSource triggerSource)
        {
            if (m_hCamera == 0)
                return;
            if (!IsOpen)
                return;
            //掉线状态禁止开启采集
            if (IsLoss)
                return;

            CameraSdkStatus play_status = MvApi.CameraPlay(m_hCamera);

            if (play_status == 0)
            {
                m_running = true;

                log?.Info(new CameraLogMessage(CameraInfo, A_StartGrabbing, R_Success));

                cts = new CancellationTokenSource();
                acqImageTask = new Task(() => AcquireImages(cts.Token), cts.Token, TaskCreationOptions.LongRunning);
                acqImageTask.Start();
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_StartGrabbing, R_Fail, $"开始采集，失败，ErrorCode ：{play_status}"));
                throw new CameraSDKException("开始采集失败");
            }
        }

        /// <summary>
        /// 停止
        /// </summary>
        protected override void Stop2()
        {
            if (m_hCamera == 0)
                return;
            if (!IsOpen)
                return;
            //掉线状态禁止停止采集，掉线回调有执行强制停止
            if (IsLoss)
                return;

            cts.Cancel();

            // 函数名   : CameraPause
            // 功能描述 : 让SDK进入暂停模式，不接收来自相机的图像数据，
            //            同时也会发送命令让相机暂停输出，释放传输带宽。
            //            暂停模式下，可以对相机的参数进行配置，并立即生效。  
            //CameraSdkStatus eStatus = MvApi.CameraPause(m_hCamera);

            // 函数名   : CameraStop
            // 功能描述 : 让SDK进入停止状态，一般是反初始化时调用该函数，
            //            该函数被调用，不能再对相机的参数进行配置。

            //根据我方需求还是应该用Stop
            CameraSdkStatus stop_status = MvApi.CameraStop(m_hCamera);
            if (stop_status == 0)
                log?.Info(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Success));
            else
                log?.Error(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Fail));
        }

        /// <summary>
        /// 强制停止
        /// </summary>
        /// <param name="camera"></param>
        private void ForcedStop(int camera)
        {
            cts.Cancel();

            try
            {
                if (MvApi.CameraStop(camera) == 0)
                    log?.Info(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Success));
                else
                    log?.Error(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Fail));
            }
            catch (Exception ex)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Fail, ex.Message));
            }
        }

        #endregion

        #region 断线\重连 回调

        bool IsLoss;
        /// <summary>
        /// 断线重连状态回调
        /// </summary>
        /// <param name="hCamera"></param>
        /// <param name="MSG"></param>
        /// <param name="uParam"></param>
        /// <param name="pContext"></param>
        private void ConStatusCallBack(int hCamera, uint MSG, uint uParam, IntPtr pContext)
        {
            switch (MSG)
            {
                case 0:
                    {
                        IsLoss = true;
                        ForcedStop(hCamera);
                        log?.Error(new CameraLogMessage(CameraInfo, A_Connect, R_Disconnect, "相机连接断开"));
                    }
                    break;
                case 1:
                    {
                        IsLoss = false;
                        SetTriggerSource(TriggerSource);
                        SetTriggerMode(TriggerMode);
                        Start2(TriggerMode, TriggerSource);
                        log?.Info(new CameraLogMessage(CameraInfo, A_Connect, R_Success, "相机连接恢复"));
                    }
                    break;
            }
        }

        #endregion 

        #region 接收图像

        ushort idx = 0;
        DateTime acqBaseTime;
        /// <summary>
        /// 主动取图
        /// </summary>
        /// <param name="token"></param>
        void AcquireImages(CancellationToken token)
        {
            CameraSdkStatus Img_status;
            tSdkFrameHead FrameHead;
            IntPtr uRawBuffer;//rawbuffer由SDK内部申请。应用层不要调用delete之类的释放函数

            while (!token.IsCancellationRequested)
            {
                if (IsOpen)
                {
                    //1000毫秒超时,图像没捕获到前，线程会被挂起,释放CPU，所以该线程中无需调用sleep
                    Img_status = MvApi.CameraGetImageBuffer(m_hCamera, out FrameHead, out uRawBuffer, 1000);

                    if (Img_status == CameraSdkStatus.CAMERA_STATUS_SUCCESS)//如果是触发模式，则有可能超时
                    {
                        idx++;

                        MvApi.CameraGetFrameID(m_hCamera, out uint fn);

                        if (TriggerMode == TriggerMode.Trigger)
                            log?.Debug(new CameraLogMessage(CameraInfo, A_AcqImage, R_Success, $"idx = {idx}  FrameNum ={fn}"));

                        // 由于SDK输出的数据默认是从底到顶的，转换为Bitmap需要做一下垂直镜像
                        MvApi.CameraFlipFrameBuffer(uRawBuffer, ref FrameHead, 1);

                        Bitmap bmp = GetImage(FrameHead, uRawBuffer);

                        //成功调用CameraGetImageBuffer后必须释放，下次才能继续调用CameraGetImageBuffer捕获图像。
                        MvApi.CameraReleaseImageBuffer(m_hCamera, uRawBuffer);

                        DateTime acqTime;
                        if (idx == 1)
                            acqBaseTime = acqTime = DateTime.Now.AddTicks(-(FrameHead.uiTimeStamp * 1000));
                        else
                            acqTime = acqBaseTime.AddTicks(FrameHead.uiTimeStamp * 1000);

                        ReceivedImage(bmp, (int)fn, acqTime);
                    }
                }
            }
        }

        const int CopyImageMaxTime = 100;    /* 拷贝图像的最大耗时，超过则产生一条警告日志 */
        /// <summary>
        /// 图片拷贝
        /// </summary>
        /// <param name="FrameHead"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Bitmap GetImage(tSdkFrameHead FrameHead, IntPtr data)
        {
            (TimeSpan ts, Bitmap bmp) = FuncWatch.ElapsedTime(() =>
            {
                return ImageUtils.DeepCopyIntPtrToBitmap(isGrey.Value, FrameHead.iWidth, FrameHead.iHeight, data);
            });
            if (ts.TotalMilliseconds > CopyImageMaxTime && TriggerMode == TriggerMode.Trigger)
                log?.Warn(new CameraLogMessage(CameraInfo, A_Copy, R_Success, "图像 Copy 耗时过长", ts.TotalMilliseconds));
            else
                log.Debug(new CameraLogMessage(CameraInfo, A_Copy, R_Success, null, ts.TotalMilliseconds));

            return bmp;
        }

        #endregion


        #region 参数获取/设置

        CameraParams m_cameraParams = new CameraParams(); /* 相机参数缓存 */
        protected CameraParamInfoCollection paramInfos;

        /// <summary>
        /// 获取所有参数
        /// </summary>
        /// <returns></returns>
        public CameraParamInfo[] GetParamInfos()
        {
            return paramInfos.All();
        }

        /// <summary>
        /// 设置参数信息
        /// </summary>
        /// <param name="paramInfos"></param>
        public void SetParamInfos(IEnumerable<CameraParamInfo> paramInfos)
        {
            if (paramInfos == null) return;
            foreach (CameraParamInfo param in paramInfos)
            {
                if (this.paramInfos.Contains(param.Name))
                {
                    CameraParamInfo2 p = this.paramInfos.GetParamInfo(param.Name);
                    p.Enabled = param.Enabled;
                    p.ReadOnly = param.ReadOnly;
                }
            }
        }

        /// <summary>
        /// 获取参数
        /// </summary>
        /// <returns></returns>
        public CameraParams GetParams()
        {
            if (m_hCamera == 0)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "未找到相机"));
                return new CameraParams();
            }
            if (!IsOpen)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "相机未打开"));
                return new CameraParams();
            }
            if (IsLoss)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "相机连接断开"));
                return new CameraParams();
            }

            m_cameraParams = new CameraParams();

            double dExpTime = 0;
            CameraSdkStatus exp_status = MvApi.CameraGetExposureTime(m_hCamera, ref dExpTime);
            if (exp_status == 0)
            {
                m_cameraParams.ExposureTime = (float?)dExpTime;
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"获取曝光，成功：{m_cameraParams.ExposureTime}"));
            }
            else
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取曝光，失败，ErrorCode：{exp_status}"));

            int iGain = 0;
            CameraSdkStatus gan_status = MvApi.CameraGetAnalogGain(m_hCamera, ref iGain);
            float fgain = cap.sExposeDesc.fAnalogGainStep * iGain;
            if (gan_status == 0)
            {
                m_cameraParams.Gain = (float?)fgain;
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"获取增益，成功：{m_cameraParams.Gain}"));
            }
            else
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取增益，失败，ErrorCode：{gan_status}"));

            tSdkImageResolution t;
            CameraSdkStatus res_status = MvApi.CameraGetImageResolution(m_hCamera, out t);
            if (res_status == 0)
            {
                m_cameraParams.ImageHeight = (int?)t.iHeight;
                m_cameraParams.ImageWidth = (int?)t.iWidth;
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"获取高度，成功：{m_cameraParams.ImageHeight}"));
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"获取宽度，成功：{m_cameraParams.ImageWidth}"));
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取高度，失败，ErrorCode：{res_status}"));
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取宽度，失败，ErrorCode：{res_status}"));
            }

            uint upDelayTimeUs = 0;
            CameraSdkStatus dt_status = MvApi.CameraGetExtTrigDelayTime(m_hCamera, ref upDelayTimeUs);
            if (dt_status == 0)
            {
                m_cameraParams.TriggerDelay = (float?)upDelayTimeUs;
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"获取拍照延时，成功：{m_cameraParams.TriggerDelay}"));
            }
            else
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取拍照延时，失败，ErrorCode：{dt_status}"));

            return m_cameraParams;
        }

        /// <summary>
        /// 设置参数
        /// </summary>
        /// <param name="params"></param>
        /// <returns></returns>
        public bool SetParams(CameraParams @params)
        {
            if (m_hCamera == 0)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "未找到相机"));
                return false;
            }
            if (!IsOpen)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机未打开"));
                return false;
            }
            if (IsLoss)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "相机连接断开"));
                return false;
            }

            bool result = true;
            if (@params.ExposureTime.HasValue )
            {
                double dExpTime = Convert.ToDouble(@params.ExposureTime.Value);
                CameraSdkStatus exp_status = MvApi.CameraSetExposureTime(m_hCamera, dExpTime);

                if (exp_status == 0)
                {
                    m_cameraParams.ExposureTime = @params.ExposureTime.Value;
                    log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置曝光，成功：{m_cameraParams.ExposureTime}"));
                }
                else
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置曝光，失败，ErrorCode：{exp_status}"));
            }
            if (@params.Gain.HasValue )
            {
                int iGain = (int)(@params.Gain.Value / cap.sExposeDesc.fAnalogGainStep);
                CameraSdkStatus gain_status = MvApi.CameraSetAnalogGain(m_hCamera, iGain);

                if (gain_status == 0)
                {
                    m_cameraParams.Gain = @params.Gain.Value;
                    log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置增益，成功：{m_cameraParams.Gain}"));
                }
                else
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置增益，失败，ErrorCode：{gain_status}"));
            }

            if (@params.ImageHeight.HasValue && @params.ImageHeight != m_cameraParams.ImageHeight)
            {
                tSdkImageResolution t;
                MvApi.CameraGetImageResolution(m_hCamera, out t);
                t.iIndex = 0xff;
                t.iHeight = Convert.ToInt32(@params.ImageHeight.Value);
                t.iHeightFOV = Convert.ToInt32(@params.ImageHeight.Value);
                CameraSdkStatus h_status = MvApi.CameraSetImageResolution(m_hCamera, ref t);

                if (h_status == 0)
                {
                    m_cameraParams.ImageHeight = @params.ImageHeight.Value;
                    log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置高度，成功：{m_cameraParams.ImageHeight}"));
                }
                else
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置高度，失败，ErrorCode：{h_status}"));
            }

            if (@params.ImageWidth.HasValue && @params.ImageWidth != m_cameraParams.ImageWidth)
            {
                tSdkImageResolution t;
                MvApi.CameraGetImageResolution(m_hCamera, out t);
                t.iIndex = 0xff;
                t.iWidth = Convert.ToInt32(@params.ImageWidth.Value);
                t.iWidthFOV = Convert.ToInt32(@params.ImageWidth.Value);
                CameraSdkStatus w_status = MvApi.CameraSetImageResolution(m_hCamera, ref t);

                if (w_status == 0)
                {
                    m_cameraParams.ImageWidth = @params.ImageWidth.Value;
                    log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置宽度，成功：{m_cameraParams.ImageWidth}"));
                }
                else
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置宽度，失败，ErrorCode：{w_status}"));
            }

            if (@params.TriggerDelay.HasValue && @params.TriggerDelay != m_cameraParams.TriggerDelay)
            {
                uint upDelayTimeUs = Convert.ToUInt32(@params.TriggerDelay.Value);
                CameraSdkStatus dt_status = MvApi.CameraSetExtTrigDelayTime(m_hCamera, upDelayTimeUs);

                if (dt_status == 0)
                {
                    m_cameraParams.TriggerDelay = @params.TriggerDelay.Value;
                    log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置拍照延时，成功：{m_cameraParams.TriggerDelay}"));
                }
                else
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置拍照延时，失败，ErrorCode：{dt_status}"));
            }

            return result;
        }

        #endregion


        #region 重命名/设置 IP

        /// <summary>
        /// 重命名昵称
        /// </summary>
        /// <param name="newUserID"></param>
        /// <returns></returns>
        public bool Rename(string newUserID)
        {
            if (m_hCamera == 0)
                throw new CameraSDKException("未找到相机");
            if (!IsOpen)
                throw new CameraSDKException("相机未打开");
            if (IsLoss)
                throw new CameraSDKException("相机连接断开");

            CameraSdkStatus rn_status = MvApi.CameraSetFriendlyName(m_hCamera, Encoding.UTF8.GetBytes(newUserID));

            if (rn_status != 0)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_Rename, R_Fail, $"ErrorCode : {rn_status}"));
                throw new CameraSDKException("设置UserID失败");
            }
            else
            {
                CameraInfo.UserDefinedName = newUserID;
                log?.Info(new CameraLogMessage(CameraInfo, A_Rename, R_Success));
            }

            return true;
        }

        public bool SetCache(int cache)
        {
            CameraSdkStatus Buffer_status = MvApi.CameraSetSysOption("NumBuffers", cache.ToString());
            if (Buffer_status == 0)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"图片缓存 = {cache}"));
                return true;
            }
            else
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"图片缓存设置失败"));
                return false;
            }
        }

        /// <summary>
        /// 设置IP
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="subnetMask"></param>
        /// <param name="defaultGateway"></param>
        /// <returns></returns>
        public bool SetIP(string ip, string subnetMask, string defaultGateway)
        {
            if (m_hCamera == 0)
                throw new CameraSDKException("未找到相机");
            if (!IsOpen)
                throw new CameraSDKException("相机未打开");
            if (IsLoss)
                throw new CameraSDKException("相机连接断开");

            if (CameraInfo.ConnectionType == ConnectionType.U3)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "U3 口相机不可以设置 IP"));
                throw new CameraSDKException("U3 口相机不可以设置 IP");
            }

            CameraSdkStatus ip_status = MvApi.CameraGigeSetIp(ref devInfo, ip, subnetMask, defaultGateway, 1);

            if (ip_status == 0)
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

                return true;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"ErrorCode : {ip_status}"));
                throw new CameraSDKException("设置相机 IP 失败");
            }
        }

        #endregion

        #region 触发模式/触发源

        /// <summary>
        /// 获取触发模式
        /// </summary>
        /// <returns></returns>
        public TriggerMode GetTriggerMode()
        {
            if (m_hCamera == 0)
                throw new CameraSDKException("未找到相机");
            if (!IsOpen)
                throw new CameraSDKException("相机未打开");
            if (IsLoss)
                throw new CameraSDKException("相机连接断开");

            //SDK提供解释；0表示连续采集模式；1表示软件触发模式；2表示硬件触发模式
            int iGrabMode = -1;
            CameraSdkStatus tm_status = MvApi.CameraGetTriggerMode(m_hCamera, ref iGrabMode);

            if (tm_status == 0)
            {
                if (iGrabMode == 0)
                {
                    this.TriggerMode = TriggerMode.Continuous;
                    log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, "获取触发模式成功：连续模式"));
                    return TriggerMode.Continuous;
                }
                else
                {
                    this.TriggerMode = TriggerMode.Trigger;
                    log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, "获取触发模式成功：触发模式"));
                    return TriggerMode.Trigger;
                }
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取触发模式失败，ErrorCode : {tm_status}"));
                throw new CameraSDKException("获取触发模式失败");
            }
        }

        /// <summary>
        /// 设置触发模式
        /// </summary>
        /// <param name="triggerMode"></param>
        public override void SetTriggerMode(TriggerMode triggerMode)
        {
            if (m_hCamera == 0)
                return;
            if (!IsOpen)
                return;
            if (IsLoss)
                return;

            if (triggerMode == TriggerMode.Continuous)
            {
                //SDK提供解释；0表示连续采集模式；1表示软件触发模式；2表示硬件触发模式
                //此相机两个概念合到一起了。只要不是连续模式，就当做触发一次的模式
                CameraSdkStatus tm_status = MvApi.CameraSetTriggerMode(m_hCamera, 0);
                if (tm_status == 0)
                {
                    this.TriggerMode = TriggerMode.Continuous;
                    log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, "设置触发模式成功：连续模式"));
                }
                else
                {
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置触发模式失败，ErrorCode : {tm_status}"));
                }
            }
            else
            {
                CameraSdkStatus tm_status = MvApi.CameraSetTriggerMode(m_hCamera, 1);
                if (tm_status == 0)
                {
                    this.TriggerMode = TriggerMode.Trigger;
                    log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, "设置触发模式成功：触发模式"));
                }
                else
                {
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置触发模式失败，ErrorCode : {tm_status}"));
                }
            }
        }

        /// <summary>
        /// 获取触发源
        /// </summary>
        /// <returns></returns>
        public TriggerSource GetTriggerSource()
        {
            if (m_hCamera == 0)
                throw new CameraSDKException("未找到相机");
            if (!IsOpen)
                throw new CameraSDKException("相机未打开");
            if (IsLoss)
                throw new CameraSDKException("相机连接断开");

            //SDK提供解释；0表示连续采集模式；1表示软件触发模式；2表示硬件触发模式
            int iGrabMode = -1;
            CameraSdkStatus ts_status = MvApi.CameraGetTriggerMode(m_hCamera, ref iGrabMode);

            if (ts_status == 0)
            {
                switch (iGrabMode)
                {
                    case 1:
                        {
                            TriggerSource = TriggerSource.Software;
                            log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, "获取触发源成功：软触发"));
                            return TriggerSource.Software;
                        }
                    case 2:
                        {
                            TriggerSource = TriggerSource.Extern;
                            log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, "获取触发源成功：硬触发"));
                            return TriggerSource.Extern;
                        }
                    default:
                        {
                            //给个默认
                            TriggerSource = TriggerSource.Software;
                            log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, "获取触发源成功：软触发"));
                            return TriggerSource.Software;
                        }
                }
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取触发源失败，ErrorCode : {ts_status}"));
                throw new CameraSDKException("获取触发源失败");
            }
        }

        /// <summary>
        /// 设置触发源
        /// </summary>
        /// <param name="triggerSource"></param>
        public override void SetTriggerSource(TriggerSource triggerSource)
        {
            if (m_hCamera == 0)
                return;
            if (!IsOpen)
                return;
            if (IsLoss)
                return;

            int setTriggerSource;
            string msg;

            switch (triggerSource)
            {
                case TriggerSource.Software:
                    {
                        setTriggerSource = 1;
                        msg = "软触发";
                    }
                    break;
                case TriggerSource.Extern:
                    {
                        setTriggerSource = 2;
                        msg = "硬触发";
                    }
                    break;
                default:
                    {
                        //给个默认
                        setTriggerSource = 1;
                        msg = "软触发";
                    }
                    break;
            }

            //SDK提供解释；0表示连续采集模式；1表示软件触发模式；2表示硬件触发模式
            //此相机两个概念合到一起了。只要不是连续模式，就当做触发一次的模式
            CameraSdkStatus ts_tatus = MvApi.CameraSetTriggerMode(m_hCamera, setTriggerSource);

            if (ts_tatus == 0)
            {
                TriggerSource = triggerSource;
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置触发源成功：{msg}"));
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置触发源失败，ErrorCode : {ts_tatus}"));
            }
        }

        /// <summary>
        /// 执行一次软触发
        /// </summary>
        public void SoftTrigger()
        {
            if (m_hCamera == 0)
                return;
            if (!IsOpen)
                return;
            if (IsLoss)
                return;

            if (TriggerMode == TriggerMode.Continuous)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Fail, "当前为连续采集模式"));
                throw new CameraSDKException("软触发失败，相机当前为连续采集模式");
            }
            if (TriggerSource == TriggerSource.Extern)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Fail, "当前为硬触发模式"));
                throw new CameraSDKException("软触发失败，相机当前为硬触发模式");
            }

            //执行软触发时，会清空相机内部缓存，重新开始曝光取一张图像。
            //厂商解释：CameraSoftTriggerEx相当于CameraClearBuffer + CameraSoftTrigger
            //CameraSoftTrigger可能拿到的是缓存里的旧图，就不用CameraSoftTrigger
            CameraSdkStatus softTrigger_status = MvApi.CameraSoftTriggerEx(m_hCamera, 1);
            if (softTrigger_status == 0)
                log?.Info(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Success));
            else
                log?.Error(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Fail, $"软触发指令不成功，ErrorCode ： {softTrigger_status}"));
        }

        #endregion


    }
}

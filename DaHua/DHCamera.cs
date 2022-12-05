using CameraSDK.Models;
using GL.Kit.Log;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThridLibray;
using static CameraSDK.CameraAction;
using static GL.Kit.Log.ActionResult;

namespace CameraSDK.DaHua
{
    class DHCamera : BaseCamera, ICamera, IDisposable
    {
        /// <summary>
        /// 掉线状态
        /// </summary>
        bool IsLoss = false;                /* 断线重连-标志位，标志是否掉线*/
        Thread checkLossThread = null;      /* 断线重连-检查断线重连线程*/

        const int CopyImageMaxTime = 50;    /* 拷贝图像的最大耗时，超过则产生一条警告日志 */

        IDevice m_camera;
        string m_Key;                       /* 相机KEY */
        internal int index = -1;

        public DHCamera(IDevice camera, IGLog log, ComCameraInfo cameraInfo)
            : base(cameraInfo, log)
        {
            m_camera = camera;

            m_Key = camera.DeviceKey;

            paramInfos = new CameraParamInfoCollection();
        }

        #region 打开/关闭

        public void Open()
        {
            if (IsOpen)
                return;

            if (m_camera == null)
            {
                List<IDeviceInfo> m_deviceInfoList = Enumerator.EnumerateDevices();
                if (!m_deviceInfoList.Any(d => d.Key == m_Key))
                {
                    log?.Error(new CameraLogMessage(CameraInfo, A_Open, R_Fail, "检索不到对应相机"));
                    throw new CameraSDKException("相机打开失败");
                }
                m_camera = Enumerator.GetDeviceByKey(m_Key);
            }

            if (m_camera.Open())
            {
                IsOpen = true;
                /* 此处用于，不是通过断线重连方法里实例化的对象。
                 * 而是在 掉线=》关闭相机=》再连上线后=》打开相机*/
                IsLoss = false;
                log?.Info(new CameraLogMessage(CameraInfo, A_Open, R_Success));

                m_camera.ConnectionLost += OnConnectLost;

                if (CameraInfo.ConnectionType == ConnectionType.GigE)
                {
                    setEnumAttr("EventNotification", "On", "消息回调");
                    m_camera.MsgChannelArgEvent += OnMsgChannel;
                }

                // 这里有 BUG，像素格式是可以修改的
                IsGrey(getEnumAttr(paramInfos.PixelFormat.Name, paramInfos.PixelFormat.Description));
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_Open, R_Fail, "连接相机失败"));
                throw new CameraSDKException("相机打开失败");
            }
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            if (m_running)
                Stop();

            /* 丢失状态的相机，点击关闭按钮，会执行到此
             * 在丢失回调中m_camera已经dispose并置为null
             * 此时先做判断,并将IsOpen设置为空，否则m_camera.close()会报错
               或者IsOpen不是false，等重连上后，IsOpen为true，没法再次打开 */
            if (IsLoss)
            {
                IsOpen = false;
                log?.Info(new CameraLogMessage(CameraInfo, A_Close, R_Success));
                return;
            }
            else
            {
                //m_camera.StreamGrabber.ImageGrabbed -= OnImageGrabbed;

                if (CameraInfo.ConnectionType == ConnectionType.GigE)
                {
                    m_camera.MsgChannelArgEvent -= OnMsgChannel;
                }

                if (m_camera.Close())
                {
                    IsOpen = false;
                    log?.Info(new CameraLogMessage(CameraInfo, A_Close, R_Success));

                    m_camera = null;
                }
                else
                {
                    IsOpen = m_camera.IsOpen;

                    log?.Error(new CameraLogMessage(CameraInfo, A_Close, R_Fail, "关闭相机失败"));
                }
            }
        }

        void CloseCamera()
        {
            m_camera.Close();

            m_camera.Dispose();

            m_camera = null;
        }

        #endregion

        #region 开始/停止

        CancellationTokenSource cts;
        Task acqImageTask;

        protected override void Start2(TriggerMode triggerMode, TriggerSource triggerSource)
        {
            // 大华相机在开始采集状态下设置此参数会报错
            setEnumAttr(paramInfos.AcquisitionMode.Name, "Continuous", paramInfos.AcquisitionMode.Description);

            SetImageCacheCount(ImageCacheCount == 0 ? 2 : ImageCacheCount);

            /* 主动取图方式开启码流
               GrabStrategyEnum.grabStrartegySequential : 按顺序取图
               GrabStrategyEnum.grabStrartegyLatestImage : 取SDK图像缓存队列里最新一帧图片 */
            if (m_camera.StreamGrabber.Start(GrabStrategyEnum.grabStrartegySequential, GrabLoop.ProvidedByUser))//主动取图用
            //if (m_camera.GrabUsingGrabLoopThread())   // 被动取图用
            {
                m_running = true;

                log?.Info(new CameraLogMessage(CameraInfo, A_StartGrabbing, R_Success));

                cts = new CancellationTokenSource();
                acqImageTask = new Task(() => AcquireImages(cts.Token), cts.Token, TaskCreationOptions.LongRunning);
                acqImageTask.Start();
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_StartGrabbing, R_Fail));
                throw new CameraSDKException("开始采集失败");
            }
        }

        bool SetImageCacheCount(int count)
        {
            if (m_camera.StreamGrabber.SetBufferCount(count))
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"图片缓存 = {count}"));

                return true;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "设置参数\"图片缓存\"失败"));
                return false;
            }
        }

        protected override void Stop2()
        {
            cts.Cancel();

            if (m_camera != null && m_camera.IsGrabbing)
            {
                if (m_camera.ShutdownGrab())
                    log?.Info(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Success));
                else
                    log?.Error(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Fail));
            }
        }

        void ForcedStop()
        {
            m_camera.ShutdownGrab();

            cts.Cancel();
        }

        #endregion

        #region 接收图像

        ushort idx = 0;
        ushort acqBaseIdx = 0;
        DateTime acqBaseTime;

        /* 主动取图处理 */
        void AcquireImages(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (IsOpen)
                {
                    bool waitFrame = m_camera.WaitForFrameTriggerReady(out IFrameRawData data, 1000);
                    idx++;

                    if (waitFrame == true)
                    {
                        if (TriggerMode == TriggerMode.Trigger)
                        {
                            log?.Debug(new CameraLogMessage(CameraInfo, A_AcqImage, R_Success, $"idx = {idx}  FrameNum = {data.BlockID}"));
                        }

                        // data.PixelFmt 可以判断是否是灰度图

                        Bitmap bmp = GetImage(data);

                        //不能用idx，idx不是有效的取到图像的次数
                        acqBaseIdx++;

                        //相机取像时间换算,ticks兑ns是1:100,DH是10ns单位，兑为1:10
                        DateTime acqTime;
                        if (acqBaseIdx == 1)
                            acqBaseTime = acqTime = DateTime.Now.AddTicks(-(data.TimeStamp / 10));
                        else
                            acqTime = acqBaseTime.AddTicks(data.TimeStamp / 10);

                        ReceivedImage(bmp, (int)data.BlockID, acqTime);

                        data.Dispose();
                    }
                }
            }
        }

        Bitmap GetImage(IFrameRawData data)
        {
            (TimeSpan ts, Bitmap bmp) = FuncWatch.ElapsedTime(() =>
            {
                //data.GrabResult.Clone().ToBitmap(!isGrey.Value);
                return ImageUtils.DeepCopyIntPtrToBitmap(isGrey.Value, data.Width, data.Height, data.RawData);
            });
            if (ts.TotalMilliseconds > CopyImageMaxTime && TriggerMode == TriggerMode.Trigger)
                log?.Warn(new CameraLogMessage(CameraInfo, A_Copy, R_Success, "图像 Copy 耗时过长", ts.TotalMilliseconds));
            else
                log.Debug(new CameraLogMessage(CameraInfo, A_Copy, R_Success, null, ts.TotalMilliseconds));

            return bmp;
        }

        private void OnImageGrabbed(object sender, GrabbedEventArgs e)
        {
            IGrabbedRawData data = e.GrabResult;

            (TimeSpan ts, Bitmap bmp) = FuncWatch.ElapsedTime(() =>
            {
                return data.ToBitmap(!isGrey.Value);
            });
            if (ts.TotalMilliseconds > CopyImageMaxTime && TriggerMode == TriggerMode.Trigger)
                log.Warn(new CameraLogMessage(CameraInfo, A_Copy, R_Tip, "ToBitmap 执行时间异常", ts.TotalMilliseconds));
            else
                log.Debug(new CameraLogMessage(CameraInfo, A_Copy, R_Tip, "ToBitmap 执行时间", ts.TotalMilliseconds));

            ReceivedImage(bmp, (int)data.BlockID);
        }

        private void OnMsgChannel(object sender, MsgChannelArgs e)
        {
            if (e.EventID == MsgChannelEvent.MSG_CHANNEL_EVENT_EXPOSURE_END)
            {
                log?.Debug(new CameraLogMessage(CameraInfo, "消息回调", "曝光完成"));

                OnExposured(EventArgs.Empty);
            }
        }

        #endregion

        #region 重命名/设置 IP

        public bool Rename(string newUserID)
        {
            if (!IsOpen)
                throw new CameraSDKException("相机未打开");

            if (setStringAttr(paramInfos.DeviceUserID.Name, newUserID, paramInfos.DeviceUserID.Description) < 0)
                throw new CameraSDKException("设置UserID失败");
            else
                CameraInfo.UserDefinedName = newUserID;

            return true;
        }

        public bool SetCache(int cache)
        {
            return true;
        }

        public bool SetIP(string ip, string subnetMask, string defaultGateway)
        {
            if (CameraInfo.ConnectionType == ConnectionType.U3)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "U3 口相机不可以设置 IP"));
                throw new CameraSDKException("U3 口相机不可以设置 IP");
            }
            if (Enumerator.GigeForceIP(index, ip, subnetMask, defaultGateway))
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
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail));
                throw new CameraSDKException("设置相机 IP 失败");
            }
        }

        #endregion

        #region 触发模式/触发源

        public TriggerMode GetTriggerMode()
        {
            if (!IsOpen)
                throw new CameraSDKException("相机未打开");
            //TriggerMode
            string sValue = getEnumAttr(paramInfos.TriggerMode.Name, paramInfos.TriggerMode.Description);
            switch (sValue)
            {
                case "On":
                    {
                        this.TriggerMode = TriggerMode.Trigger;
                        return TriggerMode.Trigger;
                    }
                case "Off":
                    {
                        this.TriggerMode = TriggerMode.Continuous;
                        return TriggerMode.Continuous;
                    }
                default:
                    {
                        this.TriggerMode = TriggerMode.Trigger;
                        return TriggerMode.Trigger;
                    }
            }
        }

        public override void SetTriggerMode(TriggerMode triggerMode)
        {
            //相机掉线后，界面逻辑的设置还能点，所以此处判断相机对象
            if (m_camera == null)
                return;
            if (!IsOpen)
                throw new CameraSDKException("相机未打开");
            switch (triggerMode)
            {
                case TriggerMode.Trigger:
                    {//触发一次
                        setEnumAttrForValueAlias(paramInfos.TriggerMode.Name, "On", paramInfos.TriggerMode.Description, "触发一次");
                        this.TriggerMode = TriggerMode.Trigger;
                        break;
                    }
                case TriggerMode.Continuous:
                    {//连续触发
                        setEnumAttrForValueAlias(paramInfos.TriggerMode.Name, "Off", paramInfos.TriggerMode.Description, "连续");
                        this.TriggerMode = TriggerMode.Continuous;
                        break;
                    }
            }
        }

        public TriggerSource GetTriggerSource()
        {
            if (!IsOpen)
                throw new CameraSDKException("相机未打开");
            //TriggerSource
            string sValue = getEnumAttr(paramInfos.TriggerSource.Name, paramInfos.TriggerSource.Description);
            switch (sValue)
            {
                case "Software":
                    {
                        TriggerSource = TriggerSource.Software;
                        return TriggerSource.Software;
                    }
                case "Line1":
                case "Line2":
                case "Line3":
                case "Line4":
                case "Line5":
                case "Line6":
                    {
                        TriggerSource = TriggerSource.Extern;
                        return TriggerSource.Extern;
                    }
                default:
                    {
                        TriggerSource = TriggerSource.Software;
                        return TriggerSource.Software;
                    }
            }
        }

        public override void SetTriggerSource(TriggerSource triggerSource)
        {
            //相机掉线后，界面逻辑的设置还能点，所以此处判断相机对象
            if (m_camera == null)
                return;
            if (!IsOpen)
                throw new CameraSDKException("相机未打开");
            switch (triggerSource)
            {
                case TriggerSource.Software:
                    {
                        //TriggerSource
                        setEnumAttr(paramInfos.TriggerSource.Name, "Software", paramInfos.TriggerSource.Description);
                        TriggerSource = TriggerSource.Software;
                    }
                    break;
                case TriggerSource.Extern:
                    {
                        string[] ExternLine = { "Line0", "Line1", "Line2", "Line3", "Line4", "Line5", "Line6" };
                        foreach (string temp in ExternLine)
                        {
                            if (setEnumAttr(paramInfos.TriggerSource.Name, temp, paramInfos.TriggerSource.Description) == 0)
                                break;
                        }
                        TriggerSource = TriggerSource.Extern;
                    }
                    break;
                default:
                    {
                        setEnumAttr(paramInfos.TriggerSource.Name, "Software", paramInfos.TriggerSource.Description);
                        TriggerSource = TriggerSource.Software;
                    }
                    break;
            }
        }

        public void SoftTrigger()
        {
            if (!IsOpen)
                throw new CameraSDKException("相机未打开");
            if (m_camera == null)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Fail, "软触发一次失败,设备未找到"));
                throw new CameraSDKException("软触发一次未找到设备");
            }
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

            if (false == m_camera.TriggerSet.Open(TriggerSourceEnum.Software))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Fail, "打开软触发失败"));
                throw new CameraSDKException("软触发失败，打开软触发失败");
            }

            if (false == m_camera.ExecuteSoftwareTrigger())
                log?.Error(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Fail, "软触发指令不成功"));
            else
                log?.Info(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Success));
        }

        #endregion

        #region 参数获取/设置
        CameraParams m_cameraParams = new CameraParams(); /* 相机参数缓存 */
        protected CameraParamInfoCollection paramInfos;
        public CameraParamInfo[] GetParamInfos()
        {
            return paramInfos.All();
        }

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

        public CameraParams GetParams()
        {
            if (!IsOpen)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "相机未打开"));
                return new CameraParams();
            }

            if (m_camera == null)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "相机未打开"));
                return new CameraParams();
            }

            m_cameraParams = new CameraParams();

            m_cameraParams.ExposureTime = (float?)getFloatAttr(paramInfos.Exposure.Name, paramInfos.Exposure.Description);
            m_cameraParams.Gain = (float?)getFloatAttr(paramInfos.Gain.Name + "Raw", paramInfos.Gain.Description);
            m_cameraParams.ImageHeight = (int?)getIntegerAttr(paramInfos.Height.Name, paramInfos.Height.Description);
            m_cameraParams.ImageWidth = (int?)getIntegerAttr(paramInfos.Width.Name, paramInfos.Width.Description);
            m_cameraParams.TriggerDelay = (float?)getFloatAttr(paramInfos.TriggerDelay.Name, paramInfos.TriggerDelay.Description);
            return m_cameraParams;
        }

        public bool SetParams(CameraParams @params)
        {
            if (!IsOpen)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机未打开"));
                return false;
            }
            if (m_camera == null)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "没有设备，请先打开相机"));
                return false;
            }

            bool result = true;
            if (@params.ExposureTime.HasValue)  
            {
                if (setFloatAttr(paramInfos.Exposure.Name, Convert.ToDouble(@params.ExposureTime.Value), paramInfos.Exposure.Description) == 0)
                    m_cameraParams.ExposureTime = @params.ExposureTime.Value;
            }
            if (@params.Gain.HasValue)  
            {
                if (setFloatAttr(paramInfos.Gain.Name + "Raw", Convert.ToDouble(@params.Gain.Value), paramInfos.Gain.Description) == 0)
                    m_cameraParams.Gain = @params.Gain.Value;
            }
            if (@params.ImageHeight.HasValue && @params.ImageHeight != m_cameraParams.ImageHeight)
            {
                if (setIntegerAttr(paramInfos.Height.Name, Convert.ToInt64(@params.ImageHeight.Value), paramInfos.Height.Description) == 0)
                    m_cameraParams.ImageHeight = @params.ImageHeight.Value;
            }
            if (@params.ImageWidth.HasValue && @params.ImageWidth != m_cameraParams.ImageWidth)
            {
                if (setIntegerAttr(paramInfos.Width.Name, Convert.ToInt64(@params.ImageWidth.Value), paramInfos.Width.Description) == 0)
                    m_cameraParams.ImageWidth = @params.ImageWidth.Value;
            }
            if (@params.TriggerDelay.HasValue && @params.TriggerDelay != m_cameraParams.TriggerDelay)
            {
                if (setFloatAttr(paramInfos.TriggerDelay.Name, Convert.ToDouble(@params.TriggerDelay.Value), paramInfos.TriggerDelay.Description) == 0)
                    m_cameraParams.TriggerDelay = @params.TriggerDelay.Value;
            }

            return result;
        }

        public void SetParamsForReconnect(CameraParams @params)
        {
            if (!IsOpen)
                throw new CameraSDKException("相机未打开");
            if (m_camera == null)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "没有设备，请先打开相机"));
                return;
            }
            if (@params.ExposureTime.HasValue)
            {
                //ExposureTime
                if (setFloatAttr(paramInfos.Exposure.Name, Convert.ToDouble(@params.ExposureTime.Value), paramInfos.Exposure.Description) == 0)
                    m_cameraParams.ExposureTime = @params.ExposureTime.Value;
            }
            if (@params.Gain.HasValue)
            {
                //GainRaw
                if (setFloatAttr(paramInfos.Gain.Name + "Raw", Convert.ToDouble(@params.Gain.Value), paramInfos.Gain.Description) == 0)
                    m_cameraParams.Gain = @params.Gain.Value;
            }
            if (@params.ImageHeight.HasValue)
            {
                //Height
                if (setIntegerAttr(paramInfos.Height.Name, Convert.ToInt64(@params.ImageHeight.Value), paramInfos.Height.Description) == 0)
                    m_cameraParams.ImageHeight = @params.ImageHeight.Value;
            }
            if (@params.ImageWidth.HasValue)
            {
                //Width
                if (setIntegerAttr(paramInfos.Width.Name, Convert.ToInt64(@params.ImageWidth.Value), paramInfos.Width.Description) == 0)
                    m_cameraParams.ImageWidth = @params.ImageWidth.Value;
            }
            if (@params.TriggerDelay.HasValue)
            {
                //TriggerDelay
                if (setFloatAttr(paramInfos.TriggerDelay.Name, Convert.ToDouble(@params.TriggerDelay.Value), paramInfos.TriggerDelay.Description) == 0)
                    m_cameraParams.TriggerDelay = @params.TriggerDelay.Value;
            }
        }
        #endregion

        #region 相机属性设置

        #region getAttr

        //DH相机获取相关属性
        private string GetStringParam(string attrName, string des)
        {
            if ((m_camera == null) || (!m_camera.IsOpen))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取{ des }({ attrName })失败，未找到设备或设备未开启"));
                return null;
            }
            string sValue = null;
            using (IStringParameter p = m_camera.ParameterCollection[new StringName(attrName)])
            {
                sValue = p.GetValue();
            }
            if (!string.IsNullOrEmpty(sValue))
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"获取{ des }({ attrName })成功:{ sValue }"));
            else
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取{ des }({ attrName })失败:{ sValue }"));
            return sValue;
        }

        //DH相机获取枚举类型相关属性
        private string getEnumAttr(string attrName, string des)
        {
            if ((m_camera == null) || (!m_camera.IsOpen))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取{ des }({ attrName })失败，未找到设备或设备未开启"));
                return null;
            }
            string sValue = null;
            using (IEnumParameter p = m_camera.ParameterCollection[new EnumName(attrName)])
            {
                sValue = p.GetValue();
            }
            if (!string.IsNullOrEmpty(sValue))
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"获取{ des }({ attrName })成功:{ sValue }"));
            else
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取{ des }({ attrName })失败:{ sValue }"));
            return sValue;
        }

        //DH相机获取INT类型相关属性
        private long getIntegerAttr(string attrName, string des)
        {
            if ((m_camera == null) || (!m_camera.IsOpen))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取{ des }({ attrName })失败，未找到设备或设备未开启"));
                return -1;
            }
            long nValue = -1;
            using (IIntegraParameter p = m_camera.ParameterCollection[new IntegerName(attrName)])
                nValue = p.GetValue();
            if (nValue > 0)
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"获取{ des }({ attrName })成功:{ nValue }"));
            else
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取{ des }({ attrName })失败:{ nValue }"));
            return nValue;
        }

        //DH相机获取Float类型相关属性
        private double getFloatAttr(string attrName, string des)
        {
            if ((m_camera == null) || (!m_camera.IsOpen))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取{ des }({ attrName })失败，未找到设备或设备未开启"));
                return -1.0;
            }
            double dValue = -1.0;
            using (IFloatParameter p = m_camera.ParameterCollection[new FloatName(attrName)])
            {
                /*获取失败失败返回0.0*/
                dValue = p.GetValue();
            }
            if (dValue > 0.0)
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"获取{ des }({ attrName })成功:{ dValue }"));
            else if (attrName == "TriggerDelay")
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"获取{ des }({ attrName })成功:{ dValue }"));
            else
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取{ des }({ attrName })失败:{ dValue }"));
            return dValue;
        }

        //DH相机获取bool类型相关属性
        private bool getBoolAttr(string attrName, string des)
        {
            if ((m_camera == null) || (!m_camera.IsOpen))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取{ des }({ attrName })失败，未找到设备或设备未开启"));
                return false;
            }
            bool bValue = false;
            using (IBooleanParameter p = m_camera.ParameterCollection[new BooleanName(attrName)])
            {
                bValue = p.GetValue();
            }
            if (bValue)
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"获取{ des }({ attrName })成功:{ bValue }"));
            else
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"获取{ des }({ attrName })失败:{ bValue }"));
            return bValue;
        }

        #endregion

        #region setAttr

        //DH相机设置相关属性
        private int setStringAttr(string attrName, string sValue, string des)
        {
            if ((m_camera == null) || (!m_camera.IsOpen))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败，未找到设备或设备未开启"));
                return -1;
            }
            using (IStringParameter p = m_camera.ParameterCollection[new StringName(attrName)])
            {
                if (false == p.SetValue(sValue))
                {
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败:{ sValue }"));
                    return -1;
                }
            }
            log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置{ des }({ attrName })成功:{ sValue }"));
            return 0;
        }

        //DH相机设置枚举类型相关属性
        private int setEnumAttr(string attrName, string sValue, string des)
        {
            if ((m_camera == null) || (!m_camera.IsOpen))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败，未找到设备或设备未开启"));
                return -1;
            }
            using (IEnumParameter p = m_camera.ParameterCollection[new EnumName(attrName)])
            {
                if (false == p.SetValue(sValue))
                {
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败:{ sValue }"));
                    return -1;
                }
            }
            log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置{ des }({ attrName })成功:{ sValue }"));
            return 0;
        }

        private int setEnumAttrForValueAlias(string attrName, string sValue, string des, string ValueAlias)
        {
            if ((m_camera == null) || (!m_camera.IsOpen))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败，未找到设备或设备未开启"));
                return -1;
            }
            using (IEnumParameter p = m_camera.ParameterCollection[new EnumName(attrName)])
            {
                if (false == p.SetValue(sValue))
                {
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败:{ ValueAlias }"));
                    return -1;
                }
            }
            log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置{ des }({ attrName })成功:{ ValueAlias }"));
            return 0;
        }

        //DH相机设置INT类型相关属性
        private int setIntegerAttr(string attrName, long nValue, string des)
        {
            if ((m_camera == null) || (!m_camera.IsOpen))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败，未找到设备或设备未开启"));
                return -1;
            }
            using (IIntegraParameter p = m_camera.ParameterCollection[new IntegerName(attrName)])
            {
                if (false == p.SetValue(nValue))
                {
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败:{ nValue }"));
                    return -1;
                }
            }
            log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置{ des }({ attrName })成功:{ nValue }"));
            return 0;
        }

        //DH相机设置Float类型相关属性
        private int setFloatAttr(string attrName, double dValue, string des)
        {
            if ((m_camera == null) || (!m_camera.IsOpen))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败，未找到设备或设备未开启"));
                return -1;
            }
            using (IFloatParameter p = m_camera.ParameterCollection[new FloatName(attrName)])
            {
                if (false == p.SetValue(dValue))
                {
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败:{ dValue }"));
                    return -1;
                }
            }
            log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置{ des }({ attrName })成功:{ dValue }"));
            return 0;
        }

        //DH相机设置bool类型相关属性
        private int setBoolAttr(string attrName, bool bValue, string des)
        {
            if ((m_camera == null) || (!m_camera.IsOpen))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败，未找到设备或设备未开启"));
                return -1;
            }
            using (IBooleanParameter p = m_camera.ParameterCollection[new BooleanName(attrName)])
            {
                if (false == p.SetValue(bValue))
                {
                    log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置{ des }({ attrName })失败:{ bValue }"));
                    return -1;
                }
            }
            log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"设置{ des }({ attrName })成功:{ bValue }"));
            return 0;
        }
        #endregion

        #endregion

        #region 断线重连

        private void OnConnectLost(object sender, EventArgs e)
        {
            IsLoss = true;

            ForcedStop();
            CloseCamera();

            log?.Fatal(new CameraLogMessage(CameraInfo, A_Connect, R_Disconnect, "开始自动重连"));

            checkLossThread = new Thread(new ThreadStart(checkThread));
            checkLossThread.Start();
        }

        /*断线重连线程*/
        private void checkThread()
        {
            while (IsOpen)
            {
                /*重新搜索相机，开始拉流*/
                List<IDeviceInfo> m_deviceInfoList = Enumerator.EnumerateDevices();
                if (!m_deviceInfoList.Any(d => d.Key == m_Key))
                {
                    Thread.Sleep(10);
                    continue;
                }
                else
                {
                    m_camera = Enumerator.GetDeviceByKey(m_Key);

                    if (m_camera.Open())
                    {
                        IsLoss = false;
                        IsOpen = true;
                        m_camera.ConnectionLost += OnConnectLost;
                        log?.Info(new CameraLogMessage(CameraInfo, A_Connect, R_Success));

                        IsGrey(getEnumAttr(paramInfos.PixelFormat.Name, paramInfos.PixelFormat.Description));
                        SetParamsForReconnect(m_cameraParams);

                        if (m_running)
                        {
                            m_running = false;
                            Start(TriggerMode, TriggerSource);
                        }

                        break;
                    }
                }
            }

            if (!IsOpen)
            {
                m_camera = null;
                log?.Info(new CameraLogMessage(CameraInfo, A_Connect, R_End, "相机关闭，退出自动重连"));
            }
        }

        #endregion 

        public void Dispose()
        {
            Close();
        }

        void IsGrey(string pixelFormat)
        {
            if (isGrey.HasValue) return;

            switch (pixelFormat)
            {
                case "Mono8":
                case "Mono10":
                case "Mono12":
                case "Mono14":
                case "Mono16":
                    isGrey = true;
                    break;
                default:
                    isGrey = false;
                    break;
            }
        }
    }
}

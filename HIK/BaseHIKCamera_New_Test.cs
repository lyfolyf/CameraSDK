using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using GL.Kit.Log;
using GL.Kit.Extension;
using static GL.Kit.Log.ActionResult;

using MvCamCtrl.NET;
using CameraSDK.Models;
using static CameraSDK.CameraAction;
using static CameraSDK.HIK.HIKErrorCode;
using System.Linq;

namespace CameraSDK.HIK
{
    /// <summary>
    /// HIK 相机基类
    /// </summary>
    public abstract class BaseHIKCamera_New : BaseCamera, ICamera
    {
        // 像素格式转化的最大耗时，超过则产生一条警告日志
        const int ConvertPixelMaxTime = 100;

        // 拷贝图像的最大耗时，超过则产生一条警告日志
        const int CopyImageMaxTime = 120;

        protected IntPtr m_deviceInfo;

        protected MyCamera m_camera;

        private Queue<(MyCamera.MvGvspPixelType enPixelType, ushort width, ushort height, long nHostTimeStamp, int frameNum, byte[] buffer)> qFrames = new Queue<(MyCamera.MvGvspPixelType enPixelType, ushort width, ushort height, long nHostTimeStamp, int frameNum, byte[] buffer)>();

        //add by LuoDian @ 20220111 为了提升效率，把触发设置和获取图像数据分开，在触发设置这里需要一个同步锁，在把图像数据压入到队列之后才释放锁，以确保拍照完成之后，再返回反馈信号
        private AutoResetEvent triggerSetEvent = new AutoResetEvent(false);

        protected HIKParamInfoCollection paramInfos;

        //是否断线
        //MV_CC_GetOneFrameTimeout_NET 函数，断线后返回的 ErrorCode 也是 MV_E_NODATA
        //MV_CC_GetImageBuffer_NET 函数则有返回 MV_E_CALLORDER 或 MV_E_HANDLE
        //不能把这些值都排除，所以做一个标志位来判断
        protected bool offLine = false;

        public BaseHIKCamera_New(IntPtr deviceInfo, IGLog log, ComCameraInfo cameraInfo)
            : base(cameraInfo, log)
        {
            m_deviceInfo = deviceInfo;
            paramInfos = new HIKParamInfoCollection();
        }

        //add by LuoDian @ 20220111 把图像数据的像素格式转换放到队列里面去做之后，需要在软件关闭的时候退出循环
        ~BaseHIKCamera_New()
        {
            triggerSetEvent.Reset();
            triggerSetEvent.Dispose();
        }

        public uint RatioR
        {
            get
            {
                MyCamera.MVCC_INTVALUE stParam = new MyCamera.MVCC_INTVALUE();
                int nRet = m_camera.MV_CC_GetBalanceRatioRed_NET(ref stParam);
                if (MyCamera.MV_OK == nRet)
                {
                    return stParam.nCurValue;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                int nRet = m_camera.MV_CC_SetBalanceRatioRed_NET(value);
            }
        }

        public uint RatioG
        {
            get
            {
                MyCamera.MVCC_INTVALUE stParam = new MyCamera.MVCC_INTVALUE();
                int nRet = m_camera.MV_CC_GetBalanceRatioGreen_NET(ref stParam);
                if (MyCamera.MV_OK == nRet)
                {
                    return stParam.nCurValue;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                int nRet = m_camera.MV_CC_SetBalanceRatioGreen_NET(value);
            }
        }

        public uint RatioB
        {
            get
            {
                MyCamera.MVCC_INTVALUE stParam = new MyCamera.MVCC_INTVALUE();
                int nRet = m_camera.MV_CC_GetBalanceRatioBlue_NET(ref stParam);
                if (MyCamera.MV_OK == nRet)
                {
                    return stParam.nCurValue;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                int nRet = m_camera.MV_CC_SetBalanceRatioBlue_NET(value);
            }
        }
        public uint Width
        {
            get
            {
                MyCamera.MVCC_INTVALUE stParam = new MyCamera.MVCC_INTVALUE();
                int nRet = m_camera.MV_CC_GetWidth_NET(ref stParam);
                if (MyCamera.MV_OK == nRet)
                {
                    return stParam.nCurValue;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                int nRet = m_camera.MV_CC_SetWidth_NET(value);
            }
        }

        public uint Height
        {
            get
            {
                MyCamera.MVCC_INTVALUE stParam = new MyCamera.MVCC_INTVALUE();
                int nRet = m_camera.MV_CC_GetHeight_NET(ref stParam);
                if (MyCamera.MV_OK == nRet)
                {
                    return stParam.nCurValue;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                int nRet = m_camera.MV_CC_SetHeight_NET(value);
            }
        }


        public uint OffsetX
        {
            get
            {
                MyCamera.MVCC_INTVALUE stParam = new MyCamera.MVCC_INTVALUE();
                int nRet = m_camera.MV_CC_GetAOIoffsetX_NET(ref stParam);
                if (MyCamera.MV_OK == nRet)
                {
                    return stParam.nCurValue;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                int nRet = m_camera.MV_CC_SetAOIoffsetX_NET(value);
            }
        }


        public uint OffsetY
        {
            get
            {
                MyCamera.MVCC_INTVALUE stParam = new MyCamera.MVCC_INTVALUE();
                int nRet = m_camera.MV_CC_GetAOIoffsetY_NET(ref stParam);
                if (MyCamera.MV_OK == nRet)
                {
                    return stParam.nCurValue;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                int nRet = m_camera.MV_CC_SetAOIoffsetY_NET(value);
            }
        }

        #region 打开/关闭

        /// <summary>
        /// 打开相机
        /// </summary>
        public abstract void Open();

        protected bool OpenDevice()
        {
            int nRet = m_camera.MV_CC_OpenDevice_NET();
            if (MyCamera.MV_OK != nRet)
            {
                m_camera.MV_CC_DestroyDevice_NET();

                log?.Error(new CameraLogMessage(CameraInfo, A_Open, R_Fail, ErrorMessage(nRet)));

                throw new CameraSDKException("打开相机失败");
            }
            var sn = CameraInfo.SN;
            UserConfig userConfig = UserConfig.LoadConfig(sn);
            if (userConfig != null)
            {
                this.RatioG = (uint)userConfig.GreenRatio;
                this.RatioR = (uint)userConfig.RedRatio;
                this.RatioB = (uint)userConfig.BlueRatio;
                this.Width = (uint)userConfig.Width;
                this.Height = (uint)userConfig.Height;
                this.OffsetX = (uint)userConfig.OffsetX;
                this.OffsetY = (uint)userConfig.OffsetY;
            }

            //add by LuoDian @ 20220121 为了提升CT，选择像素格式转换时效率最高的方式
            //m_camera.MV_CC_SetBayerCvtQuality_NET(1);

            IsOpen = true;
            offLine = false;

            // add by LuoDian @ 20220111 为了提升效率，通过队列 + 线程的方式，把图像数据的像素格式转换放到队列里面去做
            Thread thread = new Thread(GetImageFromQueueThread) { IsBackground = true };
            thread.Start();

            log?.Info(new CameraLogMessage(CameraInfo, A_Open, R_Success));
            return true;
        }

        /// <summary>
        /// 关闭相机
        /// </summary>
        public void Close()
        {
            if (!IsOpen) return;
            var sn = CameraInfo.SN;
            UserConfig userConfig = new UserConfig(sn)
            {
                GreenRatio = (int)this.RatioG,
                RedRatio = (int)this.RatioR,
                BlueRatio = (int)this.RatioB,
                Width = (int)this.Width,
                Height = (int)this.Height,
                OffsetX = (int)this.OffsetX,
                OffsetY = (int)this.OffsetY
            };
            userConfig.SaveConfig();

            if (m_running)
                Stop();

            CloseCamera();

            while (qFrames.Count > 0)
            {
                Thread.Sleep(10);
            }

            IsOpen = false;

            log?.Info(new CameraLogMessage(CameraInfo, A_Close, R_Success));
        }

        protected void CloseCamera()
        {
            m_camera.MV_CC_CloseDevice_NET();
            m_camera.MV_CC_DestroyDevice_NET();

            offLine = true;
        }

        #endregion

        #region 重命名/设置IP

        /// <summary>
        /// 重命名
        /// </summary>
        /// <param name="newUserID"></param>
        public bool Rename(string newUserID)
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_Rename, R_Fail, "相机未打开"));

                return false;
            }

            if (offLine)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_Rename, R_Fail, "相机断线"));

                return false;
            }

            bool result = SetStringParam(paramInfos.DeviceUserID, newUserID);

            if (result)
            {
                CameraInfo.UserDefinedName = newUserID;
            }

            return result;
        }

        public bool SetCache(int cache)
        {
            return true;
        }

        public abstract bool SetIP(string ip, string subnetMask, string defaultGateway);

        #endregion

        #region 触发模式/触发源

        /// <summary>
        /// 读取触发模式
        /// </summary>
        public TriggerMode GetTriggerMode()
        {
            TriggerMode_HIK mode = GetEnumParam<TriggerMode_HIK>(paramInfos.TriggerMode);

            TriggerMode = mode == TriggerMode_HIK.Off ? TriggerMode.Continuous : TriggerMode.Trigger;

            return TriggerMode;
        }

        /// <summary>
        /// 设置触发模式
        /// </summary>
        public override void SetTriggerMode(TriggerMode triggerMode)
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机未打开"));
                throw new CameraSDKException("设置触发模式失败，相机未打开");
            }
            if (offLine)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机断线"));
                throw new CameraSDKException("设置触发模式失败，相机断线");
            }

            SetEnumParam(paramInfos.TriggerMode, triggerMode == TriggerMode.Continuous ? TriggerMode_HIK.Off : TriggerMode_HIK.On);

            TriggerMode = triggerMode;
        }

        /// <summary>
        /// 读取触发源
        /// </summary>
        public TriggerSource GetTriggerSource()
        {
            TriggerSource_HIK source = GetEnumParam<TriggerSource_HIK>(paramInfos.TriggerSource);

            TriggerSource = source == TriggerSource_HIK.Software ? TriggerSource.Software : TriggerSource.Extern;

            return TriggerSource;
        }

        /// <summary>
        /// 设置触发源
        /// </summary>
        public override void SetTriggerSource(TriggerSource triggerSource)
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机未打开"));
                throw new CameraSDKException("设置触发源失败，相机未打开");
            }
            if (offLine)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机断线"));
                throw new CameraSDKException("设置触发源失败，相机断线");
            }

            SetEnumParam(paramInfos.TriggerSource, triggerSource == TriggerSource.Software ? TriggerSource_HIK.Software : TriggerSource_HIK.Extern);

            TriggerSource = triggerSource;
        }

        /// <summary>
        /// 软触发一次
        /// </summary>
        public void SoftTrigger()
        {
            if (!m_running)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Fail, "采集尚未开始"));
                throw new CameraSDKException("软触发失败，采集尚未开始");
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

            if (offLine)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Fail, "相机断线"));
                throw new CameraSDKException("软触发失败，相机断线");
            }

            int nRet = m_camera.MV_CC_SetCommandValue_NET("TriggerSoftware");

            //add by LuoDian @ 20220111 为了提升效率，把触发设置和获取图像数据分开，在触发设置这里需要一个同步锁，在把图像数据压入到队列之后才释放锁，以确保拍照完成之后，再返回反馈信号
            if (triggerSetEvent.WaitOne(2000) == false)
            {
                string msg = "软触发超时，2s内未收到图像！";
                log.Error(msg);
                throw new Exception(msg);
            }

            if (MyCamera.MV_OK == nRet)
            {
                log?.Debug(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Success));
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Fail, ErrorMessage(nRet)));
                throw new CameraSDKException("软触发失败");
            }
        }

        #endregion

        #region 开始/停止

        CancellationTokenSource cts;
        Task acqImageTask;

        /// <summary>
        /// 开始采集
        /// </summary>
        protected override void Start2(TriggerMode triggerMode, TriggerSource triggerSource)
        {
            // 设置连续单帧，这个必须设，否则会出现只出一张图的情况
            SetEnumParam(paramInfos.AcquisitionMode, AcquisitionMode_HIK.Continuous);

            if (ImageCacheCount > 1)
                SetImageCacheCount(ImageCacheCount);

            int nRet = m_camera.MV_CC_StartGrabbing_NET();
            if (MyCamera.MV_OK == nRet)
            {
                m_running = true;

                log?.Info(new CameraLogMessage(CameraInfo, A_StartGrabbing, R_Success));

                cts = new CancellationTokenSource();
                acqImageTask = new Task(() => AcquireImages(cts.Token), cts.Token, TaskCreationOptions.LongRunning);
                acqImageTask.Start();
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_StartGrabbing, R_Fail, ErrorMessage(nRet)));
                throw new CameraSDKException("开始采集失败");
            }
        }

        /// <summary>
        /// 停止采集
        /// </summary>
        protected override void Stop2()
        {
            cts.Cancel();

            // 这里不做失败判断了
            m_camera.MV_CC_StopGrabbing_NET();

            log?.Info(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Success));
        }

        // 断线重连时调用
        protected void ForcedStop()
        {
            m_camera.MV_CC_StopGrabbing_NET();

            //update by LuoDian @ 20210812 断线重连的时候，有时候cts对象为null，故加一个空值判断符“?”
            cts?.Cancel();
        }

        #endregion

        #region 接收图像

        ushort idx = 0;

        void AcquireImages(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                MyCamera.MV_FRAME_OUT stFrameOut = new MyCamera.MV_FRAME_OUT();

                int nRet = m_camera.MV_CC_GetImageBuffer_NET(ref stFrameOut, 1000);

                if (nRet == MyCamera.MV_OK)
                {
                    if (!removeCustomPixelFormats(stFrameOut.stFrameInfo.enPixelType))
                    {
                        Stopwatch watch = new Stopwatch();
                        watch.Start();
                        if (TriggerMode == TriggerMode.Trigger)
                        {
                            log?.Debug(new CameraLogMessage(CameraInfo, A_AcqImage, R_Success, $"idx = {idx}  FrameNum = {stFrameOut.stFrameInfo.nFrameNum}"));
                        }

                        #region 为了提升效率，通过队列+双线程的方式，把图像数据的像素格式转换放到另外一个线程里面去做
                        // Modified by louis on Mar. 10 2022
                        if (stFrameOut.stFrameInfo.nFrameLen > 0)
                        {
                            byte[] buffer = new byte[stFrameOut.stFrameInfo.nFrameLen];
                            Marshal.Copy(stFrameOut.pBufAddr, buffer, 0, (int)stFrameOut.stFrameInfo.nFrameLen);
                            qFrames.Enqueue((stFrameOut.stFrameInfo.enPixelType, stFrameOut.stFrameInfo.nWidth, stFrameOut.stFrameInfo.nHeight, stFrameOut.stFrameInfo.nHostTimeStamp, (int)stFrameOut.stFrameInfo.nFrameNum, buffer));
                            triggerSetEvent.Set();
                        }
                        #endregion
                    }

                    m_camera.MV_CC_FreeImageBuffer_NET(ref stFrameOut);
                }
                else
                {
                    if (nRet != MyCamera.MV_E_NODATA && !offLine && m_running)
                    {
                        log?.Error(new CameraLogMessage(CameraInfo, A_AcqImage, R_Fail, ErrorMessage(nRet)));
                    }

                    // 触发模式就停 5ms，Demo 里是这样的
                    if (TriggerMode == TriggerMode.Trigger)
                    {
                        Thread.Sleep(5);
                    }
                }
            }
        }

        #region 单独的线程中做根据需要做像素格式转化
        // Modified by louis on Mar. 10 2022
        private void GetImageFromQueueThread()
        {
            while (IsOpen)
            {
                if (qFrames.Count > 0)
                {
                    (MyCamera.MvGvspPixelType enPixelType, ushort width, ushort height, long nHostTimeStamp, int frameNum, byte[] buffer) = qFrames.Dequeue();

                    DateTime acqTime = ConverEpochTimeToDateTime(nHostTimeStamp / 1000);
                    log.Info($"【CT统计】当前相机 {CameraInfo.UserDefinedName} 中还有 {imgQueue.Count} 张图！当前这张图从触发到图像像素格式转换之前的耗时：{(DateTime.Now - acqTime).TotalMilliseconds}ms");

                    Bitmap bmp = GetImage(enPixelType, width, height, buffer);
                    if (bmp != null)
                    {
                        ReceivedImage(bmp, frameNum, acqTime);
                    }
                }
                else { Thread.Sleep(5); }
            }
        }

        Bitmap GetImage(MyCamera.MvGvspPixelType enPixelType, ushort width, ushort height, byte[] buffer)
        {
            (TimeSpan ts, Bitmap bmp) = FuncWatch.ElapsedTime(() =>
            {
                return GetBitmap(enPixelType, width, height, buffer);
            });
            if (ts.TotalMilliseconds > CopyImageMaxTime && TriggerMode == TriggerMode.Trigger)
                log.Warn(new CameraLogMessage(CameraInfo, A_Copy, R_Success, $"获取图像耗时: { ts.TotalMilliseconds }ms"));

            bmp.Save("louis1.54.bmp", ImageFormat.Bmp);
            SaveJpeg(bmp);
            return bmp;
        }

        void SaveJpeg(Bitmap bmp)
        {
            ImageCodecInfo imageCodecInfo = ImageCodecInfo.GetImageEncoders().First(a => a.FormatID == ImageFormat.Jpeg.Guid);
            EncoderParameters encoderParameters = new EncoderParameters(1);
            EncoderParameter encoderParameter = new EncoderParameter(Encoder.Quality, (long)100);
            encoderParameters.Param[0] = encoderParameter;

            bmp.Save("louis1.54.jpg", imageCodecInfo, encoderParameters);
        }

        Bitmap GetBitmap(MyCamera.MvGvspPixelType srcPixelType, ushort width, ushort height, byte[] buffer)
        {
            IntPtr srcPtr = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0);

            if (IsGrey(srcPixelType))
                return ImageUtils.DeepCopyIntPtrToBitmap(isGrey.Value, width, height, srcPtr);
            else
                return GetBitmapByConvertPixelType(srcPixelType, width, height, srcPtr, buffer.Length);
        }

        private Bitmap GetBitmapByConvertPixelType(MyCamera.MvGvspPixelType srcPixelType, ushort width, ushort height, IntPtr srcPtr, int srcBufferLen)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            BitmapData bmpData = null;

            try
            {
                bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);

                // 像素类型转换
                MyCamera.MvGvspPixelType dstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed;
                MyCamera.MV_PIXEL_CONVERT_PARAM stConverPixelParam = new MyCamera.MV_PIXEL_CONVERT_PARAM
                {
                    nWidth = width,
                    nHeight = height,
                    pSrcData = srcPtr,
                    nSrcDataLen = (uint)srcBufferLen,
                    enSrcPixelType = srcPixelType,
                    enDstPixelType = dstPixelType,
                    pDstBuffer = bmpData.Scan0,
                    nDstBufferSize = (uint)(width * height * 3)
                };

                (TimeSpan ts, int nRet) = FuncWatch.ElapsedTime(() =>
                {
                    return m_camera.MV_CC_ConvertPixelType_NET(ref stConverPixelParam);
                });

                if (ts.TotalMilliseconds > ConvertPixelMaxTime && TriggerMode == TriggerMode.Trigger)
                    log.Warn(new CameraLogMessage(CameraInfo, A_ConvertPixel, R_Success, $"转换耗时: { ts.TotalMilliseconds }ms"));

                if (MyCamera.MV_OK != nRet)
                {
                    log?.Error(new CameraLogMessage(CameraInfo, A_Other, R_Fail, $"像素类型转换失败，{ErrorMessage(nRet)}"));
                }
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }

            return bitmap;
        }

        #endregion

        /// <summary>
        /// 新纪元时间转换
        /// </summary>
        /// <param name="epochTime"></param>
        /// <returns></returns>
        public static DateTime ConverEpochTimeToDateTime(long epochTime)
        {
            //北京 东八时
            DateTime baseTime = new DateTime(1970, 1, 1, 8, 0, 0, DateTimeKind.Utc);
            long baseTicks = baseTime.Ticks;
            long epochTicks = epochTime * 10000000 + baseTicks;
            return new DateTime(epochTicks);
        }

        #endregion

        #region 参数信息

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

        public CameraParamInfo[] GetParamInfos()
        {
            return paramInfos.All();
        }

        #endregion

        #region 读取/设置参数

        // 参数的缓存
        CameraParams m_cameraParams = new CameraParams();

        /// <summary>
        /// 读取参数
        /// </summary>
        public CameraParams GetParams()
        {
            if (!IsOpen)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "相机未打开"));
                return new CameraParams();
            }

            if (offLine)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "相机断线"));
                return new CameraParams();
            }

            m_cameraParams = new CameraParams();
            m_cameraParams.ExposureTime = GetFloatParam(paramInfos.Exposure);
            if (paramInfos.Gain.Enabled)
                m_cameraParams.Gain = GetFloatParam(paramInfos.Gain);
            else if (paramInfos.PreampGain.Enabled)
                m_cameraParams.PreampGain = (PreampGainEnum?)GetEnumParam(paramInfos.PreampGain);
            m_cameraParams.ImageWidth = (int?)GetIntParam(paramInfos.Width);
            m_cameraParams.ImageHeight = (int?)GetIntParam(paramInfos.Height);
            m_cameraParams.TriggerDelay = GetFloatParam(paramInfos.TriggerDelay);

            return m_cameraParams;
        }

        /// <summary>
        /// 设置参数
        /// </summary>
        public bool SetParams(CameraParams @params)
        {
            if (!IsOpen)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机未打开"));
                return false;
            }
            if (offLine)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机断线"));
                return false;
            }

            bool result = true;

            if (@params.ExposureTime.HasValue )
            {
                result &= SetFloatParam(paramInfos.Exposure, @params.ExposureTime.Value);

                if (result)
                    m_cameraParams.ExposureTime = @params.ExposureTime;
            }

            if (paramInfos.Gain.Enabled && @params.Gain.HasValue )
            {
                //delete by LuoDian @ 20211119 调试中发现相机的增益设置不了，影响图像采集，暂时先拿掉
                //result &= SetFloatParam(paramInfos.Gain, @params.Gain.Value);

                if (result)
                    m_cameraParams.Gain = @params.Gain;
            }

            if (paramInfos.PreampGain.Enabled && @params.PreampGain.HasValue )
            {
                result &= SetEnumParam(paramInfos.PreampGain, @params.PreampGain.Value);

                if (result)
                    m_cameraParams.PreampGain = @params.PreampGain;
            }

            if (@params.ImageWidth.HasValue && @params.ImageWidth != m_cameraParams.ImageWidth)
            {
                result &= SetIntParam(paramInfos.Width, (uint)@params.ImageWidth.Value);

                if (result)
                    m_cameraParams.ImageWidth = @params.ImageWidth;
            }

            if (@params.ImageHeight.HasValue && @params.ImageHeight != m_cameraParams.ImageHeight)
            {
                result &= SetIntParam(paramInfos.Height, (uint)@params.ImageHeight.Value);

                if (result)
                    m_cameraParams.ImageHeight = @params.ImageHeight;
            }

            if (@params.TriggerDelay.HasValue && @params.TriggerDelay != m_cameraParams.TriggerDelay)
            {
                result &= SetFloatParam(paramInfos.TriggerDelay, @params.TriggerDelay.Value);

                if (result)
                    m_cameraParams.TriggerDelay = @params.TriggerDelay;
            }

            return result;
        }

        public object GetParam(string paramName)
        {
            if (!paramInfos.Contains(paramName))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"参数名\"{paramName}\"无效"));
                throw new CameraSDKException("读取参数失败，参数名无效");
            }

            CameraParamInfo2 p = paramInfos.GetParamInfo(paramName);

            if (!p.Enabled)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, $"参数名\"{paramName}\"无效"));
                throw new CameraSDKException("读取参数失败，参数名无效");
            }

            switch (p.Type.Name)
            {
                case "Int32":
                    return GetIntParam(p);
                case "Single":
                    return GetFloatParam(p);
                case "String":
                    return GetStringParam(p);
                case "Enum":
                    return GetEnumParam(p);
                default:
                    throw new CameraSDKException("无效的类型");
            }
        }

        public void SetParam(string paramName, object value)
        {
            if (!paramInfos.Contains(paramName))
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"参数名\"{paramName}\"无效"));
                throw new CameraSDKException("设置参数失败，参数名无效");
            }

            CameraParamInfo2 p = paramInfos.GetParamInfo(paramName);

            if (!p.Enabled)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"参数名\"{paramName}\"无效"));
                throw new CameraSDKException("设置参数失败，参数名无效");
            }

            if (p.ReadOnly)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"参数\"{paramName}\"是只读的"));
                throw new CameraSDKException("设置参数失败，参数是只读的");
            }

            switch (p.Type.Name)
            {
                case "Int32":
                    SetIntParam(p, (uint)value); break;
                case "Single":
                    SetFloatParam(p, (float)value); break;
                case "String":
                    SetStringParam(p, (string)value); break;
                case "Enum":
                    SetEnumParam(p, (uint)value); break;
                default:
                    throw new CameraSDKException("无效的类型");
            }
        }

        // 设置图像缓存数量，这个是 SDK 功能，非相机功能
        // 可能导致取像重复，慎用
        bool SetImageCacheCount(int count)
        {
            int nRet = m_camera.MV_CC_SetImageNodeNum_NET((uint)count);
            if (MyCamera.MV_OK == nRet)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"图片缓存 = {count}"));

                return true;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail,
                   message: $"设置参数\"图片缓存\"失败，{ErrorMessage(nRet)}"));
                return false;
            }
        }

        protected uint? GetIntParam(CameraParamInfo2 paramInfo)
        {
            MyCamera.MVCC_INTVALUE stParam = new MyCamera.MVCC_INTVALUE();
            int nRet = m_camera.MV_CC_GetIntValue_NET(paramInfo.Name, ref stParam);
            if (MyCamera.MV_OK == nRet)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description} = {stParam.nCurValue}"));
                return stParam.nCurValue;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}\"失败，{ErrorMessage(nRet)}"));
                return null;
            }
        }

        protected uint? GetEnumParam(CameraParamInfo2 paramInfo)
        {
            MyCamera.MVCC_ENUMVALUE stParam = new MyCamera.MVCC_ENUMVALUE();

            int nRet = m_camera.MV_CC_GetEnumValue_NET(paramInfo.Name, ref stParam);
            if (MyCamera.MV_OK == nRet)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description} = {stParam.nCurValue}"));
                return stParam.nCurValue;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}\"失败，{ErrorMessage(nRet)}"));
                return null;
            }
        }

        protected TEnum GetEnumParam<TEnum>(CameraParamInfo2 paramInfo) where TEnum : Enum
        {
            MyCamera.MVCC_ENUMVALUE stParam = new MyCamera.MVCC_ENUMVALUE();

            int nRet = m_camera.MV_CC_GetEnumValue_NET(paramInfo.Name, ref stParam);
            if (MyCamera.MV_OK == nRet)
            {
                TEnum @enum = (TEnum)(object)(int)stParam.nCurValue;

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description} = {@enum}"));
                return @enum;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}\"失败，{ErrorMessage(nRet)}"));
                throw new CameraSDKException("读取参数失败");
            }
        }

        protected float? GetFloatParam(CameraParamInfo2 paramInfo)
        {
            MyCamera.MVCC_FLOATVALUE stParam = new MyCamera.MVCC_FLOATVALUE();

            int nRet = m_camera.MV_CC_GetFloatValue_NET(paramInfo.Name, ref stParam);
            if (MyCamera.MV_OK == nRet)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description} = {stParam.fCurValue}"));
                return stParam.fCurValue;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}\"失败，{ErrorMessage(nRet)}"));
                return null;
            }
        }

        protected string GetStringParam(CameraParamInfo2 paramInfo)
        {
            MyCamera.MVCC_STRINGVALUE stParam = new MyCamera.MVCC_STRINGVALUE();

            int nRet = m_camera.MV_CC_GetStringValue_NET(paramInfo.Name, ref stParam);
            if (MyCamera.MV_OK == nRet)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description} = {stParam.chCurValue}"));
                return stParam.chCurValue;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}\"失败，{ErrorMessage(nRet)}"));
                return null;
            }
        }

        protected bool SetIntParam(CameraParamInfo2 paramInfo, uint value)
        {
            int nRet = m_camera.MV_CC_SetIntValue_NET(paramInfo.Name, value);
            if (nRet == MyCamera.MV_OK)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {value}"));
                return true;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{ErrorMessage(nRet)}"));
                return false;
            }
        }

        protected bool SetEnumParam(CameraParamInfo2 paramInfo, Enum @enum)
        {
            int nRet = m_camera.MV_CC_SetEnumValue_NET(paramInfo.Name, Convert.ToUInt32(@enum));
            if (nRet == MyCamera.MV_OK)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {@enum.ToDescription()}"));
                return true;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{ErrorMessage(nRet)}"));
                return false;
            }
        }

        protected bool SetEnumParam(CameraParamInfo2 paramInfo, uint value)
        {
            int nRet = m_camera.MV_CC_SetEnumValue_NET(paramInfo.Name, value);
            if (nRet == MyCamera.MV_OK)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {value}"));
                return true;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{ErrorMessage(nRet)}"));
                return false;
            }
        }

        protected bool SetFloatParam(CameraParamInfo2 paramInfo, float value)
        {
            int nRet = m_camera.MV_CC_SetFloatValue_NET(paramInfo.Name, value);
            if (nRet == MyCamera.MV_OK)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {value}"));
                return true;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{ErrorMessage(nRet)}"));
                return false;
            }
        }

        protected bool SetStringParam(CameraParamInfo2 paramInfo, string value)
        {
            int nRet = m_camera.MV_CC_SetStringValue_NET(paramInfo.Name, value);
            if (MyCamera.MV_OK == nRet)
            {
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {value}"));
                return true;
            }
            else
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{ErrorMessage(nRet)}"));
                return false;
            }
        }

        #endregion

        #region 私有方法

        // 判断是彩色照片还是黑白照片
        bool IsGrey(MyCamera.MvGvspPixelType enPixelType)
        {
            if (isGrey.HasValue) return isGrey.Value;

            switch (enPixelType)
            {
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12_Packed:
                    isGrey = true;
                    break;
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_YUV422_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_YUV422_YUYV_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_YCBCR411_8_CBYYCRYY:
                    isGrey = false;
                    break;
                default:
                    isGrey = null;
                    log?.Error(new CameraLogMessage(CameraInfo, A_Other, R_Fail, "无效的像素类型"));
                    break;
            }

            return isGrey.Value;
        }

        bool removeCustomPixelFormats(MyCamera.MvGvspPixelType enPixelFormat)
        {
            // 我也实在不知道海康这里是什么意思

            int nResult = ((int)enPixelFormat) & (unchecked((int)0x80000000));
#pragma warning disable CS0652 // 与整数常量比较无意义；该常量不在类型的范围之内
            if (0x80000000 == nResult)
#pragma warning restore CS0652 // 与整数常量比较无意义；该常量不在类型的范围之内
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

    }
}

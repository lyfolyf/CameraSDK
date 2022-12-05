using Basler.Pylon;
using CameraSDK.Models;
using GL.Kit.Log;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using static CameraSDK.CameraAction;
using static GL.Kit.Log.ActionResult;

namespace CameraSDK.Basler
{
    /// <summary>
    /// Basler 相机
    /// </summary>
    public class BaslerCamera : BaseCamera, ICamera, IDisposable
    {
        readonly Camera m_camera;

        readonly PixelDataConverter converter = new PixelDataConverter();

        long m_ImageBufSize = 0;

        // 参数的缓存
        CameraParams m_cameraParams = new CameraParams();

        CameraReadOnlyParams m_readOnlyParams = new CameraReadOnlyParams();

        BaslerParamInfoCollection paramInfos;

        //是否断线
        protected bool offLine = false;

        public BaslerCamera(IGLog log, ComCameraInfo cameraInfo)
            : base(cameraInfo, log)
        {
            m_camera = new Camera(CameraInfo.SN);

            paramInfos = new BaslerParamInfoCollection();
        }

        #region 打开/关闭

        /// <summary>
        /// 打开相机
        /// </summary>
        public void Open()
        {
            if (IsOpen) return;

            try
            {
                m_camera.Open();

                if (m_camera.IsOpen)
                {
                    IsOpen = true;

                    log?.Info(new CameraLogMessage(CameraInfo, A_Open, R_Success));

                    CameraInfo.ConnectionType = BaslerUtils.GetConnectionType(m_camera.GetSfncVersion());

                    //InitParams();

                    if (CameraInfo.ConnectionType == ConnectionType.GigE)
                    {
                        SetIntParam(paramInfos.HeartbeatTimeout, 500);
                    }

                    SetEnumParam(paramInfos.ExposureAuto, PLCamera.ExposureAuto.Off);
                    SetEnumParam(paramInfos.GainAuto, PLCamera.GainAuto.Off);

                    m_camera.StreamGrabber.ImageGrabbed += OnImageGrabbed;  // 注册采集回调函数
                    m_camera.ConnectionLost += OnConnectionLost;            // 注册掉线回调函数
                }
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_Open, R_Fail, e.Message));
                throw new CameraSDKException("相机打开失败");
            }
        }

        void InitParams()
        {
            if (CameraInfo.ConnectionType == ConnectionType.U3)
            {
                m_readOnlyParams.MaxExposureTime = (float?)GetMaxFloatParam(paramInfos.Exposure);
                m_readOnlyParams.MinExposureTime = (float?)GetMinFloatParam(paramInfos.Exposure);
                m_readOnlyParams.MaxGain = (float?)GetMaxFloatParam(paramInfos.Gain);
                m_readOnlyParams.MinGain = (float?)GetMinFloatParam(paramInfos.Gain);
            }
            else
            {
                m_readOnlyParams.MaxExposureTime = GetMaxIntParam(paramInfos.ExposureRaw);
                m_readOnlyParams.MinExposureTime = GetMinIntParam(paramInfos.ExposureRaw);
                m_readOnlyParams.MaxGain = GetMaxIntParam(paramInfos.GainRaw);
                m_readOnlyParams.MinGain = GetMinIntParam(paramInfos.GainRaw);
            }

            m_readOnlyParams.ImageMaxWidth = (int?)GetMaxIntParam(paramInfos.Width);
            m_readOnlyParams.ImageMinWidth = (int?)GetMinIntParam(paramInfos.Width);
            m_readOnlyParams.ImageMaxHeight = (int?)GetMaxIntParam(paramInfos.Height);
            m_readOnlyParams.ImageMinHeight = (int?)GetMinIntParam(paramInfos.Height);

            GetEnumParam(paramInfos.ExposureAuto);
            GetEnumParam(paramInfos.GainAuto);
        }

        /// <summary>
        /// 关闭相机
        /// </summary>
        public void Close()
        {
            if (!IsOpen) return;

            if (m_running)
                Stop();

            try
            {
                m_camera.Close();

                m_camera.StreamGrabber.ImageGrabbed -= OnImageGrabbed;
                m_camera.ConnectionLost -= OnConnectionLost;

                IsOpen = false;

                log?.Info(new CameraLogMessage(CameraInfo, A_Close, R_Success));
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_Close, R_Fail, e.Message));
                throw new CameraSDKException("相机关闭失败");
            }
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

        public bool SetIP(string ip, string subnetMask, string defaultGateway)
        {
            throw new CameraSDKException("Basler 相机暂未实现该功能");
        }

        #endregion

        #region 触发模式/触发源

        // 1300-60 和 1600-60 两个型号的相机，必须先停止采集，才可以切换模式

        /// <summary>
        /// 获取触发模式
        /// </summary>
        public TriggerMode GetTriggerMode()
        {
            string strTriggerMode = GetEnumParam(paramInfos.TriggerMode);
            return strTriggerMode == PLCamera.TriggerMode.Off ? TriggerMode.Continuous : TriggerMode.Trigger;
        }

        /// <summary>
        /// 设置触发模式
        /// <para>从连续模式设置成触发模式时，触发源会设置成软触发</para>
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

            if (m_running)
                StopGrab();

            SetEnumParam(paramInfos.TriggerMode, triggerMode == TriggerMode.Continuous ? PLCamera.TriggerMode.Off : PLCamera.TriggerMode.On);

            //Tip1：如果是Trigger模式，则不能StartBrab，否则StartGrab卡死。
            if (m_running && triggerMode == TriggerMode.Continuous)
                StartGrab();

            TriggerMode = triggerMode;
        }

        public TriggerSource GetTriggerSource()
        {
            string strTriggerSource = GetEnumParam(paramInfos.TriggerSource);
            return strTriggerSource == PLCamera.TriggerSource.Software ? TriggerSource.Software : TriggerSource.Extern;
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

            SetEnumParam(paramInfos.TriggerSource, triggerSource == TriggerSource.Software ? PLCamera.TriggerSource.Software : PLCamera.TriggerSource.Line1);

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

            //Tip2：从Continuous变为SoftTrigger时（即从实时到停止实时）不进行StartGrab。这里就需要再Start一下。否则会报无法软触发一次。
            //这个StartGrab不能放到SetTriggerMode的Tip1处。
            if (!m_camera.StreamGrabber.IsGrabbing)
                StartGrab();

            try
            {
                if (m_camera.WaitForFrameTriggerReady(1000, TimeoutHandling.ThrowException))
                {
                    m_camera.ExecuteSoftwareTrigger();

                    log?.Debug(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Success));
                }
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Fail, e.Message));
                throw new CameraSDKException("软触发失败");
            }
        }

        #endregion

        #region 开始/停止

        //Task acqImageTask;    // 这里用的是回调函数，没用主动获取

        /// <summary>
        /// 开始采集
        /// </summary>
        protected override void Start2(TriggerMode triggerMode, TriggerSource triggerSource)
        {
            SetEnumParam(paramInfos.AcquisitionMode, PLCamera.AcquisitionMode.Continuous);

            SetImageCacheCount(ImageCacheCount);

            GetImageBufSize();
            if (m_ImageBufSize == 0) return;

            try
            {
                StartGrab();

                m_running = true;

                log?.Info(new CameraLogMessage(CameraInfo, A_StartGrabbing, R_Success));

                //acqImageTask = new Task(() => AcquireImages(cts.Token), cts.Token, TaskCreationOptions.LongRunning);
                //acqImageTask.Start();
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_StartGrabbing, R_Fail, e.Message));
                throw new CameraSDKException("开始采集失败");
            }
        }

        /// <summary>
        /// 停止采集
        /// </summary>
        protected override void Stop2()
        {
            try
            {
                StopGrab();

                log?.Info(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Success));
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Fail, e.Message));
            }
        }

        void ForcedStop()
        {
            StopGrab();
        }

        void StartGrab()
        {
            if (!m_camera.StreamGrabber.IsGrabbing)
            {
                m_camera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            }
        }

        void StopGrab()
        {
            if (m_camera.StreamGrabber.IsGrabbing)
            {
                m_camera.StreamGrabber.Stop();
            }
        }

        #endregion

        #region 取像回调

        void OnImageGrabbed(object sender, ImageGrabbedEventArgs e)
        {
            try
            {
                IGrabResult grabResult = e.GrabResult;

                if (grabResult.GrabSucceeded)
                {
                    if (TriggerMode == TriggerMode.Trigger)
                    {
                        log?.Debug(new CameraLogMessage(CameraInfo, A_AcqImage, R_Success, $"FrameNum = {grabResult.ImageNumber}"));
                    }

                    TimeSpan ts = FuncWatch.ElapsedTime(() =>
                    {
                        IntPtr imageBuf = Marshal.AllocHGlobal((int)m_ImageBufSize);

                        converter.Convert(imageBuf, m_ImageBufSize, grabResult);

                        Bitmap bmp = ImageUtils.IntPtrToBitmap(isGrey.Value, grabResult.Width, grabResult.Height, imageBuf);

                        ReceivedImage(bmp, (int)grabResult.ImageNumber);
                    });
                    if (ts.TotalMilliseconds > 50 && TriggerMode == TriggerMode.Trigger)
                        log.Warn(new CameraLogMessage(CameraInfo, A_Copy, R_Success, "GetImage 执行时间异常", ts.TotalMilliseconds));
                }
                //else
                //{
                //    log?.Error(new CameraLogMessage(CameraInfo, A_AcqImage, R_Fail, grabResult.ErrorDescription));
                //    Thread.Sleep(5);
                //}
            }
            catch (Exception ex)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_AcqImage, R_Fail, ex.Message));
            }
            finally
            {
                e.DisposeGrabResultIfClone();
            }
        }

        #endregion

        #region 重连

        /// <summary>
        /// 掉线重连回调函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnConnectionLost(object sender, EventArgs e)
        {
            offLine = true;
            log?.Fatal(new CameraLogMessage(CameraInfo, A_Connect, R_Disconnect, "开始自动重连"));

            const int cTimeOutMs = 20;
            const int Interval = 20;

            try
            {
                ForcedStop();
                m_camera.Close();

                while (IsOpen)
                {
                    if (m_camera.Open(cTimeOutMs, TimeoutHandling.Return))
                    {
                        offLine = false;

                        log?.Info(new CameraLogMessage(CameraInfo, A_Connect, R_Success));

                        ReSetParams();

                        if (m_running)
                        {
                            Thread th = new Thread(() => {
                                m_running = false;
                                // 运行到 StartGrab 会卡死，不知道为什么
                                //Tip3：新起一个线程，Start里的StartGrab执行就不会卡死。
                                //结合Tip1、Tip2，貌似StopGrab和StartGrab同一个线程执行会卡死。
                                //但第一次开启相机，第一次执行，设置实时的时候，两个方法又不会卡死。第二次及以后就会卡死。
                                Start(TriggerMode, TriggerSource);
                            });
                            th.Start();
                        }
                        break;
                    }
                    else
                    {
                        Thread.Sleep(Interval);
                    }
                }

                if (!IsOpen)
                {
                    log?.Info(new CameraLogMessage(CameraInfo, A_Connect, R_Fail, "相机关闭，退出自动重连"));
                }
            }
            catch (Exception ex)
            {
                log?.Fatal(new CameraLogMessage(CameraInfo, A_Connect, R_Fail, "自动重连失败，" + ex.Message));
            }
        }

        #endregion

        #region 读取/设置参数

        /// <summary>
        /// 读取参数
        /// </summary>
        public CameraParams GetParams()
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "相机未打开"));
                return new CameraParams();
            }
            if (offLine)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "相机断线"));
                return new CameraParams();
            }

            m_cameraParams = new CameraParams();

            if (CameraInfo.ConnectionType == ConnectionType.U3)
            {
                m_cameraParams.ExposureTime = (float?)GetFloatParam(paramInfos.Exposure);
                m_cameraParams.Gain = (float?)GetFloatParam(paramInfos.Gain);
                m_cameraParams.TriggerDelay = (float?)GetFloatParam(paramInfos.TriggerDelay);
            }
            else
            {
                m_cameraParams.ExposureTime = GetIntParam(paramInfos.ExposureRaw);
                m_cameraParams.Gain = GetIntParam(paramInfos.GainRaw);
                m_cameraParams.TriggerDelay = (float?)GetFloatParam(paramInfos.TriggerDelayAbs);
            }
            m_cameraParams.ImageWidth = (int?)GetIntParam(paramInfos.Width);
            m_cameraParams.ImageHeight = (int?)GetIntParam(paramInfos.Height);

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
                if (CameraInfo.ConnectionType == ConnectionType.U3)
                    result &= SetFloatParam(paramInfos.Exposure, @params.ExposureTime.Value);
                else
                    result &= SetIntParam(paramInfos.ExposureRaw, (long)@params.ExposureTime.Value);

                if (result)
                    m_cameraParams.ExposureTime = @params.ExposureTime;
            }

            if (@params.Gain.HasValue )
            {
                if (CameraInfo.ConnectionType == ConnectionType.U3)
                    result &= SetFloatParam(paramInfos.Gain, @params.Gain.Value);
                else
                    result &= SetIntParam(paramInfos.GainRaw, (long)@params.Gain.Value);

                if (result)
                    m_cameraParams.Gain = @params.Gain;
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
                if (CameraInfo.ConnectionType == ConnectionType.U3)
                    result &= SetFloatParam(paramInfos.TriggerDelay, m_cameraParams.TriggerDelay.Value);
                else
                    result &= SetFloatParam(paramInfos.TriggerDelayAbs, m_cameraParams.TriggerDelay.Value);

                if (result)
                    m_cameraParams.TriggerDelay = @params.TriggerDelay;
            }

            return result;
        }

        void ReSetParams()
        {
            if (m_cameraParams.ExposureTime.HasValue)
            {
                if (CameraInfo.ConnectionType == ConnectionType.U3)
                    SetFloatParam(paramInfos.Exposure, m_cameraParams.ExposureTime.Value);
                else
                    SetIntParam(paramInfos.ExposureRaw, (long)m_cameraParams.ExposureTime.Value);
            }

            if (m_cameraParams.Gain.HasValue)
            {
                if (CameraInfo.ConnectionType == ConnectionType.U3)
                    SetFloatParam(paramInfos.Gain, m_cameraParams.Gain.Value);
                else
                    SetIntParam(paramInfos.GainRaw, (long)m_cameraParams.Gain.Value);
            }

            if (m_cameraParams.ImageWidth.HasValue)
            {
                SetIntParam(paramInfos.Width, (uint)m_cameraParams.ImageWidth.Value);
            }

            if (m_cameraParams.ImageHeight.HasValue)
            {
                SetIntParam(paramInfos.Height, (uint)m_cameraParams.ImageHeight.Value);
            }

            if (m_cameraParams.TriggerDelay.HasValue)
            {
                if (CameraInfo.ConnectionType == ConnectionType.U3)
                    SetFloatParam(paramInfos.TriggerDelay, m_cameraParams.TriggerDelay.Value);
                else
                    SetFloatParam(paramInfos.TriggerDelayAbs, m_cameraParams.TriggerDelay.Value);
            }
        }

        #region 参数值修正，暂时没用，值不对直接抛错

        Dictionary<string, object> incrementDict = new Dictionary<string, object>();

        // 修正值，确保设置的值是有效值，否则有可能报错
        long ReviseValue(CameraParamInfo2 paramInfo, long value, long minValue)
        {
            long increment = GetIncrement(paramInfo);

            if (increment > 0)
            {
                long temp = minValue + ((value - minValue) / increment * increment);

                if (temp != value)
                {
                    log?.Info(new CameraLogMessage(CameraInfo, "修正参数值", R_Success, $"{paramInfo.Description}: {value} -> {temp}"));

                    value = temp;
                }
            }

            return value;
        }

        // 获取增量
        long GetIncrement(CameraParamInfo2 paramInfo)
        {
            long increment;
            if (incrementDict.ContainsKey(paramInfo.Name))
            {
                increment = (long)incrementDict[paramInfo.Name];
            }
            else
            {
                increment = GetIncrementIntParam(paramInfo);
                incrementDict[paramInfo.Name] = increment;
            }

            return increment;
        }

        #endregion

        bool SetImageCacheCount(int count)
        {
            try
            {
                m_camera.Parameters[PLCameraInstance.MaxNumBuffer].SetValue(count);

                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"图片缓存 = {count}"));

                return true;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail,
                    message: $"设置参数\"图片缓存\"失败，{e.Message}"));
                return false;
            }
        }

        #region Int

        long? GetIntParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                long value = m_camera.Parameters[(IntegerName)paramInfo.Name].GetValue();

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description} = {value}"));

                return value;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}\"失败，{e.Message}"));
                return null;
            }
        }

        bool SetIntParam(CameraParamInfo2 paramInfo, long value)
        {
            try
            {
                m_camera.Parameters[(IntegerName)paramInfo.Name].SetValue(value);

                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {value}"));

                return true;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{e.Message}"));
                return false;
            }
        }

        long? GetMaxIntParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                long value = m_camera.Parameters[(IntegerName)paramInfo.Name].GetMaximum();

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description}最大值 = {value}"));

                return value;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}最大值\"失败，{e.Message}"));
                return null;
            }
        }

        long? GetMinIntParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                long value = m_camera.Parameters[(IntegerName)paramInfo.Name].GetMinimum();

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description}最小值 = {value}"));

                return value;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}最小值\"失败，{e.Message}"));
                return null;
            }
        }

        long GetIncrementIntParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                long value = m_camera.Parameters[(IntegerName)paramInfo.Name].GetIncrement();

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description}增量 = {value}"));

                return value;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}增量\"失败，{e.Message}"));
                return 0;
            }
        }

        #endregion

        #region Float

        double? GetFloatParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                double value = m_camera.Parameters[(FloatName)paramInfo.Name].GetValue();

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description} = {value}"));

                return value;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}\"失败，{e.Message}"));
                return null;
            }
        }

        bool SetFloatParam(CameraParamInfo2 paramInfo, double value)
        {
            try
            {
                m_camera.Parameters[(FloatName)paramInfo.Name].SetValue(value);

                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {value}"));

                return true;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{e.Message}"));
                return false;
            }
        }

        double? GetMaxFloatParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                double value = m_camera.Parameters[(FloatName)paramInfo.Name].GetMaximum();

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description}最大值 = {value}"));

                return value;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}最大值\"失败，{e.Message}"));
                return null;
            }
        }

        double? GetMinFloatParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                double value = m_camera.Parameters[(FloatName)paramInfo.Name].GetMinimum();

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description}最小值 = {value}"));

                return value;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}最小值\"失败，{e.Message}"));
                return null;
            }
        }

        #endregion

        #region Enum

        string GetEnumParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                string value = m_camera.Parameters[(EnumName)paramInfo.Name].GetValue();

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description} = {value}"));

                return value;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}\"失败，{e.Message}"));
                return null;
            }
        }

        bool SetEnumParam(CameraParamInfo2 paramInfo, string value)
        {
            try
            {
                m_camera.Parameters[(EnumName)paramInfo.Name].SetValue(value);

                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {value}"));

                return true;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{e.Message}"));
                return false;
            }
        }

        #endregion

        #region String

        bool SetStringParam(CameraParamInfo2 paramInfo, string value)
        {
            try
            {
                m_camera.Parameters[(StringName)paramInfo.Name].SetValue(value);

                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {value}"));

                return true;
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{e.Message}"));
                return false;
            }
        }

        #endregion

        #endregion

        #region 相机参数

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

        #region 其他私有方法

        // 判断是彩色照片还是黑白照片
        void IsGrey()
        {
            string format = GetEnumParam(paramInfos.PixelFormat);
            switch (format)
            {
                case "Mono8":
                    isGrey = true;
                    converter.OutputPixelFormat = PixelType.Mono8;
                    break;
                case "BGR8packed":
                    isGrey = false;
                    converter.OutputPixelFormat = PixelType.BGR8packed;
                    break;
                default:
                    isGrey = null;
                    break;
            }
        }

        void GetImageBufSize()
        {
            IsGrey();

            if (!m_cameraParams.ImageWidth.HasValue)
                m_cameraParams.ImageWidth = (int?)GetIntParam(paramInfos.Width);
            if (!m_cameraParams.ImageHeight.HasValue)
                m_cameraParams.ImageHeight = (int?)GetIntParam(paramInfos.Height);

            int payloadSize = m_cameraParams.ImageWidth.Value * m_cameraParams.ImageHeight.Value;

            m_ImageBufSize = isGrey.Value ? payloadSize : payloadSize * 3;
        }

        #endregion

        public void Dispose()
        {
            if (m_camera != null)
                m_camera.Dispose();
        }


    }
}

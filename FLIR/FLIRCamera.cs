using CameraSDK.Models;
using GL.Kit.Log;
using SpinnakerNET;
using SpinnakerNET.GenApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using static CameraSDK.CameraAction;
using static GL.Kit.Log.ActionResult;

namespace CameraSDK.FLIR
{
    class FLIRCamera : BaseCamera, ICamera, IDisposable
    {
        // 拷贝图像的最大耗时，超过则产生一条警告日志
        const int CopyImageMaxTime = 50;

        IManagedCamera m_camera;
        INodeMap m_nodeMap;

        FLIRParamInfoCollection paramInfos;

        public FLIRCamera(IManagedCamera camera, IGLog log, ComCameraInfo cameraInfo)
            : base(cameraInfo, log)
        {
            m_camera = camera;

            paramInfos = new FLIRParamInfoCollection();
        }

        #region 打开/关闭

        public void Open()
        {
            if (IsOpen) return;

            try
            {
                m_camera.Init();

                IsOpen = true;

                log?.Info(new CameraLogMessage(CameraInfo, A_Open, R_Success));

                m_nodeMap = m_camera.GetNodeMap();

                SetExposureAuto(ExposureAutoEnums.Off.ToString());
                SetGainAuto(GainAutoEnums.Off.ToString());
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_Open, R_Fail, e.Message));
                throw new CameraSDKException("打开相机失败");
            }
        }

        public void Close()
        {
            if (!IsOpen) return;

            if (m_running)
                Stop();

            try
            {
                m_camera.DeInit();

                IsOpen = false;

                log?.Info(new CameraLogMessage(CameraInfo, A_Close, R_Success));
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_Close, R_Fail, e.Message));
            }
        }

        #endregion

        #region 重命名/设置 IP

        public bool Rename(string newUserID)
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_Rename, R_Fail, "相机未打开"));

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
            if (CameraInfo.ConnectionType == ConnectionType.U3)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "U3 口相机不可以设置 IP"));
                throw new CameraSDKException("U3 口相机不可以设置 IP");
            }

            if (IsOpen)
            {
                throw new CameraSDKException("请先关闭相机");
            }

            try
            {
                if (m_camera == null || !m_camera.IsInitialized())
                    throw new CameraSDKException("相机未初始化！");

                // 更新参数值之前需要先停止采集
                m_camera.EndAcquisition();

#pragma warning disable CS0618 // 类型或成员已过时
                m_camera.GevPersistentIPAddress.Value = System.Net.IPAddress.Parse(ip).Address;
#pragma warning restore CS0618 // 类型或成员已过时
                //m_camera.GevPersistentSubnetMask.Value

                return true;
            }
            catch (SpinnakerException ex)
            {
                throw new CameraSDKException($"设置相机[{CameraInfo.UserDefinedName}]的IP地址失败!原因：{ex.Message}");
            }
        }

        #endregion

        #region 触发模式/触发源

        public TriggerMode GetTriggerMode()
        {
            return m_camera.TriggerMode.Value == TriggerModeEnums.Off.ToString() ? TriggerMode.Continuous : TriggerMode.Trigger;
        }

        public override void SetTriggerMode(TriggerMode triggerMode)
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机未打开"));
                throw new CameraSDKException("设置触发模式失败，相机未打开");
            }

            try
            {
                m_camera.TriggerMode.Value = triggerMode == TriggerMode.Continuous ? TriggerModeEnums.Off.ToString() : TriggerModeEnums.On.ToString();

                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"TriggerMode = {triggerMode}"));

                TriggerMode = triggerMode;
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置参数\"TriggerMode\"失败，{e.Message}"));
            }
        }

        public TriggerSource GetTriggerSource()
        {
            return m_camera.TriggerSource.Value == TriggerSourceEnums.Software.ToString() ? TriggerSource.Software : TriggerSource.Extern;
        }

        public override void SetTriggerSource(TriggerSource triggerSource)
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机未打开"));
                throw new CameraSDKException("设置触发源失败，相机未打开");
            }

            try
            {
                m_camera.TriggerSource.Value = triggerSource == TriggerSource.Software ? TriggerSourceEnums.Software.ToString() : TriggerSourceEnums.Line0.ToString();

                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"TriggerSource = {triggerSource}"));

                TriggerSource = triggerSource;
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置参数\"TriggerSource\"失败，{e.Message}"));
            }
        }

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

            try
            {
                ICommand iTriggerSoftware = m_nodeMap.GetNode<ICommand>("TriggerSoftware");

                iTriggerSoftware.Execute();

                log?.Debug(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Success));
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SoftTrigger, R_Fail, e.Message));
                throw new CameraSDKException("软触发失败");
            }
        }

        #endregion

        #region 开始/停止

        CancellationTokenSource cts;
        Task acqImageTask;

        protected override void Start2(TriggerMode triggerMode, TriggerSource triggerSource)
        {
            SetAcquisitionMode(AcquisitionModeEnums.Continuous.ToString());

            try
            {
                m_camera.BeginAcquisition();

                m_running = true;

                log?.Info(new CameraLogMessage(CameraInfo, A_StartGrabbing, R_Success));

                cts = new CancellationTokenSource();
                acqImageTask = new Task(() => AcquireImages(cts.Token), cts.Token, TaskCreationOptions.LongRunning);
                acqImageTask.Start();
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_StartGrabbing, R_Fail, e.Message));
                throw new CameraSDKException("开始采集失败");
            }
        }

        protected override void Stop2()
        {
            cts.Cancel();

            m_camera.EndAcquisition();

            log?.Info(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Success));
        }

        #endregion

        #region 接收图像

        ushort idx = 0;

        void AcquireImages(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (IManagedImage rawImage = m_camera.GetNextImage(1000))
                    {
                        idx++;

                        if (rawImage.IsIncomplete)
                        {
                            log?.Error(new CameraLogMessage(CameraInfo, A_AcqImage, R_Fail, $"Image incomplete with image status {rawImage.ImageStatus}"));
                            continue;
                        }

                        if (TriggerMode == TriggerMode.Trigger)
                        {
                            log?.Debug(new CameraLogMessage(CameraInfo, A_AcqImage, R_Success, $"idx = {idx}  FrameNum = {rawImage.FrameID}"));
                        }

                        IsGrey(rawImage.PixelFormat);

                        Bitmap bmp;
                        if (isGrey.Value)
                        {
                            using (IManagedImage convertedImage = rawImage.Convert(PixelFormatEnums.Mono8))
                            {
                                bmp = GetImage(convertedImage);
                            }
                        }
                        else
                        {
                            using (IManagedImage convertedImage = rawImage.Convert(PixelFormatEnums.BGR8))
                            {
                                bmp = GetImage(convertedImage);
                            }
                        }

                        ReceivedImage(bmp, (int)rawImage.FrameID);
                    }
                }
                catch (SpinnakerException e)
                {
                    if (e.ErrorCode != Error.SPINNAKER_ERR_TIMEOUT)
                        log?.Error(new CameraLogMessage(CameraInfo, A_AcqImage, R_Fail, e.Message));
                }
            }
        }

        Bitmap GetImage(IManagedImage rawImage)
        {
            (TimeSpan ts, Bitmap bmp) = FuncWatch.ElapsedTime(() =>
            {
                return ImageUtils.DeepCopyIntPtrToBitmap(isGrey.Value, (int)rawImage.Width, (int)rawImage.Height, rawImage.DataPtr);
            });
            if (ts.TotalMilliseconds > CopyImageMaxTime && TriggerMode == TriggerMode.Trigger)
                log?.Warn(new CameraLogMessage(CameraInfo, A_Copy, R_Success, "图像 Copy 耗时过长", ts.TotalMilliseconds));

            return bmp;
        }

        #endregion

        #region 参数

        // 参数的缓存
        CameraParams m_cameraParams = new CameraParams();

        public CameraParams GetParams()
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "相机未打开"));

                return new CameraParams();
            }

            m_cameraParams = new CameraParams();
            m_cameraParams.ExposureTime = (float?)GetFloatParam(paramInfos.Exposure);
            m_cameraParams.Gain = (float?)GetFloatParam(paramInfos.Gain);

            m_cameraParams.ImageWidth = (int?)GetIntParam(paramInfos.Width);
            m_cameraParams.ImageHeight = (int?)GetIntParam(paramInfos.Height);
            m_cameraParams.TriggerDelay = (float?)GetFloatParam(paramInfos.TriggerDelay);

            return m_cameraParams;
        }

        public bool SetParams(CameraParams @params)
        {
            if (!IsOpen)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机未打开"));
                return false;
            }

            bool result = true;

            if (@params.ExposureTime.HasValue )
            {
                result &= SetFloatParam(paramInfos.Exposure, @params.ExposureTime.Value);

                if (result)
                    m_cameraParams.ExposureTime = @params.ExposureTime;
            }

            if (@params.Gain.HasValue )
            {
                result &= SetFloatParam(paramInfos.Gain, @params.Gain.Value);

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
                result &= SetFloatParam(paramInfos.TriggerDelay, @params.TriggerDelay.Value);

                if (result)
                    m_cameraParams.TriggerDelay = @params.TriggerDelay;
            }

            return result;
        }

        #endregion

        #region

        bool SetAcquisitionMode(string acquisitionMode)
        {
            try
            {
                m_camera.AcquisitionMode.Value = acquisitionMode;
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"AcquisitionMode = {acquisitionMode}"));
                return true;
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置参数\"AcquisitionMode\"失败，{e.Message}"));
                return false;
            }
        }

        bool SetExposureAuto(string exposureAuto)
        {
            try
            {
                m_camera.ExposureAuto.Value = exposureAuto;
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"ExposureAuto = {exposureAuto}"));
                return true;
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置参数\"ExposureAuto\"失败，{e.Message}"));
                return false;
            }
        }

        bool SetGainAuto(string gainAuto)
        {
            try
            {
                m_camera.GainAuto.Value = gainAuto;
                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"GainAuto = {gainAuto}"));
                return true;
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, $"设置参数\"GainAuto\"失败，{e.Message}"));
                return false;
            }
        }

        long? GetIntParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                IInteger intParam = m_nodeMap.GetNode<IInteger>(paramInfo.Name);

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description} = {intParam.Value}"));

                return intParam.Value;
            }
            catch (SpinnakerException e)
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
                IInteger intParam = m_nodeMap.GetNode<IInteger>(paramInfo.Name);

                intParam.Value = value;

                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {value}"));

                return true;
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{e.Message}"));
                return false;
            }
        }

        double? GetFloatParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                IFloat floatParam = m_nodeMap.GetNode<IFloat>(paramInfo.Name);

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description} = {floatParam.Value}"));

                return floatParam.Value;
            }
            catch (SpinnakerException e)
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
                IFloat floatParam = m_nodeMap.GetNode<IFloat>(paramInfo.Name);

                floatParam.Value = value;

                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {value}"));

                return true;
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{e.Message}"));
                return false;
            }
        }

        string GetStringParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                IString stringParam = m_nodeMap.GetNode<IString>(paramInfo.Name);

                log?.Info(new CameraLogMessage(CameraInfo, A_GetParam, R_Success, $"{paramInfo.Description} = {stringParam.Value}"));

                return stringParam.Value;
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"读取参数\"{paramInfo.Description}\"失败，{e.Message}"));
                return null;
            }
        }

        bool SetStringParam(CameraParamInfo2 paramInfo, string value)
        {
            try
            {
                IString stringParam = m_nodeMap.GetNode<IString>(paramInfo.Name);

                stringParam.Value = value;

                log?.Info(new CameraLogMessage(CameraInfo, A_SetParam, R_Success, $"{paramInfo.Description} = {value}"));

                return true;
            }
            catch (SpinnakerException e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail,
                    message: $"设置参数\"{paramInfo.Description}\"失败，{e.Message}"));
                return false;
            }
        }

        #endregion

        #region 参数信息

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

        #endregion

        void IsGrey(PixelFormatEnums pixelFormat)
        {
            if (isGrey.HasValue) return;

            switch (pixelFormat)
            {
                case PixelFormatEnums.Mono8:
                case PixelFormatEnums.Mono10:
                case PixelFormatEnums.Mono12:
                case PixelFormatEnums.Mono14:
                case PixelFormatEnums.Mono16:
                    isGrey = true;
                    break;
                default:
                    isGrey = false;
                    break;
            }
        }

        public void Dispose()
        {
            m_camera?.Dispose();
        }
    }
}

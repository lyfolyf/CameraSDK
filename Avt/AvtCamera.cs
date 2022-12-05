using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AVT.VmbAPINET;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Drawing;
using System.Diagnostics;
using System.ComponentModel;
using CameraSDK.Models;
using static CameraSDK.CameraAction;
using static GL.Kit.Log.ActionResult;

namespace CameraSDK.Avt
{
    public class AvtCamera : BaseCamera, ICamera, IDisposable
    {
        public AvtCamera(GL.Kit.Log.IGLog log, Models.ComCameraInfo cameraInfo) : base(cameraInfo, log)
        {
            paramInfos = new CameraParamInfoCollection();

            this.id = cameraInfo.SN;
            Thread thread = new Thread(FrameMonitor) { IsBackground = true };
            thread.Start();
        }

        private void FrameMonitor()
        {
            while (true)
            {
                if (FrameCount() > 0)
                {
                    Frame frame = TakeFrame();
                    //Frame frame = new Frame(frame1.Buffer);
                    //frame = frame1;
                    try
                    {
                        // Convert frame into displayable image
                        Bitmap image = ConvertFrame(frame);
                        ReceivedImage(image, (int)frame.FrameID);
                        if (null != image)
                        {
                            //cImage = image;
                            OnImageTook(new ImageTookEventArgs { CameraId = Id, Image = image, Success = true });
                        }
                        else
                        {
                            log?.Error(new CameraLogMessage(CameraInfo, A_AcqImage, R_Fail, "收到帧但转换为图像失败！"));
                            OnImageTook(new ImageTookEventArgs { CameraId = Id, Image = null, Success = false });
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Error(new CameraLogMessage(CameraInfo, A_AcqImage, R_Fail, ex.Message));
                        OnImageTook(new ImageTookEventArgs { CameraId = Id, Image = null, Success = false });
                    }
                    Thread.Sleep(15);
                    //frame = null;
                    GC.Collect();
                }
                else { Thread.Sleep(15); }
            }
        }

        Queue<Frame> qFrames = new Queue<Frame>();

        public Dictionary<string, object> dic = new Dictionary<string, object>();
        private Camera m_camera = null;
        private string sn;
        double exposure = 0;
        double gain = 0;
        /// <summary>
        /// Flag for determining the availability of a suitable software trigger
        /// </summary>
        private bool m_IsTriggerAvailable = false;
        /// <summary>
        /// Flag to remember if acquisition is running
        /// </summary>
        private bool m_Acquiring = false;

        private bool opened = false;
        public bool IsTriggerAvailable
        {
            get { return m_IsTriggerAvailable; }
        }
        string id = null;

        Stopwatch sw = new Stopwatch();

        private double elapsed = 0;
        public double Elapsed
        {
            get
            {
                return elapsed;
            }
        }

        private bool imageBack = true;

        public static CameraListChangedHandler cameraListChangedHandler = null;

        public event ImageTookHandler ImageTook = null;
        public event Action ParamChanged = null;

        private string name = string.Empty;

        private Bitmap cImage = null;

        object synLock = new object();

        object synLock2 = new object();
        bool triggerModeSet = false;
        bool bStart = false;

        private void PushFrame(Frame frame)
        {
            lock (synLock2)
            {
                qFrames.Enqueue(frame);
            }
        }
        //int width = 0;
        //int height = 0;
        private Frame TakeFrame()
        {
            lock (synLock2)
            {
                if (qFrames.Count > 0)
                    return qFrames.Dequeue();
                else
                    return null;
            }
        }

        private int FrameCount()
        {
            lock (synLock2)
            {
                return qFrames.Count;
            }
        }

        public string Id
        {
            get { return id; }
            set
            {
                id = value;
            }
        }
        public string Name
        {
            get
            {
                return name;
            }

            set
            {
                name = value;
            }
        }

        static bool ready = true;
        public void DeviceReset()
        {
            if (m_Acquiring)
                StopContinuousImageAcquisition();
            ready = false;
            this.m_camera.Features["DeviceReset"].RunCommand();
            this.m_Acquiring = false;
            this.m_camera.Close();
            //this.m_Camera = null;
            //Thread.Sleep(100);
            //while (!ready)
            //{
            //    Thread.Sleep(10);
            //}
            //while (!AvtHelper.CameraList.Exists(k => k.ID == id))
            //{
            //    Thread.Sleep(1000);
            //}
            //Thread.Sleep(1000);
            //OpenCamera();
        }
        public double Exposure
        {
            get
            {

                var val = GetParam("ExposureTime");
                if (val == null)
                {
                    val = GetParam("ExposureTimeAbs");
                }
                if (val != null)
                    exposure = val;
                return exposure;
            }
            set
            {
                // ReSharper disable once SpecifyACultureInStringConversionExplicitly
                if (!SetParam("ExposureTime", value.ToString()))
                    SetParam("ExposureTimeAbs", value.ToString());
                exposure = value;
                ParamChanged?.Invoke();
            }
        }
        public double Gain
        {
            get
            {
                var val = GetParam("Gain");
                if (val == null)
                {
                    val = GetParam("GainAbs");
                }
                if (val != null)
                    gain = val;
                return gain;
            }
            set
            {
                if (!SetParam("Gain", value.ToString()))
                    SetParam("GainAbs", value.ToString());
                gain = value;
                ParamChanged?.Invoke();
            }
        }

        double gamma = 0;
        public double Gamma
        {
            get
            {
                var val = GetParam("Gamma");
                if (val == null)
                {
                    val = GetParam("GammaAbs");
                }
                if (val != null)
                    gamma = val;
                return gamma;
            }
            set
            {
                if (!SetParam("Gamma", value.ToString()))
                    SetParam("GammaAbs", value.ToString());
                gamma = value;
                ParamChanged?.Invoke();
            }
        }

        public bool Opened
        {
            get
            {
                return opened;
            }

            private set
            {
                opened = value;
            }
        }

        //ManualResetEvent mre = new ManualResetEvent(false);
        //public ManualResetEvent Mre
        //{
        //    get
        //    {
        //        return mre;
        //    }

        //    private set
        //    {
        //        mre = value;
        //    }
        //}

        public static void Startup()
        {
            cameraListChangedHandler = OnCameraListChange;
            AvtHelper.Startup(cameraListChangedHandler);
        }

        public static void OnCameraListChange(object sender, CameraListChangedEventArgs args)
        {
            //ListCameras();
            switch (args.Reason)
            {
                case VmbUpdateTriggerType.VmbUpdateTriggerPluggedIn:
                    ready = false;
                    break;
                case VmbUpdateTriggerType.VmbUpdateTriggerPluggedOut:
                    ready = true;
                    break;
                case VmbUpdateTriggerType.VmbUpdateTriggerOpenStateChanged:
                    break;
                default:
                    break;
            }

        }

        public static void Shutdown()
        {
            AvtHelper.Shutdown();
        }
        public static List<string> ListCameras()
        {
            List<string> idList = new List<string>();
            var camList = AvtHelper.CameraList;
            foreach (var info in camList)
            {
                idList.Add(info.ID);
            }
            return idList;
        }

        /// <summary>
        /// Opens the camera
        /// </summary>
        /// <param name="id">The camera ID</param>
        public void OpenCamera()
        {
            if (triggerModeSet == false)
            {
                triggerModeSet = true;

                // Check parameters
                if (null == Id)
                {
                    throw new ArgumentNullException("id");
                }

                // Check if API has been started up at all
                if (null == AvtHelper.Vimba)
                {
                    throw new Exception("Vimba is not started.");
                }

                // Open camera
                if (null == this.m_camera)
                {
                    if (!AvtHelper.CameraList.Exists(k => k.ID == id))
                    {
                        //return;
                        throw new ArgumentNullException("camera not online.");
                    }
                    //m_Camera = AvtHelper.Vimba.Cameras[id];

                    //m_Camera.Open(VmbAccessModeType.VmbAccessModeFull);
                    this.m_camera = AvtHelper.Vimba.OpenCameraByID(Id, VmbAccessModeType.VmbAccessModeFull);
                    //m_RingBitmap = new RingBitmap(m_RingBitmapSize);
                    sn = m_camera.SerialNumber;

                    if (null == this.m_camera)
                    {
                        throw new NullReferenceException("No camera retrieved.");
                    }

                    LoadCameraSettings();
                }

                // Determine if a suitable trigger can be found
                m_IsTriggerAvailable = false;
                if (this.m_camera.Features.ContainsName("TriggerSoftware") && this.m_camera.Features["TriggerSoftware"].IsWritable())
                {
                    EnumEntryCollection entries = this.m_camera.Features["TriggerSelector"].EnumEntries;
                    foreach (EnumEntry entry in entries)
                    {
                        if (entry.Name == "FrameStart")
                        {
                            m_IsTriggerAvailable = true;
                            break;
                        }
                    }
                }

                //if (GetParam("DeviceLinkThroughputLimitMode") == "On")
                {
                    //SetParam("DeviceLinkThroughputLimit", "100000000");
                    //SetParam("DeviceLinkThroughputLimitMode", "On");
                }
                if (GetParam("PixelFormat") != "BayerRG8")
                {
                    SetParam("PixelFormat", "BayerRG8");
                }
                //width = (int)GetParam("Width");
                //height = (int)GetParam("Height");

                AvtBalanceConfig cfg = AvtBalanceConfig.LoadConfig(id);

                SetParam("BalanceRatioSelector", "Blue");
                SetParam("BalanceRatio", cfg.BlueRatio.ToString());

                SetParam("BalanceRatioSelector", "Red");
                SetParam("BalanceRatio", cfg.RedRatio.ToString());
            }
            else
            {
                StartContinuousImageAcquisition();
            }
            opened = true;
        }


        /// <summary>
        /// Starts the continuous image acquisition and opens the camera
        /// Registers the event handler for the new frame event
        /// </summary>
        public void StartContinuousImageAcquisition()
        {
            bool bError = true;
            try
            {
                // Register frame callback
                this.m_camera.OnFrameReceived += this.OnFrameReceived;

                // Reset member variables

                this.m_Acquiring = true;
                m_running = m_Acquiring;
                // Start synchronous image acquisition (grab)
                bError = false;
                //return;
                this.m_camera.StartContinuousImageAcquisition(30);
                bError = false;
            }
            finally
            {
                // Close camera already if there was an error
                if (true == bError)
                {
                    try
                    {
                        this.CloseCamera();
                    }
                    catch
                    {
                        // Do Nothing
                    }
                }
            }
        }
        public void CloseCamera()
        {
            StopContinuousImageAcquisition();
            opened = false;
            return;
            if (null != this.m_camera)
            {
                //SaveCameraSettings();
                // We can use cascaded try-finally blocks to release the
                // camera step by step to make sure that every step is executed.
                try
                {
                    try
                    {
                        try
                        {
                            //if (null != this.ImageTook)
                            {
                                this.m_camera.OnFrameReceived -= this.OnFrameReceived;
                            }
                        }
                        finally
                        {
                            if (true == this.m_Acquiring)
                            {
                                this.m_Acquiring = false;
                                StopContinuousImageAcquisition();
                            }
                        }
                        //m_Camera.EndCapture();
                        //m_Camera.FlushQueue();
                        //m_Camera.RevokeAllFrames();
                    }
                    finally
                    {
                        this.m_camera.Close();
                    }
                }
                finally
                {
                    this.m_camera = null;
                    AvtHelper.ConnectedCamerasCount--;
                }

            }

        }
        /// <summary>
        /// Stops the image acquisition
        /// </summary>
        public void StopContinuousImageAcquisition()
        {
            // Check if API has been started up at all
            if (null == AvtHelper.Vimba)
            {
                throw new Exception("Vimba is not started.");
            }

            // Check if no camera is open
            if (null == this.m_camera)
            {
                return;
                throw new Exception("No camera open.");
            }

            if (null != this.m_camera)
            {
                // We can use cascaded try-finally blocks to release the
                // camera step by step to make sure that every step is executed.

                try
                {
                    this.m_camera.OnFrameReceived -= this.OnFrameReceived;
                }
                finally
                {
                    if (true == this.m_Acquiring)
                    {
                        this.m_Acquiring = false;
                        m_running = m_Acquiring;
                        try
                        {
                            this.m_camera.StopContinuousImageAcquisition();
                        }
                        catch { }
                    }
                }
            }
        }

        /// <summary>
        /// Handles the Frame Received event
        /// Converts the image to be displayed and queues it
        /// </summary>
        /// <param name="frame">The Vimba frame</param>
        private void OnFrameReceived(Frame frame)
        {
            sw.Stop();
            elapsed = sw.ElapsedMilliseconds;
            imageBack = true;
            PushFrame(frame);
            log?.Info($"AVT相机[{m_camera.Name}]获取图像数据成功！");
            //We make sure to always return the frame to the API if we are still acquiring
            if (true == this.m_Acquiring)
            {
                try
                {
                    this.m_camera.QueueFrame(frame);
                }
                catch (Exception exception)
                {
                    log?.Error($"AVT相机[{m_camera.Name}]清楚图像缓存数据失败！");
                }
            }
            //Mre.Set();
        }
        public void EnableSoftwareTrigger(bool enable)
        {
            try
            {
                ////频繁设置触发模式会导致相机异常，加次开关限制频繁设置触发模式
                //if (triggerModeSet == false)
                //{
                //    triggerModeSet = true;
                //}
                //else
                //{
                //    return;
                //}
                //var features = m_Camera.Features;
                //var feature = features["AcquisitionStop"];
                //feature.RunCommand();
                //m_Camera.EndCapture();
                //m_Camera.FlushQueue();
                //m_Camera.RevokeAllFrames();
                if (m_Acquiring)
                {
                    StopContinuousImageAcquisition();
                }

                if (this.m_camera != null)
                {
                    var featureValueMode = enable ? "On" : "Off";

                    // Set the trigger selector to FrameStart
                    this.m_camera.Features["TriggerSelector"].EnumValue = "FrameStart";
                    //this.m_Camera.Features["TriggerSelector"].EnumValue = "AcquisitionStart";
                    // Select the software trigger
                    this.m_camera.Features["TriggerSource"].EnumValue = "Software";
                    // And switch it on or off
                    this.m_camera.Features["TriggerMode"].EnumValue = featureValueMode;
                }

                //feature = features["AcquisitionMode"];
                //feature.EnumValue = "Continuous";

                //feature = features["AcquisitionStart"];
                //feature.RunCommand();

                if (!m_Acquiring)
                {
                    StartContinuousImageAcquisition();
                }
            }
            catch (Exception ex)
            {

            }
        }
        /// <summary>
        /// Sends a software trigger to the camera to
        /// </summary>
        public bool TriggerSoftwareTrigger()
        {
            int num = 0;
            while (/*sw.IsRunning &*/ !imageBack)
            {
                Thread.Sleep(2);
                num++;
                if (num == 1500)
                {
                    sw.Stop();
                    imageBack = true;
                    if (true == this.m_Acquiring)
                    {
                        try
                        {
                            this.m_camera.QueueFrame(new Frame(1));
                        }
                        catch (Exception exception)
                        {

                        }
                    }
                    return false;
                }
            }
            if (null != this.m_camera)
            {
                sw.Restart();
                imageBack = false;
                //Frame frame = null;
                //try
                //{
                //    m_camera.AcquireSingleImage(ref frame, 10000);
                //    //PushFrame(frame);
                //}
                //catch(Exception ex)
                //{
                //    log?.Error($"AVT相机取像超时！信息：{ex.Message}");
                //}
                bool ret = SetParam("TriggerSoftware");
                log?.Error($"AVT相机[{m_camera.Name}]触发信号已给！");
                return ret;
            }

            return false;
        }
        [Obsolete]
        private Bitmap Snap()
        {
            bool ret = TriggerSoftwareTrigger();
            if (!ret)
            {
                return null;
            }
            lock (synLock)
            {
                if (cImage != null)
                {
                    var bmp = cImage.Clone() as Bitmap;
                    //cImage.Dispose();
                    return bmp;
                }
                else
                {
                    return null;
                }
            }
        }
        /// <summary>
        /// Convert frame to displayable image and queue it in ring bitmap
        /// </summary>
        /// <param name="frame">The Vimba Frame containing the image</param>
        /// <returns>The Image extracted from the Vimba frame</returns>
        private Bitmap ConvertFrame(Frame frame)
        {
            if (null == frame)
            {
                //MessageBox.Show("null == frame");
                //throw new ArgumentNullException("frame");
                return null;
            }

            // Check if the image is valid
            if (VmbFrameStatusType.VmbFrameStatusComplete != frame.ReceiveStatus)
            {
                //MessageBox.Show("Invalid frame received. Reason: " + frame.ReceiveStatus.ToString());
                //throw new Exception("Invalid frame received. Reason: " + frame.ReceiveStatus.ToString());
                return null;
            }

            // define return variable
            int width = (int)frame.Width;
            int height = (int)frame.Height;
            Bitmap image = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            isGrey = false;
            // check if current image is in use,
            // if not we drop the frame to get not in conflict with GUI
            switch (frame.PixelFormat)
            {
                case VmbPixelFormatType.VmbPixelFormatMono8:
                    {
                        isGrey = true;
                        image = new Bitmap((int)frame.Width, (int)frame.Height, PixelFormat.Format8bppIndexed);

                        //Set greyscale palette
                        ColorPalette palette = image.Palette;
                        for (int i = 0; i < palette.Entries.Length; i++)
                        {
                            palette.Entries[i] = Color.FromArgb(i, i, i);
                        }
                        image.Palette = palette;

                        //Copy image data
                        BitmapData bitmapData = image.LockBits(new Rectangle(0, 0, (int)frame.Width, (int)frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
                        try
                        {
                            //Copy image data line by line
                            for (int y = 0; y < (int)frame.Height; y++)
                            {
                                System.Runtime.InteropServices.Marshal.Copy(frame.Buffer,
                                                                                y * (int)frame.Width,
                                                                                new IntPtr(bitmapData.Scan0.ToInt64() + y * bitmapData.Stride),
                                                                                (int)frame.Width);
                            }
                        }
                        finally
                        {
                            image.UnlockBits(bitmapData);
                        }
                    }
                    break;
                case VmbPixelFormatType.VmbPixelFormatBgr8:
                    {
                        image = new Bitmap((int)frame.Width, (int)frame.Height, PixelFormat.Format24bppRgb);

                        //Copy image data
                        BitmapData bitmapData = image.LockBits(new Rectangle(0, 0, (int)frame.Width, (int)frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                        try
                        {
                            //Copy image data line by line
                            for (int y = 0; y < (int)frame.Height; y++)
                            {
                                Marshal.Copy(frame.Buffer, y * ((int)frame.Width) * 3, new IntPtr(bitmapData.Scan0.ToInt64() + y * bitmapData.Stride), ((int)(frame.Width) * 3));
                            }
                        }
                        finally
                        {
                            image.UnlockBits(bitmapData);
                        }

                    }
                    break;

                default:
                    {
                        // Convert raw frame data into image (for image display)

                        try
                        {
                            frame.Fill(ref image);
                        }
                        catch (Exception ex)
                        {
                            //MessageBox.Show(ex.ToString());
                            image.Save(@"D:\9.bmp");
                            throw ex;
                        }

                        break;
                    }
            }
            return image;
        }

        private bool readyForWhiteBalance = true;
        public void AutoWhiteBalance()
        {
            if (!readyForWhiteBalance)
                return;
            Task.Run(() =>
            {
                readyForWhiteBalance = false;
                SetParam("BalanceWhiteAuto", "Once");
                for (int i = 0; i < 10; i++)
                {
                    TriggerSoftwareTrigger();
                    Thread.Sleep(200);
                }
                SetParam("BalanceWhiteAuto", "Off");
                readyForWhiteBalance = true;
            });
        }
        private void OnImageTook(ImageTookEventArgs e)
        {
            ImageTook?.Invoke(this, e);
        }

        public bool SetParam(string name, string value = "")
        {
            try
            {
                if (m_camera != null)
                    if (this.m_camera.Features.ContainsName(name))
                    {
                        var feature = this.m_camera.Features[name];
                        if (feature.DataType == VmbFeatureDataType.VmbFeatureDataCommand)
                        {
                            feature.RunCommand();
                            return feature.IsCommandDone();
                        }
                        else
                        {
                            if (feature.IsWritable())
                            {
                                switch (feature.DataType)
                                {
                                    case VmbFeatureDataType.VmbFeatureDataBool:
                                        {
                                            bool v = false;
                                            if (bool.TryParse(value, out v)) feature.BoolValue = v;
                                        }
                                        break;
                                    case VmbFeatureDataType.VmbFeatureDataString:
                                        {
                                            var entries = feature.EnumEntries;
                                            foreach (EnumEntry entry in entries)
                                            {
                                                if (entry.Name == value)
                                                {
                                                    feature.StringValue = value;
                                                    return true;
                                                }
                                            }
                                        }
                                        break;
                                    case VmbFeatureDataType.VmbFeatureDataEnum:
                                        {
                                            var entries = feature.EnumEntries;
                                            foreach (EnumEntry entry in entries)
                                            {
                                                if (entry.Name == value)
                                                {
                                                    feature.EnumValue = value;
                                                    return true;
                                                }
                                            }
                                        }
                                        break;
                                    case VmbFeatureDataType.VmbFeatureDataFloat:
                                        {
                                            double r = 0.0;
                                            if (double.TryParse(value, out r))
                                            {
                                                if (feature.FloatRangeMax >= r && feature.FloatRangeMin <= r)
                                                {
                                                    feature.FloatValue = r;
                                                    return true;
                                                }
                                            }
                                        }
                                        break;
                                    case VmbFeatureDataType.VmbFeatureDataInt:
                                        {
                                            long r = 0;
                                            if (long.TryParse(value, out r))
                                            {
                                                if (feature.IntRangeMax >= r && feature.IntRangeMin <= r)
                                                {
                                                    feature.IntValue = r;
                                                    return true;
                                                }
                                            }
                                        }
                                        break;
                                    case VmbFeatureDataType.VmbFeatureDataUnknown:
                                        break;
                                    default:
                                        break;
                                }

                            }
                        }
                    }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return false;
        }
        public dynamic GetParam(string name)
        {
            try
            {
                if (m_camera != null)
                    if (this.m_camera.Features.ContainsName(name))
                    {
                        var feature = this.m_camera.Features[name];
                        if (feature.DataType == VmbFeatureDataType.VmbFeatureDataCommand)
                        {
                            feature.RunCommand();
                            return null;
                        }
                        if (feature.IsReadable())
                        {
                            switch (feature.DataType)
                            {
                                case VmbFeatureDataType.VmbFeatureDataBool:
                                    return feature.BoolValue;
                                case VmbFeatureDataType.VmbFeatureDataString:
                                    return feature.StringValue;
                                case VmbFeatureDataType.VmbFeatureDataEnum:
                                    return feature.EnumValue;
                                case VmbFeatureDataType.VmbFeatureDataFloat:
                                    return feature.FloatValue;
                                case VmbFeatureDataType.VmbFeatureDataInt:
                                    return feature.IntValue;
                            }
                        }
                    }
            }
            catch { return null; }
            return null;
        }

        public void SaveCameraSettings()
        {
            try
            {
                this.m_camera.SaveCameraSettings($@"{Environment.CurrentDirectory}\{id}.xml");
                AvtBalanceConfig cfg = new AvtBalanceConfig(id);

                SetParam("BalanceRatioSelector", "Blue");
                try
                {
                    cfg.BlueRatio = (float)GetParam("BalanceRatio");
                }
                catch
                {
                    cfg.BlueRatio = (float)GetParam("BalanceRatioAbs");
                }
                SetParam("BalanceRatioSelector", "Red");
                try
                {
                    cfg.RedRatio = (float)GetParam("BalanceRatio");
                }
                catch
                {
                    cfg.RedRatio = (float)GetParam("BalanceRatioAbs");
                }
                cfg.SaveConfig();
            }
            catch { }
        }

        public void LoadCameraSettings()
        {
            var file = $@"{Environment.CurrentDirectory}\{id}.xml";
            if (File.Exists(file))
            {
                this.m_camera.LoadCameraSettings(file);
            }
        }

        ~AvtCamera()
        {
            Dispose();
        }
        /********************************************************************************************/
        public void Dispose()
        {
            if (m_camera != null)
                CloseCamera();
        }

        #region 打开/关闭
        public void Open()
        {
            OpenCamera();
            IsOpen = opened;
        }

        public void Close()
        {
            CloseCamera();
            IsOpen = opened;
        }
        #endregion

        #region 重命名/设置IP
        public bool SetIP(string ip, string subnetMask, string defaultGateway)
        {
            throw new CameraSDKException("Avt 相机暂未实现该功能");
        }

        public bool Rename(string newUserID)//没有相机，待确定
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_Rename, R_Fail, "相机未打开"));

                return false;
            }
            bool b = true;
            try
            {
                SetParam("CameraName", newUserID);
            }
            catch { b = false; }

            return b;
        }

        #endregion

        #region 触发模式/触发源
        public TriggerMode GetTriggerMode()
        {
            //？？？？？有疑虑
            return this.m_camera.Features["TriggerMode"].EnumValue == "Off" ? TriggerMode.Continuous : TriggerMode.Trigger;

        }

        public TriggerSource GetTriggerSource()
        {
            return this.m_camera.Features["TriggerSource"].EnumValue == "Software" ? TriggerSource.Software : TriggerSource.Extern;
        }
        public override void SetTriggerMode(TriggerMode triggerMode)
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机未打开"));
                throw new CameraSDKException("设置触发模式失败，相机未打开");
            }
            bool mode = triggerMode == TriggerMode.Continuous ? false : true;

            EnableSoftwareTrigger(mode);
            //if (m_running)
            //    StopContinuousImageAcquisition();
            ////？？？？？有疑虑
            //SetEnumParam(paramInfos.TriggerMode, triggerMode == TriggerMode.Continuous ? "Off" : "On");

            //if (m_running)
            //    StartContinuousImageAcquisition();

            TriggerMode = triggerMode;
        }

        public override void SetTriggerSource(TriggerSource triggerSource)
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机未打开"));
                throw new CameraSDKException("设置触发源失败，相机未打开");
            }
            SetEnumParam(paramInfos.TriggerSource, triggerSource == TriggerSource.Software ? "Software" : "Line1");

            TriggerSource = triggerSource;

        }

        public void SoftTrigger()
        {
            TriggerSoftwareTrigger();
        }
        #endregion

        #region 读取/设置参数

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
                throw new CameraSDKException("相机未打开");
            m_cameraParams = new CameraParams();
            if (m_camera == null)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_GetParam, R_Fail, "相机未打开"));
                return m_cameraParams;
            }
            //ExposureTime
            m_cameraParams.ExposureTime = (float?)Exposure;
            //GainRaw
            m_cameraParams.Gain = (float?)Gain;
            //Height
            m_cameraParams.ImageHeight = (int?)GetParam("Height");
            //Width
            m_cameraParams.ImageWidth = (int?)GetParam("Width");
            //TriggerDelay
            //m_cameraParams.TriggerDelay = (float?)GetParam("TriggerDelay");//未知
            return m_cameraParams;
        }

        public void SetParams(CameraParams @params)
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_SetParam, R_Fail, "相机未打开"));

                throw new CameraSDKException("相机未打开");
            }

            if (@params.ExposureTime.HasValue )
            {
                SetFloatParam(paramInfos.Exposure, @params.ExposureTime.Value);
                m_cameraParams.ExposureTime = @params.ExposureTime;
            }

            if (@params.Gain.HasValue )
            {
                SetFloatParam(paramInfos.Gain, @params.Gain.Value);
                m_cameraParams.Gain = @params.Gain;
            }

            if (@params.ImageWidth.HasValue && @params.ImageWidth != m_cameraParams.ImageWidth)
            {
                SetIntParam(paramInfos.Width, (uint)@params.ImageWidth.Value);
                m_cameraParams.ImageWidth = @params.ImageWidth;
            }

            if (@params.ImageHeight.HasValue && @params.ImageHeight != m_cameraParams.ImageHeight)
            {
                SetIntParam(paramInfos.Height, (uint)@params.ImageHeight.Value);
                m_cameraParams.ImageHeight = @params.ImageHeight;
            }
            //未知
            //if (@params.TriggerDelay.HasValue && @params.TriggerDelay != m_cameraParams.TriggerDelay)
            //{
            //    SetFloatParam(paramInfos.TriggerDelay, m_cameraParams.TriggerDelay.Value);
            //}
        }

        #region Int

        long? GetIntParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                long value = GetParam(paramInfo.Name);

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
                SetParam(paramInfo.Name, value.ToString());
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

        #region Float

        double? GetFloatParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                double value = GetParam(paramInfo.Name);

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
                SetParam(paramInfo.Name, value.ToString());

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

        #region Enum

        string GetEnumParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                string value = GetParam(paramInfo.Name);

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
                SetParam(paramInfo.Name, value);

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
                SetParam(paramInfo.Name, value);
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
        string GetStringParam(CameraParamInfo2 paramInfo)
        {
            try
            {
                string value = GetParam(paramInfo.Name);

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
        #endregion
        #endregion

        protected override void Start2(TriggerMode triggerMode, TriggerSource triggerSource)
        {
            try { StopContinuousImageAcquisition(); } catch { }
            //SetEnumParam(paramInfos.AcquisitionMode, "Continuous");//设置连续模式
            //SetTriggerMode(TriggerMode.Trigger);
            SetTriggerMode(triggerMode);
            try
            {
                StartContinuousImageAcquisition();

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

        protected override void Stop2()
        {
            try
            {
                StopContinuousImageAcquisition();

                log?.Info(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Success));
            }
            catch (Exception e)
            {
                log?.Error(new CameraLogMessage(CameraInfo, A_StopGrabbing, R_Fail, e.Message));
            }
        }

        public bool SetCache(int cache)
        {
            return true;
        }

        bool ICamera.SetParams(CameraParams @params)
        {
            var exp = @params.ExposureTime;
            var gain = @params.Gain;
            Exposure = (double)exp;
            Gain = (double)gain;
            return true;
        }


        /********************************************************************************************/
    }
}

using AVT.VmbAPINET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CameraSDK.Avt
{
    /// <summary>
    /// Delegate for the camera list "Callback"
    /// </summary>
    /// <param name="sender">The Sender object (here: this)</param>
    /// <param name="args">The EventArgs.</param>
    public delegate void CameraListChangedHandler(object sender, CameraListChangedEventArgs args);

    ///// <summary>
    ///// Delegate for the Frame received "Callback"
    ///// </summary>
    ///// <param name="sender">The sender object (here: this)</param>
    ///// <param name="args">The FrameEventArgs</param>
    //public delegate void FrameReceivedHandler(object sender, FrameEventArgs args);

    /// <summary>
    /// A helper class as a wrapper around Vimba
    /// </summary>
    public static class AvtHelper
    {
        /// <summary>
        /// Main Vimba API entry object
        /// </summary>
        private static Vimba m_Vimba = null;
        public static int ConnectedCamerasCount = 0;
        private static bool started = false;
        /// <summary>
        /// Camera list changed handler
        /// </summary>
        private static CameraListChangedHandler m_CameraListChangedHandler = null;
        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            //foreach (Camera camera in AvtHelper.Cameras)
            //{
            //    camera.Close();
            //}
            //if (AvtHelper.ConnectedCamerasCount == 0)
            if (null != Vimba)
                Shutdown();
        }

        /// <summary>
        /// Gets CameraList
        /// </summary>
        public static List<CameraInfo> CameraList
        {
            get
            {
                // Check if API has been started up at all
                if (null == Vimba)
                {
                    throw new Exception("Vimba is not started.");
                }

                if (cameraList.Count == 0)
                {
                    cameraList = new List<CameraInfo>();
                    CameraCollection cameras = AvtHelper.Vimba.Cameras;
                    foreach (Camera camera in cameras)
                    {
                        cameraList.Add(new CameraInfo(camera.Name, camera.Id));
                    }
                }
                return cameraList;
            }
        }

        static CameraCollection cameras = null;


        static List<CameraInfo> cameraList = new List<CameraInfo>();
        public static Vimba Vimba
        {
            get { return m_Vimba; }
            private set { m_Vimba = value; }
        }

        public static CameraCollection Cameras
        {
            get
            {
                // Check if API has been started up at all
                if (null == Vimba)
                {
                    throw new Exception("Vimba is not started.");
                }
                cameras = Vimba.Cameras;
                return cameras;
            }

        }

        /// <summary>
        /// Starts up Vimba API and loads all transport layers
        /// </summary>
        /// <param name="cameraListChangedHandler">The CameraListChangedHandler (delegate)</param>
        public static void Startup(CameraListChangedHandler cameraListChangedHandler)
        {
            if (!started)
            {
                started = true;
            }
            else
            {
                return;
            }
            // Instantiate main Vimba object
            Vimba vimba = new Vimba();

            // Start up Vimba API
            vimba.Startup();
            AvtHelper.Vimba = vimba;
            //AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            bool bError = true;
            try
            {
                // Register camera list change delegate
                if (null != cameraListChangedHandler)
                {
                    AvtHelper.Vimba.OnCameraListChanged += AvtHelper.OnCameraListChange;
                    AvtHelper.m_CameraListChangedHandler = cameraListChangedHandler;
                }

                bError = false;
            }
            finally
            {
                // Release Vimba API if an error occurred
                if (true == bError)
                {
                    AvtHelper.ReleaseVimba();
                }
            }
        }



        /// <summary>
        /// Shuts down Vimba API
        /// </summary>
        public static void Shutdown()
        {
            // Check if API has been started up at all
            if (null == AvtHelper.Vimba)
            {
                return;
                throw new Exception("Vimba has not been started.");
            }

            AvtHelper.ReleaseVimba();
        }

        /// <summary>
        /// Gets the version of the Vimba API
        /// </summary>
        /// <returns>The version of the Vimba API</returns>
        public static string GetVersion()
        {
            if (null == AvtHelper.Vimba)
            {
                throw new Exception("Vimba has not been started.");
            }

            VmbVersionInfo_t version_info = AvtHelper.Vimba.Version;
            return string.Format("{0:D}.{1:D}.{2:D}", version_info.major, version_info.minor, version_info.patch);
        }

        /// <summary>
        ///  Releases the camera
        ///  Shuts down Vimba
        /// </summary>
        private static void ReleaseVimba()
        {
            if (null != AvtHelper.Vimba)
            {
                // We can use cascaded try-finally blocks to release the
                // Vimba API step by step to make sure that every step is executed.
                try
                {
                    try
                    {
                        try
                        {

                        }
                        finally
                        {
                            if (null != AvtHelper.m_CameraListChangedHandler)
                            {
                                AvtHelper.Vimba.OnCameraListChanged -= AvtHelper.OnCameraListChange;
                            }
                        }
                    }
                    finally
                    {
                        // Now finally shutdown the API
                        AvtHelper.m_CameraListChangedHandler = null;
                        AvtHelper.Vimba.Shutdown();
                    }
                }
                finally
                {
                    AvtHelper.Vimba = null;
                }
            }
        }


        /// <summary>
        /// Handles the camera list changed event
        /// </summary>
        /// <param name="reason">The Vimba Trigger Type: Camera plugged / unplugged</param>
        public static void OnCameraListChange(VmbUpdateTriggerType reason)
        {
            m_CameraListChangedHandler?.Invoke(null, new CameraListChangedEventArgs { Reason = reason });
        }
    }

    public class CameraListChangedEventArgs : EventArgs
    {
        public VmbUpdateTriggerType Reason { get; set; }
    }

    /// <summary>
    /// A simple container class for infos (name and ID) about a camera
    /// </summary>
    public class CameraInfo
    {
        /// <summary>
        /// The camera name 
        /// </summary>
        private string m_Name = null;

        /// <summary>
        /// The camera ID
        /// </summary>
        private string m_ID = null;

        /// <summary>
        /// Initializes a new instance of the CameraInfo class.
        /// </summary>
        /// <param name="name">The camera name</param>
        /// <param name="id">The camera ID</param>
        public CameraInfo(string name, string id)
        {
            if (null == name)
            {
                throw new ArgumentNullException("name");
            }

            if (null == name)
            {
                throw new ArgumentNullException("id");
            }

            this.m_Name = name;
            this.m_ID = id;
        }

        /// <summary>
        /// Gets the name of the camera
        /// </summary>
        public string Name
        {
            get
            {
                return this.m_Name;
            }
        }

        /// <summary>
        /// Gets the ID
        /// </summary>
        public string ID
        {
            get
            {
                return this.m_ID;
            }
        }

        /// <summary>
        /// Overrides the toString Method for the CameraInfo class (this)
        /// </summary>
        /// <returns>The Name of the camera</returns>
        public override string ToString()
        {
            return this.m_Name;
        }
    }
}

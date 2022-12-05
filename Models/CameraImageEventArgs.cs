using System;

namespace CameraSDK.Models
{
    public class CameraImageEventArgs : EventArgs
    {
        public CameraImage CameraImage { get; set; }

        public CameraImageEventArgs(CameraImage cameraImage)
        {
            CameraImage = cameraImage;
        }
    }

    public class ExposureEndEventArgs : EventArgs
    {
        public ExposureEndEventArgs()
        {

        }
    }
}

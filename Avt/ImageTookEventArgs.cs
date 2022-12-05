using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CameraSDK.Avt
{
    public delegate void ImageTookHandler(object sender, ImageTookEventArgs args);
    public class ImageTookEventArgs : EventArgs
    {
        public Bitmap Image { get; set; }

        public string CameraId { get; set; }

        public bool Success { get; set; }
    }
}

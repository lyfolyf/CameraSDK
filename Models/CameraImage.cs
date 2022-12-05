using System;
using System.Drawing;

namespace CameraSDK.Models
{
    public class CameraImage : IDisposable
    {
        public Bitmap Bitmap { get; set; }

        public bool IsGrey { get; set; }

        /// <summary>
        /// 帧号
        /// </summary>
        public int FrameNum { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 获取图片的时间
        /// </summary>
        public DateTime AcqImgTime { get; set; }

        ~CameraImage()
        {
            //Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        bool disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {

            }

            Bitmap?.Dispose();

            disposed = true;
        }
    }
}

using System;
using System.IO;
using System.Drawing;
using System.Collections.Concurrent;

using GL.Kit.Log;
using static GL.Kit.Log.ActionResult;
using CameraSDK.Models;
using static CameraSDK.CameraAction;

namespace CameraSDK
{
    public abstract class BaseCamera
    {
        public event EventHandler<CameraImageEventArgs> ImageReceived;

        public event EventHandler Exposured;

        //add by LuoDian @ 20211119 存图的时候需要加线程锁，不然会报GDI+的异常，怀疑是连续采图的时候，在同一路径中同时保存图像导致的；
        readonly object SaveDiscardImageLockObj = new object();

        protected readonly IGLog log;

        /// <summary>
        /// 相机信息
        /// </summary>
        public ComCameraInfo CameraInfo { get; }

        /// <summary>
        /// 相机是否已打开
        /// </summary>
        public bool IsOpen { get; protected set; } = false;

        /// <summary>
        /// 触发模式
        /// </summary>
        public TriggerMode TriggerMode { get; protected set; }

        /// <summary>
        /// 触发源
        /// </summary>
        public TriggerSource TriggerSource { get; protected set; }

        /// <summary>
        /// 图像缓存区 add by Luodian @ 20220124 为了提升效率，低层相机出图的时候不采用事件回调出图，改为直接把图像放入这个队列中，所以要把这个队列从AcquireImage对象中挪到这里，让这个类和低层相机类都能访问
        /// 之前的队列类型是ConcurrentQueue，压入队列后到出队列的耗时太长，换成Queue
        /// </summary>
        public BlockingCollection<CameraImage> imgQueue { get; }

        public int ImageCacheCount { get; set; } = 1;

        /// <summary>
        /// 是否保存丢弃的图片
        /// </summary>
        public bool SaveDiscardImage { get; set; } = true;

        string discardImageDire;

        public BaseCamera(ComCameraInfo cameraInfo, IGLog log)
        {
            CameraInfo = cameraInfo;
            this.log = log;

            //add by Luodian @ 20220124 为了提升效率，低层相机出图的时候不采用事件回调出图，改为直接把图像放入这个队列中，所以要把这个队列从AcquireImage对象中挪到这里，让这个类和低层相机类都能访问
            imgQueue = new BlockingCollection<CameraImage>();
        }

        protected void OnExposured(EventArgs e)
        {
            Exposured?.Invoke(this, e);
        }

        protected void OnImageReceived(CameraImageEventArgs e)
        {
            if (ImageReceived != null)
            {
                ImageReceived.Invoke(this, e);
            }
            else
            {
                //add by LuoDian @ 20220124 为了提升效率，自动运行时低层相机出图的时候不采用事件回调出图，改为直接把图像放入这个队列中，所以要把这个队列从AcquireImage对象中挪到这里，让这个类和低层相机类都能访问
                imgQueue.Add(e.CameraImage);
                return;

                if (SaveDiscardImage)
                {
                    string filename = $"{DateTime.Now:yyyyMMddHHmmssfff}_FN{e.CameraImage.FrameNum}.Bmp";

                    //delete by LuoDian @ 20211119 不能在这里存图，因为这里存图的时候，有可能相机在采集新的图像，旧的图像数据就被丢弃了
                    lock (SaveDiscardImageLockObj)
                    {
                        e.CameraImage.Bitmap.Save($"{discardImageDire}\\{filename}", System.Drawing.Imaging.ImageFormat.Bmp);
                    }
                    log?.Error(new CameraLogMessage(CameraInfo, A_DiscardImage, R_Tip, filename));
                    //log?.Error(new CameraLogMessage(CameraInfo, A_DiscardImage, R_Tip));
                }
                else
                {
                    log?.Error(new CameraLogMessage(CameraInfo, A_DiscardImage, R_Tip));
                }

                e.CameraImage.Dispose();
            }
        }

        // 是否采集中
        protected bool m_running = false;

        // true: 黑白; false: 彩色; null: 未赋值
        protected bool? isGrey;

        public void Start(TriggerMode triggerMode, TriggerSource triggerSource)
        {
            if (!IsOpen)
            {
                log?.Warn(new CameraLogMessage(CameraInfo, A_StartGrabbing, R_Fail, "相机未打开"));
                return;
            }

            if (SaveDiscardImage)
            {
                discardImageDire = $"{AppDomain.CurrentDomain.BaseDirectory}\\DiscardImage";
                Directory.CreateDirectory(discardImageDire);
            }

            SetTriggerMode(triggerMode);
            SetTriggerSource(triggerSource);

            if (m_running) return;

            //add by LuoDian @ 20220113 在启动时把队列中之前缓存的图像清除
            ClearQueue();

            Start2(triggerMode, triggerSource);

            m_running = true;
        }

        public void Stop()
        {
            if (!m_running) return;

            Stop2();

            m_running = false;
        }

        protected void ReceivedImage(Bitmap bmp, int frameNum)
        {
            CameraImage cameraImage = new CameraImage
            {
                Bitmap = bmp,
                IsGrey = isGrey.Value,
                FrameNum = frameNum,
                Timestamp = DateTime.Now
            };
            OnImageReceived(new CameraImageEventArgs(cameraImage));
        }

        protected void ReceivedImage(Bitmap bmp, int frameNum, DateTime acqImgTime)
        {
            CameraImage cameraImage = new CameraImage
            {
                Bitmap = bmp,
                IsGrey = isGrey.Value,
                FrameNum = frameNum,
                Timestamp = DateTime.Now,
                AcqImgTime = acqImgTime
            };
            OnImageReceived(new CameraImageEventArgs(cameraImage));
        }

        protected abstract void Start2(TriggerMode triggerMode, TriggerSource triggerSource);

        protected abstract void Stop2();

        public abstract void SetTriggerMode(TriggerMode triggerMode);

        public abstract void SetTriggerSource(TriggerSource triggerSource);

        /// <summary>
        /// 清除图像缓存
        /// add by Luodian @ 20220124 为了提升效率，低层相机出图的时候不采用事件回调出图，改为直接把图像放入这个队列中，所以要把这个队列从AcquireImage对象中挪到这里，让这个类和低层相机类都能访问
        /// </summary>
        public void ClearQueue()
        {
            // 这里是单一的任务中
            // 当下一轮指令来的时候，上一轮的取像必定是完成的，就算 ToolBlock 还没运算完，但图像肯定已经从队列中取走了
            // 所以理论上队列就是空的
            // 如果不空，必然是出问题了，清空队列，至少可以保证下一轮的图像不错位

            int count = 0;
            while(imgQueue.TryTake(out CameraImage image))
            {
                image.Dispose();
                log.Warn(new CameraLogMessage(CameraInfo, A_DiscardImage, R_Clear, $"共丢弃 {++count} 张"));
            }
        }
    }
}

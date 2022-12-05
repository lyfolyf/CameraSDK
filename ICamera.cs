using CameraSDK.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;

namespace CameraSDK
{
    public interface ICamera
    {
        event EventHandler<CameraImageEventArgs> ImageReceived;

        /// <summary>
        /// 曝光完成后发生
        /// </summary>
        event EventHandler Exposured;

        ComCameraInfo CameraInfo { get; }

        /// <summary>
        /// 触发模式
        /// </summary>
        TriggerMode TriggerMode { get; }

        /// <summary>
        /// 触发源
        /// </summary>
        TriggerSource TriggerSource { get; }

        /// <summary>
        /// 图像缓存数量
        /// <para>此功能由相机或相机 SDK 提供</para>
        /// </summary>
        int ImageCacheCount { get; set; }

        /// <summary>
        /// 是否保存丢弃的图片
        /// </summary>
        bool SaveDiscardImage { get; set; }

        bool IsOpen { get; }

        /// <summary>
        /// 打开相机
        /// </summary>
        void Open();

        /// <summary>
        /// 关闭相机
        /// </summary>
        void Close();

        /// <summary>
        /// 设置相机 IP 地址
        /// </summary>
        bool SetIP(string ip, string subnetMask, string defaultGateway);

        /// <summary>
        /// 重命名
        /// </summary>
        bool Rename(string newUserID);

        /// <summary>
        /// 设置缓存
        /// </summary>
        /// <param name="cache"></param>
        /// <returns></returns>
        bool SetCache(int cache);

        /// <summary>
        /// 获取触发模式
        /// </summary>
        /// <returns>true: 连续模式; false: 触发模式</returns>
        TriggerMode GetTriggerMode();

        /// <summary>
        /// 设置触发模式
        /// </summary>
        void SetTriggerMode(TriggerMode triggerMode);

        /// <summary>
        /// 获取触发源
        /// </summary>
        /// <returns></returns>
        TriggerSource GetTriggerSource();

        /// <summary>
        /// 设置触发源
        /// </summary>
        void SetTriggerSource(TriggerSource triggerSource);

        /// <summary>
        /// 开始采集
        /// </summary>
        void Start(TriggerMode triggerMode, TriggerSource triggerSource);

        /// <summary>
        /// 停止采集
        /// </summary>
        void Stop();

        /// <summary>
        /// 软触发一次
        /// </summary>
        void SoftTrigger();

        CameraParamInfo[] GetParamInfos();

        void SetParamInfos(IEnumerable<CameraParamInfo> paramInfos);

        /// <summary>
        /// 获取参数
        /// </summary>
        CameraParams GetParams();

        /// <summary>
        /// 设置参数
        /// </summary>
        bool SetParams(CameraParams @params);

        /// <summary>
        /// 图像缓存区 add by Luodian @ 20220124 为了提升效率，低层相机出图的时候不采用事件回调出图，改为直接把图像放入这个队列中，所以要把这个队列从AcquireImage对象中挪到这里，让这个类和低层相机类都能访问
        /// 之前的队列类型是ConcurrentQueue，压入队列后到出队列的耗时太长，换成Queue
        /// </summary>
        BlockingCollection<CameraImage> imgQueue { get; }
    }

}

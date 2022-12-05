using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace CameraSDK.Avt
{
    /// <summary>
    /// 相机白平衡Config
    /// </summary>
    [Serializable]
    public class AvtBalanceConfig
    {
        public AvtBalanceConfig()
        {
        }
        public AvtBalanceConfig(string cameraId)
        {
            CameraId = cameraId;
        }

        public float RedRatio { get; set; } = 3.20996f;
        public float BlueRatio { get; set; } = 1.8291f;
        public string CameraId { get; set; } = string.Empty;
        public void SaveConfig()
        {
            var path = $@"{Environment.CurrentDirectory}\CameraBalanceConfig";
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            BinarySerialize($@"{path}\{CameraId}.cfg", this);
        }
        public static AvtBalanceConfig LoadConfig(string id)
        {
            var path = $@"{Environment.CurrentDirectory}\CameraBalanceConfig\{id}.cfg";
            if (!System.IO.File.Exists(path))
            {
                AvtBalanceConfig cfg = new AvtBalanceConfig(id);
                cfg.SaveConfig();
            }
            return BinaryDeserialize<AvtBalanceConfig>(path);
        }

        public static void BinarySerialize<T>(string filename, T obj)
        {
            try
            {
                //string filename = objname + ".ump";
                if (System.IO.File.Exists(filename))
                    System.IO.File.Delete(filename);
                using (FileStream fileStream = new FileStream(filename, FileMode.Create))
                {
                    // 用二进制格式序列化
                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    binaryFormatter.Serialize(fileStream, obj);
                    fileStream.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static T BinaryDeserialize<T>(string filename)
        {
            System.Runtime.Serialization.IFormatter formatter = new BinaryFormatter();
            //二进制格式反序列化
            T obj;
            if (!System.IO.File.Exists(filename))
                throw new Exception("在反序列化之前,请先序列化");
            using (Stream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                obj = (T)formatter.Deserialize(stream);
                stream.Close();
            }
            return obj;

        }
    }
}

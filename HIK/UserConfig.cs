using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace CameraSDK.HIK
{
    public class UserConfig
    {
        public UserConfig()
        {

        }
        public UserConfig(string cameraId)
        {
            CameraId = cameraId;
        }

        public int GreenRatio { get; set; } = 1000;
        public int RedRatio { get; set; } = 1000;
        public int BlueRatio { get; set; } = 1000;
        public string CameraId { get; set; } = string.Empty;
        public int Width { get; set; } = 5472;
        public int Height { get; set; } = 1200;
        public int OffsetX { get; set; } = 0;
        public int OffsetY { get; set; } = 1050;
        public void SaveConfig()
        {
            var path = $@"{Environment.CurrentDirectory}\CameraBalanceConfig";
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            XmlSerialize($@"{path}\{CameraId}.cfg", this);
        }
        public static UserConfig LoadConfig(string id)
        {
            var path = $@"{Environment.CurrentDirectory}\CameraBalanceConfig\{id}.cfg";
            if (!System.IO.File.Exists(path))
            {
                //UserConfig cfg = new UserConfig(id);
                //cfg.SaveConfig();
                return null;
            }
            return XmlDeserailize<UserConfig>(path);
        }

        /// <summary>
        /// Xml序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filename"></param>
        /// <param name="obj"></param>
        public static void XmlSerialize<T>(string filename, T obj)
        {

            try
            {
                if (System.IO.File.Exists(filename))
                    System.IO.File.Delete(filename);
                using (FileStream fileStream = new FileStream(filename, FileMode.Create))
                {
                    // 序列化为xml
                    XmlSerializer formatter = new XmlSerializer(typeof(T));
                    formatter.Serialize(fileStream, obj);
                    fileStream.Close();
                }
            }
            catch (Exception ex)
            {

            }

        }
        /// <summary>
        /// Xml反序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static T XmlDeserailize<T>(string filename)
        {
            T obj;
            if (!System.IO.File.Exists(filename))
                throw new Exception("对反序列化之前,请先序列化");
            //Xml格式反序列化
            using (Stream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                XmlSerializer formatter = new XmlSerializer(typeof(T));
                obj = (T)formatter.Deserialize(stream);
                stream.Close();
            }
            return obj;
        }

    }
}

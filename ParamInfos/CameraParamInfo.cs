using System.Collections.Generic;

namespace CameraSDK.Models
{
    public class CameraParamInfo
    {
        public string Name { get; set; }

        public bool Enabled { get; set; } = true;

        public bool ReadOnly { get; set; } = true;
    }

    public class CameraParamInfoList
    {
        public string SN { get; set; }

        public List<CameraParamInfo> ParamInfos { get; set; }
    }

}

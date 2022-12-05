using CameraSDK.Models;

namespace CameraSDK.HIK
{
    public sealed class HIKParamInfoCollection : CameraParamInfoCollection
    {
        public CameraParamInfo2 PreampGain { get; }

        public CameraParamInfo2 PacketSize { get; }

        public CameraParamInfo2 PayloadSize { get; }

        public HIKParamInfoCollection()
        {
            PreampGain = new CameraParamInfo2 { Name = CameraParamList.C_PreampGain, Description = "增益", Type = EnumType };

            PacketSize = new CameraParamInfo2 { Name = CameraParamList.C_GevSCPSPacketSize, Description = "最佳网络包大小", Type = IntType };

            PayloadSize = new CameraParamInfo2 { Name = CameraParamList.C_PayloadSize, Description = "有效载荷", Type = IntType };

            paramInfoDict.Add(PreampGain.Name, PreampGain);
            paramInfoDict.Add(PacketSize.Name, PacketSize);
            paramInfoDict.Add(PayloadSize.Name, PayloadSize);
        }
    }
}

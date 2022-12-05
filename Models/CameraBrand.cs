namespace CameraSDK.Models
{
    /// <summary>
    /// 相机品牌
    /// </summary>
    public enum CameraBrand
    {
        /// <summary>
        /// 海康（Gige 和 U3 口）
        /// </summary>
        HIK_Gige,

        /// <summary>
        /// 海康（GenTL 口）
        /// </summary>
        HIK_GenTL,

        Basler,

        /// <summary>
        /// 灰点
        /// </summary>
        FLIR,

        /// <summary>
        /// 大华
        /// </summary>
        DaHua,
        /// <summary>
        /// AVT相机
        /// </summary>
        Avt,

        /// <summary>
        /// 迈德威视
        /// </summary>
        MindVision
    }

    public static class CameraBrandUtils
    {
        public static string ToAlias(this CameraBrand brand)
        {
            switch (brand)
            {
                case CameraBrand.HIK_Gige:
                case CameraBrand.HIK_GenTL:
                    return CameraAlias.HIK;
                case CameraBrand.DaHua:
                    return CameraAlias.DaHua;
                case CameraBrand.FLIR:
                    return CameraAlias.FLIR;
                default:
                    return brand.ToString();
            }
        }
    }

    public static class CameraAlias
    {
        public const string HIK = "LD016";

        public const string DaHua = "LD017";

        public const string FLIR = "LD024";
    }
}

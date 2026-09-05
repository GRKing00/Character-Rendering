using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.PostProcessing
{
    public enum GRKingToneMappingMode
    {
        None = 0,
        NAES = 1,
        GranTurismo = 2,
        Film = 3
    }

    //普通枚举变为VolumeParameter
    [Serializable]
    public sealed class GRKingToneMappingModeParameter : VolumeParameter<GRKingToneMappingMode>
    {
        public GRKingToneMappingModeParameter(GRKingToneMappingMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenuForRenderPipeline("GRKingRP/Tone Mapping", typeof(UniversalRenderPipeline))]
    public sealed class GRKingToneMapping : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("选择 Tone Mapping 算法。None 表示关闭。")]
        public GRKingToneMappingModeParameter Mode = new(GRKingToneMappingMode.None);

        [Header("NAES")]

        [DisplayInfo(name = "Param A")]
        public FloatParameter NaesParamA = new(1.36f);

        [DisplayInfo(name = "Param B")]
        public FloatParameter NaesParamB = new(0.047f);

        [DisplayInfo(name = "Param C")]
        public FloatParameter NaesParamC = new(0.93f);

        [DisplayInfo(name = "Param D")]
        public FloatParameter NaesParamD = new(0.56f);

        [Header("Gran Turismo")]

        [DisplayInfo(name = "Maximum Brightness (P)")]
        public MinFloatParameter GtMaximumBrightness = new(1f, 0.0001f);

        [DisplayInfo(name = "Contrast (A)")]
        public MinFloatParameter GtContrast = new(1f, 0.0001f);

        [DisplayInfo(name = "Linear Start (M)")]
        public MinFloatParameter GtLinearStart = new(0.22f, 0.0001f);

        [DisplayInfo(name = "Linear Length (L)")]
        public ClampedFloatParameter GtLinearLength = new(0.4f, 0f, 1f);

        [DisplayInfo(name = "Black Power (C)")]
        public MinFloatParameter GtBlackPower = new(1.33f, 0.0001f);

        [DisplayInfo(name = "Black Minimum (B)")]
        public FloatParameter GtBlackMinimum = new(0f);

        [Header("Film")]

        public MinFloatParameter FilmSlope = new(0.88f, 0.0001f);
        public ClampedFloatParameter FilmToe = new(0.55f, 0f, 1f);
        public ClampedFloatParameter FilmShoulder = new(0.26f, 0f, 1f);
        public ClampedFloatParameter FilmBlackClip = new(0f, 0f, 1f);
        public ClampedFloatParameter FilmWhiteClip = new(0.04f, 0f, 1f);

        public GRKingToneMapping()
        {
            displayName = "GRKingRP Tone Mapping";
        }

        public bool IsActive()
        {
            return Mode.value != GRKingToneMappingMode.None;
        }

        public bool IsTileCompatible()
        {
            return true;
        }
    }
}


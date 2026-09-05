using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.PostProcessing
{
    public enum GRKingFXAAQuality
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    [Serializable]
    public sealed class GRKingFXAAQualityParameter : VolumeParameter<GRKingFXAAQuality>
    {
        public GRKingFXAAQualityParameter(GRKingFXAAQuality value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenuForRenderPipeline("GRKingRP/FXAA", typeof(UniversalRenderPipeline))]
    public sealed class GRKingFXAA : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("启用 GRKingRP FXAA。")]
        public BoolParameter Enabled = new(false);

        [Tooltip("边缘搜索质量。质量越高，沿边缘方向进行的搜索次数越多。")]
        public GRKingFXAAQualityParameter Quality = new(GRKingFXAAQuality.High);

        [Tooltip("检测边缘使用的最低固定亮度差。值越低，处理的边缘越多。")]
        public ClampedFloatParameter FixedThreshold = new(0.0833f, 0.0312f, 0.0833f);

        [Tooltip("相对于局部最高亮度的边缘阈值。值越低，处理的边缘越多。")]
        public ClampedFloatParameter RelativeThreshold = new(0.166f, 0.063f, 0.333f);

        [Tooltip("子像素锯齿的混合强度。值越高，细小边缘越平滑，也越容易变模糊。")]
        public ClampedFloatParameter SubpixelBlending = new(0.75f, 0f, 1f);

        public GRKingFXAA()
        {
            displayName = "GRKingRP FXAA";
        }

        public bool IsActive()
        {
            return Enabled.value;
        }

        public bool IsTileCompatible()
        {
            return false;
        }
    }
}

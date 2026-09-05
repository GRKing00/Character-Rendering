using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.PostProcessing
{
    public enum GRKingBloomMode
    {
        Additive = 0,
        Scatter = 1
    }

    //普通枚举变为VolumeParameter
    [Serializable]
    public sealed class GRKingBloomModeParameter : VolumeParameter<GRKingBloomMode>
    {
        public GRKingBloomModeParameter(GRKingBloomMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenuForRenderPipeline("GRKingRP/Bloom", typeof(UniversalRenderPipeline))]
    public sealed class GRKingBloom : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Bloom 的合成方式。Additive 直接叠加，Scatter 在高、低分辨率 Bloom 之间插值。")]
        public GRKingBloomModeParameter Mode = new(GRKingBloomMode.Additive);

        [Tooltip("最终 Bloom 强度。设为 0 时关闭 Bloom。")]
        public MinFloatParameter Intensity = new(0f, 0f);

        [Tooltip("仅对 Stencil 最低位为 1 的角色区域生成 Bloom。需要角色 Shader 写入标记。")]
        public BoolParameter CharactersOnly = new(false);

        [Tooltip("Scatter 模式在相邻 Bloom 层级之间插值的比例。")]
        public ClampedFloatParameter Scatter = new(0.7f, 0f, 1f);

        [Header("Filtering")]

        [Tooltip("只保留亮度高于此值的像素。")]
        public MinFloatParameter Threshold = new(1f, 0f);

        [Tooltip("阈值边界的柔和程度。0 表示硬阈值，1 表示最宽的软过渡。")]
        public ClampedFloatParameter ThresholdKnee = new(0.5f, 0f, 1f);

        [Tooltip("通过扩大预过滤采样区域并降低极亮像素权重，减少 Bloom 闪烁。")]
        public BoolParameter FadeFireflies = new(false);

        [Header("Quality")]

        [Tooltip("Bloom 金字塔允许创建的最大层级数。")]
        public ClampedIntParameter MaxIterations = new(6, 1, 16);

        [Tooltip("当 Bloom 纹理的宽或高小于此值时停止继续降采样。")]
        public MinIntParameter DownscaleLimit = new(2, 1);

        [Tooltip("上采样时使用双三次采样；质量更高，但开销也更大。")]
        public BoolParameter BicubicUpsampling = new(false);

        [Tooltip("使用相机原始像素尺寸构建 Bloom，而不是应用 Render Scale 后的尺寸。")]
        public BoolParameter IgnoreRenderScale = new(false);

        public GRKingBloom()
        {
            displayName = "GRKingRP Bloom";
        }

        public bool IsActive()
        {
            return Intensity.value > 0f && MaxIterations.value > 0;
        }

        public bool IsTileCompatible()
        {
            return false;
        }
    }
}

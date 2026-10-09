using System;
using GRKingRP.PostProcessing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.Passes
{
    /// <summary>
    /// 依次执行 HDR Bloom、Tone Mapping 和 FXAA。
    /// Renderer Feature 在 AddRenderPasses 中调用 Setup，返回 true 时再将此 Pass 入队。
    /// </summary>
    public sealed class GRKingPostProcessingPass : ScriptableRenderPass, IDisposable
    {
        private const int MaxBloomMipCount = 16;
        private const float CurveEpsilon = 0.001f;

        // 与 Bloom.shader 的 Pass 顺序一致。
        private enum BloomPass
        {
            Prefilter = 0,
            PrefilterFireflies = 1,
            Horizontal = 2,
            Vertical = 3,
            Add = 4,
            Scatter = 5,
            ScatterFinal = 6,
            CharacterCopy = 7
        }

        // Tone Mapping 的 Volume 枚举包含 None，不能直接当作 Shader Pass 索引。
        private enum ToneMappingPass
        {
            NAES = 0,
            GranTurismo = 1,
            Film = 2
        }

        private enum FXAAPass
        {
            FXAA = 0
        }

        private static class ShaderIDs
        {
            public static readonly int PostFXSource = Shader.PropertyToID("_PostFXSource");
            public static readonly int PostFXSourceTexelSize = Shader.PropertyToID("_PostFXSource_TexelSize");
            public static readonly int BloomSource2 = Shader.PropertyToID("_BloomSource2");
            public static readonly int BloomHighlightSource = Shader.PropertyToID("_BloomHighlightSource");
            public static readonly int BloomThreshold = Shader.PropertyToID("_BloomThreshold");
            public static readonly int BloomIntensity = Shader.PropertyToID("_BloomIntensity");
            public static readonly int BloomBicubicUpsampling = Shader.PropertyToID("_BloomBicubicUpsampling");
            public static readonly int NaesParams = Shader.PropertyToID("_NaesParams");
            public static readonly int GTParams1 = Shader.PropertyToID("_GTParams1");
            public static readonly int GTParams2 = Shader.PropertyToID("_GTParams2");
            public static readonly int FilmParams1 = Shader.PropertyToID("_FilmParams1");
            public static readonly int FilmParams2 = Shader.PropertyToID("_FilmParams2");
            public static readonly int FXAAConfig = Shader.PropertyToID("_FXAAConfig");
        }

        private readonly ProfilingSampler m_BloomSampler = new("GRKingRP Bloom");
        private readonly ProfilingSampler m_ToneMappingSampler = new("GRKingRP Tone Mapping");
        private readonly ProfilingSampler m_FXAASampler = new("GRKingRP FXAA");
        private readonly MaterialPropertyBlock m_Properties = new();
        private readonly RTHandle[] m_BloomDown = new RTHandle[MaxBloomMipCount];
        private readonly RTHandle[] m_BloomUp = new RTHandle[MaxBloomMipCount];
        private readonly string[] m_BloomDownNames = new string[MaxBloomMipCount];
        private readonly string[] m_BloomUpNames = new string[MaxBloomMipCount];

        private Material m_BloomMaterial;
        private Material m_ToneMappingMaterial;
        private Material m_FXAAMaterial;
        private RTHandle m_ColorCopy;
        private RTHandle m_BloomPrefilter;
        private RTHandle m_BloomCharacterColor;
        private RTHandle m_EffectResult;
        private GRKingBloom m_Bloom;
        private GRKingToneMapping m_ToneMapping;
        private GRKingFXAA m_FXAA;
        private bool m_ApplyBloom;
        private bool m_ApplyToneMapping;
        private bool m_ApplyFXAA;
        private int m_BloomMipCount;
        private Vector4 m_BloomThreshold;

        public GRKingPostProcessingPass(Shader bloomShader, Shader toneMappingShader, Shader fxaaShader)
        {
            profilingSampler = new ProfilingSampler("GRKingRP Post Processing");
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            if (bloomShader != null)
                m_BloomMaterial = CoreUtils.CreateEngineMaterial(bloomShader);
            if (toneMappingShader != null)
                m_ToneMappingMaterial = CoreUtils.CreateEngineMaterial(toneMappingShader);
            if (fxaaShader != null)
                m_FXAAMaterial = CoreUtils.CreateEngineMaterial(fxaaShader);

            for (int i = 0; i < MaxBloomMipCount; i++)
            {
                m_BloomDownNames[i] = "_GRKingBloomDown" + i;
                m_BloomUpNames[i] = "_GRKingBloomUp" + i;
            }
        }

        /// <summary>
        /// 每台相机入队前调用。
        /// </summary>
        public bool Setup(ref RenderingData renderingData)
        {
            m_ApplyBloom = false;
            m_ApplyToneMapping = false;
            m_ApplyFXAA = false;
            m_Bloom = null;
            m_ToneMapping = null;
            m_FXAA = null;

            CameraData cameraData = renderingData.cameraData;
            if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                return false;

            VolumeStack stack = VolumeManager.instance.stack;
            m_Bloom = stack.GetComponent<GRKingBloom>();
            m_ToneMapping = stack.GetComponent<GRKingToneMapping>();
            m_FXAA = stack.GetComponent<GRKingFXAA>();

            m_ApplyBloom = m_BloomMaterial != null && m_Bloom != null && m_Bloom.active && m_Bloom.IsActive();
            m_ApplyToneMapping = m_ToneMappingMaterial != null && m_ToneMapping != null &&
                m_ToneMapping.active && m_ToneMapping.IsActive();
            m_ApplyFXAA = m_FXAAMaterial != null && m_FXAA != null &&
                m_FXAA.active && m_FXAA.IsActive();

            return m_ApplyBloom || m_ApplyToneMapping || m_ApplyFXAA;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 本 Pass 自己切换中间 Render Target，通知 Renderer 不沿用之前缓存的绑定。
            ResetTarget();
            m_BloomMipCount = 0;
            if (!m_ApplyBloom && !m_ApplyToneMapping && !m_ApplyFXAA)
                return;

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.enableRandomWrite = false;
            descriptor.memoryless = RenderTextureMemoryless.None;
            descriptor.useDynamicScale = false;

            // 所有中间纹理沿用相机颜色格式，HDR 精度由 URP 与相机配置决定。
            if (m_ApplyBloom)
            {
                AllocateBloomTextures(descriptor, renderingData.cameraData.camera);
                m_ApplyBloom = m_BloomMipCount > 0;
            }

            if (!m_ApplyBloom)
                ReleaseBloomTextures();

            if (m_ApplyBloom && m_Bloom.CharactersOnly.value)
            {
                RenderTextureDescriptor characterDescriptor = descriptor;
                // 与绑定的相机深度/模板附件保持相同采样数，随后解析为可采样颜色。
                characterDescriptor.msaaSamples = renderingData.cameraData.cameraTargetDescriptor.msaaSamples;
                RenderingUtils.ReAllocateIfNeeded(ref m_BloomCharacterColor, characterDescriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_GRKingBloomCharacterColor");
            }
            else
            {
                m_BloomCharacterColor?.Release();
                m_BloomCharacterColor = null;
            }

            // 极小的目标或过高的 Downscale Limit 可能没有可用的 Bloom 层级。
            if (!m_ApplyBloom && !m_ApplyToneMapping && !m_ApplyFXAA)
                return;

            RenderingUtils.ReAllocateIfNeeded(ref m_ColorCopy, descriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_GRKingPostProcessColorCopy");

            int activeEffectCount = (m_ApplyBloom ? 1 : 0) +
                (m_ApplyToneMapping ? 1 : 0) + (m_ApplyFXAA ? 1 : 0);
            if (activeEffectCount > 1)
            {
                RenderingUtils.ReAllocateIfNeeded(ref m_EffectResult, descriptor, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_GRKingPostProcessEffectResult");
            }
            else
            {
                m_EffectResult?.Release();
                m_EffectResult = null;
            }
        }

        private void AllocateBloomTextures(RenderTextureDescriptor descriptor, Camera camera)
        {
            int width = m_Bloom.IgnoreRenderScale.value ? camera.pixelWidth : descriptor.width;
            int height = m_Bloom.IgnoreRenderScale.value ? camera.pixelHeight : descriptor.height;
            int prefilterWidth = Mathf.Max(1, width / 2);
            int prefilterHeight = Mathf.Max(1, height / 2);
            int downscaleLimit = Mathf.Max(1, m_Bloom.DownscaleLimit.value);
            int maxIterations = Mathf.Clamp(m_Bloom.MaxIterations.value, 1, MaxBloomMipCount);

            // 预过滤先降到一半；每次水平模糊再降一半，垂直模糊保持该层尺寸。
            width = prefilterWidth / 2;
            height = prefilterHeight / 2;
            if (width < downscaleLimit || height < downscaleLimit)
                return;

            descriptor.width = prefilterWidth;
            descriptor.height = prefilterHeight;
            RenderingUtils.ReAllocateIfNeeded(ref m_BloomPrefilter, descriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_GRKingBloomPrefilter");

            while (m_BloomMipCount < maxIterations && width >= downscaleLimit && height >= downscaleLimit)
            {
                descriptor.width = width;
                descriptor.height = height;
                RenderingUtils.ReAllocateIfNeeded(ref m_BloomDown[m_BloomMipCount], descriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: m_BloomDownNames[m_BloomMipCount]);
                RenderingUtils.ReAllocateIfNeeded(ref m_BloomUp[m_BloomMipCount], descriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: m_BloomUpNames[m_BloomMipCount]);
                m_BloomMipCount++;
                width /= 2;
                height /= 2;
            }

            // 减少迭代次数或分辨率后，释放不再使用的层级。
            for (int i = m_BloomMipCount; i < MaxBloomMipCount; i++)
            {
                m_BloomDown[i]?.Release();
                m_BloomDown[i] = null;
                m_BloomUp[i]?.Release();
                m_BloomUp[i] = null;
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if ((!m_ApplyBloom && !m_ApplyToneMapping && !m_ApplyFXAA) || m_ColorCopy == null)
                return;

            RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
            CommandBuffer cmd = CommandBufferPool.Get();
            try
            {
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // 先保存原图，避免读写同一纹理。
                    Blitter.BlitCameraTexture(cmd, cameraColor, m_ColorCopy);

                    int remainingEffects = (m_ApplyBloom ? 1 : 0) +
                        (m_ApplyToneMapping ? 1 : 0) + (m_ApplyFXAA ? 1 : 0);
                    RTHandle source = m_ColorCopy;

                    if (m_ApplyBloom)
                    {
                        remainingEffects--;
                        RTHandle destination = remainingEffects > 0
                            ? (source == m_ColorCopy ? m_EffectResult : m_ColorCopy)
                            : cameraColor;
                        using (new ProfilingScope(cmd, m_BloomSampler))
                        {
                            RTHandle bloomSource = source;
                            if (m_Bloom.CharactersOnly.value)
                            {
                                SetSourceProperties(source);
                                // 仅清除颜色，保留相机深度附件中的角色 Stencil 标记。
                                CoreUtils.SetRenderTarget(cmd, m_BloomCharacterColor,
                                    renderingData.cameraData.renderer.cameraDepthTargetHandle,
                                    ClearFlag.Color, Color.clear);
                                cmd.DrawProcedural(Matrix4x4.identity, m_BloomMaterial,
                                    (int)BloomPass.CharacterCopy, MeshTopology.Triangles, 3, 1, m_Properties);
                                bloomSource = m_BloomCharacterColor;
                            }
                            ExecuteBloom(cmd, bloomSource, source, destination);
                        }
                        source = destination;
                    }

                    if (m_ApplyToneMapping)
                    {
                        remainingEffects--;
                        RTHandle destination = remainingEffects > 0
                            ? (source == m_ColorCopy ? m_EffectResult : m_ColorCopy)
                            : cameraColor;
                        using (new ProfilingScope(cmd, m_ToneMappingSampler))
                            ExecuteToneMapping(cmd, source, destination);
                        source = destination;
                    }

                    if (m_ApplyFXAA)
                    {
                        remainingEffects--;
                        RTHandle destination = remainingEffects > 0
                            ? (source == m_ColorCopy ? m_EffectResult : m_ColorCopy)
                            : cameraColor;
                        using (new ProfilingScope(cmd, m_FXAASampler))
                            ExecuteFXAA(cmd, source, destination);
                    }
                }

                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
        }

        private void ExecuteBloom(CommandBuffer cmd, RTHandle bloomSource, RTHandle sceneSource, RTHandle destination)
        {
            // Threshold 按线性空间亮度使用，与 Shader 中的 max(R, G, B) 保持一致。
            float threshold = m_Bloom.Threshold.value;
            float knee = threshold * m_Bloom.ThresholdKnee.value;
            m_BloomThreshold = new Vector4(threshold, knee - threshold, 2f * knee, 0.25f / (knee + 0.00001f));

            BloomPass prefilterPass = m_Bloom.FadeFireflies.value ? BloomPass.PrefilterFireflies : BloomPass.Prefilter;
            DrawBloom(cmd, bloomSource, m_BloomPrefilter, prefilterPass);

            RTHandle lowRes = m_BloomPrefilter;
            for (int i = 0; i < m_BloomMipCount; i++)
            {
                // Up 缓冲先用作水平模糊的中间结果，回合成时再复用。
                DrawBloom(cmd, lowRes, m_BloomUp[i], BloomPass.Horizontal);
                DrawBloom(cmd, m_BloomUp[i], m_BloomDown[i], BloomPass.Vertical);
                lowRes = m_BloomDown[i];
            }

            bool scatter = m_Bloom.Mode.value == GRKingBloomMode.Scatter;
            BloomPass combinePass = scatter ? BloomPass.Scatter : BloomPass.Add;
            float combineIntensity = scatter ? m_Bloom.Scatter.value : 1f;
            for (int i = m_BloomMipCount - 2; i >= 0; i--)
            {
                DrawBloom(cmd, lowRes, m_BloomUp[i], combinePass, m_BloomDown[i], combineIntensity);
                lowRes = m_BloomUp[i];
            }

            // Scatter 最终使用 lerp，所以强度限制在 [0, 1]，避免外插；Additive 不限制上限。
            float finalIntensity = scatter ? Mathf.Clamp01(m_Bloom.Intensity.value) : m_Bloom.Intensity.value;
            DrawBloom(cmd, lowRes, destination, scatter ? BloomPass.ScatterFinal : BloomPass.Add,
                sceneSource, finalIntensity, bloomSource);
        }

        private void DrawBloom(CommandBuffer cmd, RTHandle source, RTHandle destination, BloomPass pass,
            RTHandle highRes = null, float intensity = 1f, RTHandle highlightSource = null)
        {
            SetSourceProperties(source);
            m_Properties.SetVector(ShaderIDs.BloomThreshold, m_BloomThreshold);
            m_Properties.SetFloat(ShaderIDs.BloomBicubicUpsampling,
                m_Bloom.BicubicUpsampling.value ? 1f : 0f);
            m_Properties.SetFloat(ShaderIDs.BloomIntensity, intensity);
            if (highRes != null)
                m_Properties.SetTexture(ShaderIDs.BloomSource2, highRes.rt);
            if (highlightSource != null)
                m_Properties.SetTexture(ShaderIDs.BloomHighlightSource, highlightSource.rt);

            Draw(cmd, destination, m_BloomMaterial, (int)pass);
        }

        private void ExecuteToneMapping(CommandBuffer cmd, RTHandle source, RTHandle destination)
        {
            SetSourceProperties(source);
            ToneMappingPass pass;
            switch (m_ToneMapping.Mode.value)
            {
                case GRKingToneMappingMode.NAES:
                    pass = ToneMappingPass.NAES;
                    m_Properties.SetVector(ShaderIDs.NaesParams, new Vector4(
                        m_ToneMapping.NaesParamA.value, m_ToneMapping.NaesParamB.value,
                        m_ToneMapping.NaesParamC.value, m_ToneMapping.NaesParamD.value));
                    break;

                case GRKingToneMappingMode.GranTurismo:
                    pass = ToneMappingPass.GranTurismo;
                    // 保证 0 < M < P、A > 0、L < 1，避免 GT 曲线分母为 0。
                    float maximum = Mathf.Max(2f * CurveEpsilon, m_ToneMapping.GtMaximumBrightness.value);
                    float linearStart = Mathf.Clamp(m_ToneMapping.GtLinearStart.value, CurveEpsilon, maximum - CurveEpsilon);
                    m_Properties.SetVector(ShaderIDs.GTParams1, new Vector4(
                        maximum, Mathf.Max(CurveEpsilon, m_ToneMapping.GtContrast.value),
                        linearStart, Mathf.Clamp(m_ToneMapping.GtLinearLength.value, 0f, 1f - CurveEpsilon)));
                    m_Properties.SetVector(ShaderIDs.GTParams2, new Vector4(
                        m_ToneMapping.GtBlackPower.value, m_ToneMapping.GtBlackMinimum.value, 0f, 0f));
                    break;

                case GRKingToneMappingMode.Film:
                    pass = ToneMappingPass.Film;
                    float toe = Mathf.Clamp(m_ToneMapping.FilmToe.value, 0f, 1f - CurveEpsilon);
                    float shoulder = Mathf.Clamp(m_ToneMapping.FilmShoulder.value, 0f, 1f - CurveEpsilon);
                    // Toe + Shoulder == 1 会让 Shader 的 ShoulderMatch - ToeMatch 为 0。
                    if (Mathf.Abs(toe + shoulder - 1f) < CurveEpsilon)
                        shoulder = Mathf.Max(0f, 1f - toe - CurveEpsilon);
                    m_Properties.SetVector(ShaderIDs.FilmParams1, new Vector4(
                        Mathf.Max(CurveEpsilon, m_ToneMapping.FilmSlope.value), toe, shoulder, 0f));
                    m_Properties.SetVector(ShaderIDs.FilmParams2, new Vector4(
                        m_ToneMapping.FilmBlackClip.value, m_ToneMapping.FilmWhiteClip.value, 0f, 0f));
                    break;

                default:
                    Blitter.BlitCameraTexture(cmd, source, destination);
                    return;
            }

            Draw(cmd, destination, m_ToneMappingMaterial, (int)pass);
        }

        private void ExecuteFXAA(CommandBuffer cmd, RTHandle source, RTHandle destination)
        {
            SetSourceProperties(source);
            m_Properties.SetVector(ShaderIDs.FXAAConfig, new Vector4(
                m_FXAA.FixedThreshold.value,
                m_FXAA.RelativeThreshold.value,
                m_FXAA.SubpixelBlending.value,
                0f));

            CoreUtils.SetKeyword(m_FXAAMaterial, "FXAA_QUALITY_LOW",
                m_FXAA.Quality.value == GRKingFXAAQuality.Low);
            CoreUtils.SetKeyword(m_FXAAMaterial, "FXAA_QUALITY_MEDIUM",
                m_FXAA.Quality.value == GRKingFXAAQuality.Medium);
            Draw(cmd, destination, m_FXAAMaterial, (int)FXAAPass.FXAA);
        }

        private void SetSourceProperties(RTHandle source)
        {
            m_Properties.Clear();
            m_Properties.SetTexture(ShaderIDs.PostFXSource, source.rt);

            Vector2Int size = source.rt != null
                ? new Vector2Int(source.rt.width, source.rt.height)
                : source.rtHandleProperties.currentRenderTargetSize;
            int width = Mathf.Max(1, size.x);
            int height = Mathf.Max(1, size.y);
            m_Properties.SetVector(ShaderIDs.PostFXSourceTexelSize,
                new Vector4(1f / width, 1f / height, width, height));
        }

        private void Draw(CommandBuffer cmd, RTHandle destination, Material material, int pass)
        {
            CoreUtils.SetRenderTarget(cmd, destination, RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store, ClearFlag.None, Color.clear);
            cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3, 1, m_Properties);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // 缓存的 RTHandle 留给下一帧复用；不持有上一台相机的 Volume 状态。
            m_ApplyBloom = false;
            m_ApplyToneMapping = false;
            m_ApplyFXAA = false;
            m_Bloom = null;
            m_ToneMapping = null;
            m_FXAA = null;
            m_Properties.Clear();
        }

        private void ReleaseBloomTextures()
        {
            m_BloomCharacterColor?.Release();
            m_BloomCharacterColor = null;
            m_BloomPrefilter?.Release();
            m_BloomPrefilter = null;
            for (int i = 0; i < MaxBloomMipCount; i++)
            {
                m_BloomDown[i]?.Release();
                m_BloomDown[i] = null;
                m_BloomUp[i]?.Release();
                m_BloomUp[i] = null;
            }
            m_BloomMipCount = 0;
        }

        public void Dispose()
        {
            m_ColorCopy?.Release();
            m_ColorCopy = null;
            m_EffectResult?.Release();
            m_EffectResult = null;
            ReleaseBloomTextures();
            CoreUtils.Destroy(m_BloomMaterial);
            CoreUtils.Destroy(m_ToneMappingMaterial);
            CoreUtils.Destroy(m_FXAAMaterial);
            m_BloomMaterial = null;
            m_ToneMappingMaterial = null;
            m_FXAAMaterial = null;
            m_ApplyBloom = false;
            m_ApplyToneMapping = false;
            m_ApplyFXAA = false;
            m_Bloom = null;
            m_ToneMapping = null;
            m_FXAA = null;
            m_Properties.Clear();
        }
    }
}

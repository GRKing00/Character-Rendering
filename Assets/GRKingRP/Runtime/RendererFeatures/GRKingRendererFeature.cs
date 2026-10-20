using GRKingRP.Passes;
using GRKingRP.PerObjectShadow;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.RendererFeatures
{
    /// <summary>
    /// GRKingRP 的统一渲染入口，管理角色绘制和后处理。
    /// </summary>
    [DisallowMultipleRendererFeature("GRKingRP")]
    public sealed class GRKingRendererFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        [UnityEditor.ShaderKeywordFilter.ApplyRulesIfNotGraphicsAPI(UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2)]
        [UnityEditor.ShaderKeywordFilter.SelectIf(true, keywordNames: "_MAIN_LIGHT_SHADOWS_SCREEN")]
        private const bool k_RequiresScreenShadowVariant = true;
#endif
        [Header("Character Rendering")]
        public bool EnableCharacterRendering = true;
        public bool EnableCharacterOutline = true;
        public bool EnableScreenSpaceRim = true;
        public bool EnableFrontHairShadow = true;
        [Tooltip("选择参与自定义角色绘制的 GameObject Layer。")]
        public LayerMask CharacterLayerMask = -1;

        [Header("Character Self Shadows")]
        [FormerlySerializedAs("EnablePerObjectShadow")]
        public bool EnableSelfShadow = true;
        [FormerlySerializedAs("PerObjectShadowAtlasLayout")]
        public GRKingPerObjectShadowAtlasLayout SelfShadowAtlasLayout = GRKingPerObjectShadowAtlasLayout.Grid2x2;
        [FormerlySerializedAs("PerObjectShadowAtlasResolution")]
        [Tooltip("整个自阴影 Atlas 的宽高；单个 Tile 分辨率由布局决定。")]
        public GRKingPerObjectShadowAtlasResolution SelfShadowAtlasResolution =
            GRKingPerObjectShadowAtlasResolution.Resolution2048;

        [Header("Character Scene Shadows")]
        public bool EnableSceneShadow = true;
        public GRKingPerObjectShadowAtlasLayout SceneShadowAtlasLayout = GRKingPerObjectShadowAtlasLayout.Grid2x2;
        [Tooltip("整个场景投影 Atlas 的宽高；单个 Tile 分辨率由布局决定。")]
        public GRKingPerObjectShadowAtlasResolution SceneShadowAtlasResolution =
            GRKingPerObjectShadowAtlasResolution.Resolution1024;
        [Min(0.01f)]
        [Tooltip("沿主光传播方向扩展角色阴影体积的长度，单位为世界空间距离。")]
        public float SceneShadowDistance = 100f;

        private GRKingPerObjectShadowPass m_SelfShadowPass;
        private GRKingPerObjectShadowPass m_SceneShadowPass;
        private GRKingScreenSpaceShadowPass m_ScreenShadowPass;
        private GRKingRestoreShadowKeywordsPass m_RestoreShadowKeywordsPass;

        [SerializeField, HideInInspector]
        private Shader m_ScreenSpaceShadowShader;
        private GRKingBodyDepthPass m_BodyDepthPass;
        private GRKingHairDepthPass m_HairDepthPass;
        private GRKingCharacterDrawPass m_CharacterBodyPass;
        private GRKingCharacterDrawPass m_CharacterEyeStencilPass;
        private GRKingCharacterDrawPass m_CharacterEyeShadowPass;
        private GRKingCharacterDrawPass m_CharacterHairOpaquePass;
        private GRKingCharacterDrawPass m_CharacterHairTransparentPass;
        private GRKingCharacterDrawPass m_CharacterOutlinePass;

        [Header("Post Processing")]
        [Tooltip("开启后，根据当前相机的 Volume 设置将后处理 Pass 加入渲染队列。")]
        public bool EnablePostProcessing = true;

        [SerializeField, HideInInspector]
        [Tooltip("Bloom Shader 的直接引用，确保构建时包含该 Shader。")]
        private Shader m_BloomShader = null;

        [SerializeField, HideInInspector]
        [Tooltip("Tone Mapping Shader 的直接引用，确保构建时包含该 Shader。")]
        private Shader m_ToneMappingShader = null;

        [SerializeField, HideInInspector]
        [Tooltip("FXAA Shader 的直接引用，确保构建时包含该 Shader。")]
        private Shader m_FXAAShader = null;

        private GRKingPostProcessingPass m_PostProcessingPass;

        public override void Create()
        {

            // Inspector 修改配置等操作会重复调用 Create，先释放旧 Pass 的材质和 RTHandle。
            DisposePostProcessingPass();
            DisposeBodyDepthPass();
            DisposeHairDepthPass();
            DisposeSceneShadowPass();
            DisposeSelfShadowPass();
            DisposeScreenShadowPass();
            m_SceneShadowPass = new GRKingPerObjectShadowPass(GRKingShadowUsage.Scene);
            m_SelfShadowPass = new GRKingPerObjectShadowPass(GRKingShadowUsage.Self);
            m_RestoreShadowKeywordsPass = new GRKingRestoreShadowKeywordsPass();
            m_BodyDepthPass = new GRKingBodyDepthPass(CharacterLayerMask);
            m_HairDepthPass = new GRKingHairDepthPass(CharacterLayerMask);
            m_CharacterBodyPass = new GRKingCharacterDrawPass(
                "GRKingRP Character Body", "GRKingCharacterBody", CharacterLayerMask);
            m_CharacterEyeStencilPass = new GRKingCharacterDrawPass(
                "GRKingRP Character Eye Stencil", "GRKingCharacterEyeStencil", CharacterLayerMask);
            m_CharacterEyeShadowPass = new GRKingCharacterDrawPass(
                "GRKingRP Character Eye Shadow", "GRKingCharacterEyeShadow", CharacterLayerMask);
            m_CharacterHairOpaquePass = new GRKingCharacterDrawPass(
                "GRKingRP Character Hair Opaque", "GRKingCharacterHairOpaque", CharacterLayerMask);
            m_CharacterHairTransparentPass = new GRKingCharacterDrawPass(
                "GRKingRP Character Hair Transparent", "GRKingCharacterHairTransparent", CharacterLayerMask);
            m_CharacterOutlinePass = new GRKingCharacterDrawPass(
                "GRKingRP Character Outline", "GRKingCharacterOutline", CharacterLayerMask);

#if UNITY_EDITOR
            if (m_ScreenSpaceShadowShader == null)
                m_ScreenSpaceShadowShader = Shader.Find("Hidden/GRKingRP/ScreenSpaceShadows");
            // 编辑器中自动补齐引用，并由 Renderer Data 资产序列化保存。
            // 构建后使用直接引用，不依赖 Shader.Find 查找可能被剔除的 Hidden Shader。
            if (m_BloomShader == null)
                m_BloomShader = Shader.Find("Hidden/GRKingRP/PostProcessing/Bloom");
            if (m_ToneMappingShader == null)
                m_ToneMappingShader = Shader.Find("Hidden/GRKingRP/PostProcessing/ToneMapping");
            if (m_FXAAShader == null)
                m_FXAAShader = Shader.Find("Hidden/GRKingRP/PostProcessing/FXAA");
#endif

            if (m_ScreenSpaceShadowShader != null)
                m_ScreenShadowPass = new GRKingScreenSpaceShadowPass(m_ScreenSpaceShadowShader);
            else if (EnableSceneShadow)
                Debug.LogWarning("[GRKingRP] 缺少屏幕阴影 Shader，角色对场景的投影将被跳过。", this);

            if (EnablePostProcessing &&
                (m_BloomShader == null || m_ToneMappingShader == null || m_FXAAShader == null))
            {
                Debug.LogWarning(
                    "[GRKingRP] 请在 Renderer Feature 中指定 Bloom、Tone Mapping 和 FXAA Shader。" +
                    "缺少 Shader 的后效将被跳过。", this);
            }

            // 即使当前关闭后处理，也保留 Pass，以支持运行时切换开关。
            // 中间纹理只在 Pass 真正执行时才会分配。
            if (m_BloomShader != null || m_ToneMappingShader != null || m_FXAAShader != null)
                m_PostProcessingPass = new GRKingPostProcessingPass(
                    m_BloomShader, m_ToneMappingShader, m_FXAAShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            bool enableShadows = EnableCharacterRendering && !renderingData.cameraData.isPreviewCamera &&
                (renderingData.cameraData.cameraType == CameraType.Game ||
                 renderingData.cameraData.cameraType == CameraType.SceneView);
            m_SceneShadowPass.Setup(enableShadows && EnableSceneShadow, CharacterLayerMask,
                SceneShadowAtlasLayout, SceneShadowAtlasResolution, SceneShadowDistance);
            m_SelfShadowPass.Setup(enableShadows && EnableSelfShadow, CharacterLayerMask,
                SelfShadowAtlasLayout, SelfShadowAtlasResolution);
            // 两套 Atlas 都覆盖当前相机的数量，防止相机切换沿用上一相机的阴影。
            renderer.EnqueuePass(m_SceneShadowPass);
            renderer.EnqueuePass(m_SelfShadowPass);

            if (m_ScreenShadowPass != null && m_ScreenShadowPass.Setup(
                    enableShadows && EnableSceneShadow && GRKingCharacterShadowCaster.ActiveCasters.Count > 0,
                    ref renderingData))
            {
                renderer.EnqueuePass(m_ScreenShadowPass);
                renderer.EnqueuePass(m_RestoreShadowKeywordsPass);
            }

            if (EnableCharacterRendering)
            {
                bool enableScreenSpaceRim = EnableScreenSpaceRim &&
                    !renderingData.cameraData.isPreviewCamera;
                bool enableFrontHairShadow = EnableFrontHairShadow &&
                    !renderingData.cameraData.isPreviewCamera;

                m_BodyDepthPass.Setup(enableScreenSpaceRim);
                renderer.EnqueuePass(m_BodyDepthPass);
                m_HairDepthPass.Setup(enableFrontHairShadow);
                renderer.EnqueuePass(m_HairDepthPass);

                renderer.EnqueuePass(m_CharacterBodyPass);
                // 眼影先于头发绘制，以免被头发深度遮挡。
                renderer.EnqueuePass(m_CharacterEyeStencilPass);
                renderer.EnqueuePass(m_CharacterEyeShadowPass);
                renderer.EnqueuePass(m_CharacterHairOpaquePass);
                renderer.EnqueuePass(m_CharacterHairTransparentPass);
                if (EnableCharacterOutline)
                    renderer.EnqueuePass(m_CharacterOutlinePass);
            }

            // Setup 会检查相机类型和当前 Volume 的后效激活状态。
            // 自定义后处理不依赖 Camera 和 Renderer Data 的 URP 内置后处理开关。
            // 此时不读取 cameraColorTargetHandle，由 Pass 在 Execute 阶段获取。
            if (EnablePostProcessing && m_PostProcessingPass != null &&
                m_PostProcessingPass.Setup(ref renderingData))
                renderer.EnqueuePass(m_PostProcessingPass);
        }

        protected override void Dispose(bool disposing)
        {
            DisposeSceneShadowPass();
            DisposeSelfShadowPass();
            DisposeScreenShadowPass();
            DisposeBodyDepthPass();
            DisposeHairDepthPass();
            DisposePostProcessingPass();
        }

        private void DisposeSceneShadowPass()
        {
            m_SceneShadowPass?.Dispose();
            m_SceneShadowPass = null;
        }

        private void DisposeScreenShadowPass()
        {
            m_ScreenShadowPass?.Dispose();
            m_ScreenShadowPass = null;
            m_RestoreShadowKeywordsPass = null;
        }

        private void DisposeSelfShadowPass()
        {
            m_SelfShadowPass?.Dispose();
            m_SelfShadowPass = null;
        }

        private void DisposeBodyDepthPass()
        {
            m_BodyDepthPass?.Dispose();
            m_BodyDepthPass = null;
        }

        private void DisposeHairDepthPass()
        {
            m_HairDepthPass?.Dispose();
            m_HairDepthPass = null;
        }

        private void DisposePostProcessingPass()
        {
            m_PostProcessingPass?.Dispose();
            m_PostProcessingPass = null;
        }
    }
}

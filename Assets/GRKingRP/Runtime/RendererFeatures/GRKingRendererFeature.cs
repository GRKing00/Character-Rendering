using GRKingRP.Passes;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.RendererFeatures
{
    /// <summary>
    /// GRKingRP 的统一渲染入口。目前只负责后处理，其他渲染功能之后在此扩展。
    /// </summary>
    [DisallowMultipleRendererFeature("GRKingRP")]
    public sealed class GRKingRendererFeature : ScriptableRendererFeature
    {
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

#if UNITY_EDITOR
            // 编辑器中自动补齐引用，并由 Renderer Data 资产序列化保存。
            // 构建后使用直接引用，不依赖 Shader.Find 查找可能被剔除的 Hidden Shader。
            if (m_BloomShader == null)
                m_BloomShader = Shader.Find("Hidden/GRKingRP/PostProcessing/Bloom");
            if (m_ToneMappingShader == null)
                m_ToneMappingShader = Shader.Find("Hidden/GRKingRP/PostProcessing/ToneMapping");
            if (m_FXAAShader == null)
                m_FXAAShader = Shader.Find("Hidden/GRKingRP/PostProcessing/FXAA");
#endif

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
            // Setup 会检查相机类型和当前 Volume 的后效激活状态。
            // 自定义后处理不依赖 Camera 和 Renderer Data 的 URP 内置后处理开关。
            // 此时不读取 cameraColorTargetHandle，由 Pass 在 Execute 阶段获取。
            if (EnablePostProcessing && m_PostProcessingPass != null &&
                m_PostProcessingPass.Setup(ref renderingData))
                renderer.EnqueuePass(m_PostProcessingPass);
        }

        protected override void Dispose(bool disposing)
        {
            DisposePostProcessingPass();
        }

        private void DisposePostProcessingPass()
        {
            m_PostProcessingPass?.Dispose();
            m_PostProcessingPass = null;
        }
    }
}

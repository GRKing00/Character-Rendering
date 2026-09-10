using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.Passes
{
    /// <summary>按自定义 LightMode 绘制角色，不覆盖材质的深度、剔除和模板状态。</summary>
    public sealed class GRKingCharacterDrawPass : ScriptableRenderPass
    {
        private readonly ShaderTagId m_ShaderTag;
        private FilteringSettings m_Filtering;

        public GRKingCharacterDrawPass(string name, string lightMode, LayerMask layerMask)
        {
            profilingSampler = new ProfilingSampler(name);
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            m_ShaderTag = new ShaderTagId(lightMode);
            m_Filtering = new FilteringSettings(RenderQueueRange.opaque, layerMask);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ScriptableRenderer renderer = renderingData.cameraData.renderer;
            ConfigureTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            DrawingSettings drawing = CreateDrawingSettings(
                m_ShaderTag, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
            CommandBuffer cmd = CommandBufferPool.Get();
            try
            {
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    context.DrawRenderers(renderingData.cullResults, ref drawing, ref m_Filtering);
                }
                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
        }
    }
}

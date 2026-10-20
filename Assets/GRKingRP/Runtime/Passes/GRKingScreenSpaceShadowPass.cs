using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.Passes
{
    /// <summary>将级联阴影和角色 Scene Atlas 合并，供场景不透明材质接收。</summary>
    public sealed class GRKingScreenSpaceShadowPass : ScriptableRenderPass, IDisposable
    {
        private const string MainShadows = "_MAIN_LIGHT_SHADOWS";
        private const string Cascades = "_MAIN_LIGHT_SHADOWS_CASCADE";
        private const string ScreenShadows = "_MAIN_LIGHT_SHADOWS_SCREEN";
        private static readonly int s_ScreenTexture = Shader.PropertyToID("_ScreenSpaceShadowmapTexture");
        private readonly Material m_Material;
        private RTHandle m_ScreenTexture;
        private bool m_MainLightShadows;
        private int m_CascadeCount;

        public GRKingScreenSpaceShadowPass(Shader shader)
        {
            m_Material = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses + 1;
            profilingSampler = new ProfilingSampler("GRKingRP Scene Screen Shadows");
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public bool Setup(bool enabled, ref RenderingData renderingData)
        {
            m_MainLightShadows = renderingData.shadowData.supportsMainLightShadows &&
                renderingData.lightData.mainLightIndex >= 0;
            m_CascadeCount = renderingData.shadowData.mainLightShadowCascadesCount;
            if (!enabled || m_Material == null || !m_MainLightShadows)
                return false;
            Light mainLight = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex].light;
            Camera camera = renderingData.cameraData.camera;
            return mainLight != null && mainLight.type == LightType.Directional &&
                mainLight.shadows != LightShadows.None &&
                (camera.targetTexture == null || camera.targetTexture.format != RenderTextureFormat.Depth);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            descriptor.graphicsFormat = RenderingUtils.SupportsGraphicsFormat(
                GraphicsFormat.R8_UNorm, FormatUsage.Render | FormatUsage.Linear)
                ? GraphicsFormat.R8_UNorm : GraphicsFormat.R8G8B8A8_UNorm;
            RenderingUtils.ReAllocateIfNeeded(ref m_ScreenTexture, descriptor,
                FilterMode.Point, TextureWrapMode.Clamp, name: "_ScreenSpaceShadowmapTexture");
            ConfigureTarget(m_ScreenTexture);
            ConfigureClear(ClearFlag.None, Color.white);
            cmd.SetGlobalTexture(s_ScreenTexture, m_ScreenTexture.nameID);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            try
            {
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // 合成 Shader 必须直接读取级联图，而不是回读当前屏幕阴影。
                    SetCascadeKeywords(cmd, m_MainLightShadows, m_CascadeCount);
                    Blitter.BlitCameraTexture(cmd, m_ScreenTexture, m_ScreenTexture, m_Material, 0);
                    CoreUtils.SetKeyword(cmd, MainShadows, false);
                    CoreUtils.SetKeyword(cmd, Cascades, false);
                    CoreUtils.SetKeyword(cmd, ScreenShadows, true);
                }
                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
        }

        internal static void SetCascadeKeywords(CommandBuffer cmd, bool enabled, int cascadeCount)
        {
            CoreUtils.SetKeyword(cmd, ScreenShadows, false);
            CoreUtils.SetKeyword(cmd, MainShadows, enabled && cascadeCount <= 1);
            CoreUtils.SetKeyword(cmd, Cascades, enabled && cascadeCount > 1);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            SetCascadeKeywords(cmd, m_MainLightShadows, m_CascadeCount);
        }

        public void Dispose()
        {
            m_ScreenTexture?.Release();
            m_ScreenTexture = null;
            CoreUtils.Destroy(m_Material);
        }
    }

    /// <summary>自定义角色完成后，普通透明物体恢复 URP 原来的阴影接收。</summary>
    public sealed class GRKingRestoreShadowKeywordsPass : ScriptableRenderPass
    {
        public GRKingRestoreShadowKeywordsPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            profilingSampler = new ProfilingSampler("GRKingRP Restore Shadow Keywords");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ScriptableRenderer renderer = renderingData.cameraData.renderer;
            ConfigureTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            try
            {
                using (new ProfilingScope(cmd, profilingSampler))
                    GRKingScreenSpaceShadowPass.SetCascadeKeywords(cmd,
                        renderingData.shadowData.supportsMainLightShadows && renderingData.lightData.mainLightIndex >= 0,
                        renderingData.shadowData.mainLightShadowCascadesCount);
                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
        }
    }
}

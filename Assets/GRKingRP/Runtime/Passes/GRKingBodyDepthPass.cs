// Screen-space rim depth pass adapted from StarRailNPRShader.
// Copyright (C) 2023 Stalo <stalowork@163.com>
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.Passes
{
    /// <summary>生成身体深度纹理，供身体 Shader 进行屏幕空间 Rim 判断。</summary>
    public sealed class GRKingBodyDepthPass : ScriptableRenderPass, IDisposable
    {
        private static readonly ShaderTagId s_BodyDepthTag =
            new ShaderTagId("GRKingCharacterBodyDepth");
        private static readonly int s_BodyDepthTexture =
            Shader.PropertyToID("_GRKingBodyDepthTexture");
        private static readonly int s_ScreenSpaceRimEnabled =
            Shader.PropertyToID("_GRKingScreenSpaceRimEnabled");

        private FilteringSettings m_Filtering;
        private RTHandle m_BodyDepthTexture;
        private bool m_Enabled;

        public GRKingBodyDepthPass(LayerMask layerMask)
        {
            profilingSampler = new ProfilingSampler("GRKingRP Body Depth");
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
            m_Filtering = new FilteringSettings(RenderQueueRange.opaque, layerMask);
        }

        public void Setup(bool enabled)
        {
            m_Enabled = enabled;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (!m_Enabled)
            {
                ScriptableRenderer renderer = renderingData.cameraData.renderer;
                ConfigureTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
                ConfigureClear(ClearFlag.None, Color.clear);
                return;
            }

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.graphicsFormat = GraphicsFormat.None;
            descriptor.depthStencilFormat = GraphicsFormatUtility.GetDepthStencilFormat(16, 0);

            RenderingUtils.ReAllocateIfNeeded(
                ref m_BodyDepthTexture,
                descriptor,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "_GRKingBodyDepthTexture");

            ConfigureTarget(m_BodyDepthTexture);
            ConfigureClear(ClearFlag.All, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            try
            {
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    cmd.SetGlobalFloat(s_ScreenSpaceRimEnabled, m_Enabled ? 1f : 0f);

                    if (m_Enabled)
                    {
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();

                        DrawingSettings drawing = CreateDrawingSettings(
                            s_BodyDepthTag,
                            ref renderingData,
                            renderingData.cameraData.defaultOpaqueSortFlags);
                        drawing.perObjectData = PerObjectData.None;
                        context.DrawRenderers(
                            renderingData.cullResults,
                            ref drawing,
                            ref m_Filtering);

                        cmd.SetGlobalTexture(s_BodyDepthTexture, m_BodyDepthTexture.nameID);
                    }
                }

                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
        }

        public void Dispose()
        {
            m_BodyDepthTexture?.Release();
            m_BodyDepthTexture = null;
        }
    }
}

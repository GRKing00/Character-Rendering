// Hair depth pass adapted from StarRailNPRShader.
// Copyright (C) 2023 Stalo <stalowork@163.com>
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.Passes
{
    /// <summary>将角色头发写入独立深度纹理，供脸部生成刘海阴影。</summary>
    public sealed class GRKingHairDepthPass : ScriptableRenderPass, IDisposable
    {
        public enum DownsampleMode
        {
            Full = 1,
            Half = 2,
            Quarter = 4
        }

        private static readonly ShaderTagId s_HairDepthTag =
            new ShaderTagId("GRKingCharacterHairDepth");
        private static readonly int s_HairDepthTexture =
            Shader.PropertyToID("_GRKingHairDepthTexture");
        private static readonly int s_HairShadowEnabled =
            Shader.PropertyToID("_GRKingHairShadowEnabled");

        private FilteringSettings m_Filtering;
        private RTHandle m_HairDepthTexture;
        private DownsampleMode m_Downsample = DownsampleMode.Half;
        private bool m_Enabled;

        public GRKingHairDepthPass(LayerMask layerMask)
        {
            profilingSampler = new ProfilingSampler("GRKingRP Hair Depth");
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
            m_Filtering = new FilteringSettings(RenderQueueRange.opaque, layerMask);
        }

        public void Setup(bool enabled, DownsampleMode downsample)
        {
            m_Enabled = enabled;
            m_Downsample = downsample;
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
            int divisor = (int)m_Downsample;
            descriptor.width = Mathf.Max(1, descriptor.width / divisor);
            descriptor.height = Mathf.Max(1, descriptor.height / divisor);
            descriptor.msaaSamples = 1;
            descriptor.graphicsFormat = GraphicsFormat.None;
            descriptor.depthStencilFormat = GraphicsFormatUtility.GetDepthStencilFormat(16, 0);

            RenderingUtils.ReAllocateIfNeeded(
                ref m_HairDepthTexture,
                descriptor,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "_GRKingHairDepthTexture");

            ConfigureTarget(m_HairDepthTexture);
            ConfigureClear(ClearFlag.All, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            try
            {
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    cmd.SetGlobalFloat(s_HairShadowEnabled, m_Enabled ? 1f : 0f);

                    if (m_Enabled)
                    {
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();

                        DrawingSettings drawing = CreateDrawingSettings(
                            s_HairDepthTag,
                            ref renderingData,
                            renderingData.cameraData.defaultOpaqueSortFlags);
                        drawing.perObjectData = PerObjectData.None;
                        context.DrawRenderers(
                            renderingData.cullResults,
                            ref drawing,
                            ref m_Filtering);

                        cmd.SetGlobalTexture(s_HairDepthTexture, m_HairDepthTexture.nameID);
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
            m_HairDepthTexture?.Release();
            m_HairDepthTexture = null;
        }
    }
}

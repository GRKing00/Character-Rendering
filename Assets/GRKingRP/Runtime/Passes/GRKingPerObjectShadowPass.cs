using System;
using System.Collections.Generic;
using GRKingRP.PerObjectShadow;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.Passes
{
    public enum GRKingPerObjectShadowAtlasLayout
    {
        [InspectorName("1 x 1")] Grid1x1 = 1,
        [InspectorName("2 x 2")] Grid2x2 = 2,
        [InspectorName("4 x 4")] Grid4x4 = 4
    }

    public enum GRKingPerObjectShadowAtlasResolution
    {
        [InspectorName("256")] Resolution256 = 256,
        [InspectorName("512")] Resolution512 = 512,
        [InspectorName("1024")] Resolution1024 = 1024,
        [InspectorName("2048")] Resolution2048 = 2048,
        [InspectorName("4096")] Resolution4096 = 4096
    }

    /// <summary>分别生成 Scene 和 Self Atlas，并输出采样矩阵、范围及自阴影角色 ID。</summary>
    public sealed class GRKingPerObjectShadowPass : ScriptableRenderPass, IDisposable
    {
        public const int MaxShadowCount = 16;
        private const int TileBorder = 4;
        private const string SelfShadowKeyword = "_GRKING_CASTING_SELF_SHADOW";
        private static readonly int s_ShadowBias = Shader.PropertyToID("_ShadowBias");
        private static readonly int s_LightDirection = Shader.PropertyToID("_LightDirection");
        private static readonly int s_MainLightCascadeCount = Shader.PropertyToID("_GRKingMainLightShadowCascadeCount");
        private static readonly int s_SelfCasterIds = Shader.PropertyToID("_GRKingSelfShadowCasterIds");

        private struct Candidate
        {
            public GRKingCharacterShadowCaster Caster;
            public Matrix4x4 View;
            public Matrix4x4 Projection;
            public Vector3 LightDirection;
            public float Priority;
        }

        private struct DrawCall
        {
            public Renderer Renderer;
            public Material Material;
            public int Submesh;
            public int Pass;
        }

        private struct Slice
        {
            public GRKingCharacterShadowCaster Caster;
            public Matrix4x4 View;
            public Matrix4x4 Projection;
            public Vector3 LightDirection;
            public Rect Viewport;
            public int DrawStart;
            public int DrawEnd;
        }

        private readonly GRKingShadowUsage m_Usage;
        private readonly GRKingShadowUtility m_Utility = new();
        private readonly List<Candidate> m_Candidates = new();
        private readonly List<Renderer> m_EligibleRenderers = new();
        private readonly List<Material> m_Materials = new();
        private readonly List<DrawCall> m_DrawCalls = new();
        private readonly Slice[] m_Slices = new Slice[MaxShadowCount];
        private readonly Matrix4x4[] m_Matrices = new Matrix4x4[MaxShadowCount];
        private readonly Vector4[] m_Rects = new Vector4[MaxShadowCount];
        private readonly float[] m_CasterIds = new float[MaxShadowCount];
        private readonly string m_AtlasName;
        private readonly int m_AtlasId;
        private readonly int m_CountId;
        private readonly int m_MatricesId;
        private readonly int m_RectsId;
        private readonly int m_TexelSizeId;
        private RTHandle m_Atlas;
        private bool m_Enabled;
        private int m_LayerMask;
        private int m_TilesPerSide;
        private int m_AtlasSize;
        private int m_ShadowCount;
        private float m_SceneShadowDistance;

        public GRKingShadowUsage Usage => m_Usage;
        public int ShadowCount => m_ShadowCount;
        public RTHandle Atlas => m_Atlas;

        public GRKingPerObjectShadowPass(GRKingShadowUsage usage)
        {
            m_Usage = usage;
            string prefix = usage == GRKingShadowUsage.Scene ? "_GRKingSceneShadow" : "_GRKingSelfShadow";
            m_AtlasName = prefix + "Atlas";
            m_AtlasId = Shader.PropertyToID(m_AtlasName);
            m_CountId = Shader.PropertyToID(prefix + "Count");
            m_MatricesId = Shader.PropertyToID(prefix + "Matrices");
            m_RectsId = Shader.PropertyToID(prefix + "Rects");
            m_TexelSizeId = Shader.PropertyToID(m_AtlasName + "_TexelSize");
            renderPassEvent = RenderPassEvent.AfterRenderingShadows;
            profilingSampler = new ProfilingSampler(usage == GRKingShadowUsage.Scene
                ? "GRKingRP Scene Shadow Atlas" : "GRKingRP Self Shadow Atlas");
        }

        public GRKingCharacterShadowCaster GetCaster(int tileIndex)
        {
            if (tileIndex < 0 || tileIndex >= m_ShadowCount)
                throw new ArgumentOutOfRangeException(nameof(tileIndex));
            return m_Slices[tileIndex].Caster;
        }

        public void Setup(bool enabled, LayerMask layerMask, GRKingPerObjectShadowAtlasLayout layout,
            GRKingPerObjectShadowAtlasResolution resolution, float sceneShadowDistance = 100f)
        {
            m_Enabled = enabled;
            m_LayerMask = layerMask;
            m_TilesPerSide = layout switch
            {
                GRKingPerObjectShadowAtlasLayout.Grid1x1 => 1,
                GRKingPerObjectShadowAtlasLayout.Grid4x4 => 4,
                _ => 2
            };
            m_AtlasSize = Mathf.Min(Mathf.Clamp((int)resolution, 256, 4096), SystemInfo.maxTextureSize);
            m_SceneShadowDistance = Mathf.Max(0.01f, sceneShadowDistance);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            m_ShadowCount = 0;
            m_Candidates.Clear();
            m_DrawCalls.Clear();
            Array.Clear(m_Slices, 0, m_Slices.Length);
            CameraData cameraData = renderingData.cameraData;
            int lightIndex = renderingData.lightData.mainLightIndex;
            if (m_Enabled && lightIndex >= 0 && renderingData.shadowData.supportsMainLightShadows)
            {
                VisibleLight light = renderingData.lightData.visibleLights[lightIndex];
                if (light.lightType == LightType.Directional && light.light != null &&
                    light.light.shadows != LightShadows.None)
                {
                    int layerMask = m_LayerMask & cameraData.camera.cullingMask & light.light.cullingMask;
                    CollectSlices(cameraData.camera, light.light.transform, layerMask);
                }
            }

            if (m_ShadowCount == 0)
            {
                ConfigureTarget(cameraData.renderer.cameraColorTargetHandle,
                    cameraData.renderer.cameraDepthTargetHandle);
                ConfigureClear(ClearFlag.None, Color.clear);
                return;
            }

            ShadowUtils.ShadowRTReAllocateIfNeeded(ref m_Atlas, m_AtlasSize, m_AtlasSize, 16, name: m_AtlasName);
            ConfigureTarget(m_Atlas);
            ConfigureClear(ClearFlag.All, Color.black);
        }

        private void CollectSlices(Camera camera, Transform mainLight, int layerMask)
        {
            m_Utility.PrepareCamera(camera);
            foreach (GRKingCharacterShadowCaster caster in GRKingCharacterShadowCaster.ActiveCasters)
            {
                // 缓存 DrawCall 前完成绑定；覆盖本帧刚换材质、LateUpdate 尚未执行的情况。
                if (caster != null && caster.isActiveAndEnabled)
                    caster.UpdateShadowMaterials();
                if (caster == null || !caster.isActiveAndEnabled ||
                    !caster.CollectShadowRenderers(layerMask, m_EligibleRenderers, out Bounds bounds) ||
                    !m_Utility.TryBuildMatrices(bounds, camera, mainLight, caster.transform.up, m_Usage,
                        m_SceneShadowDistance, out Matrix4x4 view, out Matrix4x4 projection, out Vector3 direction))
                    continue;

                Vector3 toCaster = bounds.center - camera.transform.position;
                float cosAngle = Vector3.Dot(camera.transform.forward, toCaster.normalized);
                m_Candidates.Add(new Candidate
                {
                    Caster = caster, View = view, Projection = projection, LightDirection = direction,
                    Priority = Mathf.Clamp01(toCaster.sqrMagnitude / 10000f) + (1f - cosAngle) * 0.5f
                });
            }

            m_Candidates.Sort((a, b) =>
            {
                int comparison = a.Priority.CompareTo(b.Priority);
                return comparison != 0 ? comparison : a.Caster.CasterId.CompareTo(b.Caster.CasterId);
            });

            int tileSize = m_AtlasSize / m_TilesPerSide;
            int usableSize = tileSize - TileBorder * 2;
            foreach (Candidate candidate in m_Candidates)
            {
                if (m_ShadowCount >= m_TilesPerSide * m_TilesPerSide)
                    break;
                int drawStart = m_DrawCalls.Count;
                CollectDrawCalls(candidate.Caster, layerMask);
                if (m_DrawCalls.Count == drawStart)
                    continue;

                int tile = m_ShadowCount++;
                m_CasterIds[tile] = candidate.Caster.ShadowCasterId;
                Rect viewport = new(tile % m_TilesPerSide * tileSize + TileBorder,
                    tile / m_TilesPerSide * tileSize + TileBorder, usableSize, usableSize);
                m_Slices[tile] = new Slice
                {
                    Caster = candidate.Caster, View = candidate.View, Projection = candidate.Projection,
                    LightDirection = candidate.LightDirection, Viewport = viewport,
                    DrawStart = drawStart, DrawEnd = m_DrawCalls.Count
                };
                m_Matrices[tile] = GRKingShadowUtility.GetWorldToShadowMatrix(candidate.View, candidate.Projection,
                    viewport, m_AtlasSize, SystemInfo.usesReversedZBuffer);
                // xy 最小值，zw 最大值；半 texel 的内缩用于后续采样边界判断。
                m_Rects[tile] = new Vector4(viewport.xMin + 0.5f, viewport.yMin + 0.5f,
                    viewport.xMax - 0.5f, viewport.yMax - 0.5f) / m_AtlasSize;
            }
        }

        private void CollectDrawCalls(GRKingCharacterShadowCaster caster, int layerMask)
        {
            caster.CollectShadowRenderers(layerMask, m_EligibleRenderers, out _);
            foreach (Renderer renderer in m_EligibleRenderers)
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
                m_Materials.Clear();
                renderer.GetSharedMaterials(m_Materials);
                for (int submesh = 0; submesh < Mathf.Min(mesh.subMeshCount, m_Materials.Count); submesh++)
                {
                    Material material = m_Materials[submesh];
                    int pass = GRKingShadowUtility.FindShadowCasterPass(material);
                    if (pass >= 0)
                        m_DrawCalls.Add(new DrawCall
                        {
                            Renderer = renderer, Material = material, Submesh = submesh, Pass = pass
                        });
                }
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            try
            {
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // 关闭、没有有效角色或不支持主光阴影时，同样覆盖上一相机的数据。
                    cmd.SetGlobalInt(m_CountId, m_ShadowCount);
                    if (m_Usage == GRKingShadowUsage.Scene)
                        cmd.SetGlobalInt(s_MainLightCascadeCount, renderingData.shadowData.mainLightShadowCascadesCount);
                    if (m_ShadowCount > 0)
                    {
                        Vector4 previousBias = Shader.GetGlobalVector(s_ShadowBias);
                        Vector4 previousDirection = Shader.GetGlobalVector(s_LightDirection);
                        bool previousSelfKeyword = Shader.IsKeywordEnabled(SelfShadowKeyword);
                        CoreUtils.SetKeyword(cmd, SelfShadowKeyword, m_Usage == GRKingShadowUsage.Self);
                        cmd.SetGlobalDepthBias(1f, 2.5f);

                        int lightIndex = renderingData.lightData.mainLightIndex;
                        VisibleLight light = renderingData.lightData.visibleLights[lightIndex];
                        for (int tile = 0; tile < m_ShadowCount; tile++)
                        {
                            Slice slice = m_Slices[tile];
                            if (m_Usage == GRKingShadowUsage.Scene)
                            {
                                // 使用单个 Tile 的有效分辨率计算主光 Bias。
                                Vector4 bias = ShadowUtils.GetShadowBias(ref light, lightIndex,
                                    ref renderingData.shadowData, slice.Projection, slice.Viewport.width);
                                cmd.SetGlobalVector(s_ShadowBias, bias);
                            }
                            // Self 的 Bias 来自各材质；光方向来自各角色的视角混合结果。
                            cmd.SetGlobalVector(s_LightDirection, slice.LightDirection);
                            cmd.SetViewProjectionMatrices(slice.View, slice.Projection);
                            cmd.SetViewport(slice.Viewport);
                            cmd.EnableScissorRect(slice.Viewport);
                            for (int i = slice.DrawStart; i < slice.DrawEnd; i++)
                            {
                                DrawCall dc = m_DrawCalls[i];
                                cmd.DrawRenderer(dc.Renderer, dc.Material, dc.Submesh, dc.Pass);
                            }
                            cmd.DisableScissorRect();
                        }

                        cmd.SetGlobalTexture(m_AtlasId, m_Atlas.nameID);
                        cmd.SetGlobalMatrixArray(m_MatricesId, m_Matrices);
                        cmd.SetGlobalVectorArray(m_RectsId, m_Rects);
                        if (m_Usage == GRKingShadowUsage.Self)
                            cmd.SetGlobalFloatArray(s_SelfCasterIds, m_CasterIds);
                        cmd.SetGlobalVector(m_TexelSizeId, new Vector4(
                            1f / m_AtlasSize, 1f / m_AtlasSize, m_AtlasSize, m_AtlasSize));

                        cmd.SetGlobalDepthBias(0f, 0f);
                        cmd.SetGlobalVector(s_ShadowBias, previousBias);
                        cmd.SetGlobalVector(s_LightDirection, previousDirection);
                        CoreUtils.SetKeyword(cmd, SelfShadowKeyword, previousSelfKeyword);
                        CameraData cameraData = renderingData.cameraData;
                        cmd.SetViewProjectionMatrices(cameraData.GetViewMatrix(), cameraData.GetProjectionMatrix());
                        cmd.SetViewport(new Rect(0f, 0f, cameraData.cameraTargetDescriptor.width,
                            cameraData.cameraTargetDescriptor.height));
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
            Shader.SetGlobalInt(m_CountId, 0);
            m_Atlas?.Release();
            m_Atlas = null;
            m_ShadowCount = 0;
            m_Candidates.Clear();
            m_DrawCalls.Clear();
            m_EligibleRenderers.Clear();
            m_Materials.Clear();
            Array.Clear(m_Slices, 0, m_Slices.Length);
        }
    }
}

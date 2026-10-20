// Self-shadow direction and shadow-volume/frustum intersection adapted from StarRailNPRShader.
// Copyright (C) 2023 Stalo <stalowork@163.com>
// https://github.com/stalomeow/StarRailNPRShader
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;
using UnityEngine.Rendering;

namespace GRKingRP.PerObjectShadow
{
    public enum GRKingShadowUsage
    {
        Scene,
        Self
    }

    /// <summary>角色阴影方向、正交投影及阴影体积与当前相机视锥的相交测试。</summary>
    public sealed class GRKingShadowUtility
    {
        private static readonly ShaderTagId s_LightMode = new("LightMode");
        private static readonly ShaderTagId s_ShadowCaster = new("GRKingCharacterShadowCaster");
        private static readonly int[] s_FrustumTriangles =
        {
            0, 3, 1, 1, 3, 2, 2, 3, 7, 2, 7, 6,
            0, 5, 4, 0, 1, 5, 1, 2, 5, 2, 6, 5,
            0, 7, 3, 0, 4, 7, 4, 7, 5, 5, 7, 6
        };

        private readonly Plane[] m_CameraPlanes = new Plane[6];
        private readonly Vector3[] m_CameraCorners = new Vector3[4];
        private readonly Vector3[] m_FrustumCorners = new Vector3[8];
        private readonly Vector3[] m_LightFrustumCorners = new Vector3[8];
        private readonly Vector3[] m_PolygonA = new Vector3[8];
        private readonly Vector3[] m_PolygonB = new Vector3[8];

        //准备当前相机的视锥数据，供后面的阴影剔除和投影范围计算使用
        //m_CameraPlanes：视锥的 6 个裁剪平面
        //m_FrustumCorners：视锥的 8 个世界空间顶点
        public void PrepareCamera(Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix, m_CameraPlanes);
            for (int plane = 0; plane < 2; plane++)
            {
                float distance = plane == 0 ? camera.nearClipPlane : camera.farClipPlane;
                if (camera.orthographic)
                {
                    float top = camera.orthographicSize;
                    float right = top * camera.aspect;
                    m_CameraCorners[0] = new Vector3(-right, -top, distance);
                    m_CameraCorners[1] = new Vector3(-right, top, distance);
                    m_CameraCorners[2] = new Vector3(right, top, distance);
                    m_CameraCorners[3] = new Vector3(right, -top, distance);
                }
                else
                {
                    camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), distance,
                        Camera.MonoOrStereoscopicEye.Mono, m_CameraCorners);
                }
                for (int i = 0; i < 4; i++)
                    m_FrustumCorners[plane * 4 + i] = camera.transform.TransformPoint(m_CameraCorners[i]);
            }
        }

        //为一个角色构建阴影绘制用的观察矩阵和正交投影矩阵，同时判断该阴影是否需要渲染
        public bool TryBuildMatrices(Bounds bounds, Camera camera, Transform mainLight,
            Vector3 casterUp, GRKingShadowUsage usage, float sceneShadowDistance,
            out Matrix4x4 view, out Matrix4x4 projection, out Vector3 lightDirection)
        {
            //初始化与初步剔除
            view = projection = Matrix4x4.identity;
            lightDirection = Vector3.zero;
            if (!IsValidBounds(bounds))
                return false;

            // 自阴影只服务于当前相机能看到的角色；不使用含其他相机状态的 Renderer.isVisible。
            if (usage == GRKingShadowUsage.Self &&
                !GeometryUtility.TestPlanesAABB(m_CameraPlanes, bounds))
                return false;

            //确定阴影相机方向
            Quaternion rotation;
            if (usage == GRKingShadowUsage.Scene)
            {
                rotation = mainLight.rotation;
                lightDirection = -mainLight.forward;
            }
            else
            {
                Vector3 up = casterUp.sqrMagnitude > 0.000001f ? casterUp.normalized : Vector3.up;
                Vector3 viewForward = bounds.center - camera.transform.position;
                viewForward = viewForward.sqrMagnitude > 0.000001f
                    ? viewForward.normalized : camera.transform.forward;
                // 美术化自阴影：80% 观察方向，20% 主光方向。
                Vector3 forward = Vector3.Lerp(viewForward, mainLight.forward, 0.2f).normalized;
                float cosAngle = Vector3.Dot(forward, up);
                float clampedCosAngle = Mathf.Clamp(cosAngle, -0.866f, 0f);
                forward += (clampedCosAngle - cosAngle) * up;
                if (forward.sqrMagnitude < 0.000001f)
                    forward = Vector3.ProjectOnPlane(mainLight.forward, up);
                if (forward.sqrMagnitude < 0.000001f)
                    forward = Vector3.Cross(up, Mathf.Abs(up.z) < 0.99f ? Vector3.forward : Vector3.right);
                forward.Normalize();

                Vector3 cameraUp = camera.transform.up;
                if (Mathf.Abs(Vector3.Dot(forward, cameraUp)) > 0.99f)
                    cameraUp = Mathf.Abs(forward.y) < 0.99f ? Vector3.up : Vector3.forward;
                rotation = Quaternion.LookRotation(forward, cameraUp);
                lightDirection = -forward;
            }

            // Unity 观察空间朝向 -Z；以角色中心构建观察矩阵。
            view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
                   Matrix4x4.TRS(bounds.center, rotation, Vector3.one).inverse;
            Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f));
                Vector3 point = view.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            // Scene 需要包含角色后方的接收点，而不能只包住角色本身。
            if (usage == GRKingShadowUsage.Scene)
                min.z = Mathf.Min(min.z, max.z - Mathf.Max(0.01f, sceneShadowDistance));

            if (!IntersectShadowVolume(view, min, max, out float frustumMinZ))
                return false;

            // 保留靠近光源的一端；仅裁掉当前视锥之外的远端深度范围。
            min.z = Mathf.Max(min.z, frustumMinZ);
            if (max.z - min.z < 0.0001f || max.x - min.x < 0.0001f || max.y - min.y < 0.0001f)
                return false;

            // 分别贴合宽和高，不使用原来会浪费横向精度的方形世界空间投影。
            projection = Matrix4x4.Ortho(min.x, max.x, min.y, max.y, -max.z, -min.z);
            return true;
        }

        private bool IntersectShadowVolume(Matrix4x4 view, Vector3 shadowMin, Vector3 shadowMax,
            out float frustumMinZ)
        {
            //转换到同一个空间
            for (int i = 0; i < 8; i++)
                m_LightFrustumCorners[i] = view.MultiplyPoint3x4(m_FrustumCorners[i]);

            frustumMinZ = float.PositiveInfinity;
            float frustumMaxZ = float.NegativeInfinity;
            // 将视锥的 12 个三角形裁到阴影体积的 XY 矩形内，再比较重叠深度范围。
            for (int triangle = 0; triangle < s_FrustumTriangles.Length; triangle += 3)
            {
                for (int vertex = 0; vertex < 3; vertex++)
                    m_PolygonA[vertex] = m_LightFrustumCorners[s_FrustumTriangles[triangle + vertex]];
                int count = ClipPolygon(m_PolygonA, 3, m_PolygonB, 0, shadowMin.x, true);
                count = ClipPolygon(m_PolygonB, count, m_PolygonA, 0, shadowMax.x, false);
                count = ClipPolygon(m_PolygonA, count, m_PolygonB, 1, shadowMin.y, true);
                count = ClipPolygon(m_PolygonB, count, m_PolygonA, 1, shadowMax.y, false);
                for (int i = 0; i < count; i++)
                {
                    frustumMinZ = Mathf.Min(frustumMinZ, m_PolygonA[i].z);
                    frustumMaxZ = Mathf.Max(frustumMaxZ, m_PolygonA[i].z);
                }
            }
            return frustumMinZ < shadowMax.z && frustumMaxZ > shadowMin.z;
        }

        private static int ClipPolygon(Vector3[] input, int count, Vector3[] output,
            int axis, float edge, bool keepGreater)
        {
            if (count == 0)
                return 0;

            int outputCount = 0;
            Vector3 previous = input[count - 1];
            bool previousInside = keepGreater ? previous[axis] >= edge : previous[axis] <= edge;
            for (int i = 0; i < count; i++)
            {
                Vector3 current = input[i];
                bool currentInside = keepGreater ? current[axis] >= edge : current[axis] <= edge;
                if (previousInside != currentInside)
                {
                    float t = (edge - previous[axis]) / (current[axis] - previous[axis]);
                    output[outputCount++] = Vector3.LerpUnclamped(previous, current, t);
                }
                if (currentInside)
                    output[outputCount++] = current;
                previous = current;
                previousInside = currentInside;
            }
            return outputCount;
        }

        public static bool IsValidBounds(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            return IsFinite(center.x) && IsFinite(center.y) && IsFinite(center.z) &&
                   IsFinite(size.x) && IsFinite(size.y) && IsFinite(size.z) &&
                   size.x >= 0f && size.y >= 0f && size.z >= 0f && size.sqrMagnitude > 0.000001f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static int FindShadowCasterPass(Material material)
        {
            // 启用标记按 LightMode 查询；FindPass 按 Pass Name 查询。
            if (material == null || !material.GetShaderPassEnabled("GRKingCharacterShadowCaster"))
                return -1;
            int pass = material.FindPass("CharacterShadowCaster");
            return pass >= 0 && material.shader.FindPassTagValue(pass, s_LightMode) == s_ShadowCaster
                ? pass : -1;
        }

        public static Matrix4x4 GetWorldToShadowMatrix(Matrix4x4 view, Matrix4x4 projection,
            Rect viewport, int atlasSize, bool reversedZ)
        {
            if (reversedZ)
            {
                projection.m20 = -projection.m20;
                projection.m21 = -projection.m21;
                projection.m22 = -projection.m22;
                projection.m23 = -projection.m23;
            }
            Matrix4x4 scaleBias = Matrix4x4.identity;
            scaleBias.m00 = 0.5f * viewport.width / atlasSize;
            scaleBias.m11 = 0.5f * viewport.height / atlasSize;
            scaleBias.m22 = 0.5f;
            scaleBias.m03 = viewport.center.x / atlasSize;
            scaleBias.m13 = viewport.center.y / atlasSize;
            scaleBias.m23 = 0.5f;
            return scaleBias * projection * view;
        }
    }
}

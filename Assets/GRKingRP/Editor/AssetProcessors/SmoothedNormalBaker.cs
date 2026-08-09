using System.Collections.Generic;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    /// <summary>
    /// Deterministically calculates angle-weighted smooth normals for imported meshes.
    /// The implementation is owned by this project and has no package dependency.
    /// </summary>
    internal static class SmoothedNormalBaker
    {
        internal static void Bake(
            GameObject modelRoot,
            SmoothedNormalStorageTarget storageTarget,
            string assetPath,
            bool verboseLogging)
        {
            //收集Meshs
            List<Mesh> meshes = CollectMeshes(modelRoot);

            if (verboseLogging)
            {
                Debug.Log(
                    $"[GRKingRP Model Import] Baking smoothed normals to {storageTarget} " +
                    $"for {meshes.Count} meshes in {assetPath}.");
            }

            for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
            {
                Mesh mesh = meshes[meshIndex];
                if (BakeMesh(mesh, storageTarget))
                {
                    mesh.MarkModified();
                    if (verboseLogging)
                    {
                        Debug.Log(
                            $"[GRKingRP Model Import] ({meshIndex + 1}/{meshes.Count}) " +
                            $"Saved smoothed normals to {storageTarget}: {mesh.name}");
                    }
                }
            }
        }

        //收集Meshs
        private static List<Mesh> CollectMeshes(GameObject modelRoot)
        {
            List<Mesh> meshes = new();
            HashSet<Mesh> uniqueMeshes = new();

            if (modelRoot == null)
            {
                return meshes;
            }

            foreach (MeshFilter meshFilter in modelRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                AddMesh(meshFilter.sharedMesh, uniqueMeshes, meshes);
            }

            foreach (SkinnedMeshRenderer renderer
                     in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                AddMesh(renderer.sharedMesh, uniqueMeshes, meshes);
            }

            return meshes;
        }

        private static void AddMesh(
            Mesh mesh,
            HashSet<Mesh> uniqueMeshes,
            List<Mesh> meshes)
        {
            if (mesh != null && uniqueMeshes.Add(mesh))
            {
                meshes.Add(mesh);
            }
        }

        //给Mesh烘焙平滑法线
        private static bool BakeMesh(
            Mesh mesh,
            SmoothedNormalStorageTarget storageTarget)
        {
            int vertexCount = mesh.vertexCount;
            if (vertexCount == 0)
            {
                return false;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;

            if (normals.Length != vertexCount)
            {
                mesh.RecalculateNormals();
                normals = mesh.normals;
            }

            if (normals.Length != vertexCount)
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] Cannot bake {mesh.name}: invalid normals.");
                return false;
            }

            //使用原始法线初始化结果
            Vector3[] smoothedNormalsOS = new Vector3[vertexCount];
            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                smoothedNormalsOS[vertexIndex] = NormalizeSafe(normals[vertexIndex]);
            }

            //法线累计字典，顶点位置 → 该位置所有三角形贡献的加权法线总和
            Dictionary<Vector3, Vector3> weightedNormalSums = new();

            //遍历所有 SubMesh
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                if (mesh.GetTopology(subMeshIndex) != MeshTopology.Triangles)
                {
                    continue;
                }

                int[] indices = mesh.GetIndices(subMeshIndex);
                //计算每个三角形对三个顶点的法线贡献
                CollectWeightedNormalSums(
                    indices,
                    vertices,
                    weightedNormalSums);
            }

            //归一化每个顶点累加的法线，存储在smoothedNormalsOS里面
            ApplySmoothedNormals(
                vertices,
                weightedNormalSums,
                smoothedNormalsOS);

            //保存到指定顶点数据
            return SaveToVertexData(mesh, normals, smoothedNormalsOS, storageTarget);
        }

        private static void CollectWeightedNormalSums(
            int[] indices,
            Vector3[] vertices,
            Dictionary<Vector3, Vector3> weightedNormalSums)
        {
            // A fixed index-buffer traversal order keeps the floating-point result
            // identical across repeated imports.
            //顶点贡献 = 三角形面法线 × 当前顶点夹角
            for (int triangleStart = 0; triangleStart + 2 < indices.Length; triangleStart += 3)
            {
                Vector3 p1 = vertices[indices[triangleStart]];
                Vector3 p2 = vertices[indices[triangleStart + 1]];
                Vector3 p3 = vertices[indices[triangleStart + 2]];

                //分别处理三角形的三个角
                for (int cornerIndex = 0; cornerIndex < 3; cornerIndex++)
                {
                    int vertexIndex = indices[triangleStart + cornerIndex];
                    Vector3 position = vertices[vertexIndex];

                    //计算面法线和当前顶点夹角
                    CalculateWeightedNormal(
                        p1,
                        p2,
                        p3,
                        cornerIndex,
                        out Vector3 faceNormal,
                        out float cornerAngle);

                    //使用夹角作为权重，然后累加记录到字典里面
                    Vector3 angleWeightedNormal = faceNormal * cornerAngle;
                    if (weightedNormalSums.TryGetValue(position, out Vector3 currentSum))
                    {
                        weightedNormalSums[position] = currentSum + angleWeightedNormal;
                    }
                    else
                    {
                        weightedNormalSums.Add(position, angleWeightedNormal);
                    }
                }
            }
        }

        private static void ApplySmoothedNormals(
            Vector3[] vertices,
            Dictionary<Vector3, Vector3> weightedNormalSums,
            Vector3[] smoothedNormalsOS)
        {
            for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            {
                Vector3 position = vertices[vertexIndex];

                if (weightedNormalSums.TryGetValue(position, out Vector3 smoothedNormal))
                {
                    smoothedNormalsOS[vertexIndex] = NormalizeSafe(smoothedNormal);
                }
            }
        }

        private static bool SaveToVertexData(
            Mesh mesh,
            Vector3[] normalsOS,
            Vector3[] smoothedNormalsOS,
            SmoothedNormalStorageTarget storageTarget)
        {
            if (storageTarget == SmoothedNormalStorageTarget.Tangent)
            {
                SaveToTangents(mesh, smoothedNormalsOS);
                return true;
            }

            //存储原始切线
            Vector4[] originalTangents = mesh.tangents;
            if (originalTangents.Length != mesh.vertexCount)
            {
                mesh.RecalculateTangents();
                originalTangents = mesh.tangents;
            }

            if (originalTangents.Length != mesh.vertexCount)
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] Cannot save tangent-space smoothed normals " +
                    $"for {mesh.name}: valid tangents are required.");
                return false;
            }

            //转换到切线空间平滑法线
            Vector3[] smoothedNormalsTS = ConvertToTangentSpace(
                normalsOS,
                originalTangents,
                smoothedNormalsOS);

            switch (storageTarget)
            {
                case SmoothedNormalStorageTarget.VertexColor:
                    SaveToVertexColors(mesh, smoothedNormalsTS);
                    return true;
                case SmoothedNormalStorageTarget.UV2:
                    SaveToUV(mesh, 1, smoothedNormalsTS);
                    return true;
                case SmoothedNormalStorageTarget.UV3:
                    SaveToUV(mesh, 2, smoothedNormalsTS);
                    return true;
                case SmoothedNormalStorageTarget.UV4:
                    SaveToUV(mesh, 3, smoothedNormalsTS);
                    return true;
                default:
                    Debug.LogError(
                        $"[GRKingRP Model Import] Unsupported smoothed normal storage: " +
                        $"{storageTarget}.");
                    return false;
            }
        }

        //模型空间平滑法线存储到切线
        private static void SaveToTangents(Mesh mesh, Vector3[] smoothedNormalsOS)
        {
            Vector4[] originalTangents = mesh.tangents;
            Vector4[] outputTangents = new Vector4[mesh.vertexCount];

            for (int vertexIndex = 0; vertexIndex < mesh.vertexCount; vertexIndex++)
            {
                Vector3 smoothedNormalOS = smoothedNormalsOS[vertexIndex];
                float originalHandedness = originalTangents.Length == mesh.vertexCount
                    ? originalTangents[vertexIndex].w
                    : 1.0f;

                outputTangents[vertexIndex] = new Vector4(
                    smoothedNormalOS.x,
                    smoothedNormalOS.y,
                    smoothedNormalOS.z,
                    originalHandedness);
            }

            mesh.tangents = outputTangents;
        }

        //切线空间平滑法线保存到顶点色，值从[-1,1]映射到[0,1]
        private static void SaveToVertexColors(Mesh mesh, Vector3[] smoothedNormalsTS)
        {
            Color[] colors = mesh.colors;
            if (colors.Length != mesh.vertexCount)
            {
                colors = new Color[mesh.vertexCount];
                for (int vertexIndex = 0; vertexIndex < colors.Length; vertexIndex++)
                {
                    colors[vertexIndex] = Color.white;
                }
            }

            for (int vertexIndex = 0; vertexIndex < mesh.vertexCount; vertexIndex++)
            {
                Vector3 smoothedNormalTS = smoothedNormalsTS[vertexIndex];
                Color color = colors[vertexIndex];
                color.r = smoothedNormalTS.x * 0.5f + 0.5f;
                color.g = smoothedNormalTS.y * 0.5f + 0.5f;
                color.b = smoothedNormalTS.z * 0.5f + 0.5f;
                colors[vertexIndex] = color;
            }

            mesh.colors = colors;
        }

        //切线空间平滑法线保存到uv
        private static void SaveToUV(Mesh mesh, int channel, Vector3[] smoothedNormalsTS)
        {
            List<Vector3> uvData = new(smoothedNormalsTS);
            mesh.SetUVs(channel, uvData);
        }

        //模型空间平滑法线转换到切线空间
        private static Vector3[] ConvertToTangentSpace(
            Vector3[] normalsOS,
            Vector4[] tangentsOS,
            Vector3[] smoothedNormalsOS)
        {
            Vector3[] smoothedNormalsTS = new Vector3[smoothedNormalsOS.Length];

            for (int vertexIndex = 0; vertexIndex < smoothedNormalsOS.Length; vertexIndex++)
            {
                Vector3 normalOS = NormalizeSafe(normalsOS[vertexIndex]);
                Vector4 tangent = tangentsOS[vertexIndex];
                Vector3 tangentOS = NormalizeSafe(
                    new Vector3(tangent.x, tangent.y, tangent.z));
                Vector3 bitangentOS = NormalizeSafe(
                    Vector3.Cross(normalOS, tangentOS) * tangent.w);
                Vector3 smoothedNormalOS = smoothedNormalsOS[vertexIndex];

                smoothedNormalsTS[vertexIndex] = NormalizeSafe(new Vector3(
                    Vector3.Dot(smoothedNormalOS, tangentOS),
                    Vector3.Dot(smoothedNormalOS, bitangentOS),
                    Vector3.Dot(smoothedNormalOS, normalOS)));
            }

            return smoothedNormalsTS;
        }

        private static void CalculateWeightedNormal(
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            int cornerIndex,
            out Vector3 faceNormal,
            out float cornerAngle)
        {
            Vector3 d1 = Vector3.zero;
            Vector3 d2 = Vector3.zero;

            switch (cornerIndex)
            {
                case 0:
                    d1 = p1 - p3;
                    d2 = p2 - p1;
                    break;
                case 1:
                    d1 = p2 - p1;
                    d2 = p3 - p2;
                    break;
                case 2:
                    d1 = p3 - p2;
                    d2 = p1 - p3;
                    break;
            }

            //计算面法线和夹角
            d1 = NormalizeSafe(d1);
            d2 = NormalizeSafe(d2);
            faceNormal = NormalizeSafe(Vector3.Cross(p1 - p3, p2 - p1));
            cornerAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d1, -d2), -1.0f, 1.0f));
        }

        private static Vector3 NormalizeSafe(Vector3 value)
        {
            float sqrMagnitude = value.sqrMagnitude;
            return sqrMagnitude > 1e-20f
                ? value / Mathf.Sqrt(sqrMagnitude)
                : Vector3.zero;
        }
    }
}

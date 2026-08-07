// Copyright (c) Jason Ma
//
// Copy and modify this file to customize where outline normals are stored.

using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace OutlineNormalSmoother
{
    internal static class SampleCustomBaker
    {
        internal static void SaveOutlineNormalToMesh(Mesh mesh, ref NativeArray<Color> bakedColors, ref NativeArray<Vector3> smoothedNormalTangentSpace)
        {
            //确保数组已经创建，并且每个顶点都有一条平滑法线
            if (!smoothedNormalTangentSpace.IsCreated || smoothedNormalTangentSpace.Length != mesh.vertexCount)
            {
                Debug.LogError(
                    $"OutlineNormalSmoother: Cannot save tangent-space smoothed normals to {mesh.name}. " +
                    $"Expected {mesh.vertexCount} values, but received " +
                    $"{(smoothedNormalTangentSpace.IsCreated ? smoothedNormalTangentSpace.Length : 0)}.");
                return;
            }

            var normals = mesh.normals;
            var originalTangents = mesh.tangents;

            if (normals.Length != mesh.vertexCount || originalTangents.Length != mesh.vertexCount)
            {
                Debug.LogError(
                    $"OutlineNormalSmoother: Cannot save smoothed normals to the tangent channel of {mesh.name}. " +
                    "The mesh does not contain valid normals or tangents.");
                return;
            }

            var smoothedNormalTangents = new Vector4[mesh.vertexCount];

            for (int i = 0; i < mesh.vertexCount; i++)
            {
                Vector3 normalOS = normals[i].normalized;
                Vector4 originalTangent = originalTangents[i];
                Vector3 tangentOS = new Vector3(
                    originalTangent.x,
                    originalTangent.y,
                    originalTangent.z
                ).normalized;
                Vector3 bitangentOS = Vector3.Cross(normalOS, tangentOS) * originalTangent.w;

                //将切线空间的平滑法线转成模型空间的平滑法线
                Vector3 smoothedNormalTS = smoothedNormalTangentSpace[i];
                Vector3 smoothedNormalOS = (
                    tangentOS * smoothedNormalTS.x +
                    bitangentOS * smoothedNormalTS.y +
                    normalOS * smoothedNormalTS.z
                ).normalized;

                smoothedNormalTangents[i] = new Vector4(
                    smoothedNormalOS.x,
                    smoothedNormalOS.y,
                    smoothedNormalOS.z,
                    originalTangent.w
                );
            }

            // Store the object-space smoothed normal in Tangent.xyz.
            // Shader declaration: float4 smoothedNormalOS : TANGENT;
            //平滑法线存储到顶点的切线
            mesh.tangents = smoothedNormalTangents;
        }

        [InitializeOnLoadMethod]
        internal static void RegisterEvent()
        {
            /* >>>>>>>>>>>>>>>>> Uncomment to register the event <<<<<<<<<<<<<<<<< */
            OutlineNormalBacker.onSaveToMesh = SaveOutlineNormalToMesh;
        }
    }
}

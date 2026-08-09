using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    /// <summary>
    /// Isolates the Unity 2022.3 editor internals required to discover source
    /// material slots while Material Creation Mode is set to None.
    /// Re-test this class when upgrading Unity.
    /// </summary>
    internal static class ModelImporterCompatibility
    {
        private const string SourceMaterialsPropertyName = "sourceMaterials";
        private const string RemapWhenImportModeIsNonePropertyName =
            "m_RemapMaterialsIfMaterialImportModeIsNone";

        private static readonly PropertyInfo SourceMaterialsProperty =
            typeof(ModelImporter).GetProperty(
                SourceMaterialsPropertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool TryGetSourceMaterials(
            ModelImporter modelImporter,
            out AssetImporter.SourceAssetIdentifier[] sourceMaterials)
        {
            sourceMaterials = Array.Empty<AssetImporter.SourceAssetIdentifier>();

            if (SourceMaterialsProperty == null)
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] ModelImporter.{SourceMaterialsPropertyName} " +
                    "is unavailable in this Unity version.",
                    modelImporter);
                return false;
            }

            try
            {
                sourceMaterials = SourceMaterialsProperty.GetValue(modelImporter)
                                      as AssetImporter.SourceAssetIdentifier[] ??
                                  Array.Empty<AssetImporter.SourceAssetIdentifier>();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, modelImporter);
                return false;
            }
        }

        internal static bool TryEnableMaterialRemappingWhenImportModeIsNone(
            ModelImporter modelImporter,
            out bool changed)
        {
            changed = false;
            SerializedObject serializedImporter = new(modelImporter);
            SerializedProperty remapProperty = serializedImporter.FindProperty(
                RemapWhenImportModeIsNonePropertyName);

            if (remapProperty == null)
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] Cannot find " +
                    $"{RemapWhenImportModeIsNonePropertyName}.",
                    modelImporter);
                return false;
            }

            if (remapProperty.boolValue)
            {
                return true;
            }

            remapProperty.boolValue = true;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
            return true;
        }
    }
}

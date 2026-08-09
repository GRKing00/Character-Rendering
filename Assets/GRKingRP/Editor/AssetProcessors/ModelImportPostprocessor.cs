using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    internal sealed class ModelImportPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 7u;

        private void OnPreprocessModel()
        {
            if (assetImporter is not ModelImporter modelImporter)
            {
                return;
            }

            if (TryGetMatchingRule(out _, out ModelImportRule rule))
            {
                ApplyPreset(modelImporter, rule);
            }
        }

        private void OnPostprocessModel(GameObject modelRoot)
        {
            if (!TryGetMatchingRule(
                    out ModelImportSettings settings,
                    out ModelImportRule rule) ||
                !rule.AutoBakeSmoothedNormals)
            {
                return;
            }

            //烘焙平滑法线
            SmoothedNormalBaker.Bake(
                modelRoot,
                rule.SmoothedNormalStorage,
                assetPath,
                settings.VerboseLogging);
        }

        private bool TryGetMatchingRule(
            out ModelImportSettings settings,
            out ModelImportRule matchedRule)
        {
            settings = null;
            matchedRule = null;

            settings = AssetProcessorGlobalSettings.LoadModelSettings();
            if (settings == null)
            {
                context.DependsOnArtifact(
                    AssetProcessorGlobalSettings.ModelSettingsAssetPath);
                return false;
            }

            //是否在指定目录下
            if (!AssetProcessorUtility.IsAssetPathUnderFolder(
                    assetPath,
                    settings.ModelImportRootFolder,
                    out string normalizedAssetPath))
            {
                return false;
            }

            context.DependsOnArtifact(
                AssetProcessorGlobalSettings.ModelSettingsAssetPath);

            if (settings.Rules == null)
            {
                return false;
            }

            string modelName = Path.GetFileNameWithoutExtension(normalizedAssetPath);

            //遍历规则，如果有适配的规则，则使用对应的预设
            foreach (ModelImportRule rule in settings.Rules)
            {
                if (rule != null && rule.TryMatch(modelName))
                {
                    matchedRule = rule;
                    return true;
                }
            }

            return false;
        }

        private void ApplyPreset(ModelImporter modelImporter, ModelImportRule rule)
        {
            Preset presetReference = rule.Preset;
            if (presetReference == null)
            {
                Debug.LogWarning(
                    $"[GRKingRP Model Import] Rule '{rule.NameGlob}' has no Preset " +
                    $"assigned for {assetPath}.",
                    modelImporter);
                return;
            }

            string presetPath = AssetDatabase.GetAssetPath(presetReference);
            if (string.IsNullOrEmpty(presetPath))
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] Rule '{rule.NameGlob}' references an " +
                    $"invalid Preset for {assetPath}.",
                    modelImporter);
                return;
            }

            // Load through AssetDatabase during this import so Unity can track that
            // the Preset artifact is actually consumed by the importer.
            Preset preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);
            if (preset == null)
            {
                // The Preset may still be importing. Registering the missing artifact
                // makes Unity retry this model when that artifact becomes available.
                context.DependsOnArtifact(presetPath);
                return;
            }

            if (!preset.CanBeAppliedTo(modelImporter))
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] {presetPath} is not a ModelImporter Preset.",
                    preset);
                return;
            }

            //存储外部材质映射
            Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> externalObjectMap =
                modelImporter.GetExternalObjectMap();

            if (preset.ApplyTo(modelImporter))
            {
                RestoreExternalObjectMap(modelImporter, externalObjectMap);

                // Presets are native assets, so their imported artifact is the
                // dependency that must invalidate this model.
                context.DependsOnArtifact(presetPath);
            }
        }

        //恢复外部材质映射
        private static void RestoreExternalObjectMap(
            ModelImporter modelImporter,
            Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> externalObjectMap)
        {
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> remap
                     in externalObjectMap)
            {
                if (remap.Value != null)
                {
                    modelImporter.AddRemap(remap.Key, remap.Value);
                }
            }
        }

    }
}

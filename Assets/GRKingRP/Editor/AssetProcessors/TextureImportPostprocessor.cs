using System.IO;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    internal sealed class TextureImportPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 7u;

        private void OnPreprocessTexture()
        {
            TextureImportSettings settings =
                AssetProcessorGlobalSettings.LoadTextureSettings();
            if (settings == null)
            {
                context.DependsOnArtifact(
                    AssetProcessorGlobalSettings.TextureSettingsAssetPath);
                return;
            }

            if (assetImporter is not TextureImporter textureImporter)
            {
                return;
            }

            //是否在指定目录下
            if (!AssetProcessorUtility.IsAssetPathUnderFolder(
                    assetPath,
                    settings.TextureImportRootFolder,
                    out string normalizedAssetPath))
            {
                return;
            }

            context.DependsOnArtifact(
                AssetProcessorGlobalSettings.TextureSettingsAssetPath);

            if (settings.Rules == null)
            {
                return;
            }

            string textureName = Path.GetFileNameWithoutExtension(normalizedAssetPath);

            //遍历规则，如果有适配的规则，则应用预设
            foreach (TextureImportRule rule in settings.Rules)
            {
                if (rule == null || !rule.TryMatch(textureName))
                {
                    continue;
                }

                ApplyPreset(textureImporter, rule);
                break;
            }
        }

        //应用预设
        private void ApplyPreset(TextureImporter textureImporter, TextureImportRule rule)
        {
            Preset presetReference = rule.Preset;
            if (presetReference == null)
            {
                Debug.LogWarning(
                    $"[GRKingRP Texture Import] Rule '{rule.NameGlob}' has no Preset " +
                    $"assigned for {assetPath}.",
                    textureImporter);
                return;
            }

            string presetPath = AssetDatabase.GetAssetPath(presetReference);
            if (string.IsNullOrEmpty(presetPath))
            {
                Debug.LogError(
                    $"[GRKingRP Texture Import] Rule '{rule.NameGlob}' references an " +
                    $"invalid Preset for {assetPath}.",
                    textureImporter);
                return;
            }

            // Load through AssetDatabase during this import so Unity can track that
            // the Preset artifact is actually consumed by the importer.
            Preset preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);
            if (preset == null)
            {
                // The Preset may still be importing. Registering the missing artifact
                // makes Unity retry this texture when that artifact becomes available.
                context.DependsOnArtifact(presetPath);
                return;
            }

            if (!preset.CanBeAppliedTo(textureImporter))
            {
                Debug.LogError(
                    $"[GRKingRP Texture Import] {presetPath} is not a TextureImporter Preset.",
                    preset);
                return;
            }

            if (preset.ApplyTo(textureImporter))
            {
                // Presets are native assets, so their imported artifact is the
                // dependency that must invalidate this texture.
                context.DependsOnArtifact(presetPath);
            }
        }

    }
}

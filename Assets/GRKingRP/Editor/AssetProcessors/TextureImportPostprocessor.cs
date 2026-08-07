using System;
using System.IO;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    internal sealed class TextureImportPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 4u;

        private void OnPreprocessTexture()
        {
            string normalizedAssetPath = assetPath.Replace('\\', '/');
            
            //判断是否在指定目录下
            if (!normalizedAssetPath.StartsWith(
                    AssetProcessorGlobalSettings.TextureImportRootFolder,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            //判断是否为TextureImporter，获取textureImporter
            if (assetImporter is not TextureImporter textureImporter)
            {
                return;
            }

            string textureName = Path.GetFileNameWithoutExtension(normalizedAssetPath);

            //遍历纹理导入规则，判断当前纹理的name是否有匹配的规则
            foreach (TextureImportRule rule in AssetProcessorGlobalSettings.TextureImportRules)
            {
                if (!rule.TryMatch(textureName))
                {
                    continue;
                }

                //应用preset到textureImporter
                ApplyPreset(textureImporter, rule);
                break;
            }
        }

        private void ApplyPreset(TextureImporter textureImporter, TextureImportRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.PresetPath))
            {
                Debug.LogWarning(
                    $"[GRKingRP Texture Import] Rule '{rule.NameGlob}' has no Preset assigned for {assetPath}.",
                    textureImporter);
                return;
            }

            //获取预设
            Preset preset = AssetDatabase.LoadAssetAtPath<Preset>(rule.PresetPath);

            if (preset == null)
            {
                // The Preset can still be importing during the same AssetDatabase refresh.
                context.DependsOnArtifact(rule.PresetPath);
                // Debug.LogWarning(
                //     $"[GRKingRP Texture Import] Preset not found: {rule.PresetPath}. " +
                //     $"It will be retried when the Preset artifact becomes available.",
                //     textureImporter);
                return;
            }

            //不能应用预设
            if (!preset.CanBeAppliedTo(textureImporter))
            {
                // Keep tracking the path so replacing the incompatible Preset retries the texture.
                context.DependsOnArtifact(rule.PresetPath);
                Debug.LogError(
                    $"[GRKingRP Texture Import] {rule.PresetPath} is not a TextureImporter Preset.",
                    preset);
                return;
            }

            if (preset.ApplyTo(textureImporter))
            {
                // Presets are native assets, so this must use the imported artifact dependency.
                context.DependsOnArtifact(rule.PresetPath);//建立当前纹理和preset直接的依赖
            }
        }
    }
}

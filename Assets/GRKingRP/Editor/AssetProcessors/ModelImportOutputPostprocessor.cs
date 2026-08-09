using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    internal sealed class ModelImportOutputPostprocessor : AssetPostprocessor
    {
        //存储模型路径
        private static readonly HashSet<string> PendingModelPaths =
            new(StringComparer.OrdinalIgnoreCase);

        private static bool isOutputProcessingScheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            //存储导入的模型和移动的模型中匹配规则的模型路径
            QueueModelOutputs(importedAssets);
            QueueModelOutputs(movedAssets);
            ScheduleOutputProcessing();
        }

        private static void QueueModelOutputs(IEnumerable<string> assetPaths)
        {
            foreach (string assetPath in assetPaths)
            {
                string normalizedAssetPath = assetPath.Replace('\\', '/');

                // Rules are name-based and can be made deliberately broad in the
                // settings asset, so verify the imported asset type as well.
                if (AssetImporter.GetAtPath(normalizedAssetPath) is not ModelImporter)
                {
                    continue;
                }

                if (!TryGetMatchingRule(
                        normalizedAssetPath,
                        out _,
                        out ModelImportRule rule) ||
                    (!rule.AutoCreatePrefab && !rule.AutoCreateMaterials))
                {
                    continue;
                }

                PendingModelPaths.Add(normalizedAssetPath);
            }
        }

        private static bool TryGetMatchingRule(
            string normalizedAssetPath,
            out ModelImportSettings settings,
            out ModelImportRule matchedRule)
        {
            settings = AssetProcessorGlobalSettings.LoadModelSettings();
            matchedRule = null;

            if (settings == null || settings.Rules == null)
            {
                return false;
            }

            if (!AssetProcessorUtility.IsAssetPathUnderFolder(
                    normalizedAssetPath,
                    settings.ModelImportRootFolder,
                    out _))
            {
                return false;
            }

            string modelName = Path.GetFileNameWithoutExtension(normalizedAssetPath);

            foreach (ModelImportRule rule in settings.Rules)
            {
                if (rule == null || !rule.TryMatch(modelName))
                {
                    continue;
                }

                matchedRule = rule;
                return true;
            }

            return false;
        }

        private static void ScheduleOutputProcessing()
        {
            if (PendingModelPaths.Count == 0 || isOutputProcessingScheduled)
            {
                return;
            }

            isOutputProcessingScheduled = true;
            //把一个函数推迟到 Unity Editor 下一次安全的更新时机执行一次
            EditorApplication.delayCall += ProcessPendingModelOutputs;
        }

        //为了匹配的模型创建材质和prefab
        private static void ProcessPendingModelOutputs()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ProcessPendingModelOutputs;
                return;
            }

            isOutputProcessingScheduled = false;

            string[] modelPaths = new string[PendingModelPaths.Count];
            PendingModelPaths.CopyTo(modelPaths);
            PendingModelPaths.Clear();

            //遍历模型
            foreach (string modelPath in modelPaths)
            {
                if (!TryGetMatchingRule(
                        modelPath,
                        out ModelImportSettings settings,
                        out ModelImportRule rule))
                {
                    continue;
                }

                //创建材质并重映射材质和模型的材质槽
                if (rule.AutoCreateMaterials)
                {
                    CreateAndRemapMaterials(modelPath, settings, rule);
                }

                //创建prefab
                if (rule.AutoCreatePrefab)
                {
                    CreatePrefabIfMissing(modelPath, settings);
                }
            }

            ScheduleOutputProcessing();
        }

        private static void CreateAndRemapMaterials(
            string modelPath,
            ModelImportSettings settings,
            ModelImportRule rule)
        {
            //获取模型的 ModelImporter
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter modelImporter)
            {
                return;
            }

            //确认模型的 Material Creation Mode
            if (modelImporter.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                Debug.LogWarning(
                    $"[GRKingRP Model Import] Automatic material creation expects " +
                    $"Material Creation Mode = None: {modelPath}",
                    modelImporter);
                return;
            }

            //读取 FBX 内部材质槽
            if (!ModelImporterCompatibility.TryGetSourceMaterials(
                    modelImporter,
                    out AssetImporter.SourceAssetIdentifier[] sourceMaterials))
            {
                return;
            }

            if (sourceMaterials.Length == 0)
            {
                Debug.LogWarning(
                    $"[GRKingRP Model Import] No source material slots were found in: {modelPath}",
                    modelImporter);
                return;
            }

            //根据模型路径计算材质输出目录
            string materialFolder = GetMaterialFolderPath(modelPath, settings);
            if (string.IsNullOrEmpty(materialFolder))
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] Material output root is not configured: {modelPath}",
                    modelImporter);
                return;
            }

            if (!AssetProcessorUtility.EnsureAssetFolder(materialFolder))
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] Cannot create material folder: {materialFolder}");
                return;
            }

            //获取材质模板
            Material materialTemplate = rule.MaterialTemplate;
            Shader fallbackShader = materialTemplate == null
                ? FindFallbackMaterialShader()
                : null;

            if (materialTemplate == null && fallbackShader == null)
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] Cannot create materials because no material " +
                    $"template or fallback shader is available: {modelPath}",
                    modelImporter);
                return;
            }

            if (!ModelImporterCompatibility.TryEnableMaterialRemappingWhenImportModeIsNone(
                    modelImporter,
                    out bool remapSettingChanged))
            {
                return;
            }

            bool importerChanged = remapSettingChanged;
            int createdMaterialCount = 0;
            Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> externalObjectMap =
                modelImporter.GetExternalObjectMap();

            //遍历每个材质槽
            foreach (AssetImporter.SourceAssetIdentifier sourceMaterial in sourceMaterials)
            {
                string materialFileName = GetSafeAssetFileName(sourceMaterial.name) + ".mat";
                string materialPath = materialFolder + "/" + materialFileName;

                //目标 .mat 已经存在：直接使用。
                if (File.Exists(materialPath) &&
                    AssetDatabase.LoadMainAssetAtPath(materialPath) == null)
                {
                    AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceSynchronousImport);
                }

                Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (existingMaterial == null &&
                    AssetDatabase.LoadMainAssetAtPath(materialPath) != null)
                {
                    Debug.LogError(
                        $"[GRKingRP Model Import] Cannot create material because another asset " +
                        $"already exists: {materialPath}",
                        modelImporter);
                    continue;
                }

                //不存在：使用材质模板或 URP/Lit 创建
                if (existingMaterial == null)
                {
                    existingMaterial = materialTemplate != null
                        ? new Material(materialTemplate)
                        : new Material(fallbackShader);
                    existingMaterial.name = sourceMaterial.name;
                    AssetDatabase.CreateAsset(existingMaterial, materialPath);
                    createdMaterialCount++;
                }

                //目标路径被其他类型资产占用：输出错误并跳过
                if (externalObjectMap.TryGetValue(sourceMaterial, out UnityEngine.Object mapped) &&
                    mapped == existingMaterial)
                {
                    continue;
                }

                //建立材质映射
                modelImporter.AddRemap(sourceMaterial, existingMaterial);
                externalObjectMap[sourceMaterial] = existingMaterial;
                importerChanged = true;
            }

            if (!importerChanged)
            {
                return;
            }

            //如果创建了新映射或修改了 Importer，就保存设置并重新导入模型
            AssetDatabase.WriteImportSettingsIfDirty(modelPath);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);

            if (createdMaterialCount > 0)
            {
                Debug.Log(
                    $"[GRKingRP Model Import] Created and remapped {createdMaterialCount} materials to: " +
                    materialFolder,
                    AssetDatabase.LoadMainAssetAtPath(modelPath));
            }
        }

        private static Shader FindFallbackMaterialShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit") ??
                   Shader.Find("Standard");
        }

        private static void CreatePrefabIfMissing(
            string modelPath,
            ModelImportSettings settings)
        {
            string prefabPath = GetPrefabPath(modelPath, settings);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] Prefab output root is not configured: {modelPath}");
                return;
            }

            // Never overwrite an existing Prefab because it may contain manual edits.
            // File.Exists also protects assets that Unity has not indexed yet.
            if (File.Exists(prefabPath) || AssetDatabase.LoadMainAssetAtPath(prefabPath) != null)
            {
                return;
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] Cannot create Prefab because the model could not be loaded: {modelPath}");
                return;
            }

            string prefabFolder = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(prefabFolder) ||
                !AssetProcessorUtility.EnsureAssetFolder(prefabFolder))
            {
                Debug.LogError(
                    $"[GRKingRP Model Import] Cannot create Prefab folder: {prefabFolder}",
                    modelAsset);
                return;
            }

            //创建prefab
            string modelName = Path.GetFileNameWithoutExtension(modelPath);
            GameObject prefabRoot = new(modelName);

            try
            {
                GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                if (modelInstance == null)
                {
                    Debug.LogError(
                        $"[GRKingRP Model Import] Cannot instantiate model for Prefab creation: {modelPath}",
                        modelAsset);
                    return;
                }

                modelInstance.transform.SetParent(prefabRoot.transform, false);

                //存储prefab到对应位置
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath, out bool success);
                if (success)
                {
                    Debug.Log($"[GRKingRP Model Import] Created Prefab: {prefabPath}", modelAsset);
                }
                else
                {
                    Debug.LogError(
                        $"[GRKingRP Model Import] Failed to create Prefab: {prefabPath}",
                        modelAsset);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        //获取prefab存储的位置
        private static string GetPrefabPath(
            string modelPath,
            ModelImportSettings settings)
        {
            string modelRoot = AssetProcessorUtility.NormalizeFolder(
                settings.ModelImportRootFolder);
            string prefabRoot = AssetProcessorUtility.NormalizeFolder(
                settings.PrefabOutputRootFolder);
            if (string.IsNullOrEmpty(modelRoot) || string.IsNullOrEmpty(prefabRoot))
            {
                return null;
            }

            string relativeModelPath = modelPath.Substring(modelRoot.Length);

            string relativeFolder = Path.GetDirectoryName(relativeModelPath)?.Replace('\\', '/');
            string modelName = Path.GetFileNameWithoutExtension(relativeModelPath);

            if (string.IsNullOrEmpty(relativeFolder))
            {
                return prefabRoot + modelName + ".prefab";
            }

            return prefabRoot + relativeFolder + "/" + modelName + ".prefab";
        }

        //获取材质存储位置
        private static string GetMaterialFolderPath(
            string modelPath,
            ModelImportSettings settings)
        {
            string modelRoot = AssetProcessorUtility.NormalizeFolder(
                settings.ModelImportRootFolder);
            string materialRoot = AssetProcessorUtility.NormalizeFolder(
                settings.MaterialOutputRootFolder);
            if (string.IsNullOrEmpty(modelRoot) || string.IsNullOrEmpty(materialRoot))
            {
                return null;
            }

            string relativeModelPath = modelPath.Substring(modelRoot.Length);

            string relativeFolder = Path.GetDirectoryName(relativeModelPath)?.Replace('\\', '/');
            string modelName = Path.GetFileNameWithoutExtension(relativeModelPath);

            if (string.IsNullOrEmpty(relativeFolder))
            {
                return materialRoot + modelName;
            }

            return materialRoot + relativeFolder + "/" + modelName;
        }

        //获取安全的资产文件名
        private static string GetSafeAssetFileName(string assetName)
        {
            string safeName = string.IsNullOrWhiteSpace(assetName) ? "Material" : assetName;

            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalidCharacter, '_');
            }

            return safeName.Replace('/', '_').Replace('\\', '_');
        }

    }
}

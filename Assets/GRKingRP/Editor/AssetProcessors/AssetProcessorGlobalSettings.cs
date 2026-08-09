using UnityEditor;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    internal static class AssetProcessorGlobalSettings
    {
        public const string SettingsFolder = "Assets/GRKingRP/Settings";
        public const string TextureSettingsAssetPath =
            SettingsFolder + "/TextureImportSettings.asset";
        public const string ModelSettingsAssetPath =
            SettingsFolder + "/ModelImportSettings.asset";

        //加载纹理设置
        internal static TextureImportSettings LoadTextureSettings()
        {
            return AssetDatabase.LoadAssetAtPath<TextureImportSettings>(
                TextureSettingsAssetPath);
        }

        //加载资产设置
        internal static ModelImportSettings LoadModelSettings()
        {
            return AssetDatabase.LoadAssetAtPath<ModelImportSettings>(
                ModelSettingsAssetPath);
        }

        //[InitializeOnLoadMethod]表示 Unity在以下情况自动调用这个静态函数：打开项目时，脚本编译完成、程序集重新加载时，发生 Domain Reload 时
        //Unity 编辑器加载或脚本重新编译后，安排一次设置资产检查
        [InitializeOnLoadMethod]
        private static void ScheduleSettingsValidation()
        {
            if (!AssetDatabase.IsAssetImportWorkerProcess())
            {
                EditorApplication.delayCall += ValidateSettingsAssets;
            }
        }

        //验证是否加载设置资产
        private static void ValidateSettingsAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ValidateSettingsAssets;
                return;
            }

            if (LoadTextureSettings() == null)
            {
                Debug.LogError(
                    $"[GRKingRP Asset Processing] Texture import settings are missing: " +
                    $"{TextureSettingsAssetPath}. Restore the asset at this path.");
            }

            if (LoadModelSettings() == null)
            {
                Debug.LogError(
                    $"[GRKingRP Asset Processing] Model import settings are missing: " +
                    $"{ModelSettingsAssetPath}. Restore the asset at this path.");
            }
        }
    }
}

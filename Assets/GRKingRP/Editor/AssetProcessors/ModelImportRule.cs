using System;
using UnityEditor.Presets;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    //平滑法线存储的位置
    public enum SmoothedNormalStorageTarget
    {
        Tangent,
        VertexColor,
        UV2,
        UV3,
        UV4
    }

    [Serializable]
    public sealed class ModelImportRule
    {
        public bool Enabled = true;

        [Delayed]
        public string NameGlob;

        public bool IgnoreCase = true;
        public Preset Preset;
        public bool AutoBakeSmoothedNormals;
        public SmoothedNormalStorageTarget SmoothedNormalStorage =
            SmoothedNormalStorageTarget.Tangent;
        public bool AutoCreatePrefab;
        public bool AutoCreateMaterials;
        public Material MaterialTemplate;

        public bool TryMatch(string modelName)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(NameGlob))
            {
                return false;
            }

            return AssetProcessorUtility.IsNameGlobMatch(
                modelName,
                NameGlob,
                IgnoreCase);
        }
    }
}

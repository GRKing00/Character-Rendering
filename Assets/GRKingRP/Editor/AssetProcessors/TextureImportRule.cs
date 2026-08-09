using System;
using UnityEditor.Presets;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    [Serializable]
    public sealed class TextureImportRule
    {
        public bool Enabled = true;

        [Delayed]
        public string NameGlob;

        public bool IgnoreCase = true;
        public Preset Preset;

        //名字是否匹配当前规则
        public bool TryMatch(string textureName)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(NameGlob))
            {
                return false;
            }

            return AssetProcessorUtility.IsNameGlobMatch(
                textureName,
                NameGlob,
                IgnoreCase);
        }
    }
}

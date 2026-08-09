using System.Collections.Generic;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    //纹理导入设置
    public sealed class TextureImportSettings : ScriptableObject
    {
        [Delayed]
        public string TextureImportRootFolder = "Assets/Textures/";
        public List<TextureImportRule> Rules = new();
    }
}

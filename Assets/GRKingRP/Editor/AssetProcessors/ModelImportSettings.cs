using System.Collections.Generic;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    /// <summary>
    /// 模型导入设置，以及材质和预制体输出位置的设置
    /// </summary>
    public sealed class ModelImportSettings : ScriptableObject
    {
        [Delayed]
        public string ModelImportRootFolder = "Assets/Models/";

        [Delayed]
        public string PrefabOutputRootFolder = "Assets/Prefabs/";

        [Delayed]
        public string MaterialOutputRootFolder = "Assets/Materials/";

        //“详细日志”开关，只控制平滑法线计算过程的日志
        public bool VerboseLogging;
        public List<ModelImportRule> Rules = new();
    }
}

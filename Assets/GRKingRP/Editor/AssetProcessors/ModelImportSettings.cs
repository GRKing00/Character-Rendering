using System.Collections.Generic;
using UnityEngine;

namespace GRKingRP.Editor.AssetProcessors
{
    /// <summary>
    /// 模型 Preset 和平滑法线导入设置
    /// </summary>
    public sealed class ModelImportSettings : ScriptableObject
    {
        [Delayed]
        public string ModelImportRootFolder = "Assets/Models/";

        //“详细日志”开关，只控制平滑法线计算过程的日志
        public bool VerboseLogging;
        public List<ModelImportRule> Rules = new();
    }
}

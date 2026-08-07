using System.Collections.Generic;

namespace GRKingRP.Editor.AssetProcessors
{
    internal static class AssetProcessorGlobalSettings
    {
        public const string TextureImportRootFolder = "Assets/Textures/";

        private const string PresetFolder = "Assets/GRKingRP/Presets/";

        public static readonly TextureImportRule ColorTextureRule = new(
            @"*_Color*",
            PresetFolder + "ColorTexture.preset",
            enabled: true,
            ignoreCase: true);

        public static readonly TextureImportRule RampTextureRule = new(
            @"*_Ramp*",
            PresetFolder + "RampTexture.preset",
            enabled: true,
            ignoreCase: true);

        public static readonly TextureImportRule LightMapTextureRule = new(
            @"*_LightMap*",
            PresetFolder + "LightMap.preset",
            enabled: true,
            ignoreCase: true);

        public static readonly TextureImportRule FaceExpressionMapTextureRule = new(
            @"*_Face_ExpressionMap*",
            PresetFolder + "FaceExpressionMap.preset",
            enabled: true,
            ignoreCase: true);

        public static readonly TextureImportRule FaceMapTextureRule = new(
            @"*_FaceMap*",
            PresetFolder + "FaceMap.preset",
            enabled: true,
            ignoreCase: true);

        // Rules are evaluated from top to bottom; the first match wins.
        //纹理导入规则
        public static IEnumerable<TextureImportRule> TextureImportRules
        {
            get
            {
                yield return ColorTextureRule;
                yield return RampTextureRule;
                yield return LightMapTextureRule;
                yield return FaceExpressionMapTextureRule;
                yield return FaceMapTextureRule;
            }
        }
    }
}

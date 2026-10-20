using GRKingRP.RendererFeatures;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GRKingRP.Editor.Build
{
    /// <summary>保护运行时使用的变体，其余变体仍由 URP 的剔除规则决定。</summary>
    public sealed class ShaderStripper : IShaderVariantStripper, IShaderVariantStripperScope
    {
        // 后续需要保留其它运行时开启的关键字，在这里添加。
        private static readonly string[] s_RuntimeKeywordNames =
        {
            ShaderKeywordStrings.MainLightShadowScreen
        };

        private readonly LocalKeyword[] m_RuntimeKeywords = new LocalKeyword[s_RuntimeKeywordNames.Length];

        //是否启用这套规则，GRKingRendererFeature启用时激活这套规则
        public bool active
        {
            get
            {
                foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
                {
                    UniversalRendererData data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    if (data == null)
                        continue;
                    foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                        if (feature is GRKingRendererFeature && feature.isActive)
                            return true;
                }
                return false;
            }
        }

        //在处理当前 Shader 的一批变体之前，查找并缓存它声明的关键字
        public void BeforeShaderStripping(Shader shader)
        {
            for (int index = 0; index < s_RuntimeKeywordNames.Length; index++)
                m_RuntimeKeywords[index] = shader.keywordSpace.FindKeyword(s_RuntimeKeywordNames[index]);
        }

        public bool CanRemoveVariant(Shader shader, ShaderSnippetData shaderVariant,
            ShaderCompilerData shaderCompilerData)
        {
            ShaderKeywordSet keywords = shaderCompilerData.shaderKeywordSet;
            //保护指定关键字的开启变体
            foreach (LocalKeyword keyword in m_RuntimeKeywords)
                if (IsEnabled(keywords, keyword))
                    return false;

            // true 仅表示本规则不阻止剔除，不是无条件删除这个变体。
            return true;
        }

        private static bool IsEnabled(ShaderKeywordSet keywords, LocalKeyword keyword)
        {
            return keyword.isValid && keywords.IsEnabled(keyword);
        }

        public void AfterShaderStripping(Shader shader) { }
    }
}

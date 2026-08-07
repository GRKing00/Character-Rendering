using System.Text.RegularExpressions;

namespace GRKingRP.Editor.AssetProcessors
{
    public sealed class TextureImportRule
    {
        public bool Enabled;
        public string NameGlob;
        public bool IgnoreCase;
        public string PresetPath;

        public TextureImportRule(
            string nameGlob,
            string presetPath,
            bool enabled = true,
            bool ignoreCase = true)
        {
            Enabled = enabled;
            NameGlob = nameGlob;
            IgnoreCase = ignoreCase;
            PresetPath = presetPath;
        }

        //做textureName与当前规则的匹配
        public bool TryMatch(string textureName)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(NameGlob))
            {
                return false;
            }

            RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Singleline;
            if (IgnoreCase)
            {
                options |= RegexOptions.IgnoreCase;
            }

            // Use '|' to separate multiple Glob patterns. '*' matches any number
            // of characters, while '?' matches exactly one character.
            foreach (string pattern in NameGlob.Split('|')) //使用 | 拆分多个 Glob 规则
            {
                string globPattern = pattern.Trim();//删除规则两侧的空格
                if (globPattern.Length == 0)
                {
                    continue;
                }

                //glob 转正则表达式
                string regexPattern = "^" + Regex.Escape(globPattern)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".") + "$";

                if (Regex.IsMatch(textureName, regexPattern, options))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

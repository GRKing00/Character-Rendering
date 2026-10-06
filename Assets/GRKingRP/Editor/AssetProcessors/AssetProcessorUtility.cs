using System;

namespace GRKingRP.Editor.AssetProcessors
{
    internal static class AssetProcessorUtility
    {
        //名字是否匹配当前规则
        internal static bool IsNameGlobMatch(
            string assetName,
            string nameGlob,
            bool ignoreCase)
        {
            if (string.IsNullOrEmpty(assetName) || string.IsNullOrWhiteSpace(nameGlob))
            {
                return false;
            }
            
            //使用 |划分nameGlob
            foreach (string pattern in nameGlob.Split('|'))
            {
                string trimmedPattern = pattern.Trim();
                if (trimmedPattern.Length > 0 &&
                    IsSingleGlobMatch(assetName, trimmedPattern, ignoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string NormalizeFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return null;
            }

            string normalizedPath = folderPath.Trim().Replace('\\', '/').TrimEnd('/');
            return normalizedPath.Length == 0 ? null : normalizedPath + "/";
        }

        //判断是否在指定文件夹下
        internal static bool IsAssetPathUnderFolder(
            string assetPath,
            string folderPath,
            out string normalizedAssetPath)
        {
            normalizedAssetPath = string.IsNullOrEmpty(assetPath)
                ? string.Empty
                : assetPath.Replace('\\', '/');

            string normalizedFolder = NormalizeFolder(folderPath);
            return normalizedFolder != null &&
                   normalizedAssetPath.StartsWith(
                       normalizedFolder,
                       StringComparison.OrdinalIgnoreCase);
        }

        //名字是否匹配当前规则
        private static bool IsSingleGlobMatch(
            string value,
            string pattern,
            bool ignoreCase)
        {
            int valueIndex = 0;
            int patternIndex = 0;
            int lastStarIndex = -1;
            int valueIndexAfterStar = -1;

            while (valueIndex < value.Length)
            {
                //普通字符相同，或者规则字符是 ?
                if (patternIndex < pattern.Length &&
                    (pattern[patternIndex] == '?' ||
                     CharactersEqual(
                         value[valueIndex],
                         pattern[patternIndex],
                         ignoreCase)))
                {
                    valueIndex++;
                    patternIndex++;
                    continue;
                }

                //遇到 *
                //先让 * 匹配最少字符，后面失败时，再逐步扩大它的匹配范围
                if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    lastStarIndex = patternIndex++;
                    valueIndexAfterStar = valueIndex;
                    continue;
                }

                //后续匹配失败
                //没遇到过 *，直接返回 false
                if (lastStarIndex < 0)
                {
                    return false;
                }

                //让最近的 * 再多匹配一个字符
                patternIndex = lastStarIndex + 1;
                valueIndex = ++valueIndexAfterStar;
            }

            while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                patternIndex++;
            }

            return patternIndex == pattern.Length;
        }

        //判断字符是否相等
        private static bool CharactersEqual(char left, char right, bool ignoreCase)
        {
            return left == right ||
                   (ignoreCase &&
                    char.ToUpperInvariant(left) == char.ToUpperInvariant(right));
        }
    }
}

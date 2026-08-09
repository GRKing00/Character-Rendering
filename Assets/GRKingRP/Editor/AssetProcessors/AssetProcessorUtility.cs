using System;
using UnityEditor;

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

        //获取文件夹
        internal static bool EnsureAssetFolder(string folderPath)
        {
            string normalizedFolder = NormalizeFolder(folderPath)?.TrimEnd('/');
            if (normalizedFolder == null)
            {
                return false;
            }

            string[] folderParts = normalizedFolder.Split('/');
            if (folderParts.Length == 0 ||
                !folderParts[0].Equals("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string currentFolder = folderParts[0];

            for (int index = 1; index < folderParts.Length; index++)
            {
                if (folderParts[index].Length == 0)
                {
                    return false;
                }

                string nextFolder = currentFolder + "/" + folderParts[index];
                if (!AssetDatabase.IsValidFolder(nextFolder))
                {
                    string folderGuid = AssetDatabase.CreateFolder(
                        currentFolder,
                        folderParts[index]);
                    if (string.IsNullOrEmpty(folderGuid))
                    {
                        return false;
                    }
                }

                currentFolder = nextFolder;
            }

            return true;
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

                if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    lastStarIndex = patternIndex++;
                    valueIndexAfterStar = valueIndex;
                    continue;
                }

                if (lastStarIndex < 0)
                {
                    return false;
                }

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

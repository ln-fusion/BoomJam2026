#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Backbone
{
    /// <summary>
    /// 程序集依赖验证器：检查 Runtime 程序集是否引用了 UnityEditor（C01 硬性验收）.
    /// </summary>
    /// <remarks>
    /// 运行位置：Assets/Game/Editor/AssemblyValidation/（Editor only）.
    /// 校验规则：任何名称以 Game. 开头且不带 Editor 的 Runtime asmdef 不得在 references 中引用 UnityEditor.
    /// </remarks>
    public static class AssemblyDependencyValidator
    {
        /// <summary>执行程序集依赖检查，返回违规描述列表.</summary>
        public static IReadOnlyList<string> Validate()
        {
            var violations = new List<string>();

            string[] guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("Game.", StringComparison.Ordinal) || name.Contains("Editor"))
                {
                    continue;
                }

                if (ReferencesEditor(path))
                {
                    violations.Add($"{name} 引用了 UnityEditor（Runtime 程序集禁止）");
                }
            }

            return violations;
        }

        /// <summary>读取 asmdef JSON 文本，检查 references 是否包含 UnityEditor.</summary>
        private static bool ReferencesEditor(string path)
        {
            if (!File.Exists(path))
                return false;

            string json = File.ReadAllText(path);
            return json.Contains("\"UnityEditor\"", StringComparison.Ordinal)
                || json.Contains("\"UnityEditor.Android.Extensions\"", StringComparison.Ordinal)
                || json.Contains("\"UnityEditor.iOS.Extensions\"", StringComparison.Ordinal);
        }
    }
}

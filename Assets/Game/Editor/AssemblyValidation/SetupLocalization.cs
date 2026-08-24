#nullable enable
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Game.Editor.Backbone
{
    /// <summary>
    /// 本地化资产一键搭建：创建 zh-CN/en-US Locale 与 UI String Table.
    /// </summary>
    /// <remarks>
    /// 菜单：Tools/Change and Run/Setup Localization
    /// 运行后新建的 String Table 出现在 Assets/Game/Localization/UI.asset,
    /// 拖到 00_Bootstrap 场景 GameRoot 的 localizationTable 字段即可使用.
    /// </remarks>
    public static class SetupLocalization
    {
        private const string OutputDirectory = "Assets/Game/Localization";
        private const string TableName = "UI";

        [MenuItem("Tools/Change and Run/Setup Localization")]
        public static void Execute()
        {
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
                AssetDatabase.Refresh();
            }

            List<Locale> locales = EnsureLocales();
            var collection = LocalizationEditorSettings.CreateStringTableCollection(
                TableName,
                OutputDirectory,
                locales
            );

            if (collection == null)
            {
                Debug.LogError("[SetupLocalization] String Table 创建失败");
                return;
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = collection;
            EditorUtility.DisplayDialog(
                "本地化搭建完成",
                $"String Table: {TableName}\nLocale: zh-CN, en-US\n\n请将其拖到 00_Bootstrap 场景 GameRoot 的 localizationTable 字段",
                "知道了"
            );
        }

        /// <summary>确保 zh-CN/en-US Locale 存在，返回可用列表.</summary>
        private static List<Locale> EnsureLocales()
        {
            var existing = new List<Locale>(LocalizationEditorSettings.GetLocales());
            var result = new List<Locale>();

            foreach (var code in new[] { "zh-CN", "en-US" })
            {
                var locale = existing.Find(l =>
                    string.Equals(l.Identifier.Code, code, System.StringComparison.OrdinalIgnoreCase)
                );
                if (locale == null)
                {
                    locale = ScriptableObject.CreateInstance<Locale>();
                    locale.Identifier = new LocaleIdentifier(code);

                    AssetDatabase.CreateAsset(locale, $"{OutputDirectory}/{code}.asset");
                    LocalizationEditorSettings.AddLocale(locale);
                }

                result.Add(locale);
            }

            return result;
        }
    }
}

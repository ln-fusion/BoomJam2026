using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEditor.Localization.Plugins.CSV.Columns;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Game.Editor.Localization
{
    /// <summary>
    /// 从版本化 CSV 初始化并更新项目 String Table 资源。
    /// </summary>
    /// <remarks>
    /// 文案编辑人员修改 <c>Assets/Localization/UI.csv</c> 后，可通过菜单重新导入；
    /// 运行时只读取生成的 Unity Localization 资产，不读取 CSV 文件。
    /// </remarks>
    public static class LocalizationAssetGenerator
    {
        private const string LocalizationRoot = "Assets/Localization";
        private const string LocaleRoot = LocalizationRoot + "/Locales";
        private const string TableRoot = LocalizationRoot + "/StringTables";
        private const string SettingsPath = LocalizationRoot + "/LocalizationSettings.asset";
        private const string SourceCsvPath = LocalizationRoot + "/UI.csv";
        private const string UiTableName = "UI";
        private static readonly string[] LocaleCodes = { "zh-CN", "en-US" };

        /// <summary>
        /// 创建或更新 UI Locale、String Table 和项目 LocalizationSettings 资源。
        /// </summary>
        [MenuItem("Boom Jam/Localization/Import UI CSV")]
        public static void ImportUiCsv()
        {
            EnsureFolder("Assets", "Localization");
            EnsureFolder(LocalizationRoot, "Locales");
            EnsureFolder(LocalizationRoot, "StringTables");

            LocalizationSettings settings = EnsureSettings();
            List<Locale> locales = EnsureLocales();
            StringTableCollection collection = EnsureCollection(locales);
            ImportSourceCsv(collection);

            Locale defaultLocale = locales.FirstOrDefault(locale =>
                string.Equals(locale.Identifier.Code, "zh-CN", StringComparison.OrdinalIgnoreCase));
            if (defaultLocale != null)
            {
                LocalizationSettings.ProjectLocale = defaultLocale;
                ConfigureDefaultStartupLocale(settings);
            }

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(collection.SharedData);
            foreach (StringTable table in collection.StringTables)
            {
                LocalizationEditorSettings.SetPreloadTableFlag(table, true);
                EditorUtility.SetDirty(table);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Imported UI localization CSV into the UI String Table.");
        }

        /// <summary>把没有存档覆盖时的启动 Locale 固定为项目默认中文。</summary>
        /// <param name="settings">活动 Localization Settings。</param>
        private static void ConfigureDefaultStartupLocale(LocalizationSettings settings)
        {
            foreach (IStartupLocaleSelector selector in settings.GetStartupLocaleSelectors())
            {
                if (selector is SpecificLocaleSelector specificLocaleSelector)
                {
                    specificLocaleSelector.LocaleId = new LocaleIdentifier("zh-CN");
                    EditorUtility.SetDirty(settings);
                }
            }
        }

        /// <summary>创建或加载项目的活动 LocalizationSettings 资源。</summary>
        /// <returns>活动设置资源。</returns>
        private static LocalizationSettings EnsureSettings()
        {
            LocalizationSettings settings =
                AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "Localization Settings";
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            if (LocalizationEditorSettings.ActiveLocalizationSettings != settings)
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;

            return settings;
        }

        /// <summary>创建缺少的 Locale 资产，并将项目 Locale 注册到 Localization。</summary>
        /// <returns>按稳定代码排序的 Locale 列表。</returns>
        private static List<Locale> EnsureLocales()
        {
            var locales = new List<Locale>(LocaleCodes.Length);
            foreach (string code in LocaleCodes)
            {
                string localePath = LocaleRoot + "/" + code + ".asset";
                Locale locale = AssetDatabase.LoadAssetAtPath<Locale>(localePath);
                if (locale == null)
                {
                    locale = Locale.CreateLocale(code);
                    AssetDatabase.CreateAsset(locale, localePath);
                }

                bool alreadyRegistered = LocalizationEditorSettings.GetLocales().Any(
                    candidate => ReferenceEquals(candidate, locale));
                if (!alreadyRegistered)
                    LocalizationEditorSettings.AddLocale(locale);

                locales.Add(locale);
            }

            return locales;
        }

        /// <summary>创建 UI String Table 集合，并为每个目标 Locale 添加表。</summary>
        /// <param name="locales">目标 Locale。</param>
        /// <returns>UI String Table 集合。</returns>
        private static StringTableCollection EnsureCollection(IList<Locale> locales)
        {
            StringTableCollection collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(
                TableRoot + "/" + UiTableName + ".asset");
            if (collection == null)
                collection = LocalizationEditorSettings.GetStringTableCollection(UiTableName);
            if (collection == null)
                collection = LocalizationEditorSettings.CreateStringTableCollection(UiTableName, TableRoot, locales);

            foreach (Locale locale in locales)
            {
                if (collection.GetTable(locale.Identifier) == null)
                    collection.AddNewTable(locale.Identifier);
            }

            return collection;
        }

        /// <summary>使用 Unity Localization CSV 导入器更新表格内容。</summary>
        /// <param name="collection">目标 String Table 集合。</param>
        private static void ImportSourceCsv(StringTableCollection collection)
        {
            if (!File.Exists(SourceCsvPath))
                throw new FileNotFoundException("Localization source CSV was not found.", SourceCsvPath);

            var mappings = new List<CsvColumns>
            {
                new KeyIdColumns { IncludeId = true, IncludeSharedComments = false },
                new LocaleColumns
                {
                    LocaleIdentifier = new LocaleIdentifier("zh-CN"),
                    FieldName = "zh-CN",
                    IncludeComments = false
                },
                new LocaleColumns
                {
                    LocaleIdentifier = new LocaleIdentifier("en-US"),
                    FieldName = "en-US",
                    IncludeComments = false
                }
            };

            using (var reader = new StreamReader(SourceCsvPath))
                Csv.ImportInto(reader, collection, mappings, true, null, true);
        }

        /// <summary>在 Unity 资源目录下创建缺失文件夹。</summary>
        /// <param name="parent">父目录。</param>
        /// <param name="name">新目录名称。</param>
        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}

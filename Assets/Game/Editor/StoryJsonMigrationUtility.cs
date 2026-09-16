#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Content;
using Game.Contracts.Content;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>将旧 TextKey/SpeakerKey 剧情内容迁移为 JSON 内置中英文文本。</summary>
    public static class StoryJsonMigrationUtility
    {
        private const string CatalogPath = "Assets/Game/Content/OfficialContentCatalog.asset";
        private const string AuthoringFolder = "Assets/Game/Content";
        private const string RuntimeFolder = "Assets/Game/Resources/StoryRuntime";
        private const string CsvPath = "Assets/Localization/UI.csv";

        /// <summary>从 OfficialContentCatalog 和 UI.csv 生成全量 authoring/runtime 剧情 JSON。</summary>
        [MenuItem("Game/Story/Migrate All Stories To Full JSON")]
        public static void MigrateAllStories()
        {
            OfficialContentCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialContentCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("Story migration failed: catalog not found at " + CatalogPath);
                return;
            }
            Dictionary<string, string[]> texts = ReadCsv(CsvPath);
            int migrated = 0;
            int missing = 0;
            foreach (StoryDefinition source in catalog.Stories)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.StoryId))
                    continue;
                StoryDefinition story = JsonUtility.FromJson<StoryDefinition>(JsonUtility.ToJson(source));
                foreach (StoryNodeDefinition node in story.Nodes ?? new List<StoryNodeDefinition>())
                {
                    if (node == null) continue;
                    missing += Fill(ref node.TextZhCn, ref node.TextEnUs, node.TextKey, texts, story.StoryId, node.NodeId);
                    missing += Fill(ref node.SpeakerTextZhCn, ref node.SpeakerTextEnUs, node.SpeakerKey, texts, story.StoryId, node.NodeId);
                    node.TextKey = null;
                    node.SpeakerKey = null;
                    if (node.Choices == null) continue;
                    foreach (StoryChoiceDefinition choice in node.Choices)
                        if (choice != null)
                        {
                            missing += Fill(ref choice.TextZhCn, ref choice.TextEnUs, choice.TextKey, texts, story.StoryId, node.NodeId + "/" + choice.ChoiceId);
                            choice.TextKey = null;
                        }
                }
                WriteStory(story);
                migrated++;
            }
            // 同时处理已经存在的 authoring JSON，避免测试剧情仍停留在旧格式或空文本状态。
            foreach (string path in Directory.GetFiles(AuthoringFolder, "*.story.authoring.json"))
            {
                StoryDefinition existing;
                try { existing = JsonUtility.FromJson<StoryDefinition>(File.ReadAllText(path)); }
                catch { continue; }
                if (existing == null || string.IsNullOrWhiteSpace(existing.StoryId)) continue;
                foreach (StoryNodeDefinition node in existing.Nodes ?? new List<StoryNodeDefinition>())
                {
                    if (node == null) continue;
                    if (node.Type == StoryNodeType.Dialogue && string.IsNullOrWhiteSpace(node.TextZhCn) && string.IsNullOrWhiteSpace(node.TextEnUs))
                    {
                        node.TextZhCn = "【待填写剧情文本：" + node.NodeId + "】";
                        node.TextEnUs = "[Story text pending: " + node.NodeId + "]";
                    }
                    if (node.Type == StoryNodeType.Choice && node.Choices != null)
                        foreach (StoryChoiceDefinition choice in node.Choices)
                        {
                            if (choice == null || (!string.IsNullOrWhiteSpace(choice.TextZhCn) || !string.IsNullOrWhiteSpace(choice.TextEnUs))) continue;
                            choice.TextZhCn = "【选项：" + choice.ChoiceId + "】";
                            choice.TextEnUs = "[Choice: " + choice.ChoiceId + "]";
                        }
                }
                WriteStory(existing);
            }
            AssetDatabase.Refresh();
            Debug.Log("Story JSON migration complete. Stories: " + migrated + ", missing text entries: " + missing);
        }

        /// <summary>将一个旧文本键转换为 CSV 中的中英文列。</summary>
        private static int Fill(ref string zhCn, ref string enUs, string key,
            Dictionary<string, string[]> texts, string storyId, string nodeId)
        {
            if (string.IsNullOrWhiteSpace(key)) return 0;
            if (texts.TryGetValue(key, out string[] values))
            {
                zhCn = values[0];
                enUs = values[1];
                return 0;
            }
            // 没有本地化源时仍生成可运行的明确占位文本，避免空对白阻断整个目录。
            zhCn = string.IsNullOrWhiteSpace(zhCn) ? "【待填写剧情文本：" + key + "】" : zhCn;
            enUs = string.IsNullOrWhiteSpace(enUs) ? "[Story text pending: " + key + "]" : enUs;
            Debug.LogWarning("Missing story text key during migration: " + storyId + "/" + nodeId + " -> " + key);
            return 1;
        }

        /// <summary>写入 authoring JSON 和 runtime JSON。</summary>
        private static void WriteStory(StoryDefinition story)
        {
            Directory.CreateDirectory(AuthoringFolder);
            Directory.CreateDirectory(RuntimeFolder);
            string authoring = Path.Combine(AuthoringFolder, story.StoryId + ".story.authoring.json");
            string runtime = Path.Combine(RuntimeFolder, story.StoryId + ".story.runtime.json");
            File.WriteAllText(authoring, JsonUtility.ToJson(story, true), new UTF8Encoding(false));
            File.WriteAllText(runtime, StoryRuntimeSerializer.SerializeEnvelope(story), new UTF8Encoding(false));
        }

        /// <summary>读取四列 UI.csv，返回 Key、中文和英文文本。</summary>
        private static Dictionary<string, string[]> ReadCsv(string path)
        {
            var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (!File.Exists(path)) return result;
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                string[] columns = ParseCsvLine(line);
                if (columns.Length >= 4 && !string.IsNullOrWhiteSpace(columns[0]) && columns[0] != "Key")
                    result[columns[0]] = new[] { columns[2], columns[3] };
            }
            return result;
        }

        /// <summary>解析支持双引号转义的 CSV 单行。</summary>
        private static string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var value = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == ',' && !quoted) { values.Add(value.ToString()); value.Clear(); }
                else value.Append(c);
            }
            values.Add(value.ToString());
            return values.ToArray();
        }
    }
}
#endif

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
            var authoredIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(AuthoringFolder, "*.story.authoring.json"))
            {
                try
                {
                    StoryDefinition authored = JsonUtility.FromJson<StoryDefinition>(File.ReadAllText(path));
                    if (authored != null && !string.IsNullOrWhiteSpace(authored.StoryId))
                        authoredIds.Add(authored.StoryId);
                }
                catch { }
            }
            int migrated = 0;
            int missing = 0;
            foreach (StoryDefinition source in catalog.Stories)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.StoryId))
                    continue;
                if (authoredIds.Contains(source.StoryId))
                    continue;
                StoryDefinition story = JsonUtility.FromJson<StoryDefinition>(JsonUtility.ToJson(source));
                foreach (StoryNodeDefinition node in story.Nodes ?? new List<StoryNodeDefinition>())
                {
                    if (node == null) continue;
                    missing += Fill(ref node.TextZhCn, ref node.TextEnUs, node.TextKey, texts, story.StoryId, node.NodeId);
                    missing += Fill(ref node.SpeakerTextZhCn, ref node.SpeakerTextEnUs, node.SpeakerKey, texts, story.StoryId, node.NodeId);
                    node.TextKey = null;
                    node.SpeakerKey = null;
                    if (node.Type == StoryNodeType.Dialogue &&
                        string.IsNullOrWhiteSpace(node.TextZhCn) && string.IsNullOrWhiteSpace(node.TextEnUs))
                    {
                        node.TextZhCn = "【待填写剧情文本：" + node.NodeId + "】";
                        node.TextEnUs = "[Story text pending: " + node.NodeId + "]";
                        missing++;
                    }
                    if (node.Choices == null) continue;
                    foreach (StoryChoiceDefinition choice in node.Choices)
                        if (choice != null)
                        {
                            missing += Fill(ref choice.TextZhCn, ref choice.TextEnUs, choice.TextKey, texts, story.StoryId, node.NodeId + "/" + choice.ChoiceId);
                            choice.TextKey = null;
                            if (string.IsNullOrWhiteSpace(choice.TextZhCn) && string.IsNullOrWhiteSpace(choice.TextEnUs))
                            {
                                choice.TextZhCn = "【待填写选项：" + choice.ChoiceId + "】";
                                choice.TextEnUs = "[Choice pending: " + choice.ChoiceId + "]";
                                missing++;
                            }
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
                    missing += Fill(ref node.TextZhCn, ref node.TextEnUs, node.TextKey, texts, existing.StoryId, node.NodeId);
                    missing += Fill(ref node.SpeakerTextZhCn, ref node.SpeakerTextEnUs, node.SpeakerKey, texts, existing.StoryId, node.NodeId);
                    node.TextKey = null;
                    node.SpeakerKey = null;
                    if (node.Type == StoryNodeType.Dialogue && string.IsNullOrWhiteSpace(node.TextZhCn) && string.IsNullOrWhiteSpace(node.TextEnUs))
                    {
                        node.TextZhCn = "【待填写剧情文本：" + node.NodeId + "】";
                        node.TextEnUs = "[Story text pending: " + node.NodeId + "]";
                    }
                    if (node.Type == StoryNodeType.Choice && node.Choices != null)
                        foreach (StoryChoiceDefinition choice in node.Choices)
                        {
                            if (choice == null) continue;
                            missing += Fill(ref choice.TextZhCn, ref choice.TextEnUs, choice.TextKey,
                                texts, existing.StoryId, node.NodeId + "/" + choice.ChoiceId);
                            choice.TextKey = null;
                            if (string.IsNullOrWhiteSpace(choice.TextZhCn) && string.IsNullOrWhiteSpace(choice.TextEnUs))
                            {
                                choice.TextZhCn = "【待填写选项：" + choice.ChoiceId + "】";
                                choice.TextEnUs = "[Choice: " + choice.ChoiceId + "]";
                                missing++;
                            }
                        }
                }
                WriteStory(existing);
            }
            AssetDatabase.Refresh();
            Debug.Log("Story JSON migration complete. Stories: " + migrated + ", missing text entries: " + missing);
            ValidateRuntimeJson();
        }

        /// <summary>校验全部 Runtime JSON，并核对关卡前后剧情引用。</summary>
        [MenuItem("Game/Story/Validate Runtime JSON")]
        public static void ValidateRuntimeJson()
        {
            IReadOnlyDictionary<string, StoryDefinition> stories = GeneratedStoryLoader.LoadAll();
            OfficialContentCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialContentCatalog>(CatalogPath);
            int referenceCount = 0;
            int missingReferences = 0;
            int invalidFiles = 0;
            if (Directory.Exists(RuntimeFolder))
                foreach (string path in Directory.GetFiles(RuntimeFolder, "*.story.runtime.json"))
                {
                    string json = File.ReadAllText(path);
                    string validationError = null;
                    bool parsed = StoryRuntimeSerializer.TryDeserialize(json, out StoryDefinition story);
                    if (!parsed || !StoryDefinitionValidator.TryValidate(story, out validationError))
                    {
                        invalidFiles++;
                        Debug.LogError("Invalid Runtime Story JSON: " + path + " -> " + validationError);
                    }
                }
            if (catalog != null)
                foreach (LevelDefinition level in catalog.Levels)
                {
                    if (level == null) continue;
                    foreach (string id in new[] { level.PreludeStoryId, level.PostludeStoryId })
                    {
                        if (string.IsNullOrWhiteSpace(id)) continue;
                        referenceCount++;
                        if (!stories.ContainsKey(id))
                        {
                            missingReferences++;
                            Debug.LogError("Missing Runtime Story JSON reference: " + level.LevelId + " -> " + id);
                        }
                    }
                }
            if (missingReferences > 0 || invalidFiles > 0)
                Debug.LogError("Story Runtime validation failed. Loaded=" + stories.Count +
                    ", references=" + referenceCount + ", missing=" + missingReferences +
                    ", invalidFiles=" + invalidFiles);
            else
                Debug.Log("Story Runtime validation passed. Loaded=" + stories.Count +
                    ", references=" + referenceCount + ", invalidFiles=0");
        }

        /// <summary>删除旧测试剧情产物并生成符合全量文本契约的新测试剧情。</summary>
        [MenuItem("Game/Story/Rebuild Legacy Test Stories")]
        public static void RebuildLegacyTestStories()
        {
            string[] ids =
            {
                "story.full_feature_test",
                "story.new",
                "story.simple_test",
                "story.test_branch",
                "story.test_linear",
            };
            foreach (string id in ids)
            {
                string runtime = Path.Combine(RuntimeFolder, id + ".story.runtime.json");
                string authoring = Path.Combine(AuthoringFolder, id + ".story.authoring.json");
                if (File.Exists(runtime)) File.Delete(runtime);
                if (File.Exists(authoring)) File.Delete(authoring);
                WriteStory(CreateTestStory(id));
            }
            AssetDatabase.Refresh();
            Debug.Log("Rebuilt legacy test stories: " + ids.Length);
            ValidateRuntimeJson();
        }

        /// <summary>创建包含对白、选项、分支汇合和结束节点的最小测试剧情。</summary>
        /// <param name="storyId">测试剧情稳定标识。</param>
        /// <returns>可通过运行时校验的剧情定义。</returns>
        private static StoryDefinition CreateTestStory(string storyId)
        {
            var story = new StoryDefinition
            {
                Header = new ContentHeader { ContentId = storyId, Source = ContentSource.Official, FormatVersion = 1, ContentRevision = 1 },
                StoryId = storyId,
                StartNodeId = "start",
                Nodes = new List<StoryNodeDefinition>(),
            };
            story.Nodes.Add(new StoryNodeDefinition
            {
                NodeId = "start", Type = StoryNodeType.Dialogue,
                TextZhCn = "这是测试剧情的开始。", TextEnUs = "This is the beginning of the test story.",
                SpeakerTextZhCn = "系统", SpeakerTextEnUs = "System", NextNodeId = storyId == "story.test_branch" ? "choice" : "end",
            });
            if (storyId == "story.test_branch")
            {
                story.Nodes.Add(new StoryNodeDefinition
                {
                    NodeId = "choice", Type = StoryNodeType.Choice,
                    TextZhCn = "请选择一条测试分支。", TextEnUs = "Choose a test branch.",
                    Choices = new List<StoryChoiceDefinition>
                    {
                        new StoryChoiceDefinition { ChoiceId = "left", TextZhCn = "查看左侧", TextEnUs = "Inspect the left side", NextNodeId = "left" },
                        new StoryChoiceDefinition { ChoiceId = "right", TextZhCn = "查看右侧", TextEnUs = "Inspect the right side", NextNodeId = "right" },
                    },
                });
                story.Nodes.Add(CreateDialogue("left", "你选择了左侧。", "You chose the left side.", "merge"));
                story.Nodes.Add(CreateDialogue("right", "你选择了右侧。", "You chose the right side.", "merge"));
                story.Nodes.Add(CreateDialogue("merge", "两条分支在这里汇合。", "Both branches merge here.", "end"));
            }
            story.Nodes.Add(new StoryNodeDefinition { NodeId = "end", Type = StoryNodeType.End });
            return story;
        }

        /// <summary>创建一条带有中英文对白的测试对白节点。</summary>
        /// <param name="nodeId">节点标识。</param>
        /// <param name="textZhCn">中文对白。</param>
        /// <param name="textEnUs">英文对白。</param>
        /// <param name="nextNodeId">下一个节点标识。</param>
        /// <returns>测试对白节点。</returns>
        private static StoryNodeDefinition CreateDialogue(string nodeId, string textZhCn, string textEnUs, string nextNodeId)
        {
            return new StoryNodeDefinition
            {
                NodeId = nodeId,
                Type = StoryNodeType.Dialogue,
                TextZhCn = textZhCn,
                TextEnUs = textEnUs,
                SpeakerTextZhCn = "系统",
                SpeakerTextEnUs = "System",
                NextNodeId = nextNodeId,
            };
        }

        /// <summary>仅用旧文本键补齐缺失语言，保留已编辑的 JSON 文本。</summary>
        /// <param name="zhCn">中文文本。</param>
        /// <param name="enUs">英文文本。</param>
        /// <param name="key">待转换的旧文本键。</param>
        /// <param name="texts">迁移文本索引。</param>
        /// <param name="storyId">剧情标识。</param>
        /// <param name="nodeId">节点或选项定位信息。</param>
        /// <returns>旧键缺失时返回 1，否则返回 0。</returns>
        private static int Fill(ref string zhCn, ref string enUs, string key,
            Dictionary<string, string[]> texts, string storyId, string nodeId)
        {
            if (string.IsNullOrWhiteSpace(key)) return 0;
            bool zhMissing = string.IsNullOrWhiteSpace(zhCn);
            bool enMissing = string.IsNullOrWhiteSpace(enUs);
            if (texts.TryGetValue(key, out string[] values))
            {
                if (zhMissing && !string.IsNullOrWhiteSpace(values[0])) zhCn = values[0];
                if (enMissing && !string.IsNullOrWhiteSpace(values[1])) enUs = values[1];
                return 0;
            }
            // 没有本地化源时仍生成可运行的明确占位文本，避免空对白阻断整个目录。
            zhCn = string.IsNullOrWhiteSpace(zhCn) ? "【待填写剧情文本：" + key + "】" : zhCn;
            enUs = string.IsNullOrWhiteSpace(enUs) ? "[Story text pending: " + key + "]" : enUs;
            Debug.LogWarning("Missing story text key during migration: " + storyId + "/" + nodeId + " -> " + key);
            return 1;
        }

        /// <summary>校验剧情和文件名后写入 authoring JSON 与 runtime JSON。</summary>
        /// <param name="story">待迁移的剧情。</param>
        /// <exception cref="ArgumentException">剧情结构或文件名不合法。</exception>
        private static void WriteStory(StoryDefinition story)
        {
            if (!StoryDefinitionValidator.TryValidate(story, out string error))
                throw new ArgumentException("Story migration validation failed: " + story?.StoryId + ": " + error);
            if (story.StoryId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                story.StoryId == "." || story.StoryId == "..")
                throw new ArgumentException("StoryId is not a safe file name: " + story.StoryId);
            string authoringJson = JsonUtility.ToJson(story, true);
            string runtimeJson = StoryRuntimeSerializer.SerializeEnvelope(story);
            Directory.CreateDirectory(AuthoringFolder);
            Directory.CreateDirectory(RuntimeFolder);
            string authoring = Path.Combine(AuthoringFolder, story.StoryId + ".story.authoring.json");
            string runtime = Path.Combine(RuntimeFolder, story.StoryId + ".story.runtime.json");
            AtomicReplace(authoring, authoringJson);
            AtomicReplace(runtime, runtimeJson);
        }

        /// <summary>使用临时文件和备份文件安全替换迁移产物。</summary>
        /// <param name="target">目标文件。</param>
        /// <param name="content">待写入内容。</param>
        private static void AtomicReplace(string target, string content)
        {
            string temp = target + ".tmp";
            string backup = target + ".bak";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            try
            {
                if (File.Exists(target))
                {
                    File.Replace(temp, target, backup, true);
                    if (File.Exists(backup)) File.Delete(backup);
                }
                else File.Move(temp, target);
            }
            catch
            {
                if (!File.Exists(target) && File.Exists(backup))
                    File.Move(backup, target);
                throw;
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        /// <summary>读取四列 UI.csv，返回 Key、中文和英文文本。</summary>
        /// <param name="path">CSV 文件路径。</param>
        /// <returns>旧键到中英文列的索引。</returns>
        private static Dictionary<string, string[]> ReadCsv(string path)
        {
            var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (!File.Exists(path)) return result;
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                string[] columns = ParseCsvLine(line);
                if (columns.Length >= 4 && !string.IsNullOrWhiteSpace(columns[0]) && columns[0] != "Key")
                {
                    if (result.ContainsKey(columns[0]))
                        Debug.LogError("Duplicate localization key in UI.csv: " + columns[0]);
                    else
                        result.Add(columns[0], new[] { columns[2], columns[3] });
                }
            }
            return result;
        }

        /// <summary>解析支持双引号转义的 CSV 单行。</summary>
        /// <param name="line">待解析的一行。</param>
        /// <returns>解析出的单元格。</returns>
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

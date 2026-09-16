#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Contracts.Content;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>C13-C15 剧情 Authoring 文件编辑与 Runtime 编译窗口。</summary>
    public sealed class StoryAuthoringWindow : EditorWindow
    {
        [SerializeField] private StoryDefinition _definition;
        private string _json = string.Empty;
        private Vector2 _scroll;
        private Vector2 _contentScroll;
        private string _path;
        private int _previewNodeIndex = -1;
        private readonly HashSet<int> _expandedNodes = new HashSet<int>();
        private bool _showJsonPreview;
        private StoryNodeType _newNodeType = StoryNodeType.Dialogue;
        private bool _dirty;

        /// <summary>窗口关闭时提示未保存的剧情编辑。</summary>
        private void OnDisable()
        {
            if (_dirty && _definition != null && EditorUtility.DisplayDialog("Story", "Story has unsaved changes. Save now?", "Save", "Discard"))
                SaveDefinition();
        }

        /// <summary>打开剧情编辑器窗口。</summary>
        [MenuItem("Game/Story Authoring Window")]
        public static void Open() => GetWindow<StoryAuthoringWindow>("Story Authoring");

        /// <summary>绘制文件操作、节点编辑和编译按钮。</summary>
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New", GUILayout.Width(80)))
                NewDefinition();
            if (GUILayout.Button("Open", GUILayout.Width(80)))
                OpenDefinition();
            if (GUILayout.Button("Save", GUILayout.Width(80)))
                SaveDefinition();
            if (GUILayout.Button("Compile", GUILayout.Width(80)))
                CompileDefinition();
            if (GUILayout.Button("Save & Compile", GUILayout.Width(110)))
            {
                SaveDefinition();
                CompileDefinition();
            }
            EditorGUILayout.EndHorizontal();
            if (_definition == null)
            {
                EditorGUILayout.HelpBox("Create or open a story authoring file.", MessageType.Info);
                return;
            }
            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            string storyId = EditorGUILayout.TextField("Story ID", _definition.StoryId);
            string startNode = EditorGUILayout.TextField("Start Node", _definition.StartNodeId);
            _definition.StoryId = storyId;
            _definition.StartNodeId = startNode;
            EditorGUILayout.LabelField("Nodes", _definition.Nodes.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < _definition.Nodes.Count; i++)
            {
                StoryNodeDefinition node = _definition.Nodes[i];
                EditorGUILayout.BeginVertical("box");
                bool expanded = _expandedNodes.Contains(i);
                bool nextExpanded = EditorGUILayout.Foldout(
                    expanded,
                    string.IsNullOrWhiteSpace(node.NodeId) ? "Unnamed Node" : node.NodeId + "  [" + node.Type + "]",
                    true
                );
                if (nextExpanded) _expandedNodes.Add(i); else _expandedNodes.Remove(i);
                if (!nextExpanded)
                {
                    EditorGUILayout.EndVertical();
                    continue;
                }
                node.NodeId = EditorGUILayout.TextField("Node ID", node.NodeId);
                node.Type = (StoryNodeType)EditorGUILayout.EnumPopup("Type", node.Type);
                if (node.Type == StoryNodeType.Dialogue)
                {
                    node.TextZhCn = EditorGUILayout.TextField("Dialogue (zh-CN)", node.TextZhCn);
                    node.TextEnUs = EditorGUILayout.TextField("Dialogue (en-US)", node.TextEnUs);
                }
                if (
                    node.Type == StoryNodeType.Dialogue
                    || node.Type == StoryNodeType.ShowCharacter
                    || node.Type == StoryNodeType.HideCharacter
                    || node.Type == StoryNodeType.MoveCharacter
                )
                {
                    node.SpeakerTextZhCn = EditorGUILayout.TextField("Speaker (zh-CN)", node.SpeakerTextZhCn);
                    node.SpeakerTextEnUs = EditorGUILayout.TextField("Speaker (en-US)", node.SpeakerTextEnUs);
                }
                if (
                    node.Type == StoryNodeType.Dialogue
                    || node.Type == StoryNodeType.ShowCharacter
                    || node.Type == StoryNodeType.HideCharacter
                    || node.Type == StoryNodeType.MoveCharacter
                )
                    node.SpeakerCharacterId = EditorGUILayout.TextField("Speaker Character", node.SpeakerCharacterId);
                if (node.Type == StoryNodeType.Dialogue || node.Type == StoryNodeType.ShowCharacter)
                    node.AppearanceOverride = EditorGUILayout.TextField("Appearance Override", node.AppearanceOverride);
                if (node.Type == StoryNodeType.ShowCg)
                    node.AssetId = EditorGUILayout.TextField("Asset ID", node.AssetId);
                if (node.Type == StoryNodeType.SetBackground)
                    node.BackgroundId = EditorGUILayout.TextField("Background ID", node.BackgroundId);
                if (node.Type == StoryNodeType.MoveCharacter)
                    node.CharacterPosition = (StoryCharacterPosition)
                        EditorGUILayout.EnumPopup("Position", node.CharacterPosition);
                if (node.Type == StoryNodeType.ShowCharacter)
                    node.ExpressionId = EditorGUILayout.TextField("Expression ID", node.ExpressionId);
                if (node.Type == StoryNodeType.PlayAudio)
                {
                    node.AudioId = EditorGUILayout.TextField("Audio ID", node.AudioId);
                    node.AudioKind = (StoryAudioKind)EditorGUILayout.EnumPopup("Audio Kind", node.AudioKind);
                }
                if (node.Type == StoryNodeType.ScreenEffect)
                    node.EffectType = (StoryScreenEffectType)EditorGUILayout.EnumPopup("Effect Type", node.EffectType);
                if (node.Type == StoryNodeType.Wait)
                    node.WaitSeconds = EditorGUILayout.FloatField("Wait Seconds", node.WaitSeconds);
                if (node.Type != StoryNodeType.End)
                    node.NextNodeId = EditorGUILayout.TextField("Next Node", node.NextNodeId);
                if (node.Type == StoryNodeType.Choice)
                {
                    if (node.Choices == null)
                        node.Choices = new System.Collections.Generic.List<StoryChoiceDefinition>();
                    for (int choiceIndex = 0; choiceIndex < node.Choices.Count; choiceIndex++)
                    {
                        StoryChoiceDefinition choice = node.Choices[choiceIndex];
                        choice.ChoiceId = EditorGUILayout.TextField("Choice ID", choice.ChoiceId);
                        choice.TextZhCn = EditorGUILayout.TextField("Choice (zh-CN)", choice.TextZhCn);
                        choice.TextEnUs = EditorGUILayout.TextField("Choice (en-US)", choice.TextEnUs);
                        choice.NextNodeId = EditorGUILayout.TextField("Choice Target", choice.NextNodeId);
                    }
                    if (GUILayout.Button("Add Choice"))
                    {
                        node.Choices.Add(
                            new StoryChoiceDefinition
                            {
                                ChoiceId = "choice_" + node.Choices.Count,
                                TextZhCn = string.Empty,
                                TextEnUs = string.Empty,
                                NextNodeId = _definition.StartNodeId,
                            }
                        );
                    }
                }
                if (GUILayout.Button("Preview Node"))
                    _previewNodeIndex = i;
                if (GUILayout.Button("Remove Node"))
                {
                    Undo.RegisterCompleteObjectUndo(this, "Remove story node");
                    _definition.Nodes.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = i > 0;
                if (GUILayout.Button("Move Up"))
                {
                    Undo.RegisterCompleteObjectUndo(this, "Move story node up");
                    StoryNodeDefinition previous = _definition.Nodes[i - 1];
                    _definition.Nodes[i - 1] = node;
                    _definition.Nodes[i] = previous;
                }
                GUI.enabled = i < _definition.Nodes.Count - 1;
                if (GUILayout.Button("Move Down"))
                {
                    Undo.RegisterCompleteObjectUndo(this, "Move story node down");
                    StoryNodeDefinition next = _definition.Nodes[i + 1];
                    _definition.Nodes[i + 1] = node;
                    _definition.Nodes[i] = next;
                }
                GUI.enabled = true;
                if (GUILayout.Button("Duplicate"))
                {
                    Undo.RegisterCompleteObjectUndo(this, "Duplicate story node");
                    StoryNodeDefinition copy = JsonUtility.FromJson<StoryNodeDefinition>(JsonUtility.ToJson(node));
                    copy.NodeId = node.NodeId + "_copy";
                    _definition.Nodes.Insert(i + 1, copy);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            DrawAddButtons();
            EditorGUILayout.Space();
            DrawPreview();
            _showJsonPreview = EditorGUILayout.Foldout(_showJsonPreview, "Advanced: JSON Preview", true);
            if (_showJsonPreview)
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(120));
                EditorGUILayout.TextArea(_json);
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndScrollView();
            if (GUI.changed)
            {
                _dirty = true;
                RefreshJson();
            }
        }

        /// <summary>创建带起始对白和结束节点的新剧情。</summary>
        private void NewDefinition()
        {
            _definition = new StoryDefinition
            {
                StoryId = "story.new",
                StartNodeId = "start",
                Nodes = new System.Collections.Generic.List<StoryNodeDefinition>
                {
                    new StoryNodeDefinition
                    {
                        NodeId = "start",
                        Type = StoryNodeType.Dialogue,
                        TextZhCn = string.Empty,
                        TextEnUs = string.Empty,
                        NextNodeId = "end",
                    },
                    new StoryNodeDefinition { NodeId = "end", Type = StoryNodeType.End },
                },
            };
            _path = null;
            _expandedNodes.Clear();
            _showJsonPreview = false;
            RefreshJson();
        }

        /// <summary>绘制各类演出节点的添加按钮。</summary>
        private void DrawAddButtons()
        {
            EditorGUILayout.BeginHorizontal();
            _newNodeType = (StoryNodeType)EditorGUILayout.EnumPopup("New Node", _newNodeType);
            if (GUILayout.Button("Add Node", GUILayout.Width(100)))
            {
                var node = new StoryNodeDefinition
                {
                    NodeId = "node_" + _definition.Nodes.Count,
                    Type = _newNodeType,
                    NextNodeId = "end",
                };
                if (_newNodeType == StoryNodeType.Dialogue)
                {
                    node.TextZhCn = string.Empty;
                    node.TextEnUs = string.Empty;
                }
                if (_newNodeType == StoryNodeType.ShowCharacter ||
                    _newNodeType == StoryNodeType.HideCharacter ||
                    _newNodeType == StoryNodeType.MoveCharacter)
                    node.SpeakerCharacterId = "official.character.hani";
                if (_newNodeType == StoryNodeType.ShowCg)
                    node.AssetId = "official.cg.test_01";
                if (_newNodeType == StoryNodeType.SetBackground)
                    node.BackgroundId = "official.background.test_01";
                if (_newNodeType == StoryNodeType.MoveCharacter)
                    node.CharacterPosition = StoryCharacterPosition.Center;
                if (_newNodeType == StoryNodeType.PlayAudio)
                {
                    node.AudioId = "official.audio.bgm_01";
                    node.AudioKind = StoryAudioKind.Music;
                }
                if (_newNodeType == StoryNodeType.ScreenEffect)
                    node.EffectType = StoryScreenEffectType.WhiteFlash;
                if (_newNodeType == StoryNodeType.Wait)
                    node.WaitSeconds = 1f;
                _definition.Nodes.Add(node);
                _expandedNodes.Add(_definition.Nodes.Count - 1);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>绘制选中节点的轻量摘要预览。</summary>
        private void DrawPreview()
        {
            if (_previewNodeIndex < 0 || _previewNodeIndex >= _definition.Nodes.Count)
            {
                EditorGUILayout.HelpBox("Click 'Preview Node' to preview a node summary.", MessageType.Info);
                return;
            }
            StoryNodeDefinition node = _definition.Nodes[_previewNodeIndex];
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Node", node.NodeId);
            EditorGUILayout.LabelField("Type", node.Type.ToString());
            switch (node.Type)
            {
                case StoryNodeType.Dialogue:
                    EditorGUILayout.LabelField("Dialogue (zh-CN)", node.TextZhCn);
                    EditorGUILayout.LabelField("Dialogue (en-US)", node.TextEnUs);
                    EditorGUILayout.LabelField("Speaker (zh-CN)", node.SpeakerTextZhCn);
                    EditorGUILayout.LabelField("Speaker (en-US)", node.SpeakerTextEnUs);
                    break;
                case StoryNodeType.ShowCharacter:
                    EditorGUILayout.LabelField("Character", node.SpeakerCharacterId);
                    EditorGUILayout.LabelField("Appearance", node.AppearanceOverride);
                    break;
                case StoryNodeType.ShowCg:
                    EditorGUILayout.LabelField("Asset", node.AssetId);
                    break;
                case StoryNodeType.SetBackground:
                    EditorGUILayout.LabelField("Background", node.BackgroundId);
                    break;
                case StoryNodeType.HideCharacter:
                    EditorGUILayout.LabelField("Character", node.SpeakerCharacterId);
                    break;
                case StoryNodeType.MoveCharacter:
                    EditorGUILayout.LabelField("Character", node.SpeakerCharacterId);
                    EditorGUILayout.LabelField("Position", node.CharacterPosition.ToString());
                    break;
                case StoryNodeType.PlayAudio:
                    EditorGUILayout.LabelField("Audio", node.AudioId);
                    EditorGUILayout.LabelField("Kind", node.AudioKind.ToString());
                    break;
                case StoryNodeType.ScreenEffect:
                    EditorGUILayout.LabelField("Effect", node.EffectType.ToString());
                    break;
                case StoryNodeType.Wait:
                    EditorGUILayout.LabelField("Seconds", node.WaitSeconds.ToString(CultureInfo.InvariantCulture));
                    break;
                default:
                    EditorGUILayout.LabelField("(no presentation)");
                    break;
            }
            EditorGUILayout.LabelField("Next", node.NextNodeId);
        }

        /// <summary>读取 authoring JSON，失败时保留当前编辑内容。</summary>
        private void OpenDefinition()
        {
            string path = EditorUtility.OpenFilePanel("Open Story", Application.dataPath, "story.authoring.json");
            if (string.IsNullOrEmpty(path))
                return;
            string json = File.ReadAllText(path);
            StoryDefinition candidate = JsonUtility.FromJson<StoryDefinition>(json);
            if (candidate == null)
            {
                EditorUtility.DisplayDialog("Story", "Invalid JSON.", "OK");
                return;
            }
            _definition = candidate;
            _path = path;
            _expandedNodes.Clear();
            _showJsonPreview = false;
            RefreshJson();
        }

        /// <summary>安全写入 authoring JSON。</summary>
        private void SaveDefinition()
        {
            if (_definition == null)
                return;
            if (string.IsNullOrEmpty(_path))
                _path = EditorUtility.SaveFilePanel(
                    "Save Story",
                    Application.dataPath,
                    _definition.StoryId,
                    "story.authoring.json"
                );
            if (string.IsNullOrEmpty(_path))
                return;
            RefreshJson();
            string temp = _path + ".tmp";
            File.WriteAllText(temp, _json);
            if (File.Exists(_path))
                File.Delete(_path);
            File.Move(temp, _path);
            AssetDatabase.Refresh();
            _dirty = false;
        }

        /// <summary>校验并编译到 Generated，失败时不覆盖旧文件。</summary>
        private void CompileDefinition()
        {
            if (_definition == null)
                return;
            if (
                !Game.Content.StoryDefinitionValidator.TryValidate(
                    _definition,
                    null,
                    AssetExists,
                    LocalizationKeyExists,
                    out string error
                )
            )
            {
                EditorUtility.DisplayDialog("Story Compile", error, "OK");
                return;
            }
            // 编译产物写入 Resources/StoryRuntime: 与运行时 GeneratedStoryLoader.ReadAll 一致,
            // 且 Resources 目录随构建打包, 可被游戏运行时读取。
            string folder = Path.Combine(Application.dataPath, "Game/Resources/StoryRuntime");
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, _definition.StoryId + ".story.runtime.json");
            // 写信封而非裸 JSON: 内含 formatVersion 与源摘要, 读取端兼容旧格式。
            string json = Game.Content.StoryRuntimeSerializer.SerializeEnvelope(_definition);
            string temp = target + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(target))
                File.Delete(target);
            File.Move(temp, target);
            AssetDatabase.Refresh();
        }

        /// <summary>刷新窗口内 JSON 预览。</summary>
        private void RefreshJson() => _json = JsonUtility.ToJson(_definition, true);

        /// <summary>
        /// 检查资源稳定 ID 是否已登记: 优先使用 00_Bootstrap GameRoot 引用的 Registry,
        /// 读不到时合并全部 ContentAssetRegistry 资产, 重复 ID 仅警告一次。
        /// </summary>
        /// <param name="id">资源稳定标识。</param>
        /// <returns>存在时为 true; 未找到任何 Registry 时返回 false。</returns>
        private static bool AssetExists(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;
            Game.Content.ContentAssetRegistry gameRootRegistry = FindGameRootRegistry();
            if (gameRootRegistry != null)
                return ContainsId(gameRootRegistry, id);
            string[] guids = AssetDatabase.FindAssets("t:ContentAssetRegistry");
            if (guids.Length == 0)
                return false;
            bool duplicateWarned = false;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var registry = AssetDatabase.LoadAssetAtPath<Game.Content.ContentAssetRegistry>(path);
                if (registry == null)
                    continue;
                if (!duplicateWarned && guids.Length > 1)
                {
                    duplicateWarned = true;
                    Debug.LogWarning(
                        "[StoryCompiler] 未找到 GameRoot 引用的 Registry, 回退合并扫描 "
                            + guids.Length
                            + " 个 Registry 资产; 建议在 GameRoot 上显式指派 Content Asset Registry。"
                    );
                }
                if (ContainsId(registry, id))
                    return true;
            }
            return false;
        }

        /// <summary>从当前 00_Bootstrap 场景的 GameRoot 读取序列化的资源 Registry。</summary>
        /// <returns>GameRoot 引用的 Registry; 未找到 GameRoot 或其引用为空时返回 null。</returns>
        private static Game.Content.ContentAssetRegistry FindGameRootRegistry()
        {
            Game.Bootstrap.GameRoot root = FindObjectOfType<Game.Bootstrap.GameRoot>();
            if (root == null)
                return null;
            var serialized = new SerializedObject(root);
            SerializedProperty property = serialized.FindProperty("contentAssetRegistry");
            return property?.objectReferenceValue as Game.Content.ContentAssetRegistry;
        }

        /// <summary>检查单个 Registry 是否包含指定精灵或音频稳定 ID。</summary>
        /// <param name="registry">目标 Registry。</param>
        /// <param name="id">资源稳定标识。</param>
        /// <returns>包含时为 true。</returns>
        private static bool ContainsId(Game.Content.ContentAssetRegistry registry, string id)
        {
            return HasSprite(registry.Sprites, id) || HasAudio(registry.AudioClips, id);
        }

        /// <summary>检查精灵映射项是否包含指定稳定 ID。</summary>
        /// <param name="entries">精灵映射项列表。</param>
        /// <param name="id">资源稳定标识。</param>
        /// <returns>包含时为 true。</returns>
        private static bool HasSprite(
            System.Collections.Generic.IReadOnlyList<Game.Content.SpriteAssetEntry> entries,
            string id
        )
        {
            if (entries == null)
                return false;
            foreach (Game.Content.SpriteAssetEntry entry in entries)
                if (entry != null && string.Equals(entry.Id, id, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>检查音频映射项是否包含指定稳定 ID。</summary>
        /// <param name="entries">音频映射项列表。</param>
        /// <param name="id">资源稳定标识。</param>
        /// <returns>包含时为 true。</returns>
        private static bool HasAudio(
            System.Collections.Generic.IReadOnlyList<Game.Content.AudioAssetEntry> entries,
            string id
        )
        {
            if (entries == null)
                return false;
            foreach (Game.Content.AudioAssetEntry entry in entries)
                if (entry != null && string.Equals(entry.Id, id, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>
        /// 检查本地化键是否存在于项目 UI String Table。
        /// 本地化资产未生成或表未找到时不阻断（返回 true），避免本地化未导入时影响剧情编译。
        /// </summary>
        /// <param name="key">本地化键。</param>
        /// <returns>键存在于 UI 表时为 true；表缺失时不阻断返回 true。</returns>
        private static bool LocalizationKeyExists(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
            try
            {
                UnityEditor.Localization.StringTableCollection collection =
                    UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection("UI");
                if (collection == null || collection.SharedData == null)
                    return true;
                return collection.SharedData.GetEntry(key) != null;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
#endif

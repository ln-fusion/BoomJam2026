#if UNITY_EDITOR
using System;
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
        private StoryDefinition _definition;
        private string _json = string.Empty;
        private Vector2 _scroll;
        private string _path;
        private int _previewNodeIndex = -1;

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
            EditorGUILayout.EndHorizontal();
            if (_definition == null)
            {
                EditorGUILayout.HelpBox("Create or open a story authoring file.", MessageType.Info);
                return;
            }
            string storyId = EditorGUILayout.TextField("Story ID", _definition.StoryId);
            string startNode = EditorGUILayout.TextField("Start Node", _definition.StartNodeId);
            _definition.StoryId = storyId;
            _definition.StartNodeId = startNode;
            EditorGUILayout.LabelField("Nodes", _definition.Nodes.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < _definition.Nodes.Count; i++)
            {
                StoryNodeDefinition node = _definition.Nodes[i];
                EditorGUILayout.BeginVertical("box");
                node.NodeId = EditorGUILayout.TextField("Node ID", node.NodeId);
                node.Type = (StoryNodeType)EditorGUILayout.EnumPopup("Type", node.Type);
                if (node.Type == StoryNodeType.Dialogue)
                    node.TextKey = EditorGUILayout.TextField("Text Key", node.TextKey);
                if (
                    node.Type == StoryNodeType.Dialogue
                    || node.Type == StoryNodeType.ShowCharacter
                    || node.Type == StoryNodeType.HideCharacter
                    || node.Type == StoryNodeType.MoveCharacter
                )
                    node.SpeakerKey = EditorGUILayout.TextField("Speaker Key", node.SpeakerKey);
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
                        choice.TextKey = EditorGUILayout.TextField("Choice Text", choice.TextKey);
                        choice.NextNodeId = EditorGUILayout.TextField("Choice Target", choice.NextNodeId);
                    }
                    if (GUILayout.Button("Add Choice"))
                    {
                        node.Choices.Add(
                            new StoryChoiceDefinition
                            {
                                ChoiceId = "choice_" + node.Choices.Count,
                                TextKey = "story.choice",
                                NextNodeId = _definition.StartNodeId,
                            }
                        );
                    }
                }
                if (GUILayout.Button("Preview Node"))
                    _previewNodeIndex = i;
                if (GUILayout.Button("Remove Node"))
                {
                    _definition.Nodes.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndVertical();
            }
            DrawAddButtons();
            EditorGUILayout.Space();
            DrawPreview();
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(120));
            EditorGUILayout.TextArea(_json);
            EditorGUILayout.EndScrollView();
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
                        TextKey = "story.new.start",
                        NextNodeId = "end",
                    },
                    new StoryNodeDefinition { NodeId = "end", Type = StoryNodeType.End },
                },
            };
            _path = null;
            RefreshJson();
        }

        /// <summary>绘制各类演出节点的添加按钮。</summary>
        private void DrawAddButtons()
        {
            if (GUILayout.Button("Add Dialogue Node"))
            {
                _definition.Nodes.Add(
                    new StoryNodeDefinition
                    {
                        NodeId = "node_" + _definition.Nodes.Count,
                        Type = StoryNodeType.Dialogue,
                        TextKey = "story.new.text",
                    }
                );
            }
            if (GUILayout.Button("Add Show Character Node"))
            {
                _definition.Nodes.Add(
                    new StoryNodeDefinition
                    {
                        NodeId = "node_" + _definition.Nodes.Count,
                        Type = StoryNodeType.ShowCharacter,
                        SpeakerCharacterId = "official.character.hani",
                        NextNodeId = "end",
                    }
                );
            }
            if (GUILayout.Button("Add Show CG Node"))
            {
                _definition.Nodes.Add(
                    new StoryNodeDefinition
                    {
                        NodeId = "node_" + _definition.Nodes.Count,
                        Type = StoryNodeType.ShowCg,
                        AssetId = "official.cg.test_01",
                        NextNodeId = "end",
                    }
                );
            }
            if (GUILayout.Button("Add Set Background Node"))
            {
                _definition.Nodes.Add(
                    new StoryNodeDefinition
                    {
                        NodeId = "node_" + _definition.Nodes.Count,
                        Type = StoryNodeType.SetBackground,
                        BackgroundId = "official.background.test_01",
                        NextNodeId = "end",
                    }
                );
            }
            if (GUILayout.Button("Add Hide Character Node"))
            {
                _definition.Nodes.Add(
                    new StoryNodeDefinition
                    {
                        NodeId = "node_" + _definition.Nodes.Count,
                        Type = StoryNodeType.HideCharacter,
                        SpeakerCharacterId = "official.character.hani",
                        NextNodeId = "end",
                    }
                );
            }
            if (GUILayout.Button("Add Move Character Node"))
            {
                _definition.Nodes.Add(
                    new StoryNodeDefinition
                    {
                        NodeId = "node_" + _definition.Nodes.Count,
                        Type = StoryNodeType.MoveCharacter,
                        SpeakerCharacterId = "official.character.hani",
                        CharacterPosition = StoryCharacterPosition.Center,
                        NextNodeId = "end",
                    }
                );
            }
            if (GUILayout.Button("Add Play Audio Node"))
            {
                _definition.Nodes.Add(
                    new StoryNodeDefinition
                    {
                        NodeId = "node_" + _definition.Nodes.Count,
                        Type = StoryNodeType.PlayAudio,
                        AudioId = "official.audio.bgm_01",
                        AudioKind = StoryAudioKind.Music,
                        NextNodeId = "end",
                    }
                );
            }
            if (GUILayout.Button("Add Screen Effect Node"))
            {
                _definition.Nodes.Add(
                    new StoryNodeDefinition
                    {
                        NodeId = "node_" + _definition.Nodes.Count,
                        Type = StoryNodeType.ScreenEffect,
                        EffectType = StoryScreenEffectType.WhiteFlash,
                        NextNodeId = "end",
                    }
                );
            }
            if (GUILayout.Button("Add Wait Node"))
            {
                _definition.Nodes.Add(
                    new StoryNodeDefinition
                    {
                        NodeId = "node_" + _definition.Nodes.Count,
                        Type = StoryNodeType.Wait,
                        WaitSeconds = 1f,
                        NextNodeId = "end",
                    }
                );
            }
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
                    EditorGUILayout.LabelField("Text Key", node.TextKey);
                    EditorGUILayout.LabelField("Speaker", node.SpeakerKey);
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
        }

        /// <summary>校验并编译到 Generated，失败时不覆盖旧文件。</summary>
        private void CompileDefinition()
        {
            if (_definition == null)
                return;
            if (!Game.Content.StoryDefinitionValidator.TryValidate(_definition, null, AssetExists, out string error))
            {
                EditorUtility.DisplayDialog("Story Compile", error, "OK");
                return;
            }
            string folder = Path.Combine(Application.dataPath, "Game/Content/Generated");
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, _definition.StoryId + ".story.runtime.json");
            RefreshJson();
            string temp = target + ".tmp";
            File.WriteAllText(temp, _json);
            if (File.Exists(target))
                File.Delete(target);
            File.Move(temp, target);
            AssetDatabase.Refresh();
        }

        /// <summary>刷新窗口内 JSON 预览。</summary>
        private void RefreshJson() => _json = JsonUtility.ToJson(_definition, true);

        /// <summary>检查资源稳定 ID 是否存在于第一个 ContentAssetRegistry 资源中。</summary>
        /// <param name="id">资源稳定标识。</param>
        /// <returns>存在时为 true；未找到 Registry 时返回 false。</returns>
        private static bool AssetExists(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;
            string[] guids = AssetDatabase.FindAssets("t:ContentAssetRegistry");
            if (guids.Length == 0)
                return false;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var registry = AssetDatabase.LoadAssetAtPath<Game.Content.ContentAssetRegistry>(path);
            if (registry == null)
                return false;
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
    }
}
#endif

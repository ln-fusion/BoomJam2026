#if UNITY_EDITOR
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
        private bool _dirty;

        /// <summary>打开剧情编辑器窗口。</summary>
        [MenuItem("Game/Story Authoring Window")]
        public static void Open() => GetWindow<StoryAuthoringWindow>("Story Authoring");

        /// <summary>绘制文件操作、节点编辑和编译按钮。</summary>
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New", GUILayout.Width(80))) NewDefinition();
            if (GUILayout.Button("Open", GUILayout.Width(80))) OpenDefinition();
            if (GUILayout.Button("Save", GUILayout.Width(80))) SaveDefinition();
            if (GUILayout.Button("Compile", GUILayout.Width(80))) CompileDefinition();
            EditorGUILayout.EndHorizontal();
            if (_definition == null)
            {
                EditorGUILayout.HelpBox("Create or open a story authoring file.", MessageType.Info);
                return;
            }
            string storyId = EditorGUILayout.TextField("Story ID", _definition.StoryId);
            string startNode = EditorGUILayout.TextField("Start Node", _definition.StartNodeId);
            _dirty |= storyId != _definition.StoryId || startNode != _definition.StartNodeId;
            _definition.StoryId = storyId;
            _definition.StartNodeId = startNode;
            EditorGUILayout.LabelField("Nodes", _definition.Nodes.Count.ToString());
            for (int i = 0; i < _definition.Nodes.Count; i++)
            {
                StoryNodeDefinition node = _definition.Nodes[i];
                EditorGUILayout.BeginVertical("box");
                node.NodeId = EditorGUILayout.TextField("Node ID", node.NodeId);
                node.Type = (StoryNodeType)EditorGUILayout.EnumPopup("Type", node.Type);
                node.TextKey = EditorGUILayout.TextField("Text Key", node.TextKey);
                node.NextNodeId = EditorGUILayout.TextField("Next Node", node.NextNodeId);
                if (node.Type == StoryNodeType.Choice)
                {
                    if (node.Choices == null) node.Choices = new System.Collections.Generic.List<StoryChoiceDefinition>();
                    for (int choiceIndex = 0; choiceIndex < node.Choices.Count; choiceIndex++)
                    {
                        StoryChoiceDefinition choice = node.Choices[choiceIndex];
                        choice.ChoiceId = EditorGUILayout.TextField("Choice ID", choice.ChoiceId);
                        choice.TextKey = EditorGUILayout.TextField("Choice Text", choice.TextKey);
                        choice.NextNodeId = EditorGUILayout.TextField("Choice Target", choice.NextNodeId);
                    }
                    if (GUILayout.Button("Add Choice"))
                    {
                        node.Choices.Add(new StoryChoiceDefinition { ChoiceId = "choice_" + node.Choices.Count,
                            TextKey = "story.choice", NextNodeId = _definition.StartNodeId });
                        _dirty = true;
                    }
                }
                if (GUILayout.Button("Remove Node")) { _definition.Nodes.RemoveAt(i); i--; }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("Add Dialogue Node"))
            {
                _definition.Nodes.Add(new StoryNodeDefinition { NodeId = "node_" + _definition.Nodes.Count,
                    Type = StoryNodeType.Dialogue, TextKey = "story.new.text" });
                _dirty = true;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(120));
            EditorGUILayout.TextArea(_json);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>创建带起始对白和结束节点的新剧情。</summary>
        private void NewDefinition()
        {
            _definition = new StoryDefinition { StoryId = "story.new", StartNodeId = "start",
                Nodes = new System.Collections.Generic.List<StoryNodeDefinition>
                {
                    new StoryNodeDefinition { NodeId = "start", Type = StoryNodeType.Dialogue,
                        TextKey = "story.new.start", NextNodeId = "end" },
                    new StoryNodeDefinition { NodeId = "end", Type = StoryNodeType.End }
                }};
            _path = null;
            _dirty = false;
            RefreshJson();
        }

        /// <summary>读取 authoring JSON，失败时保留当前编辑内容。</summary>
        private void OpenDefinition()
        {
            string path = EditorUtility.OpenFilePanel("Open Story", Application.dataPath, "story.authoring.json");
            if (string.IsNullOrEmpty(path)) return;
            string json = File.ReadAllText(path);
            StoryDefinition candidate = JsonUtility.FromJson<StoryDefinition>(json);
            if (candidate == null) { EditorUtility.DisplayDialog("Story", "Invalid JSON.", "OK"); return; }
            _definition = candidate; _path = path; RefreshJson();
        }

        /// <summary>安全写入 authoring JSON。</summary>
        private void SaveDefinition()
        {
            if (_definition == null) return;
            if (string.IsNullOrEmpty(_path))
                _path = EditorUtility.SaveFilePanel("Save Story", Application.dataPath,
                    _definition.StoryId, "story.authoring.json");
            if (string.IsNullOrEmpty(_path)) return;
            RefreshJson();
            string temp = _path + ".tmp";
            File.WriteAllText(temp, _json);
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temp, _path);
            _dirty = false;
            AssetDatabase.Refresh();
        }

        /// <summary>校验并编译到 Generated，失败时不覆盖旧文件。</summary>
        private void CompileDefinition()
        {
            if (_definition == null) return;
            if (!Game.Content.StoryDefinitionValidator.TryValidate(_definition, out string error))
            { EditorUtility.DisplayDialog("Story Compile", error, "OK"); return; }
            string folder = Path.Combine(Application.dataPath, "Game/Content/Generated");
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, _definition.StoryId + ".story.runtime.json");
            RefreshJson();
            string temp = target + ".tmp";
            File.WriteAllText(temp, _json);
            if (File.Exists(target)) File.Delete(target);
            File.Move(temp, target);
            _dirty = false;
            AssetDatabase.Refresh();
        }

        /// <summary>刷新窗口内 JSON 预览。</summary>
        private void RefreshJson() => _json = JsonUtility.ToJson(_definition, true);
    }
}
#endif

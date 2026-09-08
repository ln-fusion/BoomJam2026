using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Contracts.Content;
using UnityEngine;

namespace Game.Content
{
    /// <summary>
    /// 剧情编译产物信封: 记录格式版本与源内容摘要, 供编译器与播放链共用。
    /// </summary>
    [Serializable]
    public sealed class StoryRuntimeEnvelope
    {
        /// <summary>信封格式版本; 当前运行时只接受 1。</summary>
        public int FormatVersion = 1;

        /// <summary>
        /// 源 authoring 内容按节点稳定排序后序列化的 SHA-256 摘要（前 16 位十六进制）。
        /// </summary>
        public string SourceHash;

        /// <summary>编译后的剧情定义。</summary>
        public StoryDefinition Story;
    }

    /// <summary>
    /// 剧情 Authoring 到 Runtime 信封的序列化与兼容解析。
    /// </summary>
    /// <remarks>
    /// Generated 文件写为 <see cref="StoryRuntimeEnvelope"/>; 读取时兼容旧版裸
    /// <see cref="StoryDefinition"/> JSON, 保证既有内容不回退破坏。
    /// </remarks>
    public static class StoryRuntimeSerializer
    {
        /// <summary>把剧情定义序列化为信封 JSON 文本。</summary>
        /// <param name="story">待编译的剧情定义。</param>
        /// <returns>信封 JSON 文本。</returns>
        public static string SerializeEnvelope(StoryDefinition story)
        {
            if (story == null)
                throw new ArgumentNullException(nameof(story));
            var envelope = new StoryRuntimeEnvelope
            {
                FormatVersion = 1,
                SourceHash = ComputeSourceHash(story),
                Story = story,
            };
            return JsonUtility.ToJson(envelope, true);
        }

        /// <summary>尝试解析信封或旧版裸 JSON。</summary>
        /// <param name="json">Generated 文件文本。</param>
        /// <param name="story">解析出的剧情定义; 失败时为 null。</param>
        /// <returns>解析成功返回 true, 否则返回 false。</returns>
        public static bool TryDeserialize(string json, out StoryDefinition story)
        {
            story = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                // JsonUtility 对缺失的引用字段会创建空壳实例: 需以 StoryId 非空确认信封命中,
                // 否则裸 StoryDefinition JSON 会被误判并跳过回退解析。
                StoryRuntimeEnvelope envelope = JsonUtility.FromJson<StoryRuntimeEnvelope>(json);
                if (envelope?.Story != null && !string.IsNullOrWhiteSpace(envelope.Story.StoryId))
                {
                    story = envelope.Story;
                    return true;
                }
                story = JsonUtility.FromJson<StoryDefinition>(json);
                return story != null;
            }
            catch (Exception)
            {
                story = null;
                return false;
            }
        }

        /// <summary>计算源内容摘要: 节点按 NodeId 稳定排序后序列化的 SHA-256 前 16 位。</summary>
        /// <param name="story">待摘要的剧情定义。</param>
        /// <returns>16 位十六进制摘要。</returns>
        public static string ComputeSourceHash(StoryDefinition story)
        {
            if (story == null)
                throw new ArgumentNullException(nameof(story));
            StoryDefinition sorted = WithSortedNodes(story);
            string payload = JsonUtility.ToJson(sorted, false);
            using var sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var builder = new StringBuilder(16);
            foreach (byte part in digest)
            {
                builder.Append(part.ToString("x2", CultureInfo.InvariantCulture));
                if (builder.Length >= 16)
                    break;
            }
            return builder.ToString();
        }

        /// <summary>创建节点按 NodeId 稳定排序的剧情副本（节点引用共享, 仅用于摘要）。</summary>
        /// <param name="story">源剧情定义。</param>
        /// <returns>节点有序的剧情副本。</returns>
        private static StoryDefinition WithSortedNodes(StoryDefinition story)
        {
            var nodes = new List<StoryNodeDefinition>();
            if (story.Nodes != null)
                foreach (StoryNodeDefinition node in story.Nodes)
                    if (node != null)
                        nodes.Add(node);
            nodes.Sort((a, b) => string.CompareOrdinal(a.NodeId, b.NodeId));
            return new StoryDefinition
            {
                Header = story.Header,
                StoryId = story.StoryId,
                StartNodeId = story.StartNodeId,
                Nodes = nodes,
            };
        }
    }
}

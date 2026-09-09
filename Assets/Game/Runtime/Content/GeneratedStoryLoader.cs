using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using UnityEngine;

namespace Game.Content
{
    /// <summary>
    /// 从 Resources/StoryRuntime 目录读取编辑器编译产出的剧情定义文件。
    /// </summary>
    /// <remarks>
    /// 每个 TextAsset 是一份序列化的 <see cref="StoryDefinition"/>（由剧情编辑器编译产出）。
    /// 加载时优先于测试剧情。
    /// </remarks>
    public static class GeneratedStoryLoader
    {
        private const string RuntimeFolder = "StoryRuntime";

        /// <summary>读取目录下全部剧情定义文件并反序列化, 非法文件跳过并记录日志。</summary>
        /// <returns>按 StoryId 索引的剧情定义；无文件时为空字典。</returns>
        public static IReadOnlyDictionary<string, StoryDefinition> LoadAll()
        {
            var result = new Dictionary<string, StoryDefinition>(StringComparer.Ordinal);
            TextAsset[] assets = Resources.LoadAll<TextAsset>(RuntimeFolder);
            if (assets == null)
                return result;
            foreach (TextAsset asset in assets)
            {
                if (asset == null)
                    continue;
                if (
                    StoryRuntimeSerializer.TryDeserialize(asset.text, out StoryDefinition story)
                    && story != null
                    && !string.IsNullOrWhiteSpace(story.StoryId)
                )
                {
                    result[story.StoryId] = story;
                }
                else
                {
                    Debug.LogWarning("[GeneratedStoryLoader] 跳过非法剧情文件: " + asset.name);
                }
            }
            return result;
        }
    }
}

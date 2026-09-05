using System;
using UnityEngine;

namespace Game.Editor.Level
{
    /// <summary>
    /// 关卡 Authoring 数据的 JSON 序列化与兼容解析。
    /// </summary>
    public static class LevelAuthoringSerializer
    {
        /// <summary>当前 Authoring 文件格式版本。</summary>
        public const int CurrentFormatVersion = 1;

        /// <summary>把 Authoring 数据序列化为格式化 JSON 文本。</summary>
        /// <param name="data">待序列化数据。</param>
        /// <returns>格式化 JSON 文本。</returns>
        public static string Serialize(LevelAuthoringData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            return JsonUtility.ToJson(data, true);
        }

        /// <summary>解析 Authoring JSON; 格式版本不受支持时失败。</summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="data">解析出的数据; 失败时为 null。</param>
        /// <returns>解析成功返回 true。</returns>
        public static bool TryDeserialize(string json, out LevelAuthoringData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                LevelAuthoringData candidate = JsonUtility.FromJson<LevelAuthoringData>(json);
                if (candidate == null || candidate.FormatVersion != CurrentFormatVersion)
                    return false;
                data = candidate;
                return true;
            }
            catch (Exception)
            {
                data = null;
                return false;
            }
        }
    }
}

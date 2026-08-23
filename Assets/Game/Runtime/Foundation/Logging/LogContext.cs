using System.Collections.Generic;

namespace Game.Foundation
{
    /// <summary>
    /// 结构化日志上下文：随日志附带的定位信息，不得包含敏感平台令牌。
    /// </summary>
    /// <remarks>
    /// 参见技术设计文档 §14.1：日志包含 BuildVersion、ContentRevision、LevelId、RunId、
    /// SaveRevision 和物理 Profile Hash；此处用键值对承载，缺失字段留空。
    /// </remarks>
    public sealed class LogContext
    {
        /// <summary>空上下文（无附加字段）。</summary>
        public static LogContext Empty { get; } = new LogContext();

        private readonly Dictionary<string, string> _fields = new Dictionary<string, string>();

        /// <summary>构建版本号（BuildVersion）。</summary>
        public string BuildVersion => Get("BuildVersion");

        /// <summary>内容修订号（ContentRevision）。</summary>
        public string ContentRevision => Get("ContentRevision");

        /// <summary>获取指定字段值；不存在返回空串。</summary>
        /// <param name="key">字段名。</param>
        /// <returns>字段值；不存在时返回空串。</returns>
        public string Get(string key) => _fields.TryGetValue(key, out var value) ? value : string.Empty;

        /// <summary>带指定字段创建新上下文（不可变，不修改原实例）。</summary>
        /// <param name="key">字段名。</param>
        /// <param name="value">字段值。</param>
        /// <returns>包含新字段的上下文副本。</returns>
        public LogContext With(string key, string value)
        {
            var copy = new LogContext();
            foreach (var pair in _fields)
            {
                copy._fields[pair.Key] = pair.Value;
            }

            copy._fields[key] = value;
            return copy;
        }

        /// <summary>格式化所有字段为日志前缀文本（如 [BuildVersion=0.1.0]）。</summary>
        /// <returns>格式化后的上下文前缀。</returns>
        public string FormatPrefix()
        {
            if (_fields.Count == 0)
            {
                return "[]";
            }

            var sb = new System.Text.StringBuilder("[");
            foreach (var pair in _fields)
            {
                sb.Append(pair.Key).Append('=').Append(pair.Value).Append(' ');
            }

            sb.Length -= 1; // 去掉末尾空格
            return sb.Append(']').ToString();
        }
    }
}

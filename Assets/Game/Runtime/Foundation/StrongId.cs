using System;

namespace Game.Foundation
{
    /// <summary>
    /// 强类型 ID 基类：以 string 承载稳定内容标识，禁止不同 ID 以裸 string 互传.
    /// </summary>
    /// <typeparam name="TSelf">继承类型自身，用于实现同类型比较</typeparam>
    [Serializable]
    public abstract class StrongId<TSelf>
        where TSelf : StrongId<TSelf>
    {
        /// <summary>稳定 ID 值（小写命名空间形式，如 official.level.factory_001）。</summary>
        public string Value { get; }

        /// <summary>是否为空 ID。</summary>
        public bool IsEmpty => string.IsNullOrEmpty(Value);

        /// <summary>创建并校验一个强类型稳定 ID。</summary>
        /// <param name="value">稳定 ID 字符串。</param>
        protected StrongId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A stable ID is required.", nameof(value));

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("A stable ID cannot contain surrounding whitespace.", nameof(value));

            Value = value;
        }

        /// <summary>按序数字符串比较两个同类型 ID。</summary>
        /// <param name="other">待比较的另一个 ID。</param>
        /// <returns>值相同返回 true，否则返回 false。</returns>
        public bool Equals(StrongId<TSelf> other) =>
            other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

        /// <summary>判断对象是否为相同强类型 ID。</summary>
        /// <param name="obj">待比较对象。</param>
        /// <returns>类型和值均相同返回 true，否则返回 false。</returns>
        public override bool Equals(object obj) => obj is StrongId<TSelf> other && Equals(other);

        /// <summary>返回稳定 ID 值的哈希码。</summary>
        public override int GetHashCode() => Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        /// <summary>返回原始 ID 字符串。</summary>
        public override string ToString() => Value;

        /// <summary>判断两个 ID 是否相等。</summary>
        /// <param name="left">左侧 ID。</param>
        /// <param name="right">右侧 ID。</param>
        /// <returns>两个 ID 相等返回 true。</returns>
        public static bool operator ==(StrongId<TSelf> left, StrongId<TSelf> right) =>
            left is null ? right is null : left.Equals(right);

        /// <summary>判断两个 ID 是否不相等。</summary>
        /// <param name="left">左侧 ID。</param>
        /// <param name="right">右侧 ID。</param>
        /// <returns>两个 ID 不相等返回 true。</returns>
        public static bool operator !=(StrongId<TSelf> left, StrongId<TSelf> right) => !(left == right);
    }
}

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
        /// <summary>稳定 ID 值（小写命名空间形式，如 official.level.factory_001）</summary>
        public string Value { get; }

        /// <summary>是否为空 ID</summary>
        public bool IsEmpty => string.IsNullOrEmpty(Value);

        /// <param name="value">稳定 ID 字符串</param>
        protected StrongId(string value)
        {
            Value = value ?? string.Empty;
        }

        /// <summary>比较是否为空值</summary>
        public bool Equals(StrongId<TSelf> other) =>
            other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is StrongId<TSelf> other && Equals(other);

        public override int GetHashCode() => Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        /// <summary>返回原始 ID 字符串</summary>
        public override string ToString() => Value;

        public static bool operator ==(StrongId<TSelf> left, StrongId<TSelf> right) =>
            left is null ? right is null : left.Equals(right);

        public static bool operator !=(StrongId<TSelf> left, StrongId<TSelf> right) => !(left == right);
    }
}

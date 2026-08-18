using System;

namespace Game.Foundation.Results
{
    [Serializable]
    public readonly struct ErrorCode : IEquatable<ErrorCode>
    {
        public static readonly ErrorCode None = new ErrorCode(ErrorCategory.None, "none");

        public ErrorCategory Category { get; }
        public string Value { get; }

        public ErrorCode(ErrorCategory category, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("An error code value is required.", nameof(value));

            Category = category;
            Value = value;
        }

        public bool Equals(ErrorCode other)
        {
            return Category == other.Category &&
                   string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ErrorCode other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Category * 397) ^ (Value == null ? 0 : Value.GetHashCode());
            }
        }

        public override string ToString()
        {
            return $"{Category}:{Value}";
        }

        public static bool operator ==(ErrorCode left, ErrorCode right) => left.Equals(right);
        public static bool operator !=(ErrorCode left, ErrorCode right) => !left.Equals(right);
    }
}

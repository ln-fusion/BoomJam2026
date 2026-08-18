using System;

namespace Game.Foundation.Ids
{
    internal static class StableIdRules
    {
        public static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A stable ID is required.", parameterName);

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("A stable ID cannot contain surrounding whitespace.", parameterName);

            return value;
        }
    }
}

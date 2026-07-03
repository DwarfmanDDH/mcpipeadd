using System;

namespace McPipeAdd
{
    public static class TextUtil
    {
        public static string Clean(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        public static bool SameText(string a, string b)
        {
            return Clean(a) == Clean(b);
        }

        public static bool Contains(string source, string value)
        {
            return source != null &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string NullText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<blank>" : value;
        }

        public static string Normalize(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Trim()
                .ToUpperInvariant();
        }

        public static string NormalizeDiameter(string value)
        {
            return (value ?? string.Empty)
                .Replace("\"", "")
                .Replace("in", "")
                .Replace(" ", "")
                .Trim()
                .ToUpperInvariant();
        }

        public static bool SameNominalDiameter(string a, string b)
        {
            return NormalizeDiameter(a) == NormalizeDiameter(b);
        }
    }
}
using System;

namespace Tesserae.A2UI
{
    /// <summary>The protocol's value coercions (DynamicBoolean, DynamicString, DynamicNumber).</summary>
    public static class Coerce
    {
        /// <summary>
        /// Any value to boolean: booleans stay, a ValidationResult becomes its <c>valid</c>, "true"/"false" (any case) parse,
        /// 0 is false and any other number true, everything else (other strings, null, absent, objects, arrays) is false.
        /// </summary>
        public static bool ToBool(JsonValue v)
        {
            if (v is null) return false;
            switch (v.Kind)
            {
                case JsonKind.Boolean: return v.BoolValue;
                case JsonKind.Number:  return v.NumberValue != 0;
                case JsonKind.String:  return string.Equals(v.StringValue, "true", StringComparison.OrdinalIgnoreCase);
                case JsonKind.Object:
                    var valid = v.Get("valid");
                    return valid is object && valid.IsBool && valid.BoolValue;
                default: return false;
            }
        }

        public static string ToText(JsonValue v) => v is null ? "" : v.ToDisplayString();

        /// <summary>A number from a number or a numeric string; null when it is neither.</summary>
        public static double? ToNumber(JsonValue v)
        {
            if (v is null) return null;
            if (v.IsNumber) return v.NumberValue;
            if (v.IsString && double.TryParse(v.StringValue.Trim(), out var d)) return d;
            return null;
        }

        public static JsonValue Validation(bool valid, string message = null, string code = null)
        {
            var o = JsonValue.NewObject().Set("valid", JsonValue.Bool(valid));
            if (!valid)
            {
                if (code is object) o.Set("code", JsonValue.String(code));
                if (message is object) o.Set("message", JsonValue.String(message));
                o.Set("severity", JsonValue.String("error"));
            }
            return o;
        }
    }
}

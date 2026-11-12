using System;
using System.Text;

namespace Tesserae.A2UI
{
    /// <summary>The functions of the A2UI basic catalog (v1.0).</summary>
    internal static class BasicFunctions
    {
        public static void Register(Catalog c)
        {
            c.Function(new FunctionDefinition("required", Required) { ReturnType = "validationResult" });
            c.Function(new FunctionDefinition("regex", Regex) { ReturnType = "validationResult" });
            c.Function(new FunctionDefinition("length", Length) { ReturnType = "validationResult" });
            c.Function(new FunctionDefinition("numeric", Numeric) { ReturnType = "validationResult" });
            c.Function(new FunctionDefinition("email", Email) { ReturnType = "validationResult" });
            c.Function(new FunctionDefinition("formatString", FormatString) { ReturnType = "string" });
            c.Function(new FunctionDefinition("formatNumber", FormatNumber) { ReturnType = "string" });
            c.Function(new FunctionDefinition("formatCurrency", FormatCurrency) { ReturnType = "string" });
            c.Function(new FunctionDefinition("formatDate", FormatDate) { ReturnType = "string" });
            c.Function(new FunctionDefinition("pluralize", Pluralize) { ReturnType = "string" });
            c.Function(new FunctionDefinition("openUrl", OpenUrl) { ReturnType = "void", RequiresUserActivation = true });
            c.Function(new FunctionDefinition("and", And) { ReturnType = "boolean" });
            c.Function(new FunctionDefinition("or", Or) { ReturnType = "boolean" });
            c.Function(new FunctionDefinition("not", Not) { ReturnType = "boolean" });
        }

        // ------------------------------------------------------------------ validation

        private static JsonValue Required(EvalContext ctx, JsonValue a)
        {
            var v = a.Get("value");
            var ok = v is object && !v.IsNull && !(v.IsString && v.StringValue.Length == 0) && !(v.IsArray && v.Count == 0);
            return Coerce.Validation(ok, null, "REQUIRED");
        }

        private static JsonValue Regex(EvalContext ctx, JsonValue a)
        {
            var value = Coerce.ToText(a.Get("value"));
            return Coerce.Validation(RegexDialect.IsMatch(value, a.GetString("pattern")), null, "PATTERN_MISMATCH");
        }

        private static JsonValue Length(EvalContext ctx, JsonValue a)
        {
            var len = Coerce.ToText(a.Get("value")).Length;
            var ok  = (!a.Has("min") || len >= a.GetNumber("min")) && (!a.Has("max") || len <= a.GetNumber("max"));
            return Coerce.Validation(ok, null, "LENGTH");
        }

        private static JsonValue Numeric(EvalContext ctx, JsonValue a)
        {
            var n = Coerce.ToNumber(a.Get("value"));
            var ok = n.HasValue && (!a.Has("min") || n.Value >= a.GetNumber("min")) && (!a.Has("max") || n.Value <= a.GetNumber("max"));
            return Coerce.Validation(ok, null, "OUT_OF_RANGE");
        }

        private static JsonValue Email(EvalContext ctx, JsonValue a)
        {
            var s = Coerce.ToText(a.Get("value"));
            var at = s.IndexOf('@');
            var ok = at > 0 && at == s.LastIndexOf('@') && s.IndexOf(' ') < 0 && s.IndexOf('.', at + 1) > at + 1 && !s.EndsWith(".");
            return Coerce.Validation(ok, null, "INVALID_EMAIL");
        }

        // ------------------------------------------------------------------ formatting

        private static JsonValue FormatString(EvalContext ctx, JsonValue a) => JsonValue.String(Expressions.Interpolate(Coerce.ToText(a.Get("value")), ctx));

        private static int Decimals(JsonValue a, int fallback)
        {
            var d = Coerce.ToNumber(a.Get("decimals"));
            return d.HasValue ? Math.Max(0, Math.Min(20, (int)d.Value)) : fallback;
        }

        private static JsonValue FormatNumber(EvalContext ctx, JsonValue a)
        {
            var n = Coerce.ToNumber(a.Get("value"));
            if (!n.HasValue) return JsonValue.String("");
            var grouping = !a.Has("grouping") || Coerce.ToBool(a.Get("grouping"));
            var hasDec   = a.Has("decimals");
            var d        = Decimals(a, 0);
            return JsonValue.String(Js.FormatNumber(n.Value, hasDec ? d : 0, hasDec ? d : 3, grouping));
        }

        private static JsonValue FormatCurrency(EvalContext ctx, JsonValue a)
        {
            var n = Coerce.ToNumber(a.Get("value"));
            if (!n.HasValue) return JsonValue.String("");
            var grouping = !a.Has("grouping") || Coerce.ToBool(a.Get("grouping"));
            var hasDec   = a.Has("decimals");
            var d        = Decimals(a, 2);
            var code     = Coerce.ToText(a.Get("currency"));
            var s        = Js.FormatCurrency(n.Value, string.IsNullOrEmpty(code) ? "USD" : code, hasDec ? d : -1, hasDec ? d : -1, grouping);
            return JsonValue.String(s ?? (code + " " + JsonValue.FormatNumber(n.Value)));
        }

        private static JsonValue Pluralize(EvalContext ctx, JsonValue a)
        {
            var n = Coerce.ToNumber(a.Get("value")) ?? 0;
            var category = Js.PluralCategory(n);
            if (n == 0 && a.Has("zero")) category = "zero";
            var s = a.GetString(category) ?? a.GetString("other") ?? "";
            return JsonValue.String(s);
        }

        private static JsonValue FormatDate(EvalContext ctx, JsonValue a)
        {
            var v = a.Get("value");
            if (v is null || v.IsNull) return JsonValue.String("");

            var parts = v.IsNumber ? Js.DatePartsFromMillis(v.NumberValue) : Js.DateParts(Coerce.ToText(v));
            if (parts is null) return JsonValue.String("");

            return JsonValue.String(Tr35(parts, a.GetString("format") ?? "yyyy-MM-dd"));
        }

        /// <summary>Formats date parts with a Unicode TR35 pattern (the tokens the catalog documents; text in single quotes is literal).</summary>
        internal static string Tr35(double[] p, string pattern)
        {
            int year = (int)p[0], month = (int)p[1], day = (int)p[2], dow = (int)p[3], hour = (int)p[4], min = (int)p[5], sec = (int)p[6];
            var sb = new StringBuilder();
            var i  = 0;

            while (i < pattern.Length)
            {
                var c = pattern[i];

                if (c == '\'')
                {
                    if (i + 1 < pattern.Length && pattern[i + 1] == '\'') { sb.Append('\''); i += 2; continue; }
                    i++;
                    while (i < pattern.Length && pattern[i] != '\'') sb.Append(pattern[i++]);
                    i++;
                    continue;
                }

                if (!char.IsLetter(c)) { sb.Append(c); i++; continue; }

                var n = 1;
                while (i + n < pattern.Length && pattern[i + n] == c) n++;
                i += n;

                switch (c)
                {
                    case 'y': sb.Append(n == 2 ? Pad(year % 100, 2) : Pad(year, n)); break;
                    case 'M': sb.Append(n <= 2 ? Pad(month + 1, n) : Js.MonthName(month, n >= 4)); break;
                    case 'L': sb.Append(n <= 2 ? Pad(month + 1, n) : Js.MonthName(month, n >= 4)); break;
                    case 'd': sb.Append(Pad(day, n)); break;
                    case 'E': sb.Append(Js.WeekdayName(dow, n >= 4)); break;
                    case 'h': sb.Append(Pad(hour % 12 == 0 ? 12 : hour % 12, n)); break;
                    case 'H': sb.Append(Pad(hour, n)); break;
                    case 'm': sb.Append(Pad(min, n)); break;
                    case 's': sb.Append(Pad(sec, n)); break;
                    case 'a': sb.Append(hour < 12 ? "AM" : "PM"); break;
                    default:  sb.Append(c, n); break;
                }
            }

            return sb.ToString();
        }

        private static string Pad(int v, int width)
        {
            var s = v.ToString();
            while (s.Length < width) s = "0" + s;
            return s;
        }

        // ------------------------------------------------------------------ actions and logic

        private static JsonValue OpenUrl(EvalContext ctx, JsonValue a)
        {
            var raw = Coerce.ToText(a.Get("url")).Trim();
            var abs = Js.ResolveUrl(raw);
            if (abs is null || !(abs.StartsWith("https://") || abs.StartsWith("http://")))
                throw new A2UIFunctionException(A2UIError.ExpressionError, "openUrl only opens http and https URLs");
            Js.OpenUrl(abs);
            return JsonValue.Null;
        }

        private static JsonValue And(EvalContext ctx, JsonValue a)
        {
            var values = a.Get("values");
            if (values is null || !values.IsArray) return JsonValue.False;
            foreach (var v in values.Items) if (!Coerce.ToBool(v)) return JsonValue.False;
            return JsonValue.True;
        }

        private static JsonValue Or(EvalContext ctx, JsonValue a)
        {
            var values = a.Get("values");
            if (values is null || !values.IsArray) return JsonValue.False;
            foreach (var v in values.Items) if (Coerce.ToBool(v)) return JsonValue.True;
            return JsonValue.False;
        }

        private static JsonValue Not(EvalContext ctx, JsonValue a) => JsonValue.Bool(!Coerce.ToBool(a.Get("value")));
    }
}

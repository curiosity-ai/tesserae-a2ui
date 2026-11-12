using System;
using System.Collections.Generic;
using System.Text;

namespace Tesserae.A2UI
{
    /// <summary>
    /// Evaluates the protocol's dynamic values: a literal, a data binding <c>{"@path": ...}</c>, or a function call
    /// <c>{"@call": ..., "args": ...}</c>, plus the <c>${...}</c> expression language inside formatString. Every
    /// path an evaluation reads is recorded on the <see cref="EvalContext"/>.
    /// </summary>
    public static class Expressions
    {
        public static bool IsBinding(JsonValue v) => v is object && v.IsObject && v.Get("@path") is JsonValue p && p.IsString;
        public static bool IsCall(JsonValue v) => v is object && v.IsObject && v.Get("@call") is JsonValue c && c.IsString;
        public static bool IsDynamic(JsonValue v) => IsBinding(v) || IsCall(v);

        /// <summary>Resolves a dynamic value to a plain one. Absent data is null (undefined).</summary>
        public static JsonValue Evaluate(JsonValue value, EvalContext ctx)
        {
            if (value is null) return null;

            if (value.IsObject)
            {
                if (IsBinding(value)) return ctx.Read(value.GetString("@path"));
                if (IsCall(value)) return Call(value.GetString("@call"), value.Get("args"), value.GetString("catalogId"), ctx);

                // A literal object (an action context, a template of values): its members are dynamic values too.
                var o = JsonValue.NewObject();
                foreach (var k in value.Keys)
                {
                    var key = k.StartsWith("@@") ? k.Substring(1) : k;   // "@@x" escapes a literal "@x"
                    o.Set(key, Evaluate(value.Get(k), ctx) ?? JsonValue.Null);
                }
                return o;
            }

            if (value.IsArray)
            {
                var a = JsonValue.NewArray();
                foreach (var i in value.Items) a.Add(Evaluate(i, ctx) ?? JsonValue.Null);
                return a;
            }

            return value;
        }

        public static string EvaluateString(JsonValue v, EvalContext ctx, string fallback = "")
        {
            var r = Evaluate(v, ctx);
            return r is null || r.IsNull ? fallback : r.ToDisplayString();
        }

        public static bool EvaluateBool(JsonValue v, EvalContext ctx, bool fallback = false)
        {
            if (v is null) return fallback;
            return Coerce.ToBool(Evaluate(v, ctx));
        }

        /// <summary>Calls a catalog function by name with arguments that are still unresolved.</summary>
        public static JsonValue Call(string name, JsonValue rawArgs, string catalogId, EvalContext ctx)
        {
            if (name == "@index")
            {
                if (ctx.Indices.Count == 0) throw new A2UIFunctionException(A2UIError.ExecutionError, "@index was called outside a list template");
                var args   = rawArgs is null ? null : Evaluate(rawArgs, ctx);
                var offset = args is object ? args.GetNumber("offset", 0) : 0;
                return JsonValue.Number(ctx.Indices[ctx.Indices.Count - 1] + offset);
            }

            var catalog = ctx.Surface.Host.ResolveCatalog(catalogId ?? ctx.CatalogId);
            if (catalog is null) throw new A2UIFunctionException(A2UIError.CatalogError, "No catalog '" + (catalogId ?? ctx.CatalogId) + "' to resolve function '" + name + "'");

            var def = catalog.GetFunction(name);
            if (def is null) throw new A2UIFunctionException(A2UIError.ExpressionError, "Function '" + name + "' is not implemented by catalog '" + catalog.Id + "'");

            if (def.AllowedCallers == AllowedCallers.AgentOnly)
                throw new A2UIFunctionException(A2UIError.ExpressionError, "Function '" + name + "' is agentOnly and cannot be used in a binding or action");
            if (def.RequiresUserActivation && !ctx.UserActivated)
                throw new A2UIFunctionException(A2UIError.ExpressionError, "Function '" + name + "' requires a user gesture and cannot run in a binding or check");

            var inner = catalogId is object && catalogId != ctx.CatalogId ? ctx.WithCatalog(catalogId) : ctx;
            var resolved = rawArgs is null ? JsonValue.NewObject() : Evaluate(rawArgs, inner);
            if (!ReferenceEquals(inner, ctx)) foreach (var d in inner.Dependencies) ctx.Dependencies.Add(d);

            return def.Implementation(ctx, resolved) ?? JsonValue.Null;
        }

        // ------------------------------------------------------------------ formatString

        /// <summary>Interpolates <c>${...}</c> expressions in <paramref name="template"/>; <c>\${</c> is a literal "${".</summary>
        public static string Interpolate(string template, EvalContext ctx)
        {
            if (template is null) return "";
            if (template.IndexOf("${", StringComparison.Ordinal) < 0 && template.IndexOf("\\${", StringComparison.Ordinal) < 0) return template;

            var sb = new StringBuilder();
            var i  = 0;

            while (i < template.Length)
            {
                var c = template[i];

                if (c == '\\' && i + 2 < template.Length && template[i + 1] == '$' && template[i + 2] == '{')
                {
                    sb.Append("${");
                    i += 3;
                    continue;
                }

                if (c == '$' && i + 1 < template.Length && template[i + 1] == '{')
                {
                    var end = FindClose(template, i + 2);
                    if (end < 0) { sb.Append(template.Substring(i)); break; }

                    var expr = template.Substring(i + 2, end - (i + 2));
                    sb.Append(Coerce.ToText(new ExpressionParser(expr, ctx).ParseTop()));
                    i = end + 1;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        /// <summary>The index of the '}' that closes an expression starting at <paramref name="start"/>, skipping quotes and nested braces.</summary>
        internal static int FindClose(string s, int start)
        {
            var depth = 1;
            char quote = '\0';

            for (var i = start; i < s.Length; i++)
            {
                var c = s[i];

                if (quote != '\0')
                {
                    if (c == '\\') i++;
                    else if (c == quote) quote = '\0';
                    continue;
                }

                if (c == '\'' || c == '"') { quote = c; continue; }
                if (c == '{') depth++;
                else if (c == '}' && --depth == 0) return i;
            }

            return -1;
        }
    }

    /// <summary>The expression language inside <c>${...}</c>: literals, paths, nested <c>${...}</c> and <c>name(arg: value, ...)</c> calls.</summary>
    internal sealed class ExpressionParser
    {
        private readonly string _s;
        private readonly EvalContext _ctx;
        private int _i;

        public ExpressionParser(string s, EvalContext ctx) { _s = s; _ctx = ctx; }

        public JsonValue ParseTop()
        {
            var v = ParseValue();
            return v;
        }

        private void Ws() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }

        private JsonValue ParseValue()
        {
            Ws();
            if (_i >= _s.Length) return null;
            var c = _s[_i];

            if (c == '$' && _i + 1 < _s.Length && _s[_i + 1] == '{')
            {
                var end = Expressions.FindClose(_s, _i + 2);
                if (end < 0) throw new A2UIFunctionException(A2UIError.ExpressionError, "Unterminated ${ in '" + _s + "'");
                var inner = _s.Substring(_i + 2, end - (_i + 2));
                _i = end + 1;
                return new ExpressionParser(inner, _ctx).ParseTop();
            }

            if (c == '\'' || c == '"') return JsonValue.String(ReadQuoted(c));

            var start = _i;
            while (_i < _s.Length && _s[_i] != ',' && _s[_i] != ')' && _s[_i] != '(' && !char.IsWhiteSpace(_s[_i])) _i++;
            var token = _s.Substring(start, _i - start);

            Ws();
            if (_i < _s.Length && _s[_i] == '(' && IsIdentifier(token)) return ParseCall(token);

            if (token == "true")  return JsonValue.True;
            if (token == "false") return JsonValue.False;
            if (token == "null")  return JsonValue.Null;

            if (token.Length > 0 && (char.IsDigit(token[0]) || token[0] == '-' && token.Length > 1 && char.IsDigit(token[1])))
            {
                if (double.TryParse(token, out var d)) return JsonValue.Number(d);
            }

            return _ctx.Read(token);
        }

        private static bool IsIdentifier(string s)
        {
            if (s.Length == 0) return false;
            if (!(char.IsLetter(s[0]) || s[0] == '_' || s[0] == '@')) return false;
            for (var i = 1; i < s.Length; i++) if (!(char.IsLetterOrDigit(s[i]) || s[i] == '_')) return false;
            return true;
        }

        private string ReadQuoted(char quote)
        {
            var sb = new StringBuilder();
            _i++;
            while (_i < _s.Length)
            {
                var c = _s[_i++];
                if (c == '\\' && _i < _s.Length) { sb.Append(_s[_i++]); continue; }
                if (c == quote) return sb.ToString();
                sb.Append(c);
            }
            throw new A2UIFunctionException(A2UIError.ExpressionError, "Unterminated string in '" + _s + "'");
        }

        private JsonValue ParseCall(string name)
        {
            _i++; // (
            var args = JsonValue.NewObject();

            while (true)
            {
                Ws();
                if (_i >= _s.Length) throw new A2UIFunctionException(A2UIError.ExpressionError, "Unterminated call to '" + name + "'");
                if (_s[_i] == ')') { _i++; break; }

                var start = _i;
                while (_i < _s.Length && _s[_i] != ':' && _s[_i] != ',' && _s[_i] != ')') _i++;
                if (_i >= _s.Length || _s[_i] != ':') throw new A2UIFunctionException(A2UIError.ExpressionError, "Expected 'name: value' arguments in call to '" + name + "'");
                var key = _s.Substring(start, _i - start).Trim();
                _i++;

                args.Set(key, ParseValue() ?? JsonValue.Null);
                Ws();
                if (_i < _s.Length && _s[_i] == ',') _i++;
            }

            // Arguments are already values here; calling with them as literals must not re-interpret strings as bindings.
            return CallResolved(name, args);
        }

        private JsonValue CallResolved(string name, JsonValue resolved)
        {
            if (name == "@index")
            {
                if (_ctx.Indices.Count == 0) throw new A2UIFunctionException(A2UIError.ExecutionError, "@index was called outside a list template");
                return JsonValue.Number(_ctx.Indices[_ctx.Indices.Count - 1] + resolved.GetNumber("offset", 0));
            }

            var catalog = _ctx.Surface.Host.ResolveCatalog(_ctx.CatalogId);
            var def     = catalog?.GetFunction(name);
            if (def is null) throw new A2UIFunctionException(A2UIError.ExpressionError, "Function '" + name + "' is not implemented by the catalog");
            if (def.AllowedCallers == AllowedCallers.AgentOnly) throw new A2UIFunctionException(A2UIError.ExpressionError, "Function '" + name + "' is agentOnly");
            if (def.RequiresUserActivation && !_ctx.UserActivated) throw new A2UIFunctionException(A2UIError.ExpressionError, "Function '" + name + "' requires a user gesture");

            return def.Implementation(_ctx, resolved) ?? JsonValue.Null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Tesserae.A2UI
{
    /// <summary>
    /// The A2UI regex dialect: a portable subset of ECMAScript patterns, with a static safety check that rejects
    /// patterns which could backtrack catastrophically, so an agent cannot hang the page with a field's validator.
    /// Implemented rules: length cap, forbidden constructs (backreferences, lookbehind, named groups, \p{}, \cX,
    /// inline flags), and the nested-unbounded-quantifier rule (a repeated group that itself repeats unboundedly) plus the
    /// overlapping-alternation rule for an unbounded repeat.
    /// </summary>
    public static class RegexDialect
    {
        public const int MaxPatternLength = 256;
        public const int MaxValueLength   = 4096;

        private static readonly Dictionary<string, Regex> Cache = new Dictionary<string, Regex>();

        /// <summary>Null when the pattern is allowed, otherwise why it is not.</summary>
        public static string Check(string pattern)
        {
            if (pattern is null) return "The pattern is missing";
            if (pattern.Length > MaxPatternLength) return "The pattern is longer than " + MaxPatternLength + " characters";

            for (var i = 0; i < pattern.Length; i++)
            {
                var c = pattern[i];
                if (c == '\\' && i + 1 < pattern.Length)
                {
                    var n = pattern[i + 1];
                    if (n >= '1' && n <= '9') return "Backreferences are not allowed";
                    if (n == 'k') return "Named backreferences are not allowed";
                    if (n == 'p' || n == 'P') return "Unicode property escapes are not allowed";
                    if (n == 'c') return "\\cX control escapes are not allowed";
                    i++;
                    continue;
                }

                if (c == '(' && i + 2 < pattern.Length && pattern[i + 1] == '?')
                {
                    var k = pattern[i + 2];
                    if (k == '<' && i + 3 < pattern.Length && (pattern[i + 3] == '=' || pattern[i + 3] == '!')) return "Lookbehind is not allowed";
                    if (k == '<') return "Named groups are not allowed";
                    if (k != ':' && k != '=' && k != '!') return "Inline flags are not allowed";
                }
            }

            return NestedQuantifier(pattern);
        }

        /// <summary>Rule 2/3: an unbounded quantifier inside a group that is itself repeated more than once, or over an alternation with a shared first character.</summary>
        private static string NestedQuantifier(string p)
        {
            var stack = new Stack<GroupInfo>();
            var inClass = false;

            for (var i = 0; i < p.Length; i++)
            {
                var c = p[i];
                if (c == '\\') { i++; continue; }
                if (inClass) { if (c == ']') inClass = false; continue; }
                if (c == '[') { inClass = true; continue; }

                if (c == '(') { stack.Push(new GroupInfo { Start = i }); continue; }

                if (c == '|' && stack.Count > 0) { stack.Peek().Alternation = true; continue; }

                if ((c == '*' || c == '+') && stack.Count > 0) { stack.Peek().HasUnbounded = true; continue; }
                if (c == '{' && stack.Count > 0 && IsOpenRange(p, i)) { stack.Peek().HasUnbounded = true; continue; }

                if (c == ')' && stack.Count > 0)
                {
                    var g = stack.Pop();
                    var q = QuantifierAfter(p, i + 1);

                    if ((q == Quant.Many || q == Quant.Unbounded) && g.HasUnbounded) return "A repeated group contains an unbounded repeat, which can backtrack catastrophically";
                    if (q == Quant.Unbounded && g.HasUnbounded && g.Alternation) return "An unbounded repeat over alternatives is not allowed";
                    if (q == Quant.Unbounded && g.Alternation && BranchesShareStart(p.Substring(g.Start + 1, i - g.Start - 1))) return "An unbounded repeat over alternatives that start alike is not allowed";

                    if (stack.Count > 0 && (g.HasUnbounded || q == Quant.Unbounded)) stack.Peek().HasUnbounded = true;
                }
            }

            return null;
        }

        private sealed class GroupInfo { public int Start; public bool HasUnbounded; public bool Alternation; }

        private enum Quant { None, AtMostOne, Many, Unbounded }

        private static bool IsOpenRange(string p, int i)
        {
            var close = p.IndexOf('}', i);
            if (close < 0) return false;
            var body = p.Substring(i + 1, close - i - 1);
            return body.EndsWith(",") && body.Length > 1;
        }

        private static Quant QuantifierAfter(string p, int i)
        {
            if (i >= p.Length) return Quant.None;
            var c = p[i];
            if (c == '*' || c == '+') return Quant.Unbounded;
            if (c == '?') return Quant.AtMostOne;
            if (c == '{')
            {
                var close = p.IndexOf('}', i);
                if (close < 0) return Quant.None;
                var body = p.Substring(i + 1, close - i - 1);
                if (body.EndsWith(",")) return Quant.Unbounded;
                var comma = body.IndexOf(',');
                var max   = comma < 0 ? body : body.Substring(comma + 1);
                if (int.TryParse(max.Trim(), out var m)) return m > 1 ? Quant.Many : Quant.AtMostOne;
            }
            return Quant.None;
        }

        private static bool BranchesShareStart(string body)
        {
            var seen = new HashSet<char>();
            foreach (var branch in body.Split('|'))
            {
                var b = branch.TrimStart('(', '?', ':');
                if (b.Length == 0) continue;
                if (!seen.Add(b[0])) return true;
            }
            return false;
        }

        /// <summary>Tests <paramref name="value"/> against a dialect pattern (search semantics). Throws an EXPRESSION_ERROR for a pattern that is not allowed.</summary>
        public static bool IsMatch(string value, string pattern)
        {
            var problem = Check(pattern);
            if (problem is object) throw new A2UIFunctionException(A2UIError.ExpressionError, "Invalid pattern '" + pattern + "': " + problem);
            if (value is null || value.Length > MaxValueLength) return false;

            if (!Cache.TryGetValue(pattern, out var re))
            {
                try { re = new Regex(pattern); }
                catch (Exception e) { throw new A2UIFunctionException(A2UIError.ExpressionError, "Invalid pattern '" + pattern + "': " + e.Message); }
                Cache[pattern] = re;
            }

            return re.IsMatch(value);
        }
    }
}

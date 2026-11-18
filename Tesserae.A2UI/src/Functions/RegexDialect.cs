using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Tesserae.A2UI
{
    /// <summary>
    /// The A2UI regex dialect: a portable subset of ECMAScript patterns, with a static safety check, so an agent cannot
    /// hang the page with a field's validator. A backtracking engine cannot be interrupted once it runs, so everything is
    /// decided from the pattern's text beforehand:
    /// <list type="bullet">
    /// <item>a length cap and forbidden constructs (backreferences, lookbehind, named groups, \p{}, \cX, inline flags);</item>
    /// <item>patterns that can take exponential time are refused: a repeated group that holds an unbounded repeat, a
    /// group repeated many times over alternatives that can start with the same character, and a variably repeated group
    /// that ends in a repeat its next round could also match;</item>
    /// <item>patterns that can take polynomial time are allowed, with a shorter value: unbounded repeats that follow one
    /// another and can match the same characters (<c>a*a*a*</c>, <c>.*,.*,.*</c>) multiply, so the longest value such a
    /// pattern is tested against shrinks with how many of them there are, one more when the pattern does not start
    /// with <c>^</c> and so is tried from every position (<see cref="MaxValueLengthFor"/>).</item>
    /// </list>
    /// What a repeat can match is read from the pattern (a literal, a class, an escape) and compared over a sample of
    /// characters; a group is taken to match anything, which errs towards the shorter value.
    /// </summary>
    public static class RegexDialect
    {
        public const int MaxPatternLength = 256;
        public const int MaxValueLength   = 4096;

        /// <summary>Roughly how many steps the engine may take on one value before the length is cut: about a second of work.</summary>
        private const double StepBudget = 1e9;

        private const int Unbounded = int.MaxValue;

        /// <summary>A group repeated more often than this over alternatives that start alike is refused; fewer rounds stay cheap.</summary>
        private const int MaxAmbiguousRounds = 16;

        private const int MaxCachedPatterns = 500;

        private static readonly Dictionary<string, Compiled> Cache = new Dictionary<string, Compiled>();

        private sealed class Compiled
        {
            public Regex Regex;
            public int   MaxValueLength;
        }

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

            List<List<Item>> parsed;
            try { parsed = new PatternReader(pattern).Read(); }
            catch (PatternException e) { return e.Message; }

            return Problem(parsed);
        }

        /// <summary>
        /// The longest value <paramref name="pattern"/> is tested against: <see cref="MaxValueLength"/> for most, less for a
        /// pattern whose repeats multiply. A longer value does not match.
        /// </summary>
        public static int MaxValueLengthFor(string pattern)
        {
            if (Check(pattern) is object) return 0;

            var parsed = new PatternReader(pattern).Read();
            var degree = Degree(parsed);

            // A pattern that is not tied to the start of the value is tried from every position of it: one more factor.
            if (!StartsAtTheStart(parsed)) degree++;

            if (degree <= 2) return MaxValueLength;

            return Math.Max(16, Math.Min(MaxValueLength, (int)Math.Floor(Math.Pow(StepBudget, 1.0 / degree))));
        }

        // ------------------------------------------------------------------ the pattern, read

        /// <summary>One element of a pattern with its repeat: a set of characters, or a group of alternatives.</summary>
        private sealed class Item
        {
            public Func<char, bool>  Set;
            public List<List<Item>>  Group;
            public bool              ZeroWidth;   // an anchor, a word boundary, a lookahead
            public bool              StartAnchor; // ^
            public int               Min = 1;
            public int               Max = 1;
        }

        private sealed class PatternException : Exception
        {
            public PatternException(string message) : base(message) { }
        }

        private static readonly Func<char, bool> Any        = c => true;
        private static readonly Func<char, bool> Digit      = c => c >= '0' && c <= '9';
        private static readonly Func<char, bool> Word       = c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
        private static readonly Func<char, bool> Whitespace = c => c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f' || c == '\v' || c == '\u00a0';

        private static Func<char, bool> Not(Func<char, bool> set) => c => !set(c);

        private static Func<char, bool> Literal(char ch) => c => c == ch;

        private sealed class PatternReader
        {
            private readonly string _p;
            private int _i;
            private int _depth;

            public PatternReader(string pattern) { _p = pattern; }

            public List<List<Item>> Read()
            {
                var alternatives = ReadAlternatives();
                if (_i < _p.Length) throw new PatternException("Unbalanced ')'");
                return alternatives;
            }

            private List<List<Item>> ReadAlternatives()
            {
                var alternatives = new List<List<Item>>();
                var branch       = new List<Item>();
                alternatives.Add(branch);

                while (_i < _p.Length && _p[_i] != ')')
                {
                    if (_p[_i] == '|')
                    {
                        _i++;
                        branch = new List<Item>();
                        alternatives.Add(branch);
                        continue;
                    }

                    var item = ReadAtom();
                    ReadRepeat(item);
                    branch.Add(item);
                }

                return alternatives;
            }

            private Item ReadAtom()
            {
                var c = _p[_i];

                if (c == '(')
                {
                    if (++_depth > 32) throw new PatternException("Groups are nested deeper than 32 levels");

                    var item = new Item();
                    _i++;

                    if (_i + 1 < _p.Length && _p[_i] == '?')
                    {
                        item.ZeroWidth = _p[_i + 1] == '=' || _p[_i + 1] == '!';
                        _i += 2;
                    }

                    item.Group = ReadAlternatives();
                    if (_i >= _p.Length) throw new PatternException("Unbalanced '('");
                    _i++;
                    _depth--;
                    return item;
                }

                if (c == '[') return new Item { Set = ReadClass() };

                if (c == '\\' && _i + 1 < _p.Length)
                {
                    var e = _p[_i + 1];
                    _i += 2;

                    if (e == 'b' || e == 'B') return new Item { ZeroWidth = true };

                    // A code point escape: skip its digits; what it stands for is not worked out, so it can be anything.
                    if (e == 'x' || e == 'u')
                    {
                        var digits = e == 'x' ? 2 : 4;
                        while (digits-- > 0 && _i < _p.Length && IsHex(_p[_i])) _i++;
                        return new Item { Set = Any };
                    }

                    return new Item { Set = Escape(e) };
                }

                _i++;

                if (c == '^') return new Item { ZeroWidth = true, StartAnchor = true };
                if (c == '$') return new Item { ZeroWidth = true };
                if (c == '.') return new Item { Set = ch => ch != '\n' && ch != '\r' };

                return new Item { Set = Literal(c) };
            }

            private static Func<char, bool> Escape(char e)
            {
                switch (e)
                {
                    case 'd': return Digit;
                    case 'D': return Not(Digit);
                    case 'w': return Word;
                    case 'W': return Not(Word);
                    case 's': return Whitespace;
                    case 'S': return Not(Whitespace);
                    case 'n': return Literal('\n');
                    case 'r': return Literal('\r');
                    case 't': return Literal('\t');
                    case 'f': return Literal('\f');
                    case 'v': return Literal('\v');
                    case '0': return Literal('\0');
                    default:  return Literal(e);
                }
            }

            private Func<char, bool> ReadClass()
            {
                _i++; // [
                var negated = _i < _p.Length && _p[_i] == '^';
                if (negated) _i++;

                var sets = new List<Func<char, bool>>();

                while (_i < _p.Length && _p[_i] != ']')
                {
                    Func<char, bool> set = null;
                    var lo = _p[_i++];

                    if (lo == '\\' && _i < _p.Length)
                    {
                        var e = _p[_i++];

                        if (e == 'x' || e == 'u')
                        {
                            var digits = e == 'x' ? 2 : 4;
                            while (digits-- > 0 && _i < _p.Length && IsHex(_p[_i])) _i++;
                            sets.Add(Any);
                            continue;
                        }

                        if (e == 'b') lo = '\b';
                        else if ("dDwWsS".IndexOf(e) >= 0) set = Escape(e);
                        else lo = "nrtfv0".IndexOf(e) >= 0 ? "\n\r\t\f\v\0"["nrtfv0".IndexOf(e)] : e;
                    }

                    if (set is null && _i + 1 < _p.Length && _p[_i] == '-' && _p[_i + 1] != ']')
                    {
                        _i++;
                        var hi = _p[_i++];
                        if (hi == '\\' && _i < _p.Length) hi = _p[_i++];

                        var from = lo;
                        var to   = hi;
                        set = ch => ch >= from && ch <= to;
                    }

                    sets.Add(set ?? Literal(lo));
                }

                if (_i >= _p.Length) throw new PatternException("Unterminated '['");
                _i++; // ]

                Func<char, bool> any = ch =>
                {
                    foreach (var s in sets) if (s(ch)) return true;
                    return false;
                };

                return negated ? Not(any) : any;
            }

            private void ReadRepeat(Item item)
            {
                if (_i >= _p.Length) return;

                var c = _p[_i];

                if (c == '*')      { item.Min = 0; item.Max = Unbounded; _i++; }
                else if (c == '+') { item.Min = 1; item.Max = Unbounded; _i++; }
                else if (c == '?') { item.Min = 0; item.Max = 1; _i++; }
                else if (c == '{')
                {
                    var close = _p.IndexOf('}', _i);
                    if (close < 0) return;

                    var body  = _p.Substring(_i + 1, close - _i - 1);
                    var comma = body.IndexOf(',');
                    var first = comma < 0 ? body : body.Substring(0, comma);
                    var last  = comma < 0 ? body : body.Substring(comma + 1);

                    // Not a count: the brace is an ordinary character, read as one on the next round.
                    if (!IsCount(first) || (last.Length > 0 && !IsCount(last))) return;

                    item.Min = Count(first);
                    item.Max = comma < 0 ? item.Min : (last.Length == 0 ? Unbounded : Count(last));
                    _i = close + 1;
                }
                else return;

                if (_i < _p.Length && _p[_i] == '?') _i++; // lazy: the same work in another order
            }

            private static bool IsHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

            private static bool IsCount(string s)
            {
                if (s.Length == 0) return false;
                foreach (var c in s) if (c < '0' || c > '9') return false;
                return true;
            }

            private static int Count(string s) => s.Length > 6 ? 999999 : int.Parse(s);
        }

        // ------------------------------------------------------------------ the pattern, judged

        // What "can these two match the same character" is answered over: the printable ASCII characters, the usual
        // whitespace, and a few beyond ASCII so that a negated class and "." meet somewhere.
        private static readonly char[] Sample = CreateSample();

        private static char[] CreateSample()
        {
            var chars = new List<char> { '\t', '\n', '\r', '\u00a0', '\u00e9', '\u4e2d' };
            for (var c = ' '; c <= '~'; c++) chars.Add(c);
            return chars.ToArray();
        }

        private static bool Overlap(Func<char, bool> a, Func<char, bool> b)
        {
            foreach (var c in Sample) if (a(c) && b(c)) return true;
            return false;
        }

        /// <summary>Every character the item can consume. A group is taken to consume anything.</summary>
        private static Func<char, bool> SetOf(Item item) => item.Group is object ? Any : item.Set;

        /// <summary>Whether a match of the branch can begin with <paramref name="c"/>.</summary>
        private static bool CanStartWith(List<Item> branch, char c)
        {
            foreach (var item in branch)
            {
                if (item.ZeroWidth) continue;

                if (item.Group is object)
                {
                    foreach (var inner in item.Group) if (CanStartWith(inner, c)) return true;
                }
                else if (item.Set(c)) return true;

                if (item.Min > 0) return false;
            }

            return false;
        }

        private static bool CanStartWith(List<List<Item>> alternatives, char c)
        {
            foreach (var branch in alternatives) if (CanStartWith(branch, c)) return true;
            return false;
        }

        private static bool HasUnbounded(List<List<Item>> alternatives)
        {
            foreach (var branch in alternatives)
            {
                foreach (var item in branch)
                {
                    if (item.Max == Unbounded) return true;
                    if (item.Group is object && HasUnbounded(item.Group)) return true;
                }
            }

            return false;
        }

        private static bool StartAlike(List<List<Item>> alternatives)
        {
            if (alternatives.Count < 2) return false;

            foreach (var c in Sample)
            {
                var starting = 0;
                foreach (var branch in alternatives) if (CanStartWith(branch, c) && ++starting > 1) return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a round of the group can end in a repeat of variable length that what comes next - the rest of the round,
        /// or the start of the next one - could also match: then the same text splits between them in many ways.
        /// </summary>
        private static bool EndsAmbiguously(List<List<Item>> alternatives)
        {
            foreach (var branch in alternatives)
            {
                for (var i = 0; i < branch.Count; i++)
                {
                    var item = branch[i];
                    if (item.ZeroWidth || item.Max <= 1 || item.Min == item.Max) continue;

                    var set     = SetOf(item);
                    var settled = false;

                    for (var j = i + 1; j < branch.Count && !settled; j++)
                    {
                        var next = branch[j];
                        if (next.ZeroWidth) continue;
                        if (Overlap(set, SetOf(next))) return true;
                        if (next.Min > 0) settled = true;
                    }

                    if (settled) continue;

                    foreach (var c in Sample) if (set(c) && CanStartWith(alternatives, c)) return true;
                }
            }

            return false;
        }

        /// <summary>Null when no rule refuses the pattern, otherwise the reason.</summary>
        private static string Problem(List<List<Item>> alternatives)
        {
            foreach (var branch in alternatives)
            {
                foreach (var item in branch)
                {
                    if (item.Group is null) continue;

                    var inner = Problem(item.Group);
                    if (inner is object) return inner;

                    if (item.ZeroWidth || item.Max <= 1) continue;

                    if (HasUnbounded(item.Group)) return "A repeated group contains an unbounded repeat, which can backtrack catastrophically";

                    if (item.Max > MaxAmbiguousRounds && StartAlike(item.Group)) return "A group repeated many times over alternatives that start alike is not allowed";

                    if (item.Min != item.Max && EndsAmbiguously(item.Group)) return "A repeated group ends in a repeat that its next round could also match, which can backtrack catastrophically";
                }
            }

            return null;
        }

        /// <summary>
        /// How many unbounded repeats can compete for the same stretch of text: the exponent of the work on a value that
        /// almost matches. Two that cannot match a common character, or that have a required character between them which
        /// neither can match, do not compete.
        /// </summary>
        private static int Degree(List<List<Item>> alternatives)
        {
            var degree = 0;

            foreach (var branch in alternatives)
            {
                var                    run        = 0;
                Func<char, bool>       previous   = null;
                var                    separators = new List<Func<char, bool>>();

                foreach (var item in branch)
                {
                    if (item.ZeroWidth)
                    {
                        if (item.Group is object) degree = Math.Max(degree, Degree(item.Group));
                        continue;
                    }

                    var inner   = item.Group is object ? Degree(item.Group) : 0;
                    var repeats = item.Max == Unbounded ? 1 : inner;

                    if (repeats == 0)
                    {
                        if (item.Min > 0) separators.Add(SetOf(item));
                        continue;
                    }

                    var set      = SetOf(item);
                    var competes = previous is object && Overlap(previous, set);

                    if (competes)
                    {
                        foreach (var separator in separators)
                        {
                            if (!Overlap(separator, previous) && !Overlap(separator, set)) { competes = false; break; }
                        }
                    }

                    run      = competes ? run + repeats : repeats;
                    previous = set;
                    separators.Clear();
                    degree   = Math.Max(degree, run);
                }
            }

            return degree;
        }

        private static bool StartsAtTheStart(List<List<Item>> alternatives)
        {
            foreach (var branch in alternatives)
            {
                if (branch.Count == 0 || !branch[0].StartAnchor) return false;
            }

            return true;
        }

        /// <summary>Tests <paramref name="value"/> against a dialect pattern (search semantics). Throws an EXPRESSION_ERROR for a pattern that is not allowed.</summary>
        public static bool IsMatch(string value, string pattern)
        {
            if (!Cache.TryGetValue(pattern ?? "", out var compiled))
            {
                var problem = Check(pattern);
                if (problem is object) throw new A2UIFunctionException(A2UIError.ExpressionError, "Invalid pattern '" + pattern + "': " + problem);

                compiled = new Compiled { MaxValueLength = MaxValueLengthFor(pattern) };

                try { compiled.Regex = new Regex(pattern); }
                catch (Exception e) { throw new A2UIFunctionException(A2UIError.ExpressionError, "Invalid pattern '" + pattern + "': " + e.Message); }

                // An agent can send a new pattern with every message; what is kept for them stays bounded.
                if (Cache.Count >= MaxCachedPatterns) Cache.Clear();
                Cache[pattern] = compiled;
            }

            if (value is null || value.Length > compiled.MaxValueLength) return false;

            return compiled.Regex.IsMatch(value);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Tesserae.A2UI
{
    /// <summary>The kind of a <see cref="JsonValue"/>.</summary>
    public enum JsonKind
    {
        Null,
        Boolean,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>
    /// A typed JSON value. A2UI messages, component definitions and the data model are all JSON, and this is the one
    /// representation the whole package uses, so nothing is passed around as an untyped JavaScript object.
    /// "Undefined" (a path that is not in the data model) is a C# <c>null</c> reference; JSON <c>null</c> is
    /// <see cref="Null"/>. Object keys keep their insertion order.
    /// </summary>
    public sealed class JsonValue
    {
        private readonly bool                       _bool;
        private readonly double                     _number;
        private readonly string                     _string;
        private readonly List<JsonValue>            _items;
        private readonly List<string>               _keys;
        private readonly Dictionary<string, JsonValue> _map;

        private JsonValue(JsonKind kind, bool b = false, double n = 0, string s = null, bool array = false, bool obj = false)
        {
            Kind    = kind;
            _bool   = b;
            _number = n;
            _string = s;
            if (array) _items = new List<JsonValue>();
            if (obj)
            {
                _keys = new List<string>();
                _map  = new Dictionary<string, JsonValue>();
            }
        }

        public JsonKind Kind { get; }

        public static JsonValue Null => new JsonValue(JsonKind.Null);
        public static JsonValue True => new JsonValue(JsonKind.Boolean, b: true);
        public static JsonValue False => new JsonValue(JsonKind.Boolean, b: false);
        public static JsonValue Bool(bool value) => new JsonValue(JsonKind.Boolean, b: value);
        public static JsonValue Number(double value) => new JsonValue(JsonKind.Number, n: value);
        public static JsonValue String(string value) => value is null ? Null : new JsonValue(JsonKind.String, s: value);
        public static JsonValue NewArray() => new JsonValue(JsonKind.Array, array: true);
        public static JsonValue NewObject() => new JsonValue(JsonKind.Object, obj: true);

        public bool IsNull    => Kind == JsonKind.Null;
        public bool IsBool    => Kind == JsonKind.Boolean;
        public bool IsNumber  => Kind == JsonKind.Number;
        public bool IsString  => Kind == JsonKind.String;
        public bool IsArray   => Kind == JsonKind.Array;
        public bool IsObject  => Kind == JsonKind.Object;

        public bool   BoolValue   => _bool;
        public double NumberValue => _number;
        public string StringValue => _string;

        /// <summary>The elements of an array (empty for anything else).</summary>
        public List<JsonValue> Items => _items ?? EmptyItems;

        /// <summary>The keys of an object in insertion order (empty for anything else).</summary>
        public List<string> Keys => _keys ?? EmptyKeys;

        private static readonly List<JsonValue> EmptyItems = new List<JsonValue>();
        private static readonly List<string>    EmptyKeys  = new List<string>();

        public int Count => IsArray ? _items.Count : (IsObject ? _keys.Count : 0);

        public bool Has(string key) => _map is object && _map.ContainsKey(key);

        /// <summary>The member of an object, or null when this is not an object or has no such key.</summary>
        public JsonValue Get(string key)
        {
            if (_map is null) return null;
            return _map.TryGetValue(key, out var v) ? v : null;
        }

        /// <summary>The element of an array, or null when out of range.</summary>
        public JsonValue At(int index)
        {
            if (_items is null || index < 0 || index >= _items.Count) return null;
            return _items[index];
        }

        public JsonValue Set(string key, JsonValue value)
        {
            if (_map is null) throw new InvalidOperationException("Not a JSON object");
            if (!_map.ContainsKey(key)) _keys.Add(key);
            _map[key] = value ?? Null;
            return this;
        }

        public bool Remove(string key)
        {
            if (_map is null || !_map.ContainsKey(key)) return false;
            _map.Remove(key);
            _keys.Remove(key);
            return true;
        }

        public JsonValue Add(JsonValue value)
        {
            if (_items is null) throw new InvalidOperationException("Not a JSON array");
            _items.Add(value ?? Null);
            return this;
        }

        public string GetString(string key, string fallback = null)
        {
            var v = Get(key);
            return v is object && v.IsString ? v._string : fallback;
        }

        public bool GetBool(string key, bool fallback = false)
        {
            var v = Get(key);
            return v is object && v.IsBool ? v._bool : fallback;
        }

        public double GetNumber(string key, double fallback = 0)
        {
            var v = Get(key);
            return v is object && v.IsNumber ? v._number : fallback;
        }

        /// <summary>A deep copy: the data model never shares structure with a message it was built from.</summary>
        public JsonValue Clone()
        {
            switch (Kind)
            {
                case JsonKind.Array:
                {
                    var a = NewArray();
                    foreach (var i in _items) a._items.Add(i.Clone());
                    return a;
                }
                case JsonKind.Object:
                {
                    var o = NewObject();
                    foreach (var k in _keys) o.Set(k, _map[k].Clone());
                    return o;
                }
                case JsonKind.Boolean: return Bool(_bool);
                case JsonKind.Number:  return Number(_number);
                case JsonKind.String:  return new JsonValue(JsonKind.String, s: _string);
                default:               return Null;
            }
        }

        public static bool DeepEquals(JsonValue a, JsonValue b)
        {
            if (a is null || b is null) return a is null && b is null;
            if (a.Kind != b.Kind) return false;

            switch (a.Kind)
            {
                case JsonKind.Null:    return true;
                case JsonKind.Boolean: return a._bool == b._bool;
                case JsonKind.Number:  return a._number == b._number;
                case JsonKind.String:  return a._string == b._string;
                case JsonKind.Array:
                    if (a._items.Count != b._items.Count) return false;
                    for (var i = 0; i < a._items.Count; i++) if (!DeepEquals(a._items[i], b._items[i])) return false;
                    return true;
                default:
                    if (a._keys.Count != b._keys.Count) return false;
                    foreach (var k in a._keys)
                    {
                        if (!b._map.ContainsKey(k)) return false;
                        if (!DeepEquals(a._map[k], b._map[k])) return false;
                    }
                    return true;
            }
        }

        // ------------------------------------------------------------------ serialisation

        public override string ToString() => ToJson();

        public string ToJson(bool indented = false)
        {
            var sb = new StringBuilder();
            Write(sb, indented, 0);
            return sb.ToString();
        }

        /// <summary>The text a value takes when it is interpolated into a string: bare strings and numbers, "" for null, JSON otherwise.</summary>
        public string ToDisplayString()
        {
            switch (Kind)
            {
                case JsonKind.Null:    return "";
                case JsonKind.String:  return _string;
                case JsonKind.Boolean: return _bool ? "true" : "false";
                case JsonKind.Number:  return FormatNumber(_number);
                default:               return ToJson();
            }
        }

        public static string FormatNumber(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "null";
            if (Math.Floor(d) == d && Math.Abs(d) < 1e15) return ((long)d).ToString();
            return d.ToString();
        }

        private void Write(StringBuilder sb, bool indented, int depth)
        {
            switch (Kind)
            {
                case JsonKind.Null:    sb.Append("null"); break;
                case JsonKind.Boolean: sb.Append(_bool ? "true" : "false"); break;
                case JsonKind.Number:  sb.Append(FormatNumber(_number)); break;
                case JsonKind.String:  Quote(sb, _string); break;
                case JsonKind.Array:
                    if (_items.Count == 0) { sb.Append("[]"); break; }
                    sb.Append('[');
                    for (var i = 0; i < _items.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        if (indented) NewLine(sb, depth + 1);
                        _items[i].Write(sb, indented, depth + 1);
                    }
                    if (indented) NewLine(sb, depth);
                    sb.Append(']');
                    break;
                default:
                    if (_keys.Count == 0) { sb.Append("{}"); break; }
                    sb.Append('{');
                    for (var i = 0; i < _keys.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        if (indented) NewLine(sb, depth + 1);
                        Quote(sb, _keys[i]);
                        sb.Append(indented ? ": " : ":");
                        _map[_keys[i]].Write(sb, indented, depth + 1);
                    }
                    if (indented) NewLine(sb, depth);
                    sb.Append('}');
                    break;
            }
        }

        private static void NewLine(StringBuilder sb, int depth)
        {
            sb.Append('\n');
            for (var i = 0; i < depth; i++) sb.Append("  ");
        }

        private static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------------ parsing

        /// <summary>Parses JSON text; throws <see cref="JsonParseException"/> on malformed input.</summary>
        public static JsonValue Parse(string text)
        {
            var p = new Parser(text ?? "");
            p.SkipWhitespace();
            var v = p.ReadValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw p.Error("Unexpected content after the JSON value");
            return v;
        }

        public static bool TryParse(string text, out JsonValue value, out string error)
        {
            try
            {
                value = Parse(text);
                error = null;
                return true;
            }
            catch (JsonParseException e)
            {
                value = null;
                error = e.Message;
                return false;
            }
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s; }

            public bool AtEnd => _i >= _s.Length;

            public JsonParseException Error(string message) => new JsonParseException(message + " at position " + _i);

            public void SkipWhitespace()
            {
                while (_i < _s.Length)
                {
                    var c = _s[_i];
                    if (c == ' ' || c == '\n' || c == '\r' || c == '\t' || c == '﻿') _i++;
                    else break;
                }
            }

            public JsonValue ReadValue()
            {
                if (AtEnd) throw Error("Unexpected end of JSON");
                var c = _s[_i];

                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return String(ReadString());
                if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                if (Match("true"))  return True;
                if (Match("false")) return False;
                if (Match("null"))  return Null;
                throw Error("Unexpected character '" + c + "'");
            }

            private bool Match(string word)
            {
                if (_i + word.Length > _s.Length || _s.Substring(_i, word.Length) != word) return false;
                _i += word.Length;
                return true;
            }

            private JsonValue ReadObject()
            {
                var o = NewObject();
                _i++;
                SkipWhitespace();
                if (!AtEnd && _s[_i] == '}') { _i++; return o; }

                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != '"') throw Error("Expected a property name");
                    var key = ReadString();
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != ':') throw Error("Expected ':'");
                    _i++;
                    SkipWhitespace();
                    o.Set(key, ReadValue());
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return o; }
                    throw Error("Expected ',' or '}'");
                }
            }

            private JsonValue ReadArray()
            {
                var a = NewArray();
                _i++;
                SkipWhitespace();
                if (!AtEnd && _s[_i] == ']') { _i++; return a; }

                while (true)
                {
                    SkipWhitespace();
                    a.Add(ReadValue());
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return a; }
                    throw Error("Expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                var sb = new StringBuilder();
                _i++; // opening quote
                while (true)
                {
                    if (AtEnd) throw Error("Unterminated string");
                    var c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("Unterminated escape");
                    var e = _s[_i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case '/': sb.Append('/'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("Bad unicode escape");
                            var code = 0;
                            for (var k = 0; k < 4; k++)
                            {
                                var h = HexValue(_s[_i + k]);
                                if (h < 0) throw Error("Bad unicode escape");
                                code = code * 16 + h;
                            }
                            _i += 4;
                            sb.Append((char)code);
                            break;
                        default: throw Error("Bad escape '\\" + e + "'");
                    }
                }
            }

            private static int HexValue(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            }

            private JsonValue ReadNumber()
            {
                var start = _i;
                if (_s[_i] == '-') _i++;
                while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9') _i++;
                if (_i < _s.Length && _s[_i] == '.')
                {
                    _i++;
                    while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9') _i++;
                }
                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    _i++;
                    if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                    while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9') _i++;
                }

                if (!double.TryParse(_s.Substring(start, _i - start), out var d))
                    throw Error("Bad number");
                return Number(d);
            }
        }
    }

    public sealed class JsonParseException : Exception
    {
        public JsonParseException(string message) : base(message) { }
    }
}

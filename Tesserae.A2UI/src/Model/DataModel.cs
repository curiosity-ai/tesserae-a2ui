using System;
using System.Collections.Generic;

namespace Tesserae.A2UI
{
    /// <summary>
    /// The data model of one surface: a JSON document addressed with JSON Pointers (RFC 6901). It is the single
    /// source of truth for a surface: <c>updateDataModel</c> writes it, input components write it as the user types,
    /// and every binding that read a path is told when that path (or anything above or below it) changed.
    /// </summary>
    public sealed class DataModel
    {
        private JsonValue _root = JsonValue.NewObject();
        private readonly List<Subscription> _subscriptions = new List<Subscription>();

        /// <summary>Raised after every change with the pointer that changed ("" for the whole document).</summary>
        public event Action<string> Changed;

        public JsonValue Root => _root;

        /// <summary>Normalises "/" and "" to the root pointer "".</summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/") return "";
            return path.Length > 1 && path[path.Length - 1] == '/' ? path.Substring(0, path.Length - 1) : path;
        }

        public static string Unescape(string token) => token.Replace("~1", "/").Replace("~0", "~");
        public static string Escape(string token) => token.Replace("~", "~0").Replace("/", "~1");

        public static List<string> Tokens(string path)
        {
            var result = new List<string>();
            path = Normalize(path);
            if (path.Length == 0) return result;
            foreach (var t in path.Substring(1).Split('/')) result.Add(Unescape(t));
            return result;
        }

        /// <summary>The value at an absolute pointer, or null when there is nothing there.</summary>
        public JsonValue Get(string path)
        {
            var node = _root;
            foreach (var token in Tokens(path))
            {
                if (node is null) return null;
                if (node.IsObject) node = node.Get(token);
                else if (node.IsArray && TryIndex(token, out var i)) node = node.At(i);
                else return null;
            }
            return node;
        }

        /// <summary>
        /// Upserts <paramref name="value"/> at <paramref name="path"/>: creates what is missing, replaces what is
        /// there, and removes the key when the value is JSON null (the protocol's delete). An empty or "/" path
        /// replaces the whole document.
        /// </summary>
        public void Set(string path, JsonValue value)
        {
            path = Normalize(path);

            if (path.Length == 0)
            {
                _root = value is object && value.IsObject ? value.Clone() : JsonValue.NewObject();
                Notify("");
                return;
            }

            var tokens = Tokens(path);
            var node   = _root;

            for (var i = 0; i < tokens.Count - 1; i++)
            {
                var token = tokens[i];
                var next  = Child(node, token);

                if (next is null || !(next.IsObject || next.IsArray))
                {
                    next = TryIndex(tokens[i + 1], out _) ? JsonValue.NewArray() : JsonValue.NewObject();
                    Put(node, token, next);
                }

                node = next;
            }

            var last = tokens[tokens.Count - 1];

            if (value is null || value.IsNull)
            {
                if (node.IsObject) node.Remove(last);
                else if (node.IsArray && TryIndex(last, out var idx) && idx < node.Items.Count) node.Items.RemoveAt(idx);
            }
            else
            {
                Put(node, last, value.Clone());
            }

            Notify(path);
        }

        private static JsonValue Child(JsonValue node, string token)
        {
            if (node.IsObject) return node.Get(token);
            if (node.IsArray && TryIndex(token, out var i)) return node.At(i);
            return null;
        }

        private static void Put(JsonValue node, string token, JsonValue value)
        {
            if (node.IsObject) { node.Set(token, value); return; }

            if (node.IsArray && TryIndex(token, out var i))
            {
                while (node.Items.Count <= i) node.Items.Add(JsonValue.Null);
                node.Items[i] = value;
            }
        }

        private static bool TryIndex(string token, out int index)
        {
            index = 0;
            if (token.Length == 0) return false;
            foreach (var c in token) if (c < '0' || c > '9') return false;
            return int.TryParse(token, out index);
        }

        // ------------------------------------------------------------------ change tracking

        /// <summary>Two pointers overlap when one is the other, or an ancestor of it.</summary>
        public static bool Overlaps(string a, string b)
        {
            if (a.Length == 0 || b.Length == 0 || a == b) return true;
            return a.StartsWith(b + "/") || b.StartsWith(a + "/");
        }

        /// <summary>Calls <paramref name="onChange"/> whenever a change overlaps any of <paramref name="paths"/>.</summary>
        public IDisposable Subscribe(IEnumerable<string> paths, Action onChange)
        {
            var s = new Subscription(this, new List<string>(paths), onChange);
            _subscriptions.Add(s);
            return s;
        }

        private void Notify(string changed)
        {
            Changed?.Invoke(changed);

            if (_subscriptions.Count == 0) return;

            foreach (var s in _subscriptions.ToArray())
            {
                if (s.Disposed) continue;
                foreach (var p in s.Paths)
                {
                    if (Overlaps(p, changed)) { s.Callback(); break; }
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly DataModel _owner;
            public readonly List<string> Paths;
            public readonly Action Callback;
            public bool Disposed;

            public Subscription(DataModel owner, List<string> paths, Action callback)
            {
                _owner   = owner;
                Paths    = paths;
                Callback = callback;
            }

            public void Dispose()
            {
                Disposed = true;
                _owner._subscriptions.Remove(this);
            }
        }
    }
}

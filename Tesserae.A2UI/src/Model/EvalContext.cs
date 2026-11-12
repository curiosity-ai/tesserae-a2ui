using System;
using System.Collections.Generic;

namespace Tesserae.A2UI
{
    /// <summary>
    /// Everything an expression is evaluated against: the surface's data model, the collection scope it is evaluated
    /// in (the pointer of the template item, so relative paths resolve), the catalog its functions come from and
    /// whether the evaluation is a user gesture. It also records every absolute path an evaluation read, which is
    /// what lets a binding subscribe to exactly the data it depends on.
    /// </summary>
    public sealed class EvalContext
    {
        public EvalContext(Surface surface, string scope, string catalogId, bool userActivated)
        {
            Surface       = surface;
            Scope         = DataModel.Normalize(scope);
            CatalogId     = catalogId;
            UserActivated = userActivated;
        }

        public Surface Surface { get; }

        /// <summary>The pointer of the collection item this evaluation runs in, "" at the root.</summary>
        public string Scope { get; }

        /// <summary>The 0-based index of each enclosing template iteration, innermost last.</summary>
        public List<int> Indices { get; } = new List<int>();

        public string CatalogId { get; }

        /// <summary>True only while an action handler runs: functions that require user activation are refused otherwise.</summary>
        public bool UserActivated { get; }

        /// <summary>The absolute paths read so far.</summary>
        public HashSet<string> Dependencies { get; } = new HashSet<string>();

        public DataModel Data => Surface.Data;

        public EvalContext WithScope(string scope, int index)
        {
            var c = new EvalContext(Surface, scope, CatalogId, UserActivated);
            c.Indices.AddRange(Indices);
            c.Indices.Add(index);
            return c;
        }

        public EvalContext WithCatalog(string catalogId, bool? userActivated = null)
        {
            var c = new EvalContext(Surface, Scope, catalogId, userActivated ?? UserActivated);
            c.Indices.AddRange(Indices);
            return c;
        }

        /// <summary>Resolves a (possibly relative) pointer against the current scope.</summary>
        public string Resolve(string path)
        {
            if (path is null) path = "";
            if (path.StartsWith("/")) return DataModel.Normalize(path);
            if (path == "" || path == ".") return Scope;
            return Scope + "/" + path;
        }

        public JsonValue Read(string path)
        {
            var abs = Resolve(path);
            Dependencies.Add(abs);
            return Data.Get(abs);
        }
    }
}

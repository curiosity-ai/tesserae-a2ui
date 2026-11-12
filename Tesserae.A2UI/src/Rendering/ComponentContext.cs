using System;
using System.Collections.Generic;
using Tesserae;
using static Transpose.Core.dom;

namespace Tesserae.A2UI
{
    /// <summary>
    /// What a catalog component factory is handed: the definition it renders, the surface it lives on, and the
    /// helpers that turn the definition's dynamic properties into live ones. Anything a factory subscribes to or
    /// creates through the context is released when the component is rebuilt or removed.
    /// </summary>
    public sealed class ComponentContext
    {
        private readonly ComponentSlot _slot;

        internal ComponentContext(ComponentSlot slot, JsonValue definition, string catalogId)
        {
            _slot      = slot;
            Definition = definition;
            CatalogId  = catalogId;
        }

        public Surface Surface => _slot.Surface;
        public string Id => _slot.Id;
        public string Type => Definition.GetString("component");
        public JsonValue Definition { get; }
        public string CatalogId { get; }

        /// <summary>The collection scope this component renders in ("" at the root, "/items/2" inside a template item).</summary>
        public string Scope => _slot.Scope;

        internal Disposer Owned => _slot.Owned;
        internal ComponentSlot Slot => _slot;

        public JsonValue Prop(string name) => Definition.Get(name);
        public bool Has(string name) => Definition.Has(name);

        /// <summary>A fresh evaluation context for this component's scope.</summary>
        public EvalContext NewEval(bool userActivated = false)
        {
            var ec = new EvalContext(Surface, Scope, CatalogId, userActivated);
            ec.Indices.AddRange(_slot.Indices);
            return ec;
        }

        public void Report(string code, string message) => Surface.Host.Report(new A2UIError(code, message, Surface.Id, "/components/" + Id));

        // ------------------------------------------------------------------ reading properties

        /// <summary>Evaluates a property once (no live updates).</summary>
        public JsonValue Eval(string prop)
        {
            var v = Definition.Get(prop);
            if (v is null) return null;
            try { return Expressions.Evaluate(v, NewEval()); }
            catch (A2UIFunctionException e) { Report(e.Code, e.Message); return null; }
        }

        public string Str(string prop, string fallback = "")
        {
            var v = Eval(prop);
            return v is null || v.IsNull ? fallback : v.ToDisplayString();
        }

        /// <summary>A literal (never data-bound) string property such as an enum value.</summary>
        public string Literal(string prop, string fallback = null) => Definition.GetString(prop, fallback);

        public bool Flag(string prop, bool fallback = false) => Definition.GetBool(prop, fallback);

        public double Num(string prop, double fallback = 0)
        {
            var n = Coerce.ToNumber(Eval(prop));
            return n.HasValue ? n.Value : fallback;
        }

        // ------------------------------------------------------------------ live properties

        /// <summary>Applies a property now and again every time data it depends on changes.</summary>
        public void Bind(string prop, Action<JsonValue> apply)
        {
            var v = Definition.Get(prop);
            if (v is null) return;
            Bind(v, apply);
        }

        public void Bind(JsonValue dynamic, Action<JsonValue> apply)
        {
            if (!Expressions.IsDynamic(dynamic) && !dynamic.IsObject && !dynamic.IsArray) { apply(dynamic); return; }
            Owned.Add(new LiveBinding<JsonValue>(this, ec => Expressions.Evaluate(dynamic, ec), apply));
        }

        public void BindString(string prop, Action<string> apply, string fallback = "")
        {
            Bind(prop, v => apply(v is null || v.IsNull ? fallback : v.ToDisplayString()));
        }

        public void BindBool(string prop, Action<bool> apply)
        {
            Bind(prop, v => apply(Coerce.ToBool(v)));
        }

        /// <summary>Rebuilds the whole component whenever the property's data changes (for properties that shape the component rather than fill it).</summary>
        public void Watch(string prop)
        {
            var v = Definition.Get(prop);
            if (v is null || !Expressions.IsDynamic(v)) return;
            var first = true;
            Owned.Add(new LiveBinding<JsonValue>(this, ec => Expressions.Evaluate(v, ec), _ =>
            {
                if (first) { first = false; return; }
                _slot.RequestRebuild();
            }));
        }

        // ------------------------------------------------------------------ two-way binding

        /// <summary>The absolute data model pointer a property is bound to (<c>{"@path": ...}</c>), or null when it is not a binding.</summary>
        public string BoundPath(string prop)
        {
            var v = Definition.Get(prop);
            if (!Expressions.IsBinding(v)) return null;
            return NewEval().Resolve(v.GetString("@path"));
        }

        /// <summary>Writes an input's new value to the path its property is bound to; a property that is not a binding keeps the value locally only.</summary>
        public void WriteBack(string prop, JsonValue value)
        {
            var path = BoundPath(prop);
            if (path is object) Surface.Data.Set(path, value);
        }

        // ------------------------------------------------------------------ children

        /// <summary>A component for the child with this id, in this component's scope. Missing definitions show nothing until they arrive.</summary>
        public IComponent Child(string childId)
        {
            var slot = new ComponentSlot(Surface, childId, Scope, new List<int>(_slot.Indices), CatalogId, _slot);
            Owned.Add(slot);
            return slot;
        }

        /// <summary>Fills a stack with the children a <c>ChildList</c> property describes: static ids or a template over a list in the data model.</summary>
        public void Children(Stack stack, string prop = "children")
        {
            var spec = Definition.Get(prop);
            if (spec is null) return;

            if (spec.IsArray)
            {
                foreach (var idv in spec.Items)
                {
                    if (idv.IsString) stack.Add(Child(idv.StringValue));
                }
                return;
            }

            if (spec.IsObject) Owned.Add(new TemplateChildren(this, stack, spec));
        }

        // ------------------------------------------------------------------ checks

        /// <summary>
        /// Evaluates this component's <c>@checks</c> live. <paramref name="apply"/> receives the messages of every
        /// failing rule, empty when all pass.
        /// </summary>
        public void BindChecks(Action<List<string>> apply)
        {
            var rules = Definition.Get("@checks");
            if (rules is null || !rules.IsArray || rules.Count == 0) return;

            Owned.Add(new LiveBinding<List<string>>(this, ec => EvaluateChecks(rules, ec), apply));
        }

        private static List<string> EvaluateChecks(JsonValue rules, EvalContext ec)
        {
            var failures = new List<string>();

            foreach (var rule in rules.Items)
            {
                var condition = rule.Get("condition");
                var result    = Expressions.Evaluate(condition, ec);
                var fallback  = rule.GetString("message");

                bool ok;
                string message = fallback;

                if (result is object && result.IsObject && result.Get("valid") is JsonValue valid && valid.IsBool)
                {
                    ok = valid.BoolValue;
                    var m = result.GetString("message");
                    if (!ok && m is object) message = m;
                }
                else
                {
                    ok = Coerce.ToBool(result);
                }

                if (!ok) failures.Add(message ?? "Invalid value");
            }

            return failures;
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Runs a component's <c>action</c> (an agent event or a local function call) on behalf of this component.</summary>
        public void RunAction(JsonValue action) => Surface.Host.RunAction(this, action);
    }
}

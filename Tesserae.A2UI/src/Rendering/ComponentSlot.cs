using System;
using System.Collections.Generic;
using Tesserae;
using static Transpose.Core.dom;

namespace Tesserae.A2UI
{
    /// <summary>
    /// The place one component id occupies in the tree, in one collection scope. It builds the Tesserae component the
    /// catalog maps the definition to and puts its own element in the tree; when the agent sends a new definition for the id it swaps
    /// that element for a rebuilt one, so a change to one component never disturbs its siblings (an input keeps its
    /// focus while another component updates). A definition that has not arrived yet renders nothing and fills in
    /// when it does: progressive rendering.
    /// </summary>
    public sealed class ComponentSlot : IComponent, IDisposable
    {
        private HTMLElement _element;
        private bool _subscribed;
        private bool _disposed;
        private bool _rebuilding;

        internal ComponentSlot(Surface surface, string id, string scope, List<int> indices, string catalogId, ComponentSlot parent)
        {
            Surface   = surface;
            Id        = id;
            Scope     = DataModel.Normalize(scope);
            Indices   = indices;
            CatalogId = catalogId;
            Parent    = parent;
        }

        public Surface Surface { get; }
        public string Id { get; }
        public string Scope { get; }
        internal List<int> Indices { get; }
        internal string CatalogId { get; }
        internal ComponentSlot Parent { get; }
        internal Disposer Owned { get; private set; } = new Disposer();

        public HTMLElement Render()
        {
            if (_element is null)
            {
                if (!_subscribed) { Surface.ComponentChanged += OnComponentChanged; _subscribed = true; }
                _element = Build();
            }
            return _element;
        }

        private void OnComponentChanged(string id)
        {
            if (!_disposed && id == Id) Rebuild();
        }

        internal void RequestRebuild()
        {
            if (!_disposed && !_rebuilding) Rebuild();
        }

        private void Rebuild()
        {
            if (_element is null || _rebuilding) return;
            _rebuilding = true;

            try
            {
                Owned.Dispose();
                var next = Build();
                var old  = _element;
                _element = next;

                var parent = old.parentNode;
                if (parent is object)
                {
                    if (old.classList.contains("tss-stack-item")) next.classList.add("tss-stack-item");
                    parent.replaceChild(next, old);
                }
            }
            finally { _rebuilding = false; }
        }

        private HTMLElement Build()
        {
            var def = Surface.GetComponent(Id);
            if (def is null) return Placeholder();

            // A component that contains itself would render forever.
            for (var p = Parent; p is object; p = p.Parent)
            {
                if (p.Id == Id && p.Scope == Scope) return Problem("Component '" + Id + "' contains itself");
            }

            var type      = def.GetString("component");
            //The spec's order: the component's own @catalogId, then the surface's. A child does not take its parent's: a Grid from an extension catalog holds Text of the basic one.
            var catalogId = def.GetString("@catalogId") ?? Surface.CatalogId;
            var catalog   = Surface.Host.ResolveCatalog(catalogId);

            if (catalog is null)
            {
                var message = catalogId is null
                    ? "Component '" + Id + "' has no catalog: the surface and the component both omit a catalogId"
                    : "No catalog '" + catalogId + "' is registered for component '" + Id + "'";
                Surface.Host.Report(new A2UIError(A2UIError.CatalogError, message, Surface.Id, "/components/" + Id));
                return Problem(message);
            }

            var factory = catalog.GetComponent(type);
            if (factory is null)
            {
                var message = "Catalog '" + catalog.Id + "' has no component '" + type + "'";
                Surface.Host.Report(new A2UIError(A2UIError.ValidationFailed, message, Surface.Id, "/components/" + Id + "/component"));
                return Problem(message);
            }

            HTMLElement el;
            var cx = new ComponentContext(this, def, catalog.Id);

            try
            {
                el = factory(cx).Render();
            }
            catch (Exception e)
            {
                var message = "Component '" + Id + "' (" + type + ") failed to render: " + e.Message;
                Surface.Host.Report(new A2UIError(A2UIError.ExecutionError, message, Surface.Id, "/components/" + Id));
                return Problem(message);
            }

            Decorate(el, def, cx);
            return el;
        }

        /// <summary>Everything every component has: its id for tooling, weight, and the accessibility attributes.</summary>
        private static void Decorate(HTMLElement el, JsonValue def, ComponentContext cx)
        {
            el.setAttribute("data-a2ui-id", cx.Id);
            el.setAttribute("data-a2ui-component", cx.Type);

            var weight = def.Get("weight");
            if (weight is object && weight.IsNumber)
            {
                // "Like flex-grow" with a zero basis, as an Expanded child is in Flutter: siblings with weights split the whole
                // width in proportion, so the cells of one column line up from row to row.
                el.style.flex = JsonValue.FormatNumber(weight.NumberValue) + " 1 0%";
                el.style.minWidth = "0";
            }

            var a11y = def.Get("@accessibility");
            if (a11y is null || !a11y.IsObject) return;

            var label = a11y.Get("label");
            if (label is object) cx.Bind(label, v => SetAttr(el, "aria-label", v));

            var description = a11y.Get("description");
            if (description is object) cx.Bind(description, v => SetAttr(el, "aria-description", v));

            var live = a11y.GetString("live");
            if (live is object && (live == "polite" || live == "assertive" || live == "off")) el.setAttribute("aria-live", live);

            var hidden = a11y.Get("hidden");
            if (hidden is object) cx.Bind(hidden, v => { if (Coerce.ToBool(v)) el.setAttribute("aria-hidden", "true"); else el.removeAttribute("aria-hidden"); });
        }

        private static void SetAttr(HTMLElement el, string name, JsonValue v)
        {
            if (v is null || v.IsNull || (v.IsString && v.StringValue.Length == 0)) el.removeAttribute(name);
            else el.setAttribute(name, v.ToDisplayString());
        }

        private static HTMLElement Placeholder()
        {
            var d = document.createElement("div");
            d.setAttribute("data-a2ui-pending", "true");
            d.style.display = "none";
            return d;
        }

        private HTMLElement Problem(string message)
        {
            var d = document.createElement("div");
            d.setAttribute("data-a2ui-problem", "true");
            d.setAttribute("role", "alert");
            d.textContent = message;
            d.style.color = "var(--tss-danger-foreground-color, #a4262c)";
            d.style.fontSize = "12px";
            d.style.padding = "4px 8px";
            d.style.border = "1px dashed currentColor";
            d.style.borderRadius = "4px";
            return d;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_subscribed) Surface.ComponentChanged -= OnComponentChanged;
            Owned.Dispose();
        }
    }

    /// <summary>The children of a container that come from a template over a list in the data model: one slot per item, kept in step with the list's length.</summary>
    internal sealed class TemplateChildren : IDisposable
    {
        private readonly ComponentContext _cx;
        private readonly Stack _stack;
        private readonly string _templateId;
        private readonly string _path;
        private readonly Disposer _items = new Disposer();
        private IDisposable _subscription;
        private int _count = -1;

        public TemplateChildren(ComponentContext cx, Stack stack, JsonValue spec)
        {
            _cx         = cx;
            _stack      = stack;
            _templateId = spec.GetString("componentId");
            _path       = cx.NewEval().Resolve(spec.GetString("path"));

            _subscription = cx.Surface.Data.Subscribe(new[] { _path }, Sync);
            Sync();
        }

        private void Sync()
        {
            var list  = _cx.Surface.Data.Get(_path);
            var count = list is object && list.IsArray ? list.Count : 0;
            if (count == _count) return;
            _count = count;

            _items.Dispose();
            _stack.Clear();

            for (var i = 0; i < count; i++)
            {
                var indices = new List<int>(_cx.NewEval().Indices);
                indices.Add(i);
                var slot = new ComponentSlot(_cx.Surface, _templateId, _path + "/" + i, indices, _cx.CatalogId, _cx.Slot);
                _items.Add(slot);
                _stack.Add(slot);
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _items.Dispose();
        }
    }
}

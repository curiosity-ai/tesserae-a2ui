using System;
using System.Collections.Generic;

namespace Tesserae.A2UI
{
    /// <summary>
    /// A catalog: the components and functions a surface may use, under one id. The basic catalog is
    /// <see cref="BasicCatalog"/>; an application adds its own components or whole catalogs by registering
    /// factories, without implementing anything else.
    /// </summary>
    public class Catalog
    {
        private readonly Dictionary<string, Func<ComponentContext, Tesserae.IComponent>> _components = new Dictionary<string, Func<ComponentContext, Tesserae.IComponent>>();
        private readonly Dictionary<string, FunctionDefinition> _functions = new Dictionary<string, FunctionDefinition>();

        public Catalog(string id) { Id = id; }

        public string Id { get; }

        public IEnumerable<string> ComponentNames => _components.Keys;
        public IEnumerable<string> FunctionNames => _functions.Keys;

        /// <summary>Registers (or replaces) a component type. The factory builds the Tesserae component for one definition.</summary>
        public Catalog Component(string type, Func<ComponentContext, Tesserae.IComponent> factory)
        {
            _components[type] = factory;
            return this;
        }

        public Catalog Function(FunctionDefinition definition)
        {
            _functions[definition.Name] = definition;
            return this;
        }

        public Catalog Function(string name, Func<EvalContext, JsonValue, JsonValue> implementation) => Function(new FunctionDefinition(name, implementation));

        /// <summary>
        /// Replaces a component with one that wraps it: <paramref name="wrap"/> receives the component context and a function that builds the
        /// original, so it can add to it (a tooltip, a class, a surrounding frame) without reimplementing it. The original is built
        /// when <c>inner()</c> is called, with the same context.
        /// </summary>
        public Catalog Decorate(string type, Func<ComponentContext, Func<Tesserae.IComponent>, Tesserae.IComponent> wrap)
        {
            var original = GetComponent(type);
            if (original is null) throw new InvalidOperationException("Catalog '" + Id + "' has no component '" + type + "' to decorate");

            _components[type] = cx => wrap(cx, () => original(cx));
            return this;
        }

        /// <summary>
        /// Replaces a function with one that wraps it: <paramref name="wrap"/> receives the context, the resolved arguments and a function that
        /// calls the original. The original's declared behaviour (allowed callers, user-activation rule, return type) is kept.
        /// </summary>
        public Catalog DecorateFunction(string name, Func<EvalContext, JsonValue, Func<JsonValue>, JsonValue> wrap)
        {
            var original = GetFunction(name);
            if (original is null) throw new InvalidOperationException("Catalog '" + Id + "' has no function '" + name + "' to decorate");

            _functions[name] = new FunctionDefinition(name, (ec, args) => wrap(ec, args, () => original.Implementation(ec, args)))
            {
                AllowedCallers         = original.AllowedCallers,
                RequiresUserActivation = original.RequiresUserActivation,
                ReturnType             = original.ReturnType
            };
            return this;
        }

        /// <summary>A copy of this catalog, under the same id or a new one. Changing the copy never changes the original.</summary>
        public Catalog Clone(string id = null)
        {
            var copy = new Catalog(id ?? Id);
            foreach (var kv in _components) copy._components[kv.Key] = kv.Value;
            foreach (var kv in _functions) copy._functions[kv.Key] = kv.Value;
            return copy;
        }

        public bool HasComponent(string type) => _components.ContainsKey(type);

        public Func<ComponentContext, Tesserae.IComponent> GetComponent(string type) => _components.TryGetValue(type, out var f) ? f : null;

        public FunctionDefinition GetFunction(string name) => _functions.TryGetValue(name, out var f) ? f : null;
    }
}

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

        public bool HasComponent(string type) => _components.ContainsKey(type);

        public Func<ComponentContext, Tesserae.IComponent> GetComponent(string type) => _components.TryGetValue(type, out var f) ? f : null;

        public FunctionDefinition GetFunction(string name) => _functions.TryGetValue(name, out var f) ? f : null;
    }
}

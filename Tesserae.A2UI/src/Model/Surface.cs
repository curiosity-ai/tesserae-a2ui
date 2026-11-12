using System;
using System.Collections.Generic;

namespace Tesserae.A2UI
{
    /// <summary>
    /// One live surface: its default catalog, its data model and the component definitions the agent has sent so far
    /// (the adjacency list the tree is rebuilt from). The surface knows nothing about the DOM; <see cref="SurfaceView"/>
    /// draws it and listens to <see cref="ComponentChanged"/>.
    /// </summary>
    public sealed class Surface
    {
        private readonly Dictionary<string, JsonValue> _components = new Dictionary<string, JsonValue>();
        private readonly List<string> _order = new List<string>();

        public Surface(string id, string catalogId, bool sendDataModel)
        {
            Id            = id;
            CatalogId     = catalogId;
            SendDataModel = sendDataModel;
        }

        public string Id { get; }
        public string CatalogId { get; }
        public bool SendDataModel { get; }
        public DataModel Data { get; } = new DataModel();

        /// <summary>The host the surface lives in (set when it is created); actions and errors go through it.</summary>
        public A2UIHost Host { get; internal set; }

        /// <summary>Raised with a component id whenever its definition was added or changed.</summary>
        public event Action<string> ComponentChanged;

        public IEnumerable<string> ComponentIds => _order;

        public int ComponentCount => _order.Count;

        public JsonValue GetComponent(string id) => id is object && _components.TryGetValue(id, out var d) ? d : null;

        /// <summary>Adds or replaces components. A definition identical to what is there changes nothing, so a resent component keeps its state.</summary>
        public void UpdateComponents(JsonValue list)
        {
            if (list is null) return;

            var changed = new List<string>();

            foreach (var def in list.Items)
            {
                if (def is null || !def.IsObject) { Host?.Report(new A2UIError(A2UIError.ValidationFailed, "A component must be an object", Id)); continue; }

                var id = def.GetString("id");
                if (string.IsNullOrEmpty(id) || def.GetString("component") is null)
                {
                    Host?.Report(new A2UIError(A2UIError.ValidationFailed, "A component needs 'id' and 'component'", Id, "/components"));
                    continue;
                }

                if (id == "Surface") { Host?.Report(new A2UIError(A2UIError.ValidationFailed, "'Surface' is reserved", Id)); continue; }

                if (_components.TryGetValue(id, out var old))
                {
                    if (JsonValue.DeepEquals(old, def)) continue;
                }
                else
                {
                    _order.Add(id);
                }

                _components[id] = def.Clone();
                changed.Add(id);
            }

            foreach (var id in changed) ComponentChanged?.Invoke(id);
        }
    }
}

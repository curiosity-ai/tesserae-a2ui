using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.A2UI
{
    /// <summary>
    /// The renderer: feed it A2UI messages and it keeps a Tesserae user interface in step, one surface per
    /// <c>surfaceId</c>. It owns the catalogs, the surfaces, and the channel back to the agent: user actions, renderer
    /// errors and function responses leave through <see cref="OnMessage"/> as the protocol's renderer-to-agent envelopes.
    /// <code>
    /// var host = new A2UIHost().OnAction(a => SendToAgent(a.ToEnvelope().ToJson()));
    /// host.Process(jsonlFromTheAgent);
    /// MountToBody(host.S());
    /// </code>
    /// A host is an ordinary Tesserae component: size it with the usual helpers and put it in any container.
    /// </summary>
    public sealed class A2UIHost : IComponent
    {
        private readonly Dictionary<string, Catalog> _catalogs = new Dictionary<string, Catalog>();
        private readonly Dictionary<string, Surface> _surfaces = new Dictionary<string, Surface>();
        private readonly List<string> _surfaceOrder = new List<string>();
        private readonly Dictionary<string, SurfaceView> _views = new Dictionary<string, SurfaceView>();
        private readonly Dictionary<string, TaskCompletionSource<JsonValue>> _pendingAgentCalls = new Dictionary<string, TaskCompletionSource<JsonValue>>();
        private readonly HashSet<string> _reported = new HashSet<string>();
        private readonly HTMLElement _root;
        private int _callCounter;

        private Action<A2UIAction> _onAction;
        private Action<A2UIError> _onError;
        private Action<JsonValue, JsonValue> _onMessage;
        private Action<A2UIMessage> _onProcessed;

        public A2UIHost()
        {
            _root = document.createElement("div");
            _root.className = "a2ui-host";
            _root.style.display = "flex";
            _root.style.flexDirection = "column";
            _root.style.gap = "16px";

            _catalogs[BasicCatalog.Id] = BasicCatalog.Instance;   // shared: copied the first time this host changes it (OwnCatalog)
        }

        // ------------------------------------------------------------------ catalogs

        /// <summary>Registers a catalog under its id. A surface (or a component with <c>@catalogId</c>) names the catalog it uses.</summary>
        public A2UIHost RegisterCatalog(Catalog catalog)
        {
            _catalogs[catalog.Id] = catalog;
            _ownedCatalogs.Add(catalog.Id);   // one the application registered is its own to change
            return this;
        }

        private readonly HashSet<string> _ownedCatalogs = new HashSet<string>();

        /// <summary>
        /// The catalog registered under <paramref name="id"/>, as this host's own copy: a catalog the host started with (the shared basic
        /// catalog) is copied the first time it is changed, so a change here never reaches another host.
        /// </summary>
        public Catalog OwnCatalog(string id = BasicCatalog.Id)
        {
            if (!_catalogs.TryGetValue(id, out var catalog)) throw new InvalidOperationException("No catalog '" + id + "' is registered on this host");

            if (_ownedCatalogs.Add(id))
            {
                catalog = catalog.Clone();
                _catalogs[id] = catalog;
            }

            return catalog;
        }

        /// <summary>
        /// Starts a new catalog from an existing one (the basic catalog by default) under a new id: everything the base has, to be
        /// changed or added to. A surface names the new id as its <c>catalogId</c>.
        /// </summary>
        public Catalog Extend(string newId, string baseId = BasicCatalog.Id)
        {
            if (!_catalogs.TryGetValue(baseId, out var baseCatalog)) throw new InvalidOperationException("No catalog '" + baseId + "' is registered on this host");

            var extended = baseCatalog.Clone(newId);
            _catalogs[newId] = extended;
            _ownedCatalogs.Add(newId);
            return extended;
        }

        /// <summary>Replaces a component of a catalog (the basic one by default) on this host only.</summary>
        public A2UIHost Override(string type, Func<ComponentContext, Tesserae.IComponent> factory, string catalogId = BasicCatalog.Id)
        {
            OwnCatalog(catalogId).Component(type, factory);
            return this;
        }

        /// <summary>Wraps a component of a catalog (the basic one by default) on this host only; see <c>Catalog.Decorate</c>.</summary>
        public A2UIHost Decorate(string type, Func<ComponentContext, Func<Tesserae.IComponent>, Tesserae.IComponent> wrap, string catalogId = BasicCatalog.Id)
        {
            OwnCatalog(catalogId).Decorate(type, wrap);
            return this;
        }

        /// <summary>Replaces a function of a catalog (the basic one by default) on this host only.</summary>
        public A2UIHost OverrideFunction(string name, Func<EvalContext, JsonValue, JsonValue> implementation, string catalogId = BasicCatalog.Id)
        {
            OwnCatalog(catalogId).Function(name, implementation);
            return this;
        }

        public Catalog ResolveCatalog(string id)
        {
            if (id is null) return null;
            if (_catalogs.TryGetValue(id, out var c)) return c;
            return null;
        }

        /// <summary>
        /// When a surface names a catalog this host does not know (or none), use this one instead of showing an error.
        /// Off by default, because the protocol says an unresolved catalog is an error.
        /// </summary>
        public Catalog FallbackCatalog { get; set; }

        // ------------------------------------------------------------------ events

        /// <summary>Adds a handler. Raised when the user triggers an agent action (a Button's <c>action.event</c>).</summary>
        public A2UIHost OnAction(Action<A2UIAction> handler) { _onAction += handler; return this; }

        /// <summary>Adds a handler. Raised for every renderer-side problem (bad message, unknown component, failing function).</summary>
        public A2UIHost OnError(Action<A2UIError> handler) { _onError += handler; return this; }

        /// <summary>
        /// Raised for every renderer-to-agent envelope (action, error, function call, function response) with the
        /// transport metadata that goes with it (the data model of a <c>sendDataModel</c> surface), or null.
        /// </summary>
        public A2UIHost OnMessage(Action<JsonValue, JsonValue> handler) { _onMessage += handler; return this; }

        /// <summary>Adds a handler. Raised after each agent message has been applied.</summary>
        public A2UIHost OnProcessed(Action<A2UIMessage> handler) { _onProcessed += handler; return this; }

        // ------------------------------------------------------------------ surfaces

        public IEnumerable<Surface> Surfaces
        {
            get { foreach (var id in _surfaceOrder) yield return _surfaces[id]; }
        }

        public Surface GetSurface(string id) => id is object && _surfaces.TryGetValue(id, out var s) ? s : null;

        public bool HasSurface(string id) => GetSurface(id) is object;

        /// <summary>Removes every surface (a new conversation, a replay).</summary>
        public A2UIHost Clear()
        {
            foreach (var id in new List<string>(_surfaceOrder)) RemoveSurface(id);
            _reported.Clear();
            return this;
        }

        private void RemoveSurface(string id)
        {
            if (_views.TryGetValue(id, out var view))
            {
                view.Dispose();
                var el = view.Render();
                if (el.parentNode is object) el.parentNode.removeChild(el);
                _views.Remove(id);
            }
            _surfaces.Remove(id);
            _surfaceOrder.Remove(id);
        }

        // ------------------------------------------------------------------ processing

        /// <summary>Applies messages from any stream shape (JSON Lines, one envelope, an array, a sample file). Malformed input is reported as errors.</summary>
        public A2UIHost Process(string text)
        {
            var errors = new List<string>();
            foreach (var json in A2UIMessage.ReadStream(text, errors)) Process(json);
            foreach (var e in errors) Report(new A2UIError(A2UIError.ValidationFailed, "Not valid JSON: " + e));
            return this;
        }

        /// <summary>Applies the messages with a pause between them, as a live stream would deliver them.</summary>
        public async Task ProcessAsync(string text, int delayMs)
        {
            var errors = new List<string>();
            foreach (var json in A2UIMessage.ReadStream(text, errors))
            {
                Process(json);
                if (delayMs > 0) await Task.Delay(delayMs);
            }
            foreach (var e in errors) Report(new A2UIError(A2UIError.ValidationFailed, "Not valid JSON: " + e));
        }

        public A2UIHost Process(JsonValue json)
        {
            A2UIMessage message;
            try { message = A2UIMessage.Parse(json); }
            catch (A2UIProtocolException e)
            {
                Report(new A2UIError(A2UIError.ValidationFailed, e.Message));
                return this;
            }

            return Process(message);
        }

        public A2UIHost Process(A2UIMessage m)
        {
            switch (m.Kind)
            {
                case A2UIMessageKind.CreateSurface:
                    CreateSurface(m);
                    break;

                case A2UIMessageKind.UpdateComponents:
                    WithSurface(m, s => s.UpdateComponents(m.Components));
                    break;

                case A2UIMessageKind.UpdateDataModel:
                    WithSurface(m, s => s.Data.Set(m.Path, m.Value));
                    break;

                case A2UIMessageKind.DeleteSurface:
                    if (HasSurface(m.SurfaceId)) RemoveSurface(m.SurfaceId);
                    else Report(new A2UIError(A2UIError.ValidationFailed, "No surface '" + m.SurfaceId + "' to delete", m.SurfaceId));
                    break;

                case A2UIMessageKind.CallRendererFunction:
                    HandleRendererFunction(m);
                    break;

                case A2UIMessageKind.AgentFunctionResponse:
                    HandleAgentFunctionResponse(m);
                    break;
            }

            _onProcessed?.Invoke(m);
            return this;
        }

        private void WithSurface(A2UIMessage m, Action<Surface> apply)
        {
            var s = GetSurface(m.SurfaceId);
            if (s is null)
            {
                Report(new A2UIError(A2UIError.ValidationFailed, "No surface '" + m.SurfaceId + "': send createSurface first", m.SurfaceId));
                return;
            }
            apply(s);
        }

        private void CreateSurface(A2UIMessage m)
        {
            if (HasSurface(m.SurfaceId))
            {
                Report(new A2UIError(A2UIError.IntegrityError, "Surface '" + m.SurfaceId + "' already exists: deleteSurface it first", m.SurfaceId));
                return;
            }

            var catalogId = m.CatalogId;
            if (ResolveCatalog(catalogId) is null && FallbackCatalog is object) catalogId = FallbackCatalog.Id;
            if (FallbackCatalog is object && !_catalogs.ContainsKey(FallbackCatalog.Id)) RegisterCatalog(FallbackCatalog);

            var surface = new Surface(m.SurfaceId, catalogId, m.SendDataModel) { Host = this };
            _surfaces[m.SurfaceId] = surface;
            _surfaceOrder.Add(m.SurfaceId);

            var view = new SurfaceView(surface);
            _views[m.SurfaceId] = view;
            _root.appendChild(view.Render());

            // dataModel first, so the components bind against the initial data on their first resolution
            if (m.InitialDataModel is object) surface.Data.Set("", m.InitialDataModel);
            if (m.Components is object) surface.UpdateComponents(m.Components);
        }

        // ------------------------------------------------------------------ renderer -> agent

        /// <summary>Reports a renderer-side problem: raised locally once per distinct message and sent to the agent.</summary>
        public void Report(A2UIError error)
        {
            var key = error.Code + "|" + error.SurfaceId + "|" + error.Path + "|" + error.Message;
            if (!_reported.Add(key)) return;

            _onError?.Invoke(error);
            Send(error.ToEnvelope(), null);
        }

        private void Send(JsonValue envelope, JsonValue metadata) => _onMessage?.Invoke(envelope, metadata);

        internal void RunAction(ComponentContext cx, JsonValue action)
        {
            if (action is null || !action.IsObject) return;

            var ec = cx.NewEval(userActivated: true);

            try
            {
                var evt = action.Get("event");
                if (evt is object && evt.IsObject)
                {
                    var a = new A2UIAction
                    {
                        Name              = evt.GetString("name"),
                        SurfaceId         = cx.Surface.Id,
                        SourceComponentId = cx.Id,
                        Timestamp         = Js.NowIso(),
                        UserMessage       = evt.Get("userMessage") is JsonValue um ? Expressions.EvaluateString(um, ec, null) : null,
                        Context           = evt.Get("context") is JsonValue ctx ? Expressions.Evaluate(ctx, ec) : JsonValue.NewObject(),
                        DataModel         = cx.Surface.SendDataModel ? cx.Surface.Data.Root.Clone() : null
                    };

                    _onAction?.Invoke(a);
                    Send(a.ToEnvelope(), a.ToMetadata());
                    return;
                }

                var call = action.Get("functionCall");
                if (call is object && call.IsObject)
                {
                    var name = call.GetString("@call");
                    var catalog = ResolveCatalog(call.GetString("catalogId") ?? cx.CatalogId);

                    if (catalog is object && catalog.GetFunction(name) is object)
                    {
                        Expressions.Evaluate(call, ec);
                    }
                    else
                    {
                        // Not implemented locally: the protocol routes it to the agent.
                        var args = call.Get("args") is JsonValue raw ? Expressions.Evaluate(raw, ec) : JsonValue.NewObject();
                        CallAgentFunctionAsync(cx.Surface, name, call.GetString("catalogId") ?? cx.CatalogId, args).FireAndForget();
                    }
                }
            }
            catch (A2UIFunctionException e)
            {
                Report(new A2UIError(e.Code, e.Message, cx.Surface.Id, "/components/" + cx.Id + "/action"));
            }
        }

        /// <summary>Asks the agent to run a function (<c>callAgentFunction</c>) and waits for its <c>agentFunctionResponse</c>.</summary>
        public Task<JsonValue> CallAgentFunctionAsync(Surface surface, string name, string catalogId, JsonValue args)
        {
            var id  = "fn_" + (++_callCounter);
            var tcs = new TaskCompletionSource<JsonValue>();
            _pendingAgentCalls[id] = tcs;

            var call = JsonValue.NewObject().Set("@call", JsonValue.String(name));
            if (catalogId is object) call.Set("catalogId", JsonValue.String(catalogId));
            if (args is object) call.Set("args", args);

            var body = JsonValue.NewObject()
               .Set("surfaceId", JsonValue.String(surface.Id))
               .Set("functionCallId", JsonValue.String(id))
               .Set("callFunction", call);

            Send(A2UIAction.Envelope("callAgentFunction", body), null);
            return tcs.Task;
        }

        private void HandleAgentFunctionResponse(A2UIMessage m)
        {
            if (!_pendingAgentCalls.TryGetValue(m.FunctionCallId, out var tcs)) return;
            _pendingAgentCalls.Remove(m.FunctionCallId);

            if (m.ResponseError is object)
                Report(new A2UIError(m.ResponseError.GetString("code") ?? A2UIError.ExecutionError, m.ResponseError.GetString("message") ?? "The agent function failed", null, null, m.FunctionCallId));

            tcs.SetResult(m.ResponseValue ?? JsonValue.Null);
        }

        private void HandleRendererFunction(A2UIMessage m)
        {
            JsonValue Respond(JsonValue value, string code = null, string message = null)
            {
                var body = JsonValue.NewObject().Set("functionCallId", JsonValue.String(m.FunctionCallId));
                if (code is object) body.Set("error", JsonValue.NewObject().Set("code", JsonValue.String(code)).Set("message", JsonValue.String(message)));
                else body.Set("value", value ?? JsonValue.Null);
                var env = A2UIAction.Envelope("rendererFunctionResponse", body);
                Send(env, null);
                return env;
            }

            var call      = m.CallFunction;
            var name      = call.GetString("@call");
            var catalogId = call.GetString("catalogId");

            if (string.IsNullOrEmpty(catalogId)) { Respond(null, A2UIError.InvalidFunction, "callFunction.catalogId is required"); return; }

            var catalog = ResolveCatalog(catalogId);
            var def     = catalog?.GetFunction(name);
            if (def is null) { Respond(null, A2UIError.InvalidFunction, "Function '" + name + "' is not registered in catalog '" + catalogId + "'"); return; }
            if (def.AllowedCallers == AllowedCallers.RendererOnly) { Respond(null, A2UIError.InvalidFunction, "Function '" + name + "' cannot be called by the agent (allowedCallers is rendererOnly)"); return; }
            if (def.RequiresUserActivation) { Respond(null, A2UIError.InvalidFunction, "Function '" + name + "' requires a user gesture"); return; }

            Surface surface = null;
            if (m.SurfaceId is object)
            {
                surface = GetSurface(m.SurfaceId);
                if (surface is null) { Respond(null, A2UIError.InvalidFunction, "No active surface '" + m.SurfaceId + "'"); return; }
            }

            try
            {
                var ec = new EvalContext(surface ?? new Surface("", catalogId, false) { Host = this }, "", catalogId, false);
                var args = call.Get("args") is JsonValue raw ? Expressions.Evaluate(raw, ec) : JsonValue.NewObject();
                Respond(def.Implementation(ec, args));
            }
            catch (A2UIFunctionException e)
            {
                Respond(null, e.Code, e.Message);
            }
        }

        // ------------------------------------------------------------------ IComponent

        public HTMLElement Render() => _root;
    }
}

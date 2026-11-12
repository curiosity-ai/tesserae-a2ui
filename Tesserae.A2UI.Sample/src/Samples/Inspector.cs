using System;
using System.Collections.Generic;
using Transpose.Core;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.A2UI.Sample
{
    /// <summary>
    /// What a host is doing, as tabs next to it: the agent messages applied so far, the live data models, everything the
    /// renderer sent back to the agent (actions, function calls, errors) and the renderer's errors.
    /// </summary>
    public sealed class Inspector : IComponent
    {
        private readonly A2UIHost _host;
        private readonly JsonViewer _messages = JsonViewer("[]").ExpandToDepth(1).MaxChildren(200);
        private readonly JsonViewer _data = JsonViewer("{}").ExpandToDepth(3);
        private readonly Stack _outbound = VStack().Gap(6.px()).WS();
        private readonly Stack _errors = VStack().Gap(6.px()).WS();
        private readonly Pivot _pivot = Pivot();
        private readonly HashSet<string> _watched = new HashSet<string>();
        private JsonValue _applied = JsonValue.NewArray();
        private int _outboundCount;
        private int _errorCount;

        public Inspector(A2UIHost host)
        {
            _host = host;

            host.OnProcessed(m => { _applied.Add(m.Json); _messages.Json(_applied.ToJson()); Watch(); RefreshData(); });
            host.OnMessage((envelope, metadata) => AddOutbound(envelope, metadata));
            host.OnError(e => AddError(e));

            _pivot.Pivot("messages", PivotTitle("Messages", UIcons.Messages), () => Pane(_messages), cached: true)
                  .Pivot("data", PivotTitle("Data model", UIcons.Database), () => Pane(_data), cached: true)
                  .Pivot("outbound", PivotTitle("To agent", UIcons.ArrowUpRight), () => Pane(_outbound), cached: true)
                  .Pivot("errors", PivotTitle("Errors", UIcons.TriangleWarning), () => Pane(_errors), cached: true);

            _outbound.Add(Empty("Nothing sent yet. Press a button in the UI: its action arrives here as the agent would receive it."));
            _errors.Add(Empty("No errors."));
        }

        private static IComponent Pane(IComponent c) => VStack().WS().H(420).ScrollY().Padding(8.px()).Children(c);

        private static IComponent Empty(string text) => TextBlock(text).Small().Secondary();

        public int OutboundCount => _outboundCount;
        public int ErrorCount => _errorCount;

        /// <summary>Forgets everything shown (before a replay).</summary>
        public void Reset()
        {
            _applied = JsonValue.NewArray();
            _messages.Json("[]");
            _data.Json("{}");
            _outbound.Clear();
            _outbound.Add(Empty("Nothing sent yet. Press a button in the UI: its action arrives here as the agent would receive it."));
            _errors.Clear();
            _errors.Add(Empty("No errors."));
            _outboundCount = 0;
            _errorCount = 0;
            _watched.Clear();
        }

        private void Watch()
        {
            foreach (var s in _host.Surfaces)
            {
                if (_watched.Add(s.Id)) s.Data.Changed += _ => RefreshData();
            }
        }

        private void RefreshData()
        {
            var all = JsonValue.NewObject();
            foreach (var s in _host.Surfaces) all.Set(s.Id, s.Data.Root);
            _data.Json(all.ToJson());
        }

        private void AddOutbound(JsonValue envelope, JsonValue metadata)
        {
            if (envelope.Has("error")) return;   // errors have their own tab
            if (_outboundCount == 0) _outbound.Clear();
            _outboundCount++;

            var shown = envelope;
            if (metadata is object) shown = JsonValue.NewObject().Set("message", envelope).Set("metadata", metadata);

            var kind = envelope.Keys.Count > 1 ? envelope.Keys[1] : "message";
            var title = kind + "  " + (envelope.Get(kind)?.GetString("name") ?? "");
            _outbound.Prepend(Card(JsonViewer(shown.ToJson()).ExpandToDepth(3)).Compact().SetTitle(title.Trim()).Class("a2ui-outbound"));
        }

        private void AddError(A2UIError e)
        {
            if (_errorCount == 0) _errors.Clear();
            _errorCount++;
            _errors.Prepend(Message(e.Code, e.Message + (e.SurfaceId is object ? "  (surface " + e.SurfaceId + (e.Path is object ? ", " + e.Path : "") + ")" : "")).Icon(UIcons.TriangleWarning).Class("a2ui-error"));
        }

        public HTMLElement Render() => _pivot.Render();
    }
}

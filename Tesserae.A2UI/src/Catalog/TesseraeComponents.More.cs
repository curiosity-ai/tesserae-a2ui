using System;
using System.Collections.Generic;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.A2UI
{
    internal static partial class TesseraeComponents
    {
        private static string TextOf(JsonValue v) => v is null || v.IsNull ? "" : (v.IsNumber ? JsonValue.FormatNumber(v.NumberValue) : Coerce.ToText(v));

        // ------------------------------------------------------------------ CodeDiff

        /// <summary><c>diff</c> (a unified diff, bindable), <c>format</c> ("line" or "side"), <c>fileList</c>.</summary>
        public static IComponent CodeDiff(ComponentContext cx)
        {
            var diff = Tesserae.UI.CodeDiff("", cx.Literal("format", "line") == "side" ? Tesserae.CodeDiff.Format.SideBySide : Tesserae.CodeDiff.Format.LineByLine);
            diff.DrawFileList = cx.Flag("fileList", false);

            cx.BindString("diff", s => diff.DiffText = s ?? "");

            return diff;
        }

        // ------------------------------------------------------------------ JsonViewer

        /// <summary><c>data</c> (any JSON, bindable), <c>expandDepth</c> (default 1), <c>rootLabel</c>, <c>maxHeight</c> (pixels, default 360).</summary>
        public static IComponent JsonViewer(ComponentContext cx)
        {
            var viewer = Tesserae.UI.JsonViewer().ExpandToDepth((int)cx.Num("expandDepth", 1));

            var label = cx.Literal("rootLabel", null);
            if (label is object) viewer.RootLabel(label);

            cx.Bind("data", v => viewer.Json(v is null ? "null" : v.ToJson()));

            var host = VStack().WS().Children(viewer);
            host.Render().style.maxHeight = ((int)cx.Num("maxHeight", 360)) + "px";
            host.Render().style.overflowY = "auto";

            return host;
        }

        // ------------------------------------------------------------------ Mermaid

        /// <summary><c>source</c> (Mermaid text, bindable), <c>zoom</c>.</summary>
        public static IComponent Mermaid(ComponentContext cx)
        {
            var diagram = Tesserae.UI.Mermaid("");
            if (cx.Flag("zoom", false)) diagram.Zoom();
            diagram.OnError(message => cx.Report("INVALID_DIAGRAM", "Mermaid '" + cx.Id + "': " + message));

            cx.BindString("source", s => diagram.Source = s ?? "");

            return diagram;
        }

        // ------------------------------------------------------------------ Toggle

        /// <summary><c>value</c> (bool, two-way), <c>label</c> (text beside the switch).</summary>
        public static IComponent Toggle(ComponentContext cx)
        {
            var toggle   = Tesserae.UI.Toggle();
            var applying = false;

            //The switch has its own on/off words; a label replaces them with one fixed text.
            cx.BindString("label", s => { if (!string.IsNullOrEmpty(s)) toggle.SetText(s); });
            cx.Bind("value", v =>
            {
                var b = Coerce.ToBool(v);
                if (toggle.IsChecked == b) return;
                applying = true;
                try { toggle.SetBoundValue(b); } finally { applying = false; }
            });

            toggle.OnChange((_, __) => { if (!applying) cx.WriteBack("value", JsonValue.Bool(toggle.IsChecked)); });

            return toggle;
        }

        // ------------------------------------------------------------------ Rating

        /// <summary><c>value</c> (a number, two-way), <c>max</c> (default 5), <c>readOnly</c>, <c>label</c>.</summary>
        public static IComponent Rating(ComponentContext cx)
        {
            var rating   = Tesserae.UI.Rating((int)cx.Num("max", 5));
            var applying = false;

            if (cx.Flag("readOnly", false)) rating.ReadOnly();

            cx.Bind("value", v =>
            {
                var n = (int)(Coerce.ToNumber(v) ?? 0);
                if (rating.Value == n) return;
                applying = true;
                try { rating.SetValue(n); } finally { applying = false; }
            });

            rating.OnChange(n => { if (!applying) cx.WriteBack("value", JsonValue.Number(n)); });

            return BasicComponents.Labelled(cx, rating);
        }

        // ------------------------------------------------------------------ TagsInput

        /// <summary><c>value</c> (a list of strings, two-way), <c>placeholder</c>, <c>label</c>, <c>maxTags</c>.</summary>
        public static IComponent TagsInput(ComponentContext cx)
        {
            var tags     = Tesserae.UI.TagsInput();
            var applying = false;
            var last     = "";

            if (cx.Has("maxTags")) tags.WithMaxTags((int)cx.Num("maxTags", 10));

            cx.BindString("placeholder", p => { if (!string.IsNullOrEmpty(p)) tags.SetPlaceholder(p); });

            cx.Bind("value", v =>
            {
                var wanted = new List<string>();
                if (v is object && v.IsArray) foreach (var i in v.Items) if (i.IsString) wanted.Add(i.StringValue);

                var key = string.Join("\u0001", wanted.ToArray());
                if (key == last) return;

                last = key;
                applying = true;
                try { tags.Clear(); foreach (var t in wanted) tags.Add(t); } finally { applying = false; }
            });

            tags.OnChange(() =>
            {
                if (applying) return;

                var arr = JsonValue.NewArray();
                var all = new List<string>();
                foreach (var t in tags.Tags) { arr.Add(JsonValue.String(t)); all.Add(t); }

                last = string.Join("\u0001", all.ToArray());
                cx.WriteBack("value", arr);
            });

            return BasicComponents.Labelled(cx, tags);
        }

        // ------------------------------------------------------------------ DateRange

        /// <summary><c>value</c> (<c>{from, to}</c> as yyyy-MM-dd, two-way), <c>label</c>.</summary>
        public static IComponent DateRange(ComponentContext cx)
        {
            var picker   = DateRangePicker();
            var applying = false;

            Func<DateTime?, string> iso = d => d.HasValue ? d.Value.ToString("yyyy-MM-dd") : "";

            cx.Bind("value", v =>
            {
                var from = v is object && v.IsObject ? v.GetString("from") : null;
                var to   = v is object && v.IsObject ? v.GetString("to")   : null;

                if (iso(picker.From) == (from ?? "") && iso(picker.To) == (to ?? "")) return;

                applying = true;
                try
                {
                    DateTime d;
                    if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out d)) picker.SetFrom(d);
                    if (!string.IsNullOrEmpty(to)   && DateTime.TryParse(to,   out d)) picker.SetTo(d);
                }
                finally { applying = false; }
            });

            picker.OnChange(p =>
            {
                if (applying) return;
                cx.WriteBack("value", JsonValue.NewObject().Set("from", JsonValue.String(iso(p.From))).Set("to", JsonValue.String(iso(p.To))));
            });

            return BasicComponents.Labelled(cx, picker);
        }

    }
}

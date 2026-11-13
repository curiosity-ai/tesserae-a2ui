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

        // ------------------------------------------------------------------ Progress

        /// <summary><c>value</c> (0 to 100, bindable; left out: indeterminate), <c>label</c>, <c>style</c> ("bar" or "ring"), <c>size</c> (ring diameter, 64), <c>showValue</c>.</summary>
        public static IComponent Progress(ComponentContext cx)
        {
            var ring      = cx.Literal("style", "bar") == "ring";
            var showValue = cx.Flag("showValue", true);
            var determinate = cx.Has("value");
            var label     = TextBlock("").Small().SemiBold();
            var current   = 0.0;
            var labelText = "";

            Action<IComponent> noop = _ => { };

            cx.BindString("label", s => { labelText = s; });

            if (ring)
            {
                var r = ProgressRing((int)cx.Num("size", 64), 6);
                if (!determinate) r.Indeterminate();

                cx.Bind("value", v =>
                {
                    var n = Math.Max(0, Math.Min(100, Coerce.ToNumber(v) ?? 0));
                    r.Progress(n, 100);
                    if (showValue) r.Label(Math.Round(n) + "%");
                });

                var caption = TextBlock("").Small();
                cx.BindString("label", s => caption.Text = s ?? "");

                return VStack().Gap(4.px()).AlignItems(ItemAlign.Start).Children(r, caption);
            }

            var bar = ProgressIndicator().WS();
            if (!determinate) bar.Indeterminated();

            Action refresh = () => label.Text = labelText + (showValue && determinate ? (labelText.Length > 0 ? "  " : "") + Math.Round(current) + "%" : "");

            cx.Bind("value", v => { current = Math.Max(0, Math.Min(100, Coerce.ToNumber(v) ?? 0)); bar.Progress((float)current); refresh(); });
            cx.BindString("label", s => { labelText = s ?? ""; refresh(); });

            return VStack().WS().Gap(4.px()).Children(label, bar);
        }

        // ------------------------------------------------------------------ Stepper

        /// <summary><c>items</c>: <c>[{title, description, child}]</c>; <c>value</c>: the current step (0-based, two-way).</summary>
        public static IComponent Stepper(ComponentContext cx)
        {
            var steps = new List<StepperStep>();
            var spec  = cx.Prop("items");

            if (spec is object && spec.IsArray)
            {
                foreach (var item in spec.Items)
                {
                    var child = item.GetString("child");
                    steps.Add(Step(item.GetString("title") ?? "", child is object ? cx.Child(child) : Empty(), item.GetString("description")));
                }
            }

            var stepper  = Tesserae.UI.Stepper(steps.ToArray());
            var applying = false;

            cx.Bind("value", v =>
            {
                var i = (int)(Coerce.ToNumber(v) ?? 0);
                if (i < 0 || i >= steps.Count || stepper.CurrentStepIndex == i) return;
                applying = true;
                try { stepper.SetStep(i, raiseEvent: false); } finally { applying = false; }
            });

            stepper.OnStepChange(s => { if (!applying) cx.WriteBack("value", JsonValue.Number(s.CurrentStepIndex)); });

            return stepper;
        }

        // ------------------------------------------------------------------ Timeline

        /// <summary><c>data</c> (a bindable list of <c>{title, text, time, color}</c>), <c>sameSide</c> (default true), <c>width</c> (pixels).</summary>
        public static IComponent Timeline(ComponentContext cx)
        {
            var timeline = new Tesserae.Timeline();
            if (cx.Flag("sameSide", true)) timeline.SameSide();
            if (cx.Has("width")) timeline.TimelineWidth(((int)cx.Num("width", 640)).px());

            cx.Bind("data", v =>
            {
                timeline.Clear();

                if (v is null || !v.IsArray) return;

                foreach (var e in v.Items)
                {
                    var parts = new List<IComponent>();

                    var title = e.GetString("title");
                    var time  = e.GetString("time");
                    var text  = e.GetString("text");

                    if (!string.IsNullOrEmpty(title)) parts.Add(TextBlock(title).SemiBold());
                    if (!string.IsNullOrEmpty(time))  parts.Add(TextBlock(time).Tiny().Secondary());
                    if (!string.IsNullOrEmpty(text))  parts.Add(TextBlock(text).Small());

                    var item  = VStack().Gap(2.px()).Children(parts.ToArray());
                    var color = e.GetString("color");

                    if (string.IsNullOrEmpty(color)) timeline.Add(item);
                    else timeline.Add(item, color);
                }
            });

            return timeline;
        }

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

        // ------------------------------------------------------------------ Avatar

        /// <summary><c>name</c> (initials are made from it), <c>image</c> (a URL), <c>status</c> ("online", "away", "busy", "offline"), <c>size</c> ("small", "medium", "large", "xlarge"), <c>subtitle</c>.</summary>
        public static IComponent Avatar(ComponentContext cx)
        {
            var avatar = Tesserae.UI.Avatar();

            switch (cx.Literal("size", "medium"))
            {
                case "small":  avatar.Size(AvatarSize.Small);  break;
                case "large":  avatar.Size(AvatarSize.Large);  break;
                case "xlarge": avatar.Size(AvatarSize.XLarge); break;
                default:       avatar.Size(AvatarSize.Medium); break;
            }

            switch (cx.Literal("status", ""))
            {
                case "online":  avatar.Presence(AvatarPresence.Online);  break;
                case "away":    avatar.Presence(AvatarPresence.Away);    break;
                case "busy":    avatar.Presence(AvatarPresence.Busy);    break;
                case "offline": avatar.Presence(AvatarPresence.Offline); break;
            }

            var persona = cx.Has("subtitle") ? Persona().SetAvatar(avatar) : null;

            cx.BindString("name", n =>
            {
                var words = (n ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var initials = words.Length == 0 ? "" : (words.Length == 1 ? words[0].Substring(0, 1) : words[0].Substring(0, 1) + words[words.Length - 1].Substring(0, 1)).ToUpperInvariant();
                avatar.SetInitials(initials);
                if (persona is object) persona.SetName(n ?? "");
            });

            cx.BindString("image", u => { if (MediaUrl.IsSafe(u)) avatar.SetImage(u); });
            cx.BindString("subtitle", s => { if (persona is object) persona.SetSecondaryText(s ?? ""); });

            return persona is object ? (IComponent)persona : avatar;
        }

        // ------------------------------------------------------------------ Carousel

        /// <summary><c>children</c> (slides: ids), <c>value</c> (the current slide, 0-based, two-way).</summary>
        public static IComponent Carousel(ComponentContext cx)
        {
            var slides = new List<IComponent>();
            var ids    = cx.Prop("children");

            if (ids is object && ids.IsArray) foreach (var id in ids.Items) if (id.IsString) slides.Add(cx.Child(id.StringValue));

            var carousel = Tesserae.UI.Carousel(slides.ToArray()).PadSlides();
            var applying = false;

            cx.Bind("value", v =>
            {
                var i = (int)(Coerce.ToNumber(v) ?? 0);
                if (i < 0 || i >= carousel.SlideCount || carousel.CurrentIndex == i) return;
                applying = true;
                try { carousel.SetIndex(i, raiseEvent: false); } finally { applying = false; }
            });

            carousel.OnSlideChange(c => { if (!applying) cx.WriteBack("value", JsonValue.Number(c.CurrentIndex)); });

            return carousel.WS();
        }
    }
}

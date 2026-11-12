using System;
using System.Collections.Generic;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.A2UI
{
    internal static partial class BasicComponents
    {
        private static ItemAlign? Align(string v)
        {
            switch (v)
            {
                case "start":   return ItemAlign.Start;
                case "center":  return ItemAlign.Center;
                case "end":     return ItemAlign.End;
                case "stretch": return ItemAlign.Stretch;
                default:        return null;
            }
        }

        private static ItemJustify? Justify(string v)
        {
            switch (v)
            {
                case "start":        return ItemJustify.Start;
                case "center":       return ItemJustify.Center;
                case "end":          return ItemJustify.End;
                case "spaceBetween": return ItemJustify.Between;
                case "spaceAround":  return ItemJustify.Around;
                case "spaceEvenly":  return ItemJustify.Evenly;
                default:             return null;
            }
        }

        // ------------------------------------------------------------------ Row / Column

        public static IComponent Linear(ComponentContext cx, Stack.Orientation orientation)
        {
            var stack = Stack(orientation).Gap(GapPx.px());
            stack.Render().style.minWidth = "0";

            var align = Align(cx.Literal("align", "stretch"));
            if (align.HasValue) stack.AlignItems(align.Value);

            var justify = Justify(cx.Literal("justify", "start"));
            if (justify.HasValue) stack.JustifyContent(justify.Value);

            cx.Children(stack);
            return stack;
        }

        // ------------------------------------------------------------------ List

        public static IComponent List(ComponentContext cx)
        {
            var horizontal = cx.Literal("direction", "vertical") == "horizontal";
            var stack = Stack(horizontal ? Stack.Orientation.Horizontal : Stack.Orientation.Vertical).Gap(GapPx.px());
            var el = stack.Render();
            el.style.minWidth = "0";
            if (horizontal) el.style.overflowX = "auto"; else el.style.overflowY = "auto";

            var align = Align(cx.Literal("align", "stretch"));
            if (align.HasValue) stack.AlignItems(align.Value);

            cx.Children(stack);
            return stack;
        }

        // ------------------------------------------------------------------ Tabs

        public static IComponent Tabs(ComponentContext cx)
        {
            var tabs  = cx.Prop("tabs");
            var pivot = Pivot();

            if (tabs is null || !tabs.IsArray) return pivot;

            for (var i = 0; i < tabs.Count; i++)
            {
                var tab = tabs.At(i);
                if (tab is null || !tab.IsObject) continue;

                var titleDyn = tab.Get("title");
                if (titleDyn is object && Expressions.IsDynamic(titleDyn))
                {
                    var first = true;
                    cx.Owned.Add(new LiveBinding<JsonValue>(cx, ec => Expressions.Evaluate(titleDyn, ec), _ => { if (first) { first = false; } else cx.Slot.RequestRebuild(); }));
                }

                var title   = titleDyn is object ? Expressions.EvaluateString(titleDyn, cx.NewEval(), "") : "";
                var childId = tab.GetString("child");
                var cached  = cx.Child(childId);

                pivot.Pivot("tab" + i, PivotTitle(title), () => cached, cached: true);
            }

            return pivot;
        }

        // ------------------------------------------------------------------ Modal

        public static IComponent Modal(ComponentContext cx)
        {
            // The trigger is all that is on the page. A wrapper that takes no part in layout hears the click even when the trigger rebuilds.
            var wrapper = document.createElement("div");
            wrapper.style.display = "contents";

            var triggerId = cx.Prop("trigger");
            var contentId = cx.Prop("content");

            if (triggerId is object && triggerId.IsString) wrapper.appendChild(cx.Child(triggerId.StringValue).Render());

            Modal modal = null;

            // Heard in the capture phase: a Tesserae button handles its own click and may stop it from bubbling.
            wrapper.addEventListener("click", _ =>
            {
                if (modal is null)
                {
                    var content = contentId is object && contentId.IsString ? cx.Child(contentId.StringValue) : TextBlock("");
                    modal = Tesserae.UI.Modal().LightDismiss().ShowCloseButton();
                    modal.Content = VStack().Padding(8.px()).Children(content);
                }
                modal.Show();
            }, true);

            cx.Owned.Add(() => { if (modal is object && modal.IsVisible) modal.Hide(); });
            return Raw(wrapper);
        }
    }
}

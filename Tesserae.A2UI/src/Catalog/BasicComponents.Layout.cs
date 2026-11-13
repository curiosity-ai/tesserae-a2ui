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


        // ------------------------------------------------------------------ labelled fields

        private static int _fieldIds;

        /// <summary>
        /// A control with its <c>label</c> above it, for every input that has one.
        /// </summary>
        /// <remarks>
        /// Not Tesserae's <c>Label</c>: its stylesheet takes the top margin off the first stack item (a rule for vertical stacks), so the first of two
        /// fields in a Row sat 4px higher than the second, and it insets the label text by its own margin, so the label did not line up with the box
        /// under it. This is the label, the control, a 4px gap and one outer margin, whatever the position.
        /// </remarks>
        internal static IComponent Labelled(ComponentContext cx, IComponent control)
        {
            if (cx.Prop("label") is null) return control;

            var text = document.createElement("label");
            text.style.fontWeight = "600";
            text.style.margin     = "0";
            text.style.minWidth   = "0";
            cx.BindString("label", s => text.textContent = s);

            var element = control.Render();
            element.style.margin = "0";

            //The label is the field's name for assistive technology and a click on it focuses the field.
            var input = element.querySelector("input,textarea,select") as HTMLElement;
            if (input is object)
            {
                if (string.IsNullOrEmpty(input.id)) input.id = "a2ui-field-" + (++_fieldIds);
                text.setAttribute("for", input.id);
            }

            var wrapper = VStack().Gap(4.px()).Children(Raw(text), control);
            wrapper.Render().style.minWidth = "0";
            return wrapper;
        }

        // ------------------------------------------------------------------ Row / Column

        public static IComponent Linear(ComponentContext cx, Stack.Orientation orientation)
        {
            var stack = Stack(orientation).Gap(GapPx.px());
            stack.Render().style.minWidth = "0";

            var align = Align(cx.Literal("align", "stretch"));
            if (align.HasValue) stack.AlignItems(align.Value);

            //A row that does not say how to align its children stretches them, as the catalog says - and a label, an icon or a button stretched
            //to the height of its tallest neighbour sits at the top of that cell. Those are centred; boxes (columns, cards, fields) still stretch.
            if (orientation == Stack.Orientation.Horizontal && !cx.Has("align")) stack.Render().classList.add("a2ui-row-auto");

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

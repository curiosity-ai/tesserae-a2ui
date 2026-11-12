using System;
using System.Collections.Generic;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.A2UI
{
    internal static partial class BasicComponents
    {
        private sealed class Option
        {
            public string Value;
            public string Label;
        }

        public static IComponent ChoicePicker(ComponentContext cx)
        {
            var multiple = cx.Literal("variant", "mutuallyExclusive") == "multipleSelection";
            var chips    = cx.Literal("displayStyle", "checkbox") == "chips";
            var filterable = cx.Flag("filterable");

            // Option labels may be bound; a change to one rebuilds the picker (the selection lives in the data model, so nothing is lost).
            var options = new List<Option>();
            var rawOptions = cx.Prop("options");
            if (rawOptions is object && rawOptions.IsArray)
            {
                foreach (var o in rawOptions.Items)
                {
                    var labelDyn = o.Get("label");
                    if (labelDyn is object && Expressions.IsDynamic(labelDyn))
                    {
                        var first = true;
                        cx.Owned.Add(new LiveBinding<JsonValue>(cx, ec => Expressions.Evaluate(labelDyn, ec), _ => { if (first) first = false; else cx.Slot.RequestRebuild(); }));
                    }
                    options.Add(new Option { Value = o.GetString("value") ?? "", Label = labelDyn is object ? Expressions.EvaluateString(labelDyn, cx.NewEval(), "") : "" });
                }
            }

            var selected = new List<string>();
            var applying = false;
            var setters  = new List<Action<bool>>();   // per option: reflect "is selected" into its control

            void Reflect()
            {
                applying = true;
                try { for (var i = 0; i < options.Count; i++) setters[i](selected.Contains(options[i].Value)); }
                finally { applying = false; }
            }

            void Commit()
            {
                if (applying) return;
                var arr = JsonValue.NewArray();
                foreach (var s in selected) arr.Add(JsonValue.String(s));
                cx.WriteBack("value", arr);
            }

            void Toggle(string value, bool on)
            {
                if (applying) return;
                if (!multiple) { selected.Clear(); if (on) selected.Add(value); }
                else if (on && !selected.Contains(value)) selected.Add(value);
                else if (!on) selected.Remove(value);
                Reflect();
                Commit();
            }

            var itemElements = new List<HTMLElement>();
            var list = chips ? (Stack)HStack().Wrap().Gap(GapPx.px()) : VStack().Gap(4.px());

            if (!multiple && !chips)
            {
                var group = ChoiceGroup();
                var choices = new List<ChoiceGroup.Choice>();
                for (var i = 0; i < options.Count; i++)
                {
                    var value  = options[i].Value;
                    var choice = new ChoiceGroup.Choice(options[i].Label);
                    choice.OnSelected(c => Toggle(value, true));
                    choices.Add(choice);
                    var idx = i;
                    setters.Add(on => { if (choices[idx].IsSelected != on) choices[idx].IsSelected = on; });
                    itemElements.Add(choice.Render());
                }
                group.Choices(choices.ToArray());
                list.Add(group);
            }
            else
            {
                for (var i = 0; i < options.Count; i++)
                {
                    var value = options[i].Value;
                    if (chips)
                    {
                        var toggle = new ToggleButton(Tesserae.UI.Button(options[i].Label).Compact());
                        toggle.OnChange((s, e) => Toggle(value, toggle.IsChecked));
                        setters.Add(on => { if (toggle.IsChecked != on) toggle.IsChecked = on; });
                        itemElements.Add(toggle.Render());
                        list.Add(toggle);
                    }
                    else
                    {
                        var box = Tesserae.UI.CheckBox(options[i].Label);
                        box.OnChange((s, e) => Toggle(value, box.IsChecked));
                        setters.Add(on => { if (box.IsChecked != on) box.IsChecked = on; });
                        itemElements.Add(box.Render());
                        list.Add(box);
                    }
                }
            }

            cx.Bind("value", v =>
            {
                selected.Clear();
                if (v is object && v.IsArray) foreach (var i in v.Items) { if (i.IsString) selected.Add(i.StringValue); }
                else if (v is object && v.IsString) selected.Add(v.StringValue);
                if (!multiple && selected.Count > 1) selected.RemoveRange(1, selected.Count - 1);
                Reflect();
            });

            var parts = new List<IComponent>();

            if (filterable)
            {
                var filter = TextBox().SetPlaceholder("Filter...");
                filter.Render().setAttribute("aria-label", "Filter options");
                filter.OnInput((_, __) =>
                {
                    var q = (filter.Text ?? "").Trim().ToLowerInvariant();
                    for (var i = 0; i < options.Count; i++)
                        itemElements[i].style.display = q.Length == 0 || options[i].Label.ToLowerInvariant().Contains(q) ? "" : "none";
                });
                parts.Add(filter);
            }

            parts.Add(list);

            IComponent control = parts.Count == 1 ? parts[0] : VStack().Gap(GapPx.px()).Children(parts.ToArray());

            if (cx.Prop("label") is object)
            {
                var l = Label("").SetContent(control);
                cx.BindString("label", s => l.Text = s);
                control = l;
            }

            return WithChecks(cx, control);
        }
    }
}

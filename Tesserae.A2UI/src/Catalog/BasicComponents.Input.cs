using System;
using System.Collections.Generic;
using System.Linq;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.A2UI
{
    internal static partial class BasicComponents
    {
        // ------------------------------------------------------------------ Button

        public static IComponent Button(ComponentContext cx)
        {
            var button = Tesserae.UI.Button();

            var variant = cx.Literal("variant", "default");
            if (variant == "primary") button.Primary();
            else if (variant == "borderless") button.Link();

            // A label or an icon is what a button is nearly always made of; those become the button's own text and icon so it
            // looks like every other Tesserae button. Anything else (a Row with an icon and text) is placed inside it.
            var childId = cx.Prop("child");
            var childDef = childId is object && childId.IsString ? cx.Surface.GetComponent(childId.StringValue) : null;

            if (childDef is object && childDef.GetString("component") == "Text" && !childDef.Has("@catalogId"))
            {
                var text = childDef.Get("text");
                if (text is object) cx.Bind(text, v => button.Text = StripMarkdown(Coerce.ToText(v)));
            }
            else if (childDef is object && childDef.GetString("component") == "Icon" && !childDef.Has("@catalogId"))
            {
                var nameValue = childDef.Get("name");
                var name = nameValue is object && nameValue.IsString ? nameValue.StringValue : Expressions.EvaluateString(nameValue, cx.NewEval(), "");
                IconMap.Glyph g;
                if (IconMap.TryGet(name, out g)) button.SetIcon(g.Icon, weight: g.Weight);
            }
            else if (childId is object && childId.IsString)
            {
                button.ReplaceContent(cx.Child(childId.StringValue));
            }

            var el = button.Render();
            el.setAttribute("type", "button");

            cx.BindChecks(failures =>
            {
                button.Disabled(failures.Count > 0);
                if (failures.Count > 0) el.title = string.Join("\n", failures);
                else el.removeAttribute("title");
            });

            var action = cx.Prop("action");
            button.OnClick(() => cx.RunAction(action));
            return button;
        }

        // ------------------------------------------------------------------ errors under a control that has no error line of its own

        private static IComponent WithChecks(ComponentContext cx, IComponent control)
        {
            var rules = cx.Prop("@checks");
            if (rules is null || !rules.IsArray || rules.Count == 0) return control;

            var error = TextBlock("").Small();
            var ee    = error.Render();
            ee.style.color = "var(--tss-danger-foreground-color, #a4262c)";
            ee.setAttribute("role", "alert");
            ee.style.display = "none";

            // A failing check is said out loud once the user has had a go at the control, not on a form nobody has touched yet.
            var touched  = false;
            var failures = new List<string>();

            Action show = () =>
            {
                var visible = touched && failures.Count > 0;
                ee.textContent   = visible ? failures[0] : "";
                ee.style.display = visible ? "" : "none";
            };

            var wrapper = VStack().Gap(2.px()).Children(control, error);
            var we = wrapper.Render();
            we.addEventListener("change", _ => { touched = true; show(); });
            we.addEventListener("focusout", _ => { touched = true; show(); });

            cx.BindChecks(f => { failures = f; show(); });

            return wrapper;
        }

        // ------------------------------------------------------------------ TextField

        public static IComponent TextField(ComponentContext cx)
        {
            var variant  = cx.Literal("variant", "shortText");
            var applying = false;

            Func<string> get;
            Action<string> set;
            Action<string> placeholder;
            Action<string> error;
            IComponent control;
            Action<Action> onInput;

            if (variant == "longText")
            {
                var ta = TextArea().Height(96.px());
                get = () => ta.Text; set = s => ta.Text = s; placeholder = s => ta.Placeholder = s;
                error = m => { ta.Error = m ?? ""; ta.IsInvalid = !string.IsNullOrEmpty(m); };
                onInput = h => ta.OnInput((_, __) => h());
                control = ta;
            }
            else
            {
                var tb = TextBox();
                if (variant == "obscured") tb.Password();
                if (variant == "number")
                {
                    var input = tb.Render();
                    input.setAttribute("type", "number");
                    input.setAttribute("step", "any");
                    input.setAttribute("inputmode", "decimal");
                }
                get = () => tb.Text; set = s => tb.Text = s; placeholder = s => tb.Placeholder = s;
                error = m => { tb.Error = m ?? ""; tb.IsInvalid = !string.IsNullOrEmpty(m); };
                onInput = h => tb.OnInput((_, __) => h());
                control = tb;
            }

            cx.BindString("placeholder", placeholder);

            cx.Bind("value", v =>
            {
                var s = Coerce.ToText(v);
                if (get() == s) return;
                applying = true;
                try { set(s); } finally { applying = false; }
            });

            onInput(() =>
            {
                if (applying) return;
                cx.WriteBack("value", JsonValue.String(get()));
            });

            // A failing check is shown once the field has been touched or holds something, so an empty form is not a wall of red.
            var touched  = false;
            var failing  = new List<string>();
            Action showError = () => error(failing.Count > 0 && (touched || !string.IsNullOrEmpty(get())) ? failing[0] : null);
            control.Render().addEventListener("focusout", _ => { touched = true; showError(); });
            onInput(() => { touched = true; showError(); });
            cx.BindChecks(failures => { failing = failures; showError(); });

            var label = cx.Prop("label");
            if (label is null) return control;

            var l = Label("").SetContent(control);
            cx.BindString("label", s => l.Text = s);
            return l;
        }

        // ------------------------------------------------------------------ CheckBox

        public static IComponent CheckBox(ComponentContext cx)
        {
            var box = Tesserae.UI.CheckBox();
            var applying = false;

            cx.BindString("label", s => box.SetText(s));
            cx.Bind("value", v =>
            {
                var b = Coerce.ToBool(v);
                if (box.IsChecked == b) return;
                applying = true;
                try { box.IsChecked = b; } finally { applying = false; }
            });

            box.OnChange((_, __) =>
            {
                if (applying) return;
                cx.WriteBack("value", JsonValue.Bool(box.IsChecked));
            });

            return WithChecks(cx, box);
        }

        // ------------------------------------------------------------------ Slider

        public static IComponent Slider(ComponentContext cx)
        {
            var min   = cx.Num("min", 0);
            var max   = cx.Num("max", 100);
            var range = max - min;
            if (range <= 0) range = 1;

            // Tesserae's slider moves in whole steps, so the range is mapped onto integer positions: one per unit of a whole-number
            // range, else the requested number of steps, else a hundred.
            var steps = cx.Has("steps") ? (int)Math.Max(1, cx.Num("steps", 100)) : (min == Math.Floor(min) && max == Math.Floor(max) ? (int)range : 100);
            if (steps < 1) steps = 1;
            var unit = range / steps;

            Func<double, int> toPos = v => (int)Math.Round((Math.Max(min, Math.Min(max, v)) - min) / unit);
            Func<int, double> toValue = p => Math.Round((min + p * unit) * 1e9) / 1e9;

            var slider = Tesserae.UI.Slider(0, 0, steps, 1);
            var readout = TextBlock("").Small();
            var applying = false;

            cx.Bind("value", v =>
            {
                var n = Coerce.ToNumber(v) ?? min;
                var p = toPos(n);
                readout.Text = JsonValue.FormatNumber(toValue(p));
                if (slider.Value == p) return;
                applying = true;
                try { slider.Value = p; } finally { applying = false; }
            });

            slider.OnInput((_, __) =>
            {
                var val = toValue(slider.Value);
                readout.Text = JsonValue.FormatNumber(val);
                if (applying) return;
                cx.WriteBack("value", JsonValue.Number(val));
            });

            var row = HStack().AlignItemsCenter().Gap(GapPx.px()).Children(slider.Grow(), readout);

            var labelProp = cx.Prop("label");
            IComponent control = row;
            if (labelProp is object)
            {
                var l = Label("").SetContent(row);
                cx.BindString("label", s => l.Text = s);
                control = l;
            }

            return WithChecks(cx, control);
        }

        // ------------------------------------------------------------------ DateTimeInput

        public static IComponent DateTimeInput(ComponentContext cx)
        {
            var date = cx.Flag("enableDate");
            var time = cx.Flag("enableTime");
            if (!date && !time) date = true;

            // The pickers wrap the browser's native inputs, whose text is "yyyy-MM-dd", "yyyy-MM-ddTHH:mm" or "HH:mm". The data model holds
            // ISO 8601 ("...:ss" added), and what the user picked is stored as picked, with no time zone conversion.
            var applying = false;
            Func<string> get;
            Action<string> set;
            Action<Action> onChange;
            Action<string, string> limits;
            IComponent control;
            HTMLElement input;

            if (date && time)
            {
                var p = DateTimePicker();
                input = p.Render();
                get = () => FromNative(p.Text, true, true);
                set = s => p.Text = ToNative(s, true, true);
                onChange = h => p.OnChange((_, __) => h());
                control = p;
            }
            else if (date)
            {
                var p = DatePicker();
                input = p.Render();
                get = () => FromNative(p.Text, true, false);
                set = s => p.Text = ToNative(s, true, false);
                onChange = h => p.OnChange((_, __) => h());
                control = p;
            }
            else
            {
                var p = new TimePicker();
                input = p.Render();
                get = () => FromNative(p.Text, false, true);
                set = s => p.Text = ToNative(s, false, true);
                onChange = h => p.OnChange((_, __) => h());
                control = p;
            }

            limits = (min, max) =>
            {
                if (!string.IsNullOrEmpty(min)) input.setAttribute("min", ToNative(min, date, time));
                if (!string.IsNullOrEmpty(max)) input.setAttribute("max", ToNative(max, date, time));
            };
            limits(cx.Str("min", null), cx.Str("max", null));

            cx.Bind("value", v =>
            {
                var s = Coerce.ToText(v);
                if (get() == s) return;
                applying = true;
                try { set(s); } finally { applying = false; }
            });

            onChange(() =>
            {
                if (applying) return;
                cx.WriteBack("value", JsonValue.String(get()));
            });

            if (cx.Prop("label") is JsonValue)
            {
                var l = Label("").SetContent(control);
                cx.BindString("label", s => l.Text = s);
                control = l;
            }

            return WithChecks(cx, control);
        }

        /// <summary>ISO 8601 text to what the native input takes.</summary>
        private static string ToNative(string iso, bool date, bool time)
        {
            if (string.IsNullOrEmpty(iso)) return "";
            iso = iso.Trim();
            var t = iso.IndexOf('T');
            var datePart = t >= 0 ? iso.Substring(0, t) : (iso.IndexOf(':') >= 0 ? "" : iso);
            var timePart = t >= 0 ? iso.Substring(t + 1) : (iso.IndexOf(':') >= 0 ? iso : "");

            if (timePart.Length > 5) timePart = timePart.Substring(0, 5);
            if (date && time) return datePart.Length >= 10 ? datePart.Substring(0, 10) + "T" + (timePart.Length == 5 ? timePart : "00:00") : "";
            if (date) return datePart.Length >= 10 ? datePart.Substring(0, 10) : "";
            return timePart.Length == 5 ? timePart : "";
        }

        /// <summary>The native input's text to ISO 8601.</summary>
        private static string FromNative(string native, bool date, bool time)
        {
            if (string.IsNullOrEmpty(native)) return "";
            if (date && time) return native.Length == 16 ? native + ":00" : native;
            if (time) return native.Length == 5 ? native + ":00" : native;
            return native;
        }
    }
}

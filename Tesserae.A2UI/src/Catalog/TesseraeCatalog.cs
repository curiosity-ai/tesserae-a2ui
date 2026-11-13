using System;
using System.Collections.Generic;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.A2UI
{
    /// <summary>
    /// An extension catalog of the Tesserae components a dashboard or a report is made of and the basic catalog has no counterpart for:
    /// <c>Table</c>, <c>Grid</c>, <c>Metric</c>, <c>Banner</c>, <c>Badge</c>, <c>PropertyGrid</c> and <c>Accordion</c>. A surface mixes it with the basic
    /// catalog the way any second catalog is mixed: each component names it with <c>@catalogId</c>.
    /// </summary>
    public static class TesseraeCatalog
    {
        public const string Id = "https://curiosity.ai/a2ui/catalogs/tesserae/1.0";

        /// <summary>A catalog holding the seven components, ready for <see cref="A2UIHost.RegisterCatalog"/>.</summary>
        public static Catalog Create() => new Catalog(Id)
           .Component("Table",        TesseraeComponents.Table)
           .Component("Grid",         TesseraeComponents.Grid)
           .Component("Metric",       TesseraeComponents.Metric)
           .Component("Banner",       TesseraeComponents.Banner)
           .Component("Badge",        TesseraeComponents.Badge)
           .Component("PropertyGrid", TesseraeComponents.PropertyGrid)
           .Component("Accordion",    TesseraeComponents.Accordion);

        /// <summary>Registers the extension on a host (or its private copy of the catalogs).</summary>
        public static A2UIHost AddTo(A2UIHost host) => host.RegisterCatalog(Create());
    }

    /// <summary>How a cell, a value or a property is turned into text, by the <c>format</c> a column or a field names.</summary>
    internal static class CellFormat
    {
        public static string Text(JsonValue v, string format, string currency)
        {
            if (v is null || v.IsNull) return "";

            switch (format)
            {
                case "number":
                {
                    var n = Coerce.ToNumber(v);
                    return n.HasValue ? Js.FormatNumber(n.Value, 0, 3, true) : Coerce.ToText(v);
                }
                case "percent":
                {
                    var n = Coerce.ToNumber(v);
                    return n.HasValue ? Js.FormatNumber(n.Value * 100, 0, 1, true) + "%" : Coerce.ToText(v);
                }
                case "currency":
                {
                    var n = Coerce.ToNumber(v);
                    return n.HasValue ? (Js.FormatCurrency(n.Value, string.IsNullOrEmpty(currency) ? "USD" : currency, -1, -1, true) ?? JsonValue.FormatNumber(n.Value)) : Coerce.ToText(v);
                }
                case "date":
                case "datetime":
                {
                    var parts = v.IsNumber ? Js.DatePartsFromMillis(v.NumberValue) : Js.DateParts(Coerce.ToText(v));
                    return parts is null ? Coerce.ToText(v) : BasicFunctions.Tr35(parts, format == "date" ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm");
                }
                case "bool":
                    return Coerce.ToBool(v) ? "yes" : "no";
            }

            if (v.IsObject || v.IsArray) return v.ToJson();

            return Coerce.ToText(v);
        }

        /// <summary>A width such as <c>120</c>, <c>"120px"</c>, <c>"2fr"</c> or <c>"30%"</c>.</summary>
        public static UnitSize Width(JsonValue v, UnitSize fallback)
        {
            if (v is null || v.IsNull) return fallback;

            if (v.IsNumber) return ((int)v.NumberValue).px();

            var s = Coerce.ToText(v).Trim();
            double n;

            if (s.EndsWith("fr") && double.TryParse(s.Substring(0, s.Length - 2), out n)) return ((int)Math.Max(1, n)).fr();
            if (s.EndsWith("%") && double.TryParse(s.Substring(0, s.Length - 1), out n)) return ((int)n).percent();
            if (s.EndsWith("px") && double.TryParse(s.Substring(0, s.Length - 2), out n)) return ((int)n).px();
            if (double.TryParse(s, out n)) return ((int)n).px();

            return fallback;
        }

        public static string Humanize(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";

            var sb = new System.Text.StringBuilder();

            for (var i = 0; i < field.Length; i++)
            {
                var c = field[i];

                if (c == '_' || c == '-') { sb.Append(' '); continue; }
                if (i > 0 && char.IsUpper(c) && char.IsLower(field[i - 1])) sb.Append(' ');

                sb.Append(i == 0 ? c.ToString().ToUpperInvariant() : c.ToString());
            }

            return sb.ToString();
        }
    }

    /// <summary>One column of a <c>Table</c>, from the definition's <c>columns</c> or inferred from the first row.</summary>
    internal sealed class TableColumn
    {
        public string Field;
        public string Label;
        public string Format;
        public string Currency;
        public string Align;
        public JsonValue Width;
    }

    /// <summary>One row of a <c>Table</c>: the JSON object and what to do with a click on it.</summary>
    internal sealed class JsonRow : IDetailsListItem<JsonRow>
    {
        private readonly List<TableColumn> _columns;
        private readonly Action<JsonValue>  _onClick;

        public JsonRow(JsonValue row, List<TableColumn> columns, Action<JsonValue> onClick)
        {
            Row      = row;
            _columns = columns;
            _onClick = onClick;
        }

        public JsonValue Row { get; }

        public bool EnableOnListItemClickEvent => _onClick is object;

        public void OnListItemClick(int listItemIndex) { if (_onClick is object) _onClick(Row); }

        public int CompareTo(JsonRow other, string columnSortingKey)
        {
            var a = Row.Get(columnSortingKey);
            var b = other.Row.Get(columnSortingKey);

            if (a is null || a.IsNull) return b is null || b.IsNull ? 0 : -1;
            if (b is null || b.IsNull) return 1;

            var x = a.IsNumber ? a.NumberValue : (double?)null;
            var y = b.IsNumber ? b.NumberValue : (double?)null;

            if (x.HasValue && y.HasValue) return x.Value.CompareTo(y.Value);

            return string.Compare(Coerce.ToText(a), Coerce.ToText(b), StringComparison.OrdinalIgnoreCase);
        }

        public IEnumerable<IComponent> Render(IList<IDetailsListColumn> columns, Func<IDetailsListColumn, Func<IComponent>, IComponent> createGridCellExpression)
        {
            for (var i = 0; i < columns.Count && i < _columns.Count; i++)
            {
                var spec = _columns[i];
                var text = CellFormat.Text(Row.Get(spec.Field), spec.Format, spec.Currency);

                yield return createGridCellExpression(columns[i], () =>
                {
                    var cell = TextBlock(text);

                    if (spec.Align == "end") cell.Render().style.textAlign = "right";
                    else if (spec.Align == "center") cell.Render().style.textAlign = "center";

                    return cell;
                });
            }
        }
    }

    internal static class TesseraeComponents
    {
        private const int GapPx = 12;

        // ------------------------------------------------------------------ Table

        /// <summary>
        /// <c>data</c> (bindable list of row objects), <c>columns</c> (<c>[{field, label, width, format, currency, align}]</c>, inferred from the first row when
        /// left out), <c>sortable</c> (default true), <c>sortBy</c> (a field), <c>maxHeight</c> (pixels, default 440), <c>emptyText</c>, <c>selected</c> / <c>action</c>.
        /// </summary>
        public static IComponent Table(ComponentContext cx)
        {
            var host = VStack().WS();

            JsonValue          rows      = null;
            var                signature = "";
            var                sortable  = cx.Flag("sortable", true);
            var                maxHeight = (int)cx.Num("maxHeight", 440);
            var                empty     = cx.Str("emptyText", "No rows");
            DetailsList<JsonRow> list    = null;

            Action<JsonValue> onClick = null;

            if (cx.Has("selected") || cx.Has("action"))
            {
                onClick = row =>
                {
                    cx.WriteBack("selected", row.Clone());
                    cx.RunAction(cx.Prop("action"));
                };
            }

            Action draw = () =>
            {
                if (rows is null || !rows.IsArray)
                {
                    host.Clear();
                    signature = "";
                    list      = null;
                    return;
                }

                var columns = ColumnsFor(cx, rows);
                var sig     = columns.Count + ":" + string.Join("|", columns.ConvertAll(c => c.Field).ToArray());
                var items   = new List<JsonRow>();

                foreach (var r in rows.Items) if (r.IsObject) items.Add(new JsonRow(r, columns, onClick));

                if (sig != signature || list is null)
                {
                    signature = sig;
                    host.Clear();

                    var defs = new IDetailsListColumn[columns.Count];

                    for (var i = 0; i < columns.Count; i++)
                    {
                        var c = columns[i];
                        defs[i] = DetailsListColumn(c.Label, CellFormat.Width(c.Width, 1.fr()), isRowHeader: i == 0, enableColumnSorting: sortable, sortingKey: c.Field);
                    }

                    list = DetailsList<JsonRow>(defs).Compact().WithEmptyMessage(() => TextBlock(empty).Padding("12px"));

                    //SortedBy only marks the header; the first rows come in already in that order.
                    var sortBy = cx.Literal("sortBy", null);
                    if (sortBy is object && sortable)
                    {
                        list.SortedBy(sortBy);
                        items.Sort((a, b) => a.CompareTo(b, sortBy));
                    }

                    host.Add(list);
                }

                // A list scrolls inside a height of its own: as tall as its rows, up to maxHeight.
                list.Height(Math.Min(maxHeight, 52 + 44 * Math.Max(items.Count, 1)).px());
                list.WithListItems(items.ToArray());
            };

            cx.Bind("data", v => { rows = v; draw(); });

            return host;
        }

        private static List<TableColumn> ColumnsFor(ComponentContext cx, JsonValue rows)
        {
            var list = new List<TableColumn>();
            var spec = cx.Prop("columns");

            if (spec is object && spec.IsArray && spec.Count > 0)
            {
                foreach (var c in spec.Items)
                {
                    var field = c.IsString ? c.StringValue : c.GetString("field");
                    if (string.IsNullOrEmpty(field)) continue;

                    list.Add(new TableColumn
                    {
                        Field    = field,
                        Label    = c.IsString ? CellFormat.Humanize(field) : (c.GetString("label") ?? CellFormat.Humanize(field)),
                        Format   = c.IsString ? null : c.GetString("format"),
                        Currency = c.IsString ? null : c.GetString("currency"),
                        Align    = c.IsString ? null : c.GetString("align"),
                        Width    = c.IsString ? null : c.Get("width"),
                    });
                }

                return list;
            }

            //No columns named: the keys of the first row that is an object, in order, up to twelve of them.
            foreach (var r in rows.Items)
            {
                if (!r.IsObject) continue;

                foreach (var key in r.Keys)
                {
                    if (list.Count >= 12) break;
                    list.Add(new TableColumn { Field = key, Label = CellFormat.Humanize(key) });
                }

                break;
            }

            return list;
        }

        // ------------------------------------------------------------------ Grid

        /// <summary><c>children</c> (ids or a template), <c>columns</c> (a count, or a track list such as "2fr 1fr"), <c>minColumnWidth</c> (pixels: as many columns as fit), <c>gap</c>.</summary>
        public static IComponent Grid(ComponentContext cx)
        {
            var stack = VStack().WS();
            var gap   = (int)cx.Num("gap", GapPx);
            var el    = stack.Render();

            el.style.display = "grid";
            el.style.gap     = gap + "px";

            var columns = cx.Prop("columns");

            if (cx.Has("minColumnWidth"))
            {
                el.style.gridTemplateColumns = "repeat(auto-fill, minmax(" + (int)cx.Num("minColumnWidth", 240) + "px, 1fr))";
            }
            else if (columns is object && columns.IsNumber)
            {
                el.style.gridTemplateColumns = "repeat(" + Math.Max(1, (int)columns.NumberValue) + ", minmax(0, 1fr))";
            }
            else if (columns is object && columns.IsString)
            {
                el.style.gridTemplateColumns = columns.StringValue;
            }
            else
            {
                el.style.gridTemplateColumns = "repeat(auto-fill, minmax(240px, 1fr))";
            }

            cx.Children(stack);
            return stack;
        }

        // ------------------------------------------------------------------ Metric

        /// <summary>
        /// A KPI tile. <c>label</c>, <c>value</c> (text or number), <c>delta</c> (text such as "+12.3%"), <c>trend</c> ("up", "down", "flat"; from the sign of
        /// <c>delta</c> when left out), <c>good</c> ("up" by default, or "down": which way is good news), <c>data</c> (a bindable list of numbers: a sparkline),
        /// <c>icon</c>, <c>large</c>.
        /// </summary>
        public static IComponent Metric(ComponentContext cx)
        {
            var title = TextBlock("").Small().SemiBold();
            var value = TextBlock("").XXLarge().Bold();

            cx.BindString("label", s => title.Text = s);
            cx.Bind("value", v => value.Text = v is null || v.IsNull ? "" : (v.IsNumber ? JsonValue.FormatNumber(v.NumberValue) : Coerce.ToText(v)));

            var metric = Tesserae.UI.Metric(title, value);

            if (cx.Flag("large", false)) metric.Large();

            IconMap.Glyph glyph;
            var iconName = cx.Literal("icon", null);
            if (iconName is object && IconMap.TryGet(iconName, out glyph)) metric.SetIcon(glyph.Icon, null, glyph.Weight);

            if (cx.Has("delta"))
            {
                var badge = TrendBadge("");
                var good  = cx.Literal("good", "up");

                cx.Bind("delta", v =>
                {
                    var text = v is null || v.IsNull ? "" : (v.IsNumber ? (v.NumberValue > 0 ? "+" : "") + JsonValue.FormatNumber(v.NumberValue) : Coerce.ToText(v));
                    var trend = cx.Literal("trend", null);

                    if (trend is null)
                    {
                        var t = text.TrimStart();
                        trend = t.StartsWith("-") || t.StartsWith("\u2212") ? "down" : (t.StartsWith("+") ? "up" : "flat");
                    }

                    var direction = trend == "up" ? TrendDirection.Up : (trend == "down" ? TrendDirection.Down : TrendDirection.Flat);

                    badge.SetText(text);
                    badge.Direction(direction);

                    if (direction == TrendDirection.Flat) badge.Neutral();
                    else if ((direction == TrendDirection.Up) == (good != "down")) badge.Good();
                    else badge.Bad();
                });

                metric.Change(badge);
            }

            if (cx.Has("data"))
            {
                var chart = VStack().WS();

                cx.Bind("data", v =>
                {
                    chart.Clear();

                    if (v is null || !v.IsArray || v.Count < 2) return;

                    var numbers = new List<double>();
                    foreach (var item in v.Items) { var n = Coerce.ToNumber(item); if (n.HasValue) numbers.Add(n.Value); }

                    if (numbers.Count >= 2) chart.Add(Sparkline(numbers.ToArray(), 200, 36).WS());
                });

                metric.Chart(chart);
            }

            return Card(metric).WS();
        }

        // ------------------------------------------------------------------ Banner

        /// <summary><c>title</c>, <c>text</c>, <c>severity</c> ("info", "success", "warning", "error"), <c>dismissible</c>, <c>actionLabel</c> with <c>action</c>.</summary>
        public static IComponent Banner(ComponentContext cx)
        {
            var banner = Tesserae.UI.Banner();

            switch (cx.Literal("severity", "info"))
            {
                case "success": banner.Success(); break;
                case "warning": banner.Warning(); break;
                case "error":   banner.Danger();  break;
                default:        banner.Primary(); break;
            }

            cx.BindString("title", s => banner.SetTitle(s));
            cx.BindString("text",  s => banner.SetText(s));

            if (cx.Flag("dismissible", false)) banner.OnDismiss(() => { });

            if (cx.Has("actionLabel") && cx.Has("action")) banner.Action(cx.Str("actionLabel", ""), () => cx.RunAction(cx.Prop("action")));

            return banner;
        }

        // ------------------------------------------------------------------ Badge

        /// <summary><c>text</c>, <c>tone</c> ("neutral", "primary", "success", "warning", "danger", "info"), <c>outline</c>, <c>icon</c>.</summary>
        public static IComponent Badge(ComponentContext cx)
        {
            var badge = Tesserae.UI.Badge().Pill();

            switch (cx.Literal("tone", "neutral"))
            {
                case "primary": badge.Primary(); break;
                case "success": badge.Success(); break;
                case "warning": badge.Warning(); break;
                case "danger":
                case "error":   badge.Danger();  break;
                case "info":    badge.Info();    break;
                default:        badge.Neutral(); break;
            }

            if (cx.Flag("outline", false)) badge.Outline();

            IconMap.Glyph glyph;
            var iconName = cx.Literal("icon", null);
            if (iconName is object && IconMap.TryGet(iconName, out glyph)) badge.SetIcon(Tesserae.Icon.Transform(glyph.Icon, glyph.Weight));

            cx.BindString("text", s => badge.SetText(s));

            return badge;
        }

        // ------------------------------------------------------------------ PropertyGrid

        /// <summary>
        /// The fields of one record. <c>data</c> (a bindable object), <c>fields</c> (names, or <c>[{field, label, format, currency}]</c>; every key when left out),
        /// <c>columns</c> (1 or 2), <c>editable</c> (strings, numbers and booleans become inputs that write back into <c>data</c>).
        /// </summary>
        public static IComponent PropertyGrid(ComponentContext cx)
        {
            var host     = VStack().WS();
            var columns  = Math.Max(1, Math.Min(3, (int)cx.Num("columns", 1)));
            var editable = cx.Flag("editable", false);
            var path     = cx.BoundPath("data");

            var el = host.Render();
            el.style.display             = "grid";
            el.style.gridTemplateColumns = "repeat(" + columns + ", minmax(0, 1fr))";
            el.style.columnGap           = "24px";
            el.style.rowGap              = "4px";

            var shown   = new Dictionary<string, IComponent>();
            var applying = false;
            var layout  = "";

            cx.Bind("data", v =>
            {
                if (v is null || !v.IsObject) { host.Clear(); layout = ""; shown.Clear(); return; }

                var fields = FieldsFor(cx, v);
                var sig    = string.Join("|", fields.ConvertAll(f => f.Field).ToArray());

                if (sig == layout && editable)
                {
                    //Editing writes the object back, which comes round to here: update what is shown, keep the inputs the person is typing in.
                    applying = true;
                    try { foreach (var f in fields) { IComponent c; if (shown.TryGetValue(f.Field, out c) && c is TextBox) { var tb = (TextBox)c; var t = CellFormat.Text(v.Get(f.Field), null, null); if (tb.Text != t && document.activeElement != tb.Render()) tb.Text = t; } } }
                    finally { applying = false; }
                    return;
                }

                layout = sig;
                host.Clear();
                shown.Clear();

                foreach (var f in fields)
                {
                    var current = v.Get(f.Field);
                    var name    = TextBlock(f.Label).Small().SemiBold();
                    name.Render().style.opacity = "0.7";
                    name.Render().style.minWidth = "0";

                    IComponent valueComponent;

                    if (editable && current is object && (current.IsString || current.IsNumber || current.IsBool))
                    {
                        var field = f.Field;

                        if (current.IsBool)
                        {
                            var box = CheckBox().SetText("");
                            box.IsChecked = current.BoolValue;
                            box.OnChange((_, __) => { if (!applying && path is object) cx.Surface.Data.Set(path + "/" + field, JsonValue.Bool(box.IsChecked)); });
                            valueComponent = box;
                        }
                        else
                        {
                            var isNumber = current.IsNumber;
                            var tb       = TextBox(CellFormat.Text(current, null, null));
                            tb.OnInput((_, __) =>
                            {
                                if (applying || path is null) return;

                                var n = isNumber ? Coerce.ToNumber(JsonValue.String(tb.Text)) : null;
                                cx.Surface.Data.Set(path + "/" + field, isNumber && n.HasValue ? JsonValue.Number(n.Value) : JsonValue.String(tb.Text));
                            });
                            shown[field]   = tb;
                            valueComponent = tb;
                        }
                    }
                    else
                    {
                        valueComponent = TextBlock(CellFormat.Text(current, f.Format, f.Currency));
                    }

                    // One cell per field: the name above its value, so two columns read as two columns.
                    host.Add(VStack().Gap(2.px()).Children(name, valueComponent));
                }
            });

            return host;
        }

        private static List<TableColumn> FieldsFor(ComponentContext cx, JsonValue record)
        {
            var list = new List<TableColumn>();
            var spec = cx.Prop("fields");

            if (spec is object && spec.IsArray && spec.Count > 0)
            {
                foreach (var f in spec.Items)
                {
                    var field = f.IsString ? f.StringValue : f.GetString("field");
                    if (string.IsNullOrEmpty(field)) continue;

                    list.Add(new TableColumn
                    {
                        Field    = field,
                        Label    = f.IsString ? CellFormat.Humanize(field) : (f.GetString("label") ?? CellFormat.Humanize(field)),
                        Format   = f.IsString ? null : f.GetString("format"),
                        Currency = f.IsString ? null : f.GetString("currency"),
                    });
                }

                return list;
            }

            foreach (var key in record.Keys) list.Add(new TableColumn { Field = key, Label = CellFormat.Humanize(key) });

            return list;
        }

        // ------------------------------------------------------------------ Accordion

        /// <summary><c>items</c>: <c>[{title, child, open}]</c> (<c>child</c> is a component id), <c>multiple</c> (several open at once; default false).</summary>
        public static IComponent Accordion(ComponentContext cx)
        {
            var expanders = new List<Expander>();
            var spec      = cx.Prop("items");

            if (spec is object && spec.IsArray)
            {
                foreach (var item in spec.Items)
                {
                    var expander = Tesserae.UI.Expander("", item.IsObject && item.GetString("child") is object ? cx.Child(item.GetString("child")) : null);
                    var title    = item.Get("title");

                    if (title is object) cx.Bind(title, v => expander.SetTitle(v is null || v.IsNull ? "" : Coerce.ToText(v)));
                    if (item.GetBool("open", false)) expander.Expanded();

                    expanders.Add(expander);
                }
            }

            return Tesserae.UI.Accordion(expanders.ToArray()).AllowMultipleOpen(cx.Flag("multiple", false)).WS();
        }
    }
}

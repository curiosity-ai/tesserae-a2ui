using System;
using Tesserae;
using static Tesserae.UI;

namespace Tesserae.A2UI
{
    /// <summary>
    /// An extension catalog with one component, <c>FlintChart</c>, drawn with Tesserae's <see cref="Tesserae.FlintChart"/> (Microsoft's Flint over Vega-Lite).
    /// A surface mixes it with the basic catalog the way any second catalog is mixed: the component names it with <c>@catalogId</c>.
    /// <code>{ "id": "c", "component": "FlintChart", "@catalogId": "https://curiosity.ai/a2ui/catalogs/flint-chart/1.0",
    ///         "chartType": "Bar Chart", "data": { "@path": "/_tools/q/value" }, "encodings": { "x": "Type", "y": "Count" } }</code>
    /// </summary>
    public static class FlintChartCatalog
    {
        public const string Id = "https://curiosity.ai/a2ui/catalogs/flint-chart/1.0";

        /// <summary>The id the extension was first published under; <see cref="AddTo"/> still answers to it, so an interface written with it keeps drawing.</summary>
        public const string LegacyId = "https://tesserae.dev/a2ui/catalogs/flint-chart/1.0";

        /// <summary>A catalog holding the FlintChart component, ready for <see cref="A2UIHost.RegisterCatalog"/>.</summary>
        public static Catalog Create() => new Catalog(Id).Component("FlintChart", FlintChartComponent);

        /// <summary>Registers the extension on a host under <see cref="Id"/> and <see cref="LegacyId"/>.</summary>
        public static A2UIHost AddTo(A2UIHost host)
        {
            var catalog = Create();
            return host.RegisterCatalog(catalog).RegisterCatalog(catalog.Clone(LegacyId));
        }

        /// <summary>
        /// Properties: <c>chartType</c> (Flint's name, "Bar Chart", "Line Chart", "Donut Chart"...), <c>data</c> (a list of row objects, bindable:
        /// a CSV file's <c>/files/x/rows</c> or a tool's rows), <c>encodings</c> (channel to field, or to a list of fields), <c>semanticTypes</c>
        /// (field to Flint's semantic type), <c>properties</c> (the chart type's own settings), <c>title</c>, <c>subtitle</c>, <c>height</c> (pixels),
        /// or <c>spec</c>, a whole Flint spec, in which <c>data</c> still replaces the rows. <c>selected</c> is a path that receives the row a click
        /// stood for, and <c>action</c> runs after it is written.
        /// </summary>
        private static IComponent FlintChartComponent(ComponentContext cx)
        {
            var chart = Tesserae.UI.FlintChart(FlintChartType.BarChart).WS();

            JsonValue rows  = null;
            JsonValue spec  = null;
            var       ready = false;
            var       title = "";
            var       subtitle = "";

            Action apply = () =>
            {
                if (!ready) return;

                if (rows is null || !rows.IsArray || rows.Count == 0) return;   // nothing to draw yet (a tool still running, an empty result)

                var whole = spec is object && spec.IsObject ? spec.Clone() : JsonValue.NewObject();

                var data = JsonValue.NewObject();
                data.Set("values", rows);
                whole.Set("data", data);

                var types = cx.Prop("semanticTypes");
                if (types is object && types.IsObject) whole.Set("semantic_types", types);

                var chartSpec = whole.Get("chart_spec");
                chartSpec = chartSpec is object && chartSpec.IsObject ? chartSpec.Clone() : JsonValue.NewObject();

                var chartType = cx.Literal("chartType", null);
                if (chartType is object) chartSpec.Set("chartType", JsonValue.String(chartType));

                var encodings = cx.Prop("encodings");
                if (encodings is object && encodings.IsObject) chartSpec.Set("encodings", encodings);

                var properties = cx.Prop("properties");
                if (properties is object && properties.IsObject) chartSpec.Set("chartProperties", properties);

                whole.Set("chart_spec", chartSpec);

                chart.Spec = whole.ToJson();

                // A spec replaces the one the title was written into.
                if (title.Length > 0) chart.Title(title);
                if (subtitle.Length > 0) chart.Subtitle(subtitle);
            };

            cx.Bind("data", v => { rows = v; apply(); });

            var specValue = cx.Prop("spec");
            if (specValue is object)
            {
                cx.Bind("spec", v =>
                {
                    if (v is object && v.IsString)
                    {
                        JsonValue parsed;
                        string error;
                        spec = JsonValue.TryParse(v.StringValue, out parsed, out error) ? parsed : null;
                        if (spec is null) cx.Report("INVALID_SPEC", "FlintChart '" + cx.Id + "': spec is not JSON (" + error + ")");
                    }
                    else spec = v;
                    apply();
                });
            }

            cx.BindString("title", s => { title = s ?? ""; apply(); });
            cx.BindString("subtitle", s => { subtitle = s ?? ""; apply(); });

            if (cx.Has("height")) chart.FillHeight().H(((int)cx.Num("height", 320)).px());

            // The chart is mounted before a bound list has arrived (a tool still running), and Flint says it has no rows; that is waiting, not an error.
            chart.OnError(message =>
            {
                if (rows is object && rows.IsArray && rows.Count > 0) cx.Report("INVALID_CHART", "FlintChart '" + cx.Id + "': " + message);
            });

            if (cx.Has("selected") || cx.Has("action"))
            {
                chart.OnElementClick(click =>
                {
                    if (click.Row is object) cx.WriteBack("selected", JsonValue.Parse(click.Row.ToJson()));
                    cx.RunAction(cx.Prop("action"));
                });
            }

            ready = true;
            apply();

            return chart;
        }
    }
}

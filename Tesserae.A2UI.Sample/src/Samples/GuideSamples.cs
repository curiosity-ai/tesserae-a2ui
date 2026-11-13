using System;
using System.Linq;
using System.Threading.Tasks;
using Transpose.Core;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.A2UI.Sample.SamplesHelper;

namespace Tesserae.A2UI.Sample
{
    /// <summary>The frame the guide pages share: title, an overview card, then the live host next to its inspector.</summary>
    internal static class Guide
    {
        public static IComponent Page(string title, UIcons icon, string subtitle, string source, string overview, params IComponent[] sections)
        {
            var s = SectionStack().Secondary().SampleTitle(title, icon, subtitle, source)
               .FlatSection(VStack().Children(Card(VStack().WS().Children(MarkdownBlock(overview))).SetTitle("Overview")));
            foreach (var e in sections) s = s.FlatSection(VStack().WS().Children(e));
            return s;
        }

        public static IComponent Live(A2UIHost host, Inspector inspector, string stageTitle = "Rendered by Tesserae.A2UI") =>
            HStack().WS().Wrap().Gap(12.px()).AlignItems(ItemAlign.Start).Children(
                Card(VStack().WS().Padding(16.px()).Children(host.WS())).SetTitle(stageTitle).Grow().MinWidth(360.px()),
                Card(inspector).SetTitle("Protocol traffic").Grow(2).MinWidth(360.px()));
    }

    [SampleDetails(Group = "Guides", Order = 10, Icon = UIcons.Home)]
    public class OverviewSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public OverviewSample()
        {
            var host = new A2UIHost();
            var inspector = new Inspector(host);

            host.Process(J(@"[
              {'version':'1.0','createSurface':{'surfaceId':'hello','catalogId':'" + BASIC + @"','dataModel':{'name':'world'},'components':[
                {'id':'root','component':'Card','child':'col'},
                {'id':'col','component':'Column','children':['title','field','greeting','go']},
                {'id':'title','component':'Text','text':'## Hello from an agent'},
                {'id':'field','component':'TextField','label':'Your name','value':{'@path':'/name'}},
                {'id':'greeting','component':'Text','text':{'@call':'formatString','args':{'value':'Hello, ${/name}!'}}},
                {'id':'go_label','component':'Text','text':'Tell the agent'},
                {'id':'go','component':'Button','child':'go_label','variant':'primary','action':{'event':{'name':'greeted','context':{'who':{'@path':'/name'}}}}}
              ]}}
            ]"));

            _content = Guide.Page("Overview", UIcons.Home, "Agent messages in, a Tesserae interface out", "GuideSamples.cs",
@"**Tesserae.A2UI** draws [A2UI](https://a2ui.org) (Agent-to-User Interface) v1.0 messages with Tesserae components. An agent
streams JSON: `createSurface`, `updateComponents`, `updateDataModel`, `deleteSurface`. The host keeps a live interface
in step with it and sends what the user does back as an `action`.

```csharp
var host = new A2UIHost()
    .OnAction(a => SendToAgent(a.ToEnvelope().ToJson()));   // a user pressed a button

host.Process(jsonlFromTheAgent);                             // JSON Lines, one envelope, or an array
MountToBody(host.S());
```

Type in the field below: the greeting changes as you type (two-way binding to the data model). Press the button and look
at the **To agent** tab: that is the message the agent receives, with the bindings in its context already resolved.
The examples in the sidebar are the specification's own, each next to the messages that produce it.",
                Guide.Live(host, inspector));
        }

        public HTMLElement Render() => _content.Render();
    }

    [SampleDetails(Group = "Guides", Order = 20, Icon = UIcons.Terminal)]
    public class PlaygroundSample : IComponent, ISample
    {
        private const string Starter = @"{""version"":""1.0"",""createSurface"":{""surfaceId"":""play"",""catalogId"":""" + BASIC + @""",""dataModel"":{""count"":3},""components"":[
{""id"":""root"",""component"":""Card"",""child"":""col""},
{""id"":""col"",""component"":""Column"",""children"":[""t"",""s"",""b""]},
{""id"":""t"",""component"":""Text"",""text"":{""@call"":""formatString"",""args"":{""value"":""**${/count}** ${pluralize(value: ${/count}, one: 'item', other: 'items')}""}}},
{""id"":""s"",""component"":""Slider"",""label"":""Count"",""min"":0,""max"":10,""value"":{""@path"":""/count""}},
{""id"":""bl"",""component"":""Text"",""text"":""Reset""},
{""id"":""b"",""component"":""Button"",""child"":""bl"",""action"":{""event"":{""name"":""reset"",""context"":{""count"":{""@path"":""/count""}}}}}
]}}";

        private readonly IComponent _content;

        public PlaygroundSample()
        {
            var host = new A2UIHost();
            var inspector = new Inspector(host);
            var input = TextArea(Starter).Height(280.px()).WS();
            var textarea = input.Render().querySelector("textarea") as HTMLElement;
            textarea.setAttribute("aria-label", "A2UI messages");
            textarea.style.fontFamily = "var(--tss-monospace-font-family, monospace)";

            var render = Button("Render").Primary().SetIcon(UIcons.Play).OnClick(() => { host.Clear(); inspector.Reset(); host.Process(input.Text); });
            var append = Button("Append").SetIcon(UIcons.Plus).Tooltip("Applies the messages without clearing: send an updateComponents or updateDataModel to a surface that is already there").OnClick(() => host.Process(input.Text));
            var clear = Button("Clear").SetIcon(UIcons.Cross).OnClick(() => { host.Clear(); inspector.Reset(); });
            render.Render().id = "playground-render";

            _content = Guide.Page("Playground", UIcons.Terminal, "Paste A2UI messages, see the interface", "GuideSamples.cs",
                "Paste JSON Lines, one envelope, an array, or a whole example file (`{ \"messages\": [...] }`). **Render** replaces what is on screen; **Append** applies the messages to what is there, which is how a stream delivers an update. Problems show on the **Errors** tab and are what the renderer would report to the agent.",
                Card(VStack().WS().Gap(8.px()).Children(input, HStack().Gap(8.px()).Children(render, append, clear))).SetTitle("Messages"),
                Guide.Live(host, inspector));
            host.Process(Starter);
        }

        public HTMLElement Render() => _content.Render();
    }

    [SampleDetails(Group = "Guides", Order = 30, Icon = UIcons.Link)]
    public class DataBindingSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public DataBindingSample()
        {
            var host = new A2UIHost();
            var inspector = new Inspector(host);

            host.Process(J(@"[{'version':'1.0','createSurface':{'surfaceId':'bind','catalogId':'" + BASIC + @"','sendDataModel':true,
              'dataModel':{'user':{'name':'Ada'},'volume':40,'notify':true,'tags':['a2ui','tesserae'],'todos':[{'title':'Write the spec','done':true},{'title':'Render it','done':false}]},
              'components':[
                {'id':'root','component':'Column','children':['name','hello','vol','vol_text','notify','todos_title','todos']},
                {'id':'name','component':'TextField','label':'Name (writes /user/name)','value':{'@path':'/user/name'}},
                {'id':'hello','component':'Text','text':{'@call':'formatString','args':{'value':'### Hello, ${/user/name}'}}},
                {'id':'vol','component':'Slider','label':'Volume','min':0,'max':100,'value':{'@path':'/volume'}},
                {'id':'vol_text','component':'Text','variant':'caption','text':{'@call':'formatString','args':{'value':'Volume is ${/volume}, notifications ${/notify}'}}},
                {'id':'notify','component':'CheckBox','label':'Notifications','value':{'@path':'/notify'}},
                {'id':'todos_title','component':'Text','text':'**To do** (a template over /todos)'},
                {'id':'todos','component':'Column','children':{'componentId':'todo','path':'/todos'}},
                {'id':'todo','component':'CheckBox','label':{'@path':'title'},'value':{'@path':'done'}}
              ]}}]"));

            var addTodo = Button("Agent: add a to-do").SetIcon(UIcons.Plus).OnClick(() =>
            {
                var surface = host.GetSurface("bind");
                var todos = surface.Data.Get("/todos");
                var n = todos is object ? todos.Count : 0;
                host.Process(new JsonValue[] { JsonValue.Parse(J("{'version':'1.0','updateDataModel':{'surfaceId':'bind','path':'/todos/" + n + "','value':{'title':'Item " + (n + 1) + "','done':false}}}")) }[0]);
            });
            var rename = Button("Agent: rename to Grace").SetIcon(UIcons.Pencil).OnClick(() =>
                host.Process(JsonValue.Parse(J("{'version':'1.0','updateDataModel':{'surfaceId':'bind','path':'/user/name','value':'Grace'}}"))));
            var removeName = Button("Agent: remove /user/name").SetIcon(UIcons.Trash).Tooltip("value: null deletes the key").OnClick(() =>
                host.Process(JsonValue.Parse(J("{'version':'1.0','updateDataModel':{'surfaceId':'bind','path':'/user/name','value':null}}"))));

            _content = Guide.Page("Data binding", UIcons.Link, "One data model, two-way", "GuideSamples.cs",
@"A property is a literal, a binding `{""@path"": ""/user/name""}`, or a function call `{""@call"": ""formatString"", ...}`. Input components
**write** the path they are bound to as the user types, so the greeting, the caption and the to-do list below update immediately and the agent
is only told when an action fires (this surface is created with `sendDataModel`, so the whole model rides along with it).
A template (`children: { componentId, path }`) makes one child per item and resolves **relative** paths (`title`, `done`) inside it.
The buttons play the agent: `updateDataModel` upserts a path, and `null` deletes it.",
                HStack().WS().Wrap().Gap(8.px()).Children(addTodo, rename, removeName),
                Guide.Live(host, inspector));
        }

        public HTMLElement Render() => _content.Render();
    }

    [SampleDetails(Group = "Guides", Order = 40, Icon = UIcons.Bolt)]
    public class ActionsAndFunctionsSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ActionsAndFunctionsSample()
        {
            var host = new A2UIHost();
            var inspector = new Inspector(host);

            // The agent: it answers callAgentFunction, which is what the renderer sends for a function its catalog does not implement.
            host.OnMessage((envelope, metadata) =>
            {
                var call = envelope.Get("callAgentFunction");
                if (call is null) return;
                var id = call.GetString("functionCallId");
                var args = call.Get("callFunction").Get("args");
                Task.Delay(400).ContinueWith(_ => host.Process(JsonValue.Parse(J("{'version':'1.0','agentFunctionResponse':{'functionCallId':'" + id + "','value':{'price':'" + (args?.GetString("sku") ?? "?") + " costs 12.50 EUR'}}}"))));
            });

            host.Process(J(@"[{'version':'1.0','createSurface':{'surfaceId':'acts','catalogId':'" + BASIC + @"',
              'dataModel':{'email':'','sku':'A-42','agree':false},
              'components':[
                {'id':'root','component':'Card','child':'col'},
                {'id':'col','component':'Column','children':['email','agree','row']},
                {'id':'email','component':'TextField','label':'Email','value':{'@path':'/email'},'@checks':[
                  {'condition':{'@call':'required','args':{'value':{'@path':'/email'}}},'message':'Email is required'},
                  {'condition':{'@call':'email','args':{'value':{'@path':'/email'}}},'message':'That does not look like an email address'}]},
                {'id':'agree','component':'CheckBox','label':'I agree to the terms','value':{'@path':'/agree'}},
                {'id':'row','component':'Row','children':['submit','docs','price'],'justify':'start'},
                {'id':'submit_l','component':'Text','text':'Submit'},
                {'id':'submit','component':'Button','variant':'primary','child':'submit_l','action':{'event':{'name':'submit','userMessage':{'@call':'formatString','args':{'value':'Submitted ${/email}'}},'context':{'email':{'@path':'/email'},'agreed':{'@path':'/agree'}}}},
                  '@checks':[{'condition':{'@call':'and','args':{'values':[{'@call':'email','args':{'value':{'@path':'/email'}}},{'@path':'/agree'}]}},'message':'Enter a valid email and accept the terms'}]},
                {'id':'docs_l','component':'Text','text':'Open a2ui.org'},
                {'id':'docs','component':'Button','variant':'borderless','child':'docs_l','action':{'functionCall':{'@call':'openUrl','args':{'url':'https://a2ui.org'}}}},
                {'id':'price_l','component':'Text','text':'Ask the agent for a price'},
                {'id':'price','component':'Button','child':'price_l','action':{'functionCall':{'@call':'lookupPrice','args':{'sku':{'@path':'/sku'}}}}}
              ]}}]"));

            // A function the agent may call: the catalog says so with allowedCallers.
            const string DEVICE = "https://example.com/catalogs/device/1.0";
            host.RegisterCatalog(new Catalog(DEVICE).Function(new FunctionDefinition("getScreenSize", (ec, args) =>
                JsonValue.NewArray().Add(JsonValue.Number(window.innerWidth)).Add(JsonValue.Number(window.innerHeight))) { AllowedCallers = AllowedCallers.RendererOrAgent, ReturnType = "array" }));

            var rf = Button("Agent calls getScreenSize on the renderer").SetIcon(UIcons.Rocket).OnClick(() =>
                host.Process(JsonValue.Parse(J("{'version':'1.0','callRendererFunction':{'surfaceId':'acts','functionCallId':'call_" + Guid.NewGuid().ToString().Substring(0, 4) + "','callFunction':{'@call':'getScreenSize','catalogId':'" + DEVICE + "'}}}"))));
            var rf1 = Button("Agent calls formatCurrency (refused)").SetIcon(UIcons.Lock).Tooltip("basic-catalog functions are rendererOnly unless the catalog says otherwise").OnClick(() =>
                host.Process(JsonValue.Parse(J("{'version':'1.0','callRendererFunction':{'surfaceId':'acts','functionCallId':'call_fmt','callFunction':{'@call':'formatCurrency','catalogId':'" + BASIC + "','args':{'value':1234.5,'currency':'EUR'}}}}"))));
            var rf2 = Button("Agent calls openUrl (refused)").SetIcon(UIcons.Lock).Tooltip("openUrl needs a user gesture and is rendererOnly: the renderer answers with INVALID_FUNCTION_CALL").OnClick(() =>
                host.Process(JsonValue.Parse(J("{'version':'1.0','callRendererFunction':{'functionCallId':'call_open','callFunction':{'@call':'openUrl','catalogId':'" + BASIC + "','args':{'url':'https://a2ui.org'}}}}"))));

            _content = Guide.Page("Actions and functions", UIcons.Bolt, "Events, checks, and calls in both directions", "GuideSamples.cs",
@"A button's `action` is either an **event** (sent to the agent with its context resolved at the moment of the click) or a **functionCall**
(run on the renderer; a function the catalog does not implement is routed to the agent as `callAgentFunction`, and the page plays that agent
here). **`@checks`** validate inputs live: the field shows the first failing message, and a button with a failing check is disabled.
The agent can call back too: `callRendererFunction` runs a function whose catalog says `rendererOrAgent` (`getScreenSize`, answered in **To agent**), while a `rendererOnly`\
function (the default, so every basic-catalog function) or a user-gesture one such as `openUrl` is refused with `INVALID_FUNCTION_CALL`.",
                HStack().WS().Wrap().Gap(8.px()).Children(rf, rf1, rf2),
                Guide.Live(host, inspector));
        }

        public HTMLElement Render() => _content.Render();
    }

    [SampleDetails(Group = "Guides", Order = 50, Icon = UIcons.Puzzle)]
    public class CustomCatalogSample : IComponent, ISample
    {
        private const string Weather = "https://example.com/catalogs/stars/1.0";
        private readonly IComponent _content;

        public CustomCatalogSample()
        {
            var host = new A2UIHost();
            var inspector = new Inspector(host);

            // A catalog is a name, a set of components and a set of functions. These two come from Tesserae and from the application.
            var stars = new Catalog(Weather)
               .Component("Rating", cx =>
                {
                    var rating = Rating((int)cx.Num("max", 5));
                    var applying = false;
                    cx.Bind("value", v => { applying = true; try { rating.SetValue((int)(Coerce.ToNumber(v) ?? 0)); } finally { applying = false; } });
                    rating.OnChange(v => { if (!applying) cx.WriteBack("value", JsonValue.Number(v)); });
                    if (cx.Flag("readOnly")) rating.ReadOnly();
                    return rating;
                })
               .Function("shout", (ec, args) => JsonValue.String(Coerce.ToText(args.Get("value")).ToUpperInvariant() + "!"));

            host.RegisterCatalog(stars);

            // The page changes its own copy of the basic catalog (OwnCatalog): host.Override / Decorate change the shared one for every host,
            // which would change every other page of this gallery.
            host.OwnCatalog()
               .Component("Divider", cx => HorizontalSeparator("✦ replaced Divider ✦").Primary())
               .Decorate("Button", (cx, inner) =>
                {
                    var button = inner();                                   // the basic Button, bindings and action included
                    button.Render().setAttribute("data-decorated", "true");
                    return button;
                });

            host.Process(J(@"[{'version':'1.0','createSurface':{'surfaceId':'custom','catalogId':'" + BASIC + @"','dataModel':{'stars':3,'name':'tesserae'},'components':[
                {'id':'root','component':'Card','child':'col'},
                {'id':'col','component':'Column','children':['t','rate','shown','shout','div','btn']},
                {'id':'div','component':'Divider'},
                {'id':'btn_l','component':'Text','text':'A decorated Button'},
                {'id':'btn','component':'Button','child':'btn_l','action':{'event':{'name':'decorated'}}},
                {'id':'t','component':'Text','text':'A basic Card and Text around a component from another catalog'},
                {'id':'rate','component':'Rating','@catalogId':'" + Weather + @"','max':5,'value':{'@path':'/stars'}},
                {'id':'shown','component':'Text','text':{'@call':'formatString','args':{'value':'You rated it ${/stars} of 5'}}},
                {'id':'shout','component':'Text','text':{'@call':'shout','catalogId':'" + Weather + @"','args':{'value':{'@path':'/name'}}}}
              ]}}]"));

            _content = Guide.Page("Custom catalogs", UIcons.Puzzle, "Your own components and functions", "GuideSamples.cs",
@"A catalog is an id plus components and functions. Register one on the host and a surface can mix it with the basic catalog: a component names its
catalog with `@catalogId`, a function call with `catalogId`. This one adds a **Rating** (the Tesserae component, bound two-way to `/stars`) and a **shout**
function, in a handful of lines of C#:

```csharp
var catalog = new Catalog(""https://example.com/catalogs/stars/1.0"")
    .Component(""Rating"", cx => { var r = Rating(5); cx.Bind(""value"", v => r.SetValue((int)(Coerce.ToNumber(v) ?? 0)));
                                   r.OnChange(v => cx.WriteBack(""value"", JsonValue.Number(v))); return r; })
    .Function(""shout"", (ec, args) => JsonValue.String(Coerce.ToText(args.Get(""value"")).ToUpperInvariant() + ""!""));
host.RegisterCatalog(catalog);

// Changing the basic catalog for every host (it is one shared instance):
host.Override(""Divider"", cx => HorizontalSeparator(""replaced"").Primary());            // replace a component
host.Decorate(""Button"", (cx, inner) => { var b = inner(); /* add to it */ return b; });   // wrap the original
host.OverrideFunction(""formatCurrency"", (ec, args) => JsonValue.String(""..."") );      // replace a function

// Or for one host only: change its own copy.
host.OwnCatalog().Component(""Divider"", cx => HorizontalSeparator(""replaced"").Primary());
var mine = host.Extend(""https://example.com/catalogs/mine/1.0"");                       // a new catalog id, starting from the basic one
```

Nothing is declared on the components themselves: the host finds the catalog by id, and the context helpers (`Bind`, `WriteBack`, `Child`, `Children`, `BindChecks`,
`RunAction`) give a component bindings, two-way data, children and validation without knowing how any of them work.

The basic catalog can be changed too. `Override` replaces a component, `Decorate` wraps the original and `OverrideFunction` replaces a function: these change the shared basic catalog, so they apply to every host. `OwnCatalog()` gives one host a private copy to change instead (this page replaces the Divider and tags the Button that way, so the other pages of the gallery are untouched), and `Extend` starts a catalog under a new id from an existing one.",
                Guide.Live(host, inspector));
        }

        public HTMLElement Render() => _content.Render();
    }

    [SampleDetails(Group = "Guides", Order = 55, Icon = UIcons.ChartHistogram)]
    public class ChartsSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ChartsSample()
        {
            var host = new A2UIHost();
            var inspector = new Inspector(host);

            FlintChartCatalog.AddTo(host);

            host.Process(J(@"[{'version':'1.0','createSurface':{'surfaceId':'charts','catalogId':'" + BASIC + @"','dataModel':{
                'sales':[{'Product':'Notebooks','Sales':1840,'Region':'North'},{'Product':'Pens','Sales':2650,'Region':'North'},{'Product':'Ink','Sales':730,'Region':'South'},{'Product':'Paper','Sales':3120,'Region':'South'}],
                'picked':{}},'components':[
                {'id':'root','component':'Card','child':'col'},
                {'id':'col','component':'Column','children':['chart','hint']},
                {'id':'chart','component':'FlintChart','@catalogId':'" + FlintChartCatalog.Id + @"','chartType':'Bar Chart','height':260,
                   'data':{'@path':'/sales'},'semanticTypes':{'Sales':'Amount','Product':'Category'},'encodings':{'x':'Product','y':'Sales'},
                   'title':'Sales per product','selected':{'@path':'/picked'},'action':{'event':{'name':'bar_clicked','context':{'row':{'@path':'/picked'}}}}},
                {'id':'hint','component':'Text','variant':'caption','text':{'@call':'formatString','args':{'value':'Clicked: ${/picked/Product} (${/picked/Sales})'}}}
              ]}}]"));

            _content = Guide.Page("Charts", UIcons.ChartHistogram, "A FlintChart component from an extension catalog", "GuideSamples.cs",
@"`FlintChartCatalog` is an **extension catalog**: one component, `FlintChart`, drawn with Tesserae's `FlintChart` (Microsoft's Flint over Vega-Lite). It is not part of the basic
catalog, so a surface names it per component with `@catalogId`, the way the **Custom catalogs** page mixes in its Rating:

```csharp
FlintChartCatalog.AddTo(host);        // host.RegisterCatalog(FlintChartCatalog.Create())
```

```json
{ ""id"": ""chart"", ""component"": ""FlintChart"", ""@catalogId"": ""https://curiosity.ai/a2ui/catalogs/flint-chart/1.0"",
  ""chartType"": ""Bar Chart"", ""data"": { ""@path"": ""/sales"" },
  ""encodings"": { ""x"": ""Product"", ""y"": ""Sales"" }, ""semanticTypes"": { ""Sales"": ""Amount"" },
  ""title"": ""Sales per product"", ""height"": 260 }
```

`data` is a list of row objects and can be bound, so the chart redraws when the list changes. `chartType`, `encodings`, `semanticTypes` and `properties` are Flint's own
vocabulary; `spec` takes a whole Flint spec instead. `selected` is a path that receives the row a click stood for, and `action` runs after it is written (click a bar).",
                Guide.Live(host, inspector));
        }

        public HTMLElement Render() => _content.Render();
    }

    [SampleDetails(Group = "Guides", Order = 56, Icon = UIcons.ChartTree)]
    public class DashboardComponentsSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public DashboardComponentsSample()
        {
            var host = new A2UIHost();
            var inspector = new Inspector(host);

            TesseraeCatalog.AddTo(host);

            var json = @"[{'version':'1.0','createSurface':{'surfaceId':'dash','catalogId':'" + BASIC + @"','dataModel':{
                'sales':[
                  {'Product':'Notebooks','Region':'North','Units':1840,'Revenue':9200.5,'Updated':'2026-11-02'},
                  {'Product':'Pens','Region':'North','Units':2650,'Revenue':3180,'Updated':'2026-11-05'},
                  {'Product':'Ink','Region':'South','Units':730,'Revenue':2190,'Updated':'2026-10-28'},
                  {'Product':'Paper','Region':'South','Units':3120,'Revenue':15600,'Updated':'2026-11-09'},
                  {'Product':'Folders','Region':'East','Units':910,'Revenue':1820,'Updated':'2026-11-01'}],
                'trend':[12,14,13,18,17,22,24,23,29],
                'account':{'Name':'Acme GmbH','Plan':'Team','Seats':25,'Active':true,'Owner':'Ada Weber'},
                'picked':{}},'components':[
                {'id':'root','component':'Column','children':['title','banner','tiles','table','hint','details','who','faq']},
                {'id':'title','component':'Text','text':'# Quarter at a glance'},
                {'id':'banner','component':'Banner','@catalogId':'__TC__','severity':'warning','title':'South is behind plan','text':'Ink and Paper are 12% under the quarterly target.'},
                {'id':'tiles','component':'Grid','@catalogId':'__TC__','columns':3,'children':['m1','m2','m3']},
                {'id':'m1','component':'Metric','@catalogId':'__TC__','label':'Revenue','value':'$32.0k','delta':'+12.3%','data':{'@path':'/trend'},'icon':'payment'},
                {'id':'m2','component':'Metric','@catalogId':'__TC__','label':'Open tickets','value':41,'delta':'+6','good':'down'},
                {'id':'m3','component':'Metric','@catalogId':'__TC__','label':'Churn','value':'2.1%','delta':'-0.4pt','good':'down'},
                {'id':'table','component':'Table','@catalogId':'__TC__','data':{'@path':'/sales'},'sortBy':'Product',
                  'columns':[{'field':'Product'},{'field':'Region'},{'field':'Units','format':'number'},{'field':'Revenue','format':'currency','currency':'USD'},{'field':'Updated','format':'date'}],
                  'selected':{'@path':'/picked'},'action':{'event':{'name':'row_clicked','context':{'row':{'@path':'/picked'}}}}},
                {'id':'hint','component':'Text','variant':'caption','text':{'@call':'formatString','args':{'value':'Clicked row: ${/picked/Product}'}}},
                {'id':'details','component':'PropertyGrid','@catalogId':'__TC__','data':{'@path':'/account'},'columns':2,'editable':true},
                {'id':'who','component':'Text','variant':'caption','text':{'@call':'formatString','args':{'value':'Owner in the data model: ${/account/Owner}'}}},
                {'id':'faq','component':'Accordion','@catalogId':'__TC__','items':[
                  {'title':'What is a Metric?','child':'a1','open':true},
                  {'title':'Which formats does a Table know?','child':'a2'}]},
                {'id':'a1','component':'Text','text':'A tile with a number, how it moved and, given a list of numbers, a sparkline.'},
                {'id':'a2','component':'Text','text':'number, percent, currency, date, datetime and bool. Anything else is shown as it is.'}
              ]}}]";

            host.Process(J(json.Replace("__TC__", TesseraeCatalog.Id)));

            _content = Guide.Page("Dashboard components", UIcons.ChartTree, "Table, Grid, Metric, Banner, Badge, PropertyGrid and Accordion", "GuideSamples.cs",
@"`TesseraeCatalog` is an **extension catalog** of the Tesserae components a report is made of, the way `FlintChartCatalog` adds a chart. Register it and name it per component with
`@catalogId`:

```csharp
TesseraeCatalog.AddTo(host);   // host.RegisterCatalog(TesseraeCatalog.Create())
```

| Component | Properties |
|---|---|
| `Table` | `data` (bindable list of rows), `columns` (`field`, `label`, `width`, `format`, `currency`, `align`; every key of the first row when left out), `sortable`, `sortBy`, `maxHeight`, `emptyText`, `selected` + `action` for a clicked row |
| `Grid` | `children`, `columns` (a count or a track list), `minColumnWidth`, `gap` |
| `Metric` | `label`, `value`, `delta`, `trend`, `good`, `data` (numbers: a sparkline), `icon`, `large` |
| `Banner` | `title`, `text`, `severity` (`info`, `success`, `warning`, `error`), `dismissible`, `actionLabel` + `action` |
| `Badge` | `text`, `tone`, `outline`, `icon` |
| `PropertyGrid` | `data` (an object), `fields`, `columns`, `editable` |
| `Accordion` | `items` (`title`, `child`, `open`), `multiple` |

Click a row, sort a column, edit a property: the data model on the right follows.",
                Guide.Live(host, inspector));
        }

        public HTMLElement Render() => _content.Render();
    }

    [SampleDetails(Group = "Guides", Order = 57, Icon = UIcons.Layers)]
    public class MoreComponentsSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public MoreComponentsSample()
        {
            var host = new A2UIHost();
            var inspector = new Inspector(host);

            TesseraeCatalog.AddTo(host);

            var json = @"[{'version':'1.0','createSurface':{'surfaceId':'more','catalogId':'" + BASIC + @"','dataModel':{
                'progress':64,'step':0,'enabled':true,'stars':3,'tags':['ops','q4'],'range':{'from':'2026-10-01','to':'2026-12-31'},'slide':0,
                'events':[{'title':'Kick-off','time':'Oct 3','text':'Scope agreed with the three regions.','color':'#0a7a3d'},
                          {'title':'Beta','time':'Nov 11','text':'Fourteen customers invited.'},
                          {'title':'Launch','time':'Dec 2','text':'Announcement and docs go out.','color':'#c0392b'}],
                'config':{'name':'Acme','limits':{'seats':25,'storageGb':200},'regions':['north','south','east'],'beta':true},
                'diff':'--- a/notes.txt\n+++ b/notes.txt\n@@ -1,3 +1,3 @@\n Plan\n-Ship in November\n+Ship in December\n Owner: Ada\n',
                'flow':'flowchart LR\n  A[Draft] --> B{Review}\n  B -- approved --> C([Published])\n  B -- changes --> A'},'components':[
                {'id':'root','component':'Column','children':['t1','prog','ring','stepper','stepinfo','t2','controls','echo','who','t3','timeline','t4','json','diff','flow','t5','carousel']},
                {'id':'t1','component':'Text','text':'## Progress and steps'},
                {'id':'prog','component':'Progress','@catalogId':'__TC__','value':{'@path':'/progress'},'label':'Import'},
                {'id':'ring','component':'Progress','@catalogId':'__TC__','style':'ring','value':{'@path':'/progress'},'label':'Quota'},
                {'id':'stepper','component':'Stepper','@catalogId':'__TC__','value':{'@path':'/step'},'items':[
                  {'title':'Source','description':'Where from','child':'s1'},{'title':'Mapping','child':'s2'},{'title':'Review','child':'s3'}]},
                {'id':'s1','component':'Text','text':'Pick the source file.'},{'id':'s2','component':'Text','text':'Map the columns.'},{'id':'s3','component':'Text','text':'Check and finish.'},
                {'id':'stepinfo','component':'Text','variant':'caption','text':{'@call':'formatString','args':{'value':'Step index: ${/step}'}}},
                {'id':'t2','component':'Text','text':'## Inputs'},
                {'id':'controls','component':'Row','children':['toggle','rating','tags','range'],'align':'start'},
                {'id':'toggle','component':'Toggle','@catalogId':'__TC__','label':'Notifications','value':{'@path':'/enabled'}},
                {'id':'rating','component':'Rating','@catalogId':'__TC__','label':'Rating','value':{'@path':'/stars'}},
                {'id':'tags','component':'TagsInput','@catalogId':'__TC__','label':'Tags','placeholder':'Add a tag','value':{'@path':'/tags'}},
                {'id':'range','component':'DateRange','@catalogId':'__TC__','label':'Period','value':{'@path':'/range'}},
                {'id':'echo','component':'Text','variant':'caption','text':{'@call':'formatString','args':{'value':'Notifications ${/enabled}, rating ${/stars}, tags ${/tags}, period ${/range/from} to ${/range/to}'}}},
                {'id':'who','component':'Avatar','@catalogId':'__TC__','name':'Ada Weber','subtitle':'Owner','status':'online'},
                {'id':'t3','component':'Text','text':'## Story'},
                {'id':'timeline','component':'Timeline','@catalogId':'__TC__','data':{'@path':'/events'},'width':520},
                {'id':'t4','component':'Text','text':'## Data, changes and diagrams'},
                {'id':'json','component':'JsonViewer','@catalogId':'__TC__','data':{'@path':'/config'},'rootLabel':'config','maxHeight':200},
                {'id':'diff','component':'CodeDiff','@catalogId':'__TC__','diff':{'@path':'/diff'}},
                {'id':'flow','component':'Mermaid','@catalogId':'__TC__','source':{'@path':'/flow'}},
                {'id':'t5','component':'Text','text':'## Slides'},
                {'id':'carousel','component':'Carousel','@catalogId':'__TC__','value':{'@path':'/slide'},'children':['c1','c2','c3']},
                {'id':'c1','component':'Text','text':'**One**: Table, Grid, Metric.'},{'id':'c2','component':'Text','text':'**Two**: Stepper, Timeline, Progress.'},{'id':'c3','component':'Text','text':'**Three**: inputs, diff, diagrams.'}
              ]}}]";

            host.Process(J(json.Replace("__TC__", TesseraeCatalog.Id)));

            _content = Guide.Page("More components", UIcons.Layers, "Progress, Stepper, Timeline, CodeDiff, JsonViewer, Mermaid, Toggle, Rating, TagsInput, DateRange, Avatar and Carousel", "GuideSamples.cs",
@"The rest of the `TesseraeCatalog`, next to the dashboard components:

| Component | Properties |
|---|---|
| `Progress` | `value` (0 to 100, bindable; left out: indeterminate), `label`, `style` (`bar`, `ring`), `size`, `showValue` |
| `Stepper` | `items` (`title`, `description`, `child`), `value` (current step, two-way) |
| `Timeline` | `data` (list of `title`, `text`, `time`, `color`), `sameSide`, `width` |
| `CodeDiff` | `diff` (a unified diff, bindable), `format` (`line`, `side`), `fileList` |
| `JsonViewer` | `data` (any JSON, bindable), `expandDepth`, `rootLabel`, `maxHeight` |
| `Mermaid` | `source` (bindable), `zoom` |
| `Toggle`, `Rating`, `TagsInput`, `DateRange` | `value` (two-way), `label`; `max`, `readOnly` / `placeholder`, `maxTags` / `{from, to}` as `yyyy-MM-dd` |
| `Avatar` | `name`, `image`, `status`, `size`, `subtitle` |
| `Carousel` | `children` (slide ids), `value` (current slide, two-way) |

Change an input and the data model on the right follows.",
                Guide.Live(host, inspector));
        }

        public HTMLElement Render() => _content.Render();
    }

    [SampleDetails(Group = "Guides", Order = 60, Icon = UIcons.Bug)]
    public class ErrorHandlingSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ErrorHandlingSample()
        {
            var host = new A2UIHost();
            var inspector = new Inspector(host);

            var send = Button("Send the broken messages").Primary().SetIcon(UIcons.Bug).OnClick(() =>
            {
                host.Clear();
                inspector.Reset();
                host.Process(J(@"[
                  {'version':'1.0','updateComponents':{'surfaceId':'nowhere','components':[]}},
                  {'version':'1.0','createSurface':{'surfaceId':'errs','catalogId':'" + BASIC + @"','components':[
                    {'id':'root','component':'Column','children':['a','b','c','d','self']},
                    {'id':'a','component':'Text','text':'This one is fine'},
                    {'id':'b','component':'Hologram'},
                    {'id':'c','component':'Text','text':{'@call':'doesNotExist','args':{}}},
                    {'id':'d','component':'TextField','label':'Pattern that could backtrack forever','value':'aaaa','@checks':[{'condition':{'@call':'regex','args':{'value':'aaaa','pattern':'(a+)+$'}},'message':'never shown'}]},
                    {'id':'self','component':'Card','child':'self'}
                  ]}},
                  {'version':'1.0','createSurface':{'surfaceId':'errs','catalogId':'" + BASIC + @"'}},
                  {'version':'1.0','createSurface':{'surfaceId':'themed','catalogId':'" + BASIC + @"','theme':{'primaryColor':'#ff0000'}}},
                  {'version':'1.0','bogus':{}}
                ]"));
            });

            _content = Guide.Page("Error handling", UIcons.Bug, "Bad messages never break the page", "GuideSamples.cs",
@"The renderer reports every problem with the protocol's error codes (`VALIDATION_FAILED`, `INTEGRITY_ERROR`, `EXPRESSION_ERROR`, `CATALOG_ERROR`, ...) and keeps going: an
unknown component or a component that contains itself draws a small notice where it would be, a function the catalog does not have or a regular
expression that could backtrack catastrophically (`(a+)+$`, rejected by the A2UI regex dialect's safety check) is an error instead of a hung tab, and a v1.0
`createSurface` that carries `theme` is invalid. Each error goes to `OnError` and to the agent. Press the button and read the **Errors** tab.",
                send, Guide.Live(host, inspector));
        }

        public HTMLElement Render() => _content.Render();
    }
}

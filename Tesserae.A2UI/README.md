# tesserae-a2ui

**Tesserae.A2UI** renders [A2UI](https://a2ui.org) (Agent-to-User Interface) v1.0 messages as a live
[Tesserae](https://github.com/curiosity-ai/tesserae) user interface. An agent streams JSON (`createSurface`, `updateComponents`,
`updateDataModel`, `deleteSurface`); the host keeps a real interface in step with it, two-way binds the user's input to a data model,
and sends what the user does back as protocol messages. It is C# compiled to JavaScript with the Transpose compiler, depends on
Tesserae only, and has no JavaScript of its own.

- **The protocol, all of v1.0**: surfaces, adjacency-list component trees that fill in progressively (a child can arrive after its parent),
  `createSurface` with components and data model in one message, JSON Lines / array / single-envelope input, `callRendererFunction` /
  `callAgentFunction` and their responses, `sendDataModel`, the protocol's error codes.
- **A reactive data model**: JSON Pointer paths, upsert and delete semantics, relative paths and `@index` inside templates,
  `formatString` with nested `${...}` expressions, and bindings that re-evaluate only when data they read changes.
- **The basic catalog**: all 18 components drawn with Tesserae components (see the table) and all 14 functions, with `@checks` validation
  (inputs show the first failing message once touched; a Button with a failing check is disabled), the A2UI regex dialect with its
  backtracking safety check, media URL rules, accessibility attributes.
- **Your own catalogs**: register components and functions under a catalog id, mix catalogs in one surface with `@catalogId`.
- **A sample gallery** with every example from the A2UI specification drawn next to the messages that produce it, a playground, guides, and a
  **Playwright suite** (every example, the interactions, 15 themes in light and dark).

See [CLAUDE.md](CLAUDE.md) for how the repository works and [TODO.md](TODO.md) for what is not done. The upstream A2UI repository this was built
against is kept, unmodified, in [`.reference/`](.reference/) (its CI is disabled there).

## Use

```csharp
// once, before the first use (the package's JavaScript is loaded on demand):
await Transpose.Require.RequireAsync(Transpose.RequireKind.Module, "./Tesserae.A2UI.js");

var host = new A2UIHost()
    .OnAction(a => SendToAgent(a.ToEnvelope().ToJson()))       // the user pressed a Button with an action.event
    .OnError(e => Log(e.ToString()));                          // anything the renderer could not do

host.Process(jsonlFromTheAgent);                               // JSON Lines, an array, or one envelope; call it again for every chunk
MountToBody(host.S());                                         // a host is an ordinary Tesserae component
```

| | |
|---|---|
| Input | `Process(string)` (any stream shape), `Process(JsonValue)`, `Process(A2UIMessage)`, `ProcessAsync(text, delayMs)` (replay a stream), `Clear()` |
| Output | `OnAction(A2UIAction)` (resolved context, optional `DataModel` for `sendDataModel` surfaces), `OnMessage(envelope, metadata)` (every renderer-to-agent envelope), `OnError(A2UIError)`, `OnProcessed(message)` |
| Surfaces | `Surfaces`, `GetSurface(id)`, `surface.Data` (a `DataModel`: `Get(path)`, `Set(path, value)`, `Changed`), `surface.GetComponent(id)` |
| Catalogs | `RegisterCatalog(catalog)`, `FallbackCatalog`, `BasicCatalog.Instance` / `BasicCatalog.Create()` |
| Agent functions | `CallAgentFunctionAsync(surface, name, catalogId, args)` sends `callAgentFunction` and awaits the agent's `agentFunctionResponse` (a function a catalog does not implement is routed here) |

### Basic catalog

| Component | Drawn with |
|---|---|
| `Text` | `MarkdownBlock` (no links or embedded content); `caption` variant |
| `Image` | `Image` with the six variants and `fit`; URL rules (http, https, data, same-host relative) |
| `Icon` | `Icon` with the 60 catalog names mapped onto `UIcons`; `{svgPath}` icons |
| `Row`, `Column` | `Stack`; `justify`, `align`, `weight` (flex with a zero basis, so columns line up); static children or a template over the data model |
| `List` | a scrolling `Stack`, vertical or horizontal |
| `Card` | `Card` |
| `Tabs` | `Pivot` |
| `Divider` | `HorizontalSeparator`; the vertical axis is a provisional one-pixel rule (see TODO) |
| `Modal` | `Modal` opened by the trigger |
| `Button` | `Button` (`primary` / `borderless` variants; a Text or Icon child becomes its label or icon); `@checks` disable it |
| `CheckBox` | `CheckBox` |
| `TextField` | `TextBox` (short, number, obscured) / `TextArea` (long) |
| `DateTimeInput` | `DatePicker`, `TimePicker`, `DateTimePicker`; ISO 8601 in the data model |
| `ChoicePicker` | `ChoiceGroup` (one), `CheckBox`es (several), `ToggleButton` chips, an optional filter box |
| `Slider` | `Slider`, mapped onto whole steps so decimal ranges work |
| `Video`, `AudioPlayer` | **no Tesserae component yet**: a labelled placeholder naming the media (see TODO) |

Functions: `required` `regex` `length` `numeric` `email` `formatString` `formatNumber` `formatCurrency` `formatDate` (Unicode TR35 patterns)
`pluralize` `openUrl` (user gesture only, http/https only) `and` `or` `not`, and the system function `@index`.

### A custom catalog

```csharp
var stars = new Catalog("https://example.com/catalogs/stars/1.0")
    .Component("Rating", cx =>
    {
        var rating = Rating(5);
        cx.Bind("value", v => rating.SetValue((int)(Coerce.ToNumber(v) ?? 0)));          // model -> view, live
        rating.OnChange(v => cx.WriteBack("value", JsonValue.Number(v)));                 // view -> model
        return rating;
    })
    .Function("shout", (ec, args) => JsonValue.String(Coerce.ToText(args.Get("value")).ToUpperInvariant() + "!"));

host.RegisterCatalog(stars);   // a component then says "@catalogId": "https://example.com/catalogs/stars/1.0"
```

`ComponentContext` is what a factory is handed: `Bind` / `BindString` / `BindBool` (live properties), `Watch` (rebuild on change), `WriteBack`
(two-way), `Child(id)` / `Children(stack)` (children, templates, progressive rendering), `BindChecks` (validation), `RunAction` (agent events and local
functions). Everything it creates is released when the component is rebuilt or removed.

## Build and run

```bash
dotnet tool update --global Transpose.Compiler
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet build Tesserae.A2UI.slnx                       # Debug: one readable bundle; Release: minified, chunked
cd Tesserae.A2UI.Sample/bin/Debug/netstandard2.0/tps && dotnet serve --port 5000

cd Tesserae.A2UI.Sample/playwright && npm install && npx playwright install chromium && npm test
```

`node scripts/make-sample-examples.mjs` refreshes the gallery's example files from `.reference/`. Nothing is built for Node: the examples are fetched by the page.

## Packaging

`.azure-devops/build-nuget-transpose.yml` builds `Tesserae.A2UI` with a CalVer version (`yy.M.build`) and pushes the package to NuGet.
GitHub workflows run the Playwright suite on pull requests (`tests.yml`) and publish the gallery to GitHub Pages (`pages.yml`).

## License

MIT, see [LICENSE](LICENSE). The A2UI specification and examples in `.reference/` are Apache-2.0 (see `.reference/LICENSE`).

# CLAUDE.md

Guidance for working in this repository.

## What this is

`tesserae-a2ui` ships **Tesserae.A2UI**, a [Tesserae](https://github.com/curiosity-ai/tesserae) (Transpose C#-to-JavaScript) renderer for
[A2UI](https://a2ui.org) v1.0: agent messages in, a live Tesserae interface out, user actions back. It depends on **Tesserae only** and has
no JavaScript of its own.

```
.reference/                         the upstream a2ui repository, moved here unmodified (spec, catalogs, examples, other renderers).
                                    Read it, do not edit it. Its GitHub workflows and every other `.yml` file are renamed `*.yml.disabled` and must stay that way.
Tesserae.A2UI/                      the package
  src/Json/JsonValue.cs             typed JSON (parse, serialise, deep equality). "undefined" is a C# null, JSON null is JsonValue.Null
  src/Protocol/                     A2UIMessage (agent -> renderer, parsed), A2UIAction / A2UIError (renderer -> agent)
  src/Model/                        DataModel (JSON Pointer, subscriptions), Surface (components + data), EvalContext (scope + dependencies)
  src/Functions/                    Expressions (evaluator, ${...} parser), BasicFunctions, RegexDialect, Coerce
  src/Catalog/                      Catalog, BasicCatalog, BasicComponents.*.cs (the 18 components), FlintChartCatalog (FlintChart), TesseraeCatalog (Table, Grid, Metric, Banner, Badge, PropertyGrid, Accordion; Progress, Stepper, Timeline, CodeDiff, JsonViewer, Mermaid, Toggle, Rating, TagsInput, DateRange, Avatar, Carousel in TesseraeComponents.More.cs), IconMap, MediaUrl
  src/Rendering/                    ComponentContext (what a factory gets), ComponentSlot (id in a scope), Disposer / LiveBinding
  src/Components/                   A2UIHost (the public entry), SurfaceView
  src/Interop/Js.cs                 the only direct JavaScript: Intl number/plural/date services, window.open, URL resolution
Tesserae.A2UI.Sample/               the gallery: sidebar, theme switch, reflection-found guide pages, one page per A2UI example
  assets/examples/                  generated and committed by scripts/make-sample-examples.mjs from .reference (never edit by hand)
  playwright/                       the test suite (committed): `npm test`
scripts/                            make-sample-examples.mjs, stage-samples.mjs
.azure-devops/                      NuGet pipeline;  .github/workflows/  Playwright suite + GitHub Pages
```

## Build

```bash
dotnet tool update --global Transpose.Compiler
export PATH="$PATH:$HOME/.dotnet/tools"            # tps; in the cloud sandbox also DOTNET_ROOT=/usr/share/dotnet
dotnet build Tesserae.A2UI.slnx                    # Debug: one readable bundle; Release: minified, chunked
```

Versions follow tesserae-3d: `Transpose.Build.Target/26.8.4125` SDK, `LangVersion` 7.2 (Transpose's C# subset: no switch expressions, no
records, no `?.` on delegates, no `string.CompareOrdinal`, no `NumberStyles`; `x is object y` types `y` as `object`, write `x is JsonValue y`),
`Transpose.BCL` / `Core` and `Tesserae` as NuGet packages. Tesserae is **2026.11.73511**, not tesserae-3d's older one, because the sample uses `JsonViewer`;
the themes must be the same build number (`26.11.73511`; a newer Tesserae than its themes has no matching set on NuGet).
Zero build warnings. Type safety: no `dynamic`; JSON is `JsonValue`, never an untyped JavaScript object.

The package is built as modules and its chunks defer (`tps.json`: `outputBy: Module`, `loadCompiledOutput: false`, `IsPackable`): a consumer
loads the entry itself first, `await Transpose.Require.RequireAsync(Transpose.RequireKind.Module, "./Tesserae.A2UI.js")` (the sample does it in `Main`;
`.min.js` in Release), before touching anything in the namespace.

## How it works

- **A surface is data, the view is derived.** `Surface` holds the component definitions the agent has sent (an adjacency list by id) and a `DataModel`.
  It knows nothing about the DOM. `Surface.ComponentChanged(id)` fires only when a definition really changed (deep-equal resends do nothing).
- **A `ComponentSlot` is one id in one collection scope.** It builds the Tesserae component the catalog maps the definition to, puts the element
  in the tree, and swaps it for a rebuilt one when its definition changes. A missing definition draws a hidden placeholder and fills in when it arrives
  (progressive rendering). Rebuilding disposes everything the old build owned (`Disposer`: subscriptions, child slots). A component that contains itself
  is caught by walking the slot's parents. A slot's element changes identity on rebuild, so **never put a style, listener or class on a slot's element from outside**:
  wrap it (Card and Modal do) or let the slot's `Decorate` do it.
- **Bindings are live and dependency-tracked.** Evaluating a dynamic value records every absolute path it read (`EvalContext.Dependencies`); `LiveBinding`
  subscribes to exactly those and re-evaluates (and resubscribes) when one overlaps a change. Two-way inputs guard with an `applying` flag and compare before
  assigning, because Tesserae's `Text`/`IsChecked` setters raise their change event.
- **Templates** (`children: {componentId, path}`) subscribe to the list and rebuild only when its **length** changes; items bind to their own paths, so typing in a
  field inside a template never rebuilds the list.
- **The basic catalog is one shared instance, and overriding it is global on purpose.** `A2UIHost` starts with `BasicCatalog.Instance`; `Override`, `Decorate` and `OverrideFunction`
  change the registered catalog itself, for every host. `OwnCatalog` clones it the first time it is asked for, and `RegisterCatalog` / `Extend` mark a catalog as the host's own,
  for a change that must stay on one host. Code that serves several pages of one app (the gallery) uses `OwnCatalog`, or changing it on one page changes it on all.
- **Catalogs resolve strictly**: a component's `@catalogId`, else the surface's `catalogId`, else an error (no fallback unless `A2UIHost.FallbackCatalog`). A child never takes its parent's catalog, so an extension container (`Grid`, `Accordion`) holds basic-catalog children without naming it.
  Functions are `rendererOnly` by default; the agent may call only `agentOnly` / `rendererOrAgent` ones; `requiresUserActivation` functions run only in an action.
- **Everything the renderer cannot do is a protocol error, not an exception**: `A2UIHost.Report` dedupes, raises `OnError` and sends the envelope to the agent.

## Tesserae is a library and its consumers are invisible

Same rule as Tesserae's own CLAUDE.md: a consumer's component must not have to declare anything to behave correctly. Custom components get bindings, two-way data,
children, validation and actions through `ComponentContext` alone, registered on a `Catalog` by id. Do not add an attribute, interface or naming convention.

## Where Tesserae has no matching component

**Stop and report before implementing one** (the maintainer asked for this). There are none today: `Video`, `AudioPlayer` and the vertical `Divider` were gaps and now live
in Tesserae itself (`VideoPlayer`, `AudioPlayer`, `VerticalSeparator`), which this package maps onto.

## The sample gallery and its data

`SampleDetails(Group, Order, Icon)` + `ISample`, found by reflection (the sample's tps.json asks for inline reflection metadata); the route is the class name
without `Sample`, spaced (`#/view/Data Binding`). The A2UI examples are not classes: `assets/examples/index.json` (generated) lists them and `App.cs` builds a
page per entry. Each example page is `ExampleViewer` (the surface, and `Inspector`: messages applied, live data model, everything sent to the agent, errors).
The theme is chosen with `?theme=<id>&dark=1` or the Theme nav (`localStorage["tss-a2ui-sample-theme"]`); the gallery opens in Curiosity.

## Tests

`Tesserae.A2UI.Sample/playwright/` (committed): plain node + Playwright, `npm test`; it serves the built sample itself. Screenshots go to `$SCREENS_DIR`
(gitignored), named after the page, theme and mode. Suites: `examples` (all 43 render with no console error and no renderer problem), `interaction` (binding,
actions, checks, templates, tabs, modal, streaming, playground, errors, custom catalog, function calls both ways), `themes` (the Showcase page in 15 themes, light and
dark; `THEMES=stratus,washi` limits it). `node run.mjs --only a,b`; `FULL_ERR=1` prints a failure whole. Wait out the sections' fade-in before reading or capturing
(`L.open` does). Pivot tabs are `role=tab`. A remote image the sandbox cannot reach logs `Failed to load resource`: the tests ignore exactly that.

## Conventions inherited from Tesserae

Follow `tesserae/CLAUDE.md`: no scrollbar JavaScript, icons through `UIcons`/`UI.I` (aria-hidden), every icon-only control has a tooltip or `.AriaLabel`, `SampleDetails` icons
unique per guide page (the example pages share one icon per group on purpose), and test in every theme.

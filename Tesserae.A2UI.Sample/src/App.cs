using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Transpose.Core;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.A2UI.Sample
{
    /// <summary>
    /// The gallery: a sidebar of pages on the left, one page per topic on the right, and a route per page so any of them
    /// can be linked to or opened in its own tab. The pages are the A2UI basic-catalog examples (every one of the
    /// protocol's own samples, drawn live next to the messages that produce them) and a few guides written against the
    /// C# API: the playground, data binding, actions and functions, custom catalogs and error handling.
    /// </summary>
    internal static class App
    {
        private const string _sidebarOpenStateKey = "tss-a2ui-sidebar-open-close";
        private const string _themeKey            = "tss-a2ui-sample-theme";

        private static void Main()
        {
            StartAsync().FireAndForget();
        }

        /// <summary>
        /// The package asks not to be scripted by index.html (its tps.json says
        /// <c>loadCompiledOutput: false</c>), so that an application only pays for its JavaScript when a
        /// document is opened. The gallery is nothing but documents, so it fetches the entry first thing
        /// and builds the rest once the <c>Tesserae.Pdf</c> namespace exists.
        /// </summary>
        private static async Task StartAsync()
        {
            // Require falls back between the two spellings, so either name works - but this site is not built
            // as modules, so its Release output holds only the minified bundle, and asking for the other name
            // first is a 404 in the console on every load. A module site (mosaik) has Tesserae.A2UI.js in both.
#if DEBUG
            await Transpose.Require.RequireAsync(Transpose.RequireKind.Module, "./Tesserae.A2UI.js");
#else
            await Transpose.Require.RequireAsync(Transpose.RequireKind.Module, "./Tesserae.A2UI.min.js");
#endif

            var examples = new List<ExampleEntry>();
            try { examples = ExampleEntry.Parse(await SamplesHelper.FetchTextAsync(SamplesHelper.EXAMPLES + "index.json")); }
            catch (Exception e) { console.error(e.Message); }

            Start(examples);
        }

        private static void Start(List<ExampleEntry> examples)
        {
            document.body.style.overflow = "hidden";

            // Ensure the viewport meta tag is present so that mobile browsers use the device width
            // instead of rendering at a desktop width and scaling down.
            if (document.head.querySelector("meta[name='viewport']") is null)
            {
                var viewportMeta = document.createElement("meta");
                viewportMeta["name"]    = "viewport";
                viewportMeta["content"] = "width=device-width, initial-scale=1.0, maximum-scale=5.0";
                document.head.appendChild(viewportMeta);
            }

            // Adds/removes the tss-mobile class on body whenever the viewport is 768px or narrower.
            Theme.EnableMobileDetection(breakpoint: 768);

            // The theme: ?theme=<id> beats the one picked last time (stored), which beats Curiosity. ?dark=1 forces dark mode.
            // The page stays invisible until the theme's stylesheet has arrived, so the first paint is already themed.
            var query          = new URL(window.location.href).searchParams;
            var requested      = query.get("theme");
            var savedThemeId   = localStorage.getItem(_themeKey);
            var initialThemeId = Themes.Find(requested) is object ? requested : (Themes.Find(savedThemeId) is object ? savedThemeId : "curiosity");

            if (query.get("dark") == "1") Theme.Dark();
            else if (query.get("dark") == "0") Theme.Light();

            if (initialThemeId != Themes.Default)
            {
                document.body.style.visibility = "hidden";

                async Task ActivateInitialTheme()
                {
                    try     { await Theme.SetCustomTheme(Themes.Find(initialThemeId).Theme()); }
                    finally { document.body.style.visibility = ""; }
                }

                ActivateInitialTheme().FireAndForget();
            }

            var allSidebarItems     = new List<ISidebarItem>();
            var sampleToSidebarItem = new Dictionary<SamplePage, ISidebarItem>();

            void SelectSidebar(ISidebarItem toSelect)
            {
                allSidebarItems.ForEach(i => i.IsSelected = i == toSelect);
            }

            var currentPage = new SettableObservable<SamplePage>(null);

            currentPage.Observe(selected =>
            {
                if (selected is object && sampleToSidebarItem.TryGetValue(selected, out var item))
                {
                    SelectSidebar(item);
                }
            });

            var sidebar = Sidebar();

            // On mobile we use navbar mode: horizontal bar + full-screen sliding drawer. Evaluated
            // once at startup; the CSS (tss-mobile class) handles the visual switch on resize.
            if (Theme.IsMobileMode)
            {
                sidebar.AsNavbar();
            }

            sidebar.AddHeader(new SidebarText("header", "Tesserae.A2UI", "A2UI", textSize: Theme.IsMobileMode ? TextSize.Large : TextSize.XLarge, textWeight: TextWeight.Bold));

            var searchBox = new SidebarSearchBox("search", "Search...");
            searchBox.OnSearch(term => sidebar.Search(term));
            sidebar.AddHeader(searchBox);

            var sourceButton = new SidebarButton("SOURCE_CODE", UIcons.CodeBranch, "Source Code",
                    new SidebarCommand(UIcons.ArrowUpRightFromSquare).Tooltip("Open repository on GitHub")
                       .OnClick(() => window.open("https://github.com/curiosity-ai/tesserae-a2ui", "_blank")))
               .CommandsAlwaysVisible()
               .OnClick(() => window.open("https://github.com/curiosity-ai/tesserae-a2ui", "_blank"));

            // In the phone navbar the header row is the title and the menu button only: the link goes in the list.
            if (Theme.IsMobileMode) sidebar.AddContent(sourceButton); else sidebar.AddHeader(sourceButton);

            // A page is only built when it is opened, so one visit creates one page's viewers rather
            // than every page's at startup - and leaving a page unmounts them, which exercises the
            // components' teardown on every click.
            var contentArea = Defer(currentPage, async page => page is null
                ? (IComponent)CenteredCardWithBackground(Message("Tesserae.A2UI", "Renders A2UI agent messages as a live Tesserae interface. Pick a page on the left: every example from the A2UI specification is here.").Icon(UIcons.Sparkles))
                : VStack().S().ScrollY().Children((await page.ContentGenerator()).WS().MinHeight(100.percent())));

            // On mobile the sidebar is a fixed top navbar, so the layout is vertical (sidebar then
            // content). On desktop it is horizontal (sidebar left, content right).
            Stack pageContent;

            if (Theme.IsMobileMode)
            {
                pageContent = VStack().Class("tss-page-layout").S().Children(sidebar.WS(), contentArea.WS().H(1).Grow());
            }
            else
            {
                pageContent = HStack().Class("tss-page-layout").S().Children(sidebar.HS(), contentArea.HS().W(1).Grow());
            }

            MountToBody(pageContent);

            // Important: reflection only works here because the metadata is emitted inline with the
            // JavaScript rather than into a separate .meta.js file - i.e. tps.json needs
            //     "reflection": { "disabled": false, "target": "inline" }
            var samples = typeof(ISample).Assembly.GetTypes()
               .Where(t => typeof(ISample).IsAssignableFrom(t) && !t.IsInterface)
               .Select(sampleType =>
                {
                    var details = sampleType.GetCustomAttributes(typeof(SampleDetailsAttribute), true).FirstOrDefault() as SampleDetailsAttribute;
                    var group   = details is object ? details.Group : "Others";
                    var order   = details is object ? details.Order : 0;
                    var icon    = details is object ? details.Icon : UIcons.Circle;

                    return new SamplePage(sampleType.Name, SamplePage.FormatName(sampleType), group, order, icon,
                        async () => await Activator.CreateInstanceAsync(sampleType) as IComponent);
                })
               .ToList();

            foreach (var ex in examples)
            {
                var entry = ex;
                samples.Add(new SamplePage(entry.Name, entry.Name, entry.Group, 1000 + samples.Count, ExampleEntry.IconFor(entry.Group), async () =>
                {
                    var text = await SamplesHelper.FetchTextAsync(SamplesHelper.EXAMPLES + entry.Id + ".json");
                    return new ExampleViewer(entry.Name, entry.Description, entry.Group, string.Join(", ", entry.Components), text, ExampleEntry.IconFor(entry.Group));
                }));
            }

            var samplesByName = samples.ToDictionary(s => s.Name, s => s);

            var openClose = new SidebarCommand(UIcons.AngleLeft).Tooltip("Close Sidebar");

            // In mobile/navbar mode the drawer always starts closed (the hamburger opens it). On
            // desktop, restore the last open/closed preference.
            if (!Theme.IsMobileMode)
            {
                var sidebarOpenState = bool.TryParse(localStorage.getItem(_sidebarOpenStateKey), out var v) ? v : true;
                sidebar.Closed(!sidebarOpenState);
            }

            openClose.OnClick(() =>
            {
                sidebar.Toggle();

                if (sidebar.IsClosed)
                {
                    openClose.SetIcon(UIcons.AngleRight).Tooltip("Open Sidebar");
                    localStorage.setItem(_sidebarOpenStateKey, false.ToString());
                }
                else
                {
                    openClose.SetIcon(UIcons.AngleLeft).Tooltip("Close Sidebar");
                    localStorage.setItem(_sidebarOpenStateKey, true.ToString());
                }
            });

            var lightDark = new SidebarCommand(Theme.IsDark ? UIcons.Moon : UIcons.Sun).Tooltip(Theme.IsDark ? "Dark Mode" : "Light Mode");

            lightDark.OnClick(() =>
            {
                if (Theme.IsDark)
                {
                    Theme.Light();
                    lightDark.SetIcon(UIcons.Sun).Tooltip("Light Mode");
                }
                else
                {
                    Theme.Dark();
                    lightDark.SetIcon(UIcons.Moon).Tooltip("Dark Mode");
                }
            });

            // Theme switcher: one nav at the foot of the sidebar, one button per Tesserae custom theme.
            var themeNav     = new SidebarNav("THEME", UIcons.Palette, "Theme", initiallyCollapsed: true).NotSortable().KeepCollapsedOnSelection();
            var themeButtons = new Dictionary<string, SidebarButton>();

            void MarkTheme(string id) { foreach (var kv in themeButtons) kv.Value.IsSelected = kv.Key == id; }

            foreach (var entry in Themes.All)
            {
                var themeEntry = entry;
                var button     = new SidebarButton("THEME_" + themeEntry.Id.ToUpper(), themeEntry.Icon, themeEntry.Name + " Theme");

                button.OnClick(() =>
                {
                    localStorage.setItem(_themeKey, themeEntry.Id);
                    MarkTheme(themeEntry.Id);
                    Theme.SetCustomTheme(themeEntry.Theme()).FireAndForget();
                });

                themeButtons[themeEntry.Id] = button;
                themeNav.Add(button);
            }

            MarkTheme(initialThemeId);

            themeNav.OnClick(() => themeNav.Toggle());
            sidebar.AddFooter(themeNav);
            sidebar.AddFooter(new SidebarCommands("CONFIG", lightDark, openClose));

            var groupIndex = 0;

            var groupOrder = new[] { "Guides", "Basics", "Forms and validation", "Lists and templates", "Cards", "Streaming", "Media" };

            foreach (var group in samples.GroupBy(s => s.Group).OrderBy(g => Array.IndexOf(groupOrder, g.Key)))
            {
                sidebar.AddContent(new SidebarSeparator(group.Key + groupIndex++, group.Key));

                var itemIndex = 0;

                foreach (var item in group.OrderBy(s => s.Order).ThenBy(s => s.Name.ToLower()))
                {
                    var sidebarItem = new SidebarButton(item.Name + itemIndex++, item.Icon, item.Name,
                        new SidebarCommand(UIcons.ArrowUpRightFromSquare).Tooltip("Open in new tab").OnClick(() => window.open($"#/view/{item.Name}", "_blank")));

                    sidebarItem.OnClick(() =>
                    {
                        // Push updates the URL without reloading, so the page has a route of its own
                        // to link to, and the browser's back button walks the pages visited.
                        Router.Push($"#/view/{item.Name}");

                        currentPage.Value = item;
                    });

                    sidebar.AddContent(sidebarItem);
                    allSidebarItems.Add(sidebarItem);
                    sampleToSidebarItem[item] = sidebarItem;
                }
            }

            Router.Register("home", "/", _ => currentPage.Value = null);

            foreach (var kv in samplesByName)
            {
                Router.Register($"#/view/{kv.Key.Replace(" ", "%20")}", _ => currentPage.Value = kv.Value);
            }

            Router.Initialize();

            // Forcibly match the current route at first load: the routes were only just registered,
            // and we want them matched against the current URL without changing it.
            Router.Refresh(onDone: Router.ForceMatchCurrent);
        }
    }
}

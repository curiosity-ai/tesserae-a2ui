using System;
using System.Collections.Generic;
using Tesserae;
using Tesserae.Themes.Curiosity;
using Tesserae.Themes.Stratus;
using Tesserae.Themes.RetroOS;
using Tesserae.Themes.Rechenwerk;
using Tesserae.Themes.Weniger;
using Tesserae.Themes.Zeitung;
using Tesserae.Themes.Blaupause;
using Tesserae.Themes.Phosphor;
using Tesserae.Themes.Glas;
using Tesserae.Themes.Kontrast;
using Tesserae.Themes.Memphis;
using Tesserae.Themes.Washi;
using Tesserae.Themes.Graustufe;
using Tesserae.Themes.Kristall;

namespace Tesserae.A2UI.Sample
{
    /// <summary>The Tesserae custom themes the gallery can switch between (the same set Tesserae's own gallery has).</summary>
    internal static class Themes
    {
        public const string Default = "default";

        public sealed class Entry
        {
            public string Id, Name; public UIcons Icon; public Func<ICustomTheme> Theme;
        }

        public static readonly List<Entry> All = new List<Entry>
        {
            new Entry { Id = Default, Name = "Default", Icon = UIcons.Square, Theme = () => null },
            new Entry { Id = "curiosity", Name = "Curiosity", Icon = UIcons.SquareSmall, Theme = () => CuriosityTheme.Instance },
            new Entry { Id = "stratus", Name = "Stratus", Icon = UIcons.Plane, Theme = () => StratusTheme.Instance },
            new Entry { Id = "retroos", Name = "RetroOS", Icon = UIcons.ComputerClassic, Theme = () => RetroOSTheme.Instance },
            new Entry { Id = "rechenwerk", Name = "Rechenwerk", Icon = UIcons.Calculator, Theme = () => RechenwerkTheme.Instance },
            new Entry { Id = "weniger", Name = "Weniger", Icon = UIcons.Radio, Theme = () => WenigerTheme.Instance },
            new Entry { Id = "zeitung", Name = "Zeitung", Icon = UIcons.Newspaper, Theme = () => ZeitungTheme.Instance },
            new Entry { Id = "blaupause", Name = "Blaupause", Icon = UIcons.DraftingCompass, Theme = () => BlaupauseTheme.Instance },
            new Entry { Id = "phosphor", Name = "Phosphor", Icon = UIcons.TvRetro, Theme = () => PhosphorTheme.Instance },
            new Entry { Id = "glas", Name = "Glas", Icon = UIcons.Glass, Theme = () => GlasTheme.Instance },
            new Entry { Id = "kristall", Name = "Kristall", Icon = UIcons.Gem, Theme = () => KristallTheme.Instance },
            new Entry { Id = "kontrast", Name = "Kontrast", Icon = UIcons.Contrast, Theme = () => KontrastTheme.Instance },
            new Entry { Id = "memphis", Name = "Memphis", Icon = UIcons.Triangle, Theme = () => MemphisTheme.Instance },
            new Entry { Id = "washi", Name = "Washi", Icon = UIcons.Stamp, Theme = () => WashiTheme.Instance },
            new Entry { Id = "graustufe", Name = "Graustufe", Icon = UIcons.BookAlt, Theme = () => GraustufeTheme.Instance },
        };

        public static Entry Find(string id)
        {
            foreach (var e in All) if (e.Id == id) return e;
            return null;
        }
    }
}

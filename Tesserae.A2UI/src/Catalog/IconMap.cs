using System.Collections.Generic;
using Tesserae;

namespace Tesserae.A2UI
{
    /// <summary>The catalog's icon names mapped onto Tesserae's bundled UIcons glyph set.</summary>
    internal static class IconMap
    {
        internal struct Glyph
        {
            public UIcons Icon;
            public UIconsWeight Weight;
        }

        private static readonly Dictionary<string, Glyph> Map = Build();

        private static Glyph G(UIcons icon, UIconsWeight weight = UIconsWeight.Regular) => new Glyph { Icon = icon, Weight = weight };

        private static Dictionary<string, Glyph> Build()
        {
            var m = new Dictionary<string, Glyph>();
            m["accountCircle"]     = G(UIcons.CircleUser);
            m["add"]               = G(UIcons.Plus);
            m["arrowBack"]         = G(UIcons.ArrowLeft);
            m["arrowForward"]      = G(UIcons.ArrowRight);
            m["attachFile"]        = G(UIcons.Clip);
            m["calendarToday"]     = G(UIcons.CalendarDay);
            m["call"]              = G(UIcons.PhoneCall);
            m["camera"]            = G(UIcons.Camera);
            m["check"]             = G(UIcons.Check);
            m["close"]             = G(UIcons.Cross);
            m["delete"]            = G(UIcons.Trash);
            m["download"]          = G(UIcons.Download);
            m["edit"]              = G(UIcons.Pencil);
            m["event"]             = G(UIcons.Calendar);
            m["error"]             = G(UIcons.Exclamation);
            m["fastForward"]       = G(UIcons.ForwardFast);
            m["favorite"]          = G(UIcons.Heart, UIconsWeight.Solid);
            m["favoriteOff"]       = G(UIcons.Heart);
            m["folder"]            = G(UIcons.Folder);
            m["help"]              = G(UIcons.Question);
            m["home"]              = G(UIcons.Home);
            m["info"]              = G(UIcons.Info);
            m["locationOn"]        = G(UIcons.Marker);
            m["lock"]              = G(UIcons.Lock);
            m["lockOpen"]          = G(UIcons.Unlock);
            m["mail"]              = G(UIcons.Envelope);
            m["menu"]              = G(UIcons.MenuBurger);
            m["moreVert"]          = G(UIcons.MenuDotsVertical);
            m["moreHoriz"]         = G(UIcons.MenuDots);
            m["notificationsOff"]  = G(UIcons.BellSlash);
            m["notifications"]     = G(UIcons.Bell);
            m["pause"]             = G(UIcons.Pause);
            m["payment"]           = G(UIcons.CreditCard);
            m["person"]            = G(UIcons.User);
            m["phone"]             = G(UIcons.PhoneFlip);
            m["photo"]             = G(UIcons.Picture);
            m["play"]              = G(UIcons.Play);
            m["print"]             = G(UIcons.Print);
            m["refresh"]           = G(UIcons.Refresh);
            m["rewind"]            = G(UIcons.Rewind);
            m["search"]            = G(UIcons.Search);
            m["send"]              = G(UIcons.PaperPlane);
            m["settings"]          = G(UIcons.Settings);
            m["share"]             = G(UIcons.Share);
            m["shoppingCart"]      = G(UIcons.ShoppingCart);
            m["skipNext"]          = G(UIcons.StepForward);
            m["skipPrevious"]      = G(UIcons.StepBackward);
            m["star"]              = G(UIcons.Star, UIconsWeight.Solid);
            m["starHalf"]          = G(UIcons.StarSharpHalf);
            m["starOff"]           = G(UIcons.Star);
            m["stop"]              = G(UIcons.Stop);
            m["upload"]            = G(UIcons.Upload);
            m["visibility"]        = G(UIcons.Eye);
            m["visibilityOff"]     = G(UIcons.EyeCrossed);
            m["volumeDown"]        = G(UIcons.VolumeDown);
            m["volumeMute"]        = G(UIcons.VolumeMute);
            m["volumeOff"]         = G(UIcons.VolumeOff);
            m["volumeUp"]          = G(UIcons.Volume);
            m["warning"]           = G(UIcons.TriangleWarning);
            return m;
        }

        public static bool TryGet(string name, out Glyph glyph) => Map.TryGetValue(name ?? "", out glyph);

        public static IEnumerable<string> Names => Map.Keys;
    }
}

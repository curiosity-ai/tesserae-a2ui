using Transpose;

namespace Tesserae.A2UI
{
    /// <summary>
    /// The only places this package reaches into the JavaScript runtime directly: locale-aware number, plural and
    /// date services that C# has no equivalent for in a browser. Every method has a typed signature and returns a
    /// primitive; nothing untyped leaves this class.
    /// </summary>
    internal static class Js
    {
        public static string FormatNumber(double value, int minFraction, int maxFraction, bool grouping) =>
            Script.Write<string>("{0}.toLocaleString(undefined, { minimumFractionDigits: {1}, maximumFractionDigits: {2}, useGrouping: {3} })", value, minFraction, maxFraction, grouping);

        public static string FormatCurrency(double value, string currency, int minFraction, int maxFraction, bool grouping) =>
            Script.Write<string>("(function(v, c, mn, mx, g){ try { return v.toLocaleString(undefined, { style: 'currency', currency: c, minimumFractionDigits: mn < 0 ? undefined : mn, maximumFractionDigits: mx < 0 ? undefined : mx, useGrouping: g }); } catch (e) { return null; } })({0}, {1}, {2}, {3}, {4})", value, currency, minFraction, maxFraction, grouping);

        public static string PluralCategory(double value) =>
            Script.Write<string>("new Intl.PluralRules().select({0})", value);

        /// <summary>year, month (0-11), day, weekday (0-6), hour, minute, second in local time, or null when the value is not a date.</summary>
        public static double[] DateParts(string iso)
        {
            return Script.Write<double[]>("(function(s){ var d = new Date(s); if (isNaN(d.getTime())) return null; return [d.getFullYear(), d.getMonth(), d.getDate(), d.getDay(), d.getHours(), d.getMinutes(), d.getSeconds()]; })({0})", iso);
        }

        public static double[] DatePartsFromMillis(double ms)
        {
            return Script.Write<double[]>("(function(t){ var d = new Date(t); if (isNaN(d.getTime())) return null; return [d.getFullYear(), d.getMonth(), d.getDate(), d.getDay(), d.getHours(), d.getMinutes(), d.getSeconds()]; })({0})", ms);
        }

        public static string MonthName(int month0, bool longForm) =>
            Script.Write<string>("new Date(2000, {0}, 1).toLocaleString(undefined, { month: {1} ? 'long' : 'short' })", month0, longForm);

        public static string WeekdayName(int day0, bool longForm) =>
            Script.Write<string>("new Date(2000, 0, 2 + {0}).toLocaleString(undefined, { weekday: {1} ? 'long' : 'short' })", day0, longForm);

        public static string NowIso() => Script.Write<string>("new Date().toISOString()");

        public static void OpenUrl(string url) =>
            Script.Write("window.open({0}, '_blank', 'noopener,noreferrer')", url);

        public static string ResolveUrl(string url) =>
            Script.Write<string>("(function(u){ try { return new URL(u, document.baseURI).href; } catch (e) { return null; } })({0})", url);
    }
}

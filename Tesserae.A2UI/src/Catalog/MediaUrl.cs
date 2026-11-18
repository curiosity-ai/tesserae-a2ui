using Tesserae;

namespace Tesserae.A2UI
{
    /// <summary>
    /// The media URL rules of the basic catalog guide: http, https, data and same-host relative URLs only. A host that
    /// restricts where content may load from (<see cref="A2UIHost.ContentSources"/>) narrows that further, to its own
    /// origin and the hosts it lists.
    /// </summary>
    public static class MediaUrl
    {
        /// <summary>Whether the URL has a shape the catalog allows, whatever host it names.</summary>
        public static bool IsSafe(string url)
        {
            if (url is null) return false;

            var t = url.Trim();
            if (t.Length == 0) return false;

            // Browsers drop tab, CR and LF inside a URL before parsing it, so the checks look at the same text.
            var sb = new System.Text.StringBuilder();
            foreach (var c in t) if (c != '\t' && c != '\n' && c != '\r' && c >= ' ') sb.Append(c);
            var s = sb.ToString();
            if (s.Length == 0) return false;

            var colon = s.IndexOf(':');
            var slash = s.IndexOfAny(new[] { '/', '?', '#', '\\' });
            var hasScheme = colon > 0 && (slash < 0 || colon < slash);

            if (hasScheme)
            {
                var scheme = s.Substring(0, colon).ToLowerInvariant();
                if (scheme == "data") return true;
                if (scheme != "http" && scheme != "https") return false;
                var rest = s.Substring(colon + 1);
                return rest.StartsWith("//") && rest.Length > 2 && rest[2] != '/' && rest[2] != '\\';
            }

            // relative: "//host" (and "\\host") point at another host
            var first = s.Replace('\\', '/');
            return !first.StartsWith("//");
        }

        /// <summary>
        /// Whether the URL has a shape the catalog allows and <paramref name="sources"/> lets content load from it.
        /// <c>null</c> and <see cref="MarkdownContentSources.Any"/> allow every host.
        /// </summary>
        public static bool IsSafe(string url, MarkdownContentSources sources)
        {
            if (!IsSafe(url)) return false;

            return sources is null || !sources.IsRestricted || sources.Allows(url.Trim());
        }
    }
}

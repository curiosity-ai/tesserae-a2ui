using System;
using System.Collections.Generic;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.A2UI
{
    internal static partial class BasicComponents
    {
        private const int GapPx = 8;

        // ------------------------------------------------------------------ Text

        public static IComponent Text(ComponentContext cx)
        {
            // Markdown without links or embedded content, which is what the catalog allows (and what makes agent text safe to show).
            var md = MarkdownBlock("", MarkdownSanitization.NoLinksOrEmbeddedContent);
            var el = md.Render();

            if (cx.Literal("variant") == "caption")
            {
                el.style.fontSize = "0.85em";
                el.style.opacity  = "0.75";
                el.style.fontStyle = "italic";
            }

            el.style.minWidth = "0";
            cx.BindString("text", s => md.Text = s);
            return md;
        }

        /// <summary>Plain text of a Text component's string with the common Markdown markers removed (for a label inside a Button).</summary>
        internal static string StripMarkdown(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            while (s.StartsWith("#")) s = s.Substring(1);
            return s.Replace("**", "").Replace("__", "").Replace("`", "").Trim();
        }

        // ------------------------------------------------------------------ Image

        public static IComponent Image(ComponentContext cx)
        {
            var img = Tesserae.UI.Image("");
            var el  = img.Render();
            el.setAttribute("alt", "");

            var variant = cx.Literal("variant", "mediumFeature");
            switch (variant)
            {
                case "icon":
                    el.style.width = "24px"; el.style.height = "24px";
                    break;
                case "avatar":
                    el.style.width = "40px"; el.style.height = "40px"; el.style.borderRadius = "50%";
                    break;
                case "smallFeature":
                    el.style.width = "100px"; el.style.height = "100px";
                    break;
                case "largeFeature":
                    el.style.width = "100%"; el.style.maxHeight = "400px";
                    break;
                case "header":
                    el.style.width = "100%"; el.style.height = "200px";
                    break;
                default:
                    el.style.width = "100%"; el.style.maxWidth = "300px";
                    break;
            }

            var fit = cx.Literal("fit", variant == "avatar" || variant == "header" ? "cover" : "fill");
            el.style.objectFit = fit == "scaleDown" ? "scale-down" : fit;
            el.style.display = "block";
            if (variant == "avatar" || variant == "icon" || variant == "smallFeature") el.style.flexShrink = "0";

            cx.BindString("description", s => el.setAttribute("alt", s));
            cx.BindString("url", u =>
            {
                if (MediaUrl.IsSafe(u)) { el.removeAttribute("hidden"); img.Source = u.Trim(); }
                else { el.removeAttribute("src"); el.setAttribute("hidden", ""); }
            });

            return img;
        }

        // ------------------------------------------------------------------ Icon

        public static IComponent Icon(ComponentContext cx)
        {
            cx.Watch("name");

            var name = cx.Prop("name");
            string glyphName = null;
            string svgPath = null;

            var resolved = cx.Eval("name");
            if (resolved is object && resolved.IsString) glyphName = resolved.StringValue;
            else if (name is object && name.IsObject && name.Has("svgPath"))
                svgPath = Expressions.EvaluateString(name.Get("svgPath"), cx.NewEval(), "");

            if (svgPath is object)
            {
                var holder = document.createElement("span");
                holder.style.display = "inline-flex";
                holder.innerHTML = SvgIcon(svgPath);
                return Raw(holder);
            }

            IconMap.Glyph g;
            if (!IconMap.TryGet(glyphName, out g))
            {
                if (!string.IsNullOrEmpty(glyphName)) cx.Report(A2UIError.ValidationFailed, "Unknown icon '" + glyphName + "'");
                g = new IconMap.Glyph { Icon = UIcons.Question, Weight = UIconsWeight.Regular };
            }

            var icon = Tesserae.UI.Icon(g.Icon, g.Weight);
            var el = icon.Render();
            el.style.fontSize = "24px";
            el.style.lineHeight = "1";
            el.setAttribute("data-a2ui-icon", glyphName ?? "");
            return icon;
        }

        private static string SvgIcon(string path)
        {
            // Only path data (commands, numbers, separators) goes into the markup: an agent's string must not be able to add elements or attributes.
            foreach (var c in path)
            {
                var ok = char.IsLetterOrDigit(c) || c == ' ' || c == ',' || c == '.' || c == '-' || c == '+';
                if (!ok) return "<span></span>";
            }

            return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' width='24' height='24' fill='currentColor' aria-hidden='true' data-a2ui-icon='svg'><path d='" + path + "'/></svg>";
        }

        // ------------------------------------------------------------------ Video and AudioPlayer

        public static IComponent Video(ComponentContext cx)
        {
            var player = Tesserae.UI.VideoPlayer("");
            var el = player.Render();
            el.style.width = "100%";
            el.style.display = "none";

            cx.BindString("url", u =>
            {
                if (MediaUrl.IsSafe(u)) { el.style.display = ""; player.SetSource(u.Trim()); }
                else { el.style.display = "none"; }
            });

            cx.BindString("posterUrl", u => { if (MediaUrl.IsSafe(u)) player.Poster(u.Trim()); });
            return player;
        }

        public static IComponent AudioPlayer(ComponentContext cx)
        {
            var player = Tesserae.UI.AudioPlayer("");
            var el = player.Render();
            el.style.width = "100%";
            el.style.display = "none";

            cx.BindString("url", u =>
            {
                if (MediaUrl.IsSafe(u)) { el.style.display = ""; player.SetSource(u.Trim()); }
                else { el.style.display = "none"; }
            });

            // The description is the player's accessible name; with none it keeps its default.
            cx.BindString("description", d => { if (d.Length > 0) el.setAttribute("aria-label", d); else el.removeAttribute("aria-label"); });
            return player;
        }

        // ------------------------------------------------------------------ Divider

        public static IComponent Divider(ComponentContext cx)
        {
            if (cx.Literal("axis", "horizontal") == "vertical") return VerticalSeparator();

            return HorizontalSeparator("");
        }

        // ------------------------------------------------------------------ Card

        public static IComponent Card(ComponentContext cx)
        {
            var child = cx.Prop("child");
            IComponent content = child is object && child.IsString ? cx.Child(child.StringValue) : TextBlock("");

            // A Tesserae card is a flex row that shrinks its content to fit; a column inside it, whose children are meant to stretch, needs to
            // fill the card. The wrapper does that and also survives the child being rebuilt (a rebuilt child cannot lose a style set on it).
            var fill = VStack().Children(content);
            var fe = fill.Render();
            fe.style.flex = "1 1 0%";
            fe.style.minWidth = "0";
            return Tesserae.UI.Card(fill);
        }
    }
}

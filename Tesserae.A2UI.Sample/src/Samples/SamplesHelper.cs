using System;
using System.Linq;
using System.Threading.Tasks;
using Transpose;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.A2UI.Sample
{
    /// <summary>The bits every sample page shares: its title row, small text helpers and the way example files are fetched.</summary>
    public static class SamplesHelper
    {
        private const string REPOSITORY = "https://github.com/curiosity-ai/tesserae-a2ui";

        /// <summary>Where the A2UI example files are served from, relative to the page (copied by the sample's <c>_CopySampleExamples</c> target).</summary>
        public const string EXAMPLES = "assets/examples/";

        /// <summary>The basic catalog's id, as every example writes it.</summary>
        public const string BASIC = BasicCatalog.Id;

        /// <summary>Fetches a text file relative to the page.</summary>
        public static Task<string> FetchTextAsync(string url)
        {
            var tcs = new TaskCompletionSource<string>();
            Script.Write("fetch({0}).then(function(r){ if (!r.ok) throw new Error(r.status + ' ' + r.statusText); return r.text(); }).then({1}, function(e){ {2}(new Error('Could not load ' + {0} + ': ' + (e && e.message || e))); })",
                url, (Action<string>)(t => tcs.SetResult(t)), (Action<Exception>)(e => tcs.SetException(e)));
            return tcs.Task;
        }

        public static SectionStack SampleTitle(this SectionStack stack, string title, UIcons icon, string subtitle, string sourceFile = null)
        {
            return stack.Title(icon, title, subtitle,
                Button("Specification").SetIcon(UIcons.Books).Tooltip("The A2UI v1.0 protocol specification").OnClick(() => window.open("https://a2ui.org/specification/v1.0-a2ui/", "_blank")),
                Button("Source").SetIcon(UIcons.SquareTerminal).Tooltip("This page's source on GitHub").OnClick(() => window.open($"{REPOSITORY}/tree/main/Tesserae.A2UI.Sample/src/Samples{(sourceFile is null ? "" : "/" + sourceFile)}", "_blank")));
        }

        public static IComponent SampleHint(string text) => TextBlock(text).Small().Secondary().PT(4);

        /// <summary>Single quotes in a JSON literal written in C#: <c>J("{'a': 1}")</c>. (A page's JSON never needs a real apostrophe.)</summary>
        public static string J(string json) => json.Replace('\'', '"');
    }
}

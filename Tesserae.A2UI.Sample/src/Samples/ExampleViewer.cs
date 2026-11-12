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
    /// <summary>
    /// One A2UI example file on a page: the surface it describes drawn live on the left, and the protocol traffic on the
    /// right. "Replay" sends the messages again one at a time with a pause, which is how a streaming agent delivers them.
    /// </summary>
    public sealed class ExampleViewer : IComponent
    {
        private readonly A2UIHost _host = new A2UIHost();
        private readonly Inspector _inspector;
        private readonly string _text;
        private readonly Stack _stage;
        private int _run;

        public ExampleViewer(string title, string description, string group, string components, string text, UIcons icon)
        {
            _text = text;
            _inspector = new Inspector(_host);

            _stage = VStack().WS().Padding(16.px()).Children(_host.WS());

            var replay = Button("Replay as a stream").SetIcon(UIcons.Play).Tooltip("Clears the surface and sends the messages again, 400 ms apart").OnClick(() => Run(400));
            var reset = Button("Reset").SetIcon(UIcons.Refresh).Tooltip("Clears the surface and applies every message at once").OnClick(() => Run(0));

            var content = SectionStack().Secondary().SampleTitle(title, icon, description, "ExampleViewer.cs")
               .FlatSection(VStack().WS().Children(
                    HStack().WS().Gap(8.px()).AlignItemsCenter().Children(
                        TextBlock("Components used: " + components).Small().Secondary().Grow(),
                        replay, reset),
                    HStack().WS().Wrap().Gap(12.px()).AlignItems(ItemAlign.Start).Children(
                        Card(_stage).SetTitle("Rendered by Tesserae.A2UI").Grow(3).MinWidth(360.px()).Class("a2ui-example-stage"),
                        Card(_inspector).SetTitle("Protocol traffic").Grow(2).MinWidth(360.px()))));

            _content = content;
            Run(0);
        }

        private readonly IComponent _content;

        private void Run(int delay)
        {
            var run = ++_run;
            _host.Clear();
            _inspector.Reset();
            if (delay == 0) _host.Process(_text);
            else ReplayAsync(run, delay).FireAndForget();
        }

        private async Task ReplayAsync(int run, int delay)
        {
            var errors = new System.Collections.Generic.List<string>();
            foreach (var json in A2UIMessage.ReadStream(_text, errors))
            {
                if (run != _run) return;
                _host.Process(json);
                await Task.Delay(delay);
            }
        }

        public HTMLElement Render() => _content.Render();
    }
}

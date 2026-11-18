namespace Tesserae.A2UI
{
    /// <summary>
    /// How much an agent may ask one host to hold. The defaults are far above what a real interface needs: they are
    /// there so that a runaway or hostile stream ends as a reported error and a page that still answers, not as a tab
    /// that has stopped. Reaching a limit never throws: the part over the limit is left out and the host reports it
    /// (<see cref="A2UIHost.OnError"/>, and to the agent), once.
    /// Change them on <see cref="A2UIHost.Limits"/> before feeding the host messages.
    /// </summary>
    public sealed class A2UILimits
    {
        /// <summary>Characters in one call to <see cref="A2UIHost.Process(string)"/>. A longer text is not parsed.</summary>
        public int MaxMessageLength { get; set; } = 32 * 1024 * 1024;

        /// <summary>Surfaces alive at once. A <c>createSurface</c> beyond it is refused.</summary>
        public int MaxSurfaces { get; set; } = 1000;

        /// <summary>Component definitions one surface keeps. A new id beyond it is ignored; ids already there still update.</summary>
        public int MaxComponentsPerSurface { get; set; } = 50000;

        /// <summary>Components drawn on one surface at once, template items included. One beyond it draws a notice in its place.</summary>
        public int MaxRenderedComponents { get; set; } = 100000;

        /// <summary>How deep components may nest. One nested deeper draws a notice in its place.</summary>
        public int MaxComponentDepth { get; set; } = 200;

        /// <summary>Items one template draws from its list. The rest of a longer list is not drawn.</summary>
        public int MaxTemplateItems { get; set; } = 10000;

        /// <summary>
        /// How far past the end of an array a write may land. A JSON Pointer naming an index pads the array up to it,
        /// so without a limit one path can ask for billions of empty items.
        /// </summary>
        public int MaxArrayGap { get; set; } = 10000;

        /// <summary>How deep JSON may nest. It is read before a host exists, so it is the same for every host.</summary>
        public const int MaxJsonDepth = 256;

        /// <summary>How deep <c>${...}</c> expressions and the calls inside them may nest.</summary>
        public const int MaxExpressionDepth = 64;
    }
}

using System;
using System.Collections.Generic;
using Tesserae;
using static Transpose.Core.dom;

namespace Tesserae.A2UI
{
    /// <summary>The element one surface is drawn in: a container for its <c>root</c> component slot.</summary>
    public sealed class SurfaceView : IComponent, IDisposable
    {
        private readonly HTMLElement _element;
        private readonly ComponentSlot _root;

        public SurfaceView(Surface surface)
        {
            Surface  = surface;
            _element = document.createElement("div");
            _element.className = "a2ui-surface";
            _element.setAttribute("data-a2ui-surface", surface.Id);

            _root = new ComponentSlot(surface, "root", "", new List<int>(), surface.CatalogId, null);
            _element.appendChild(_root.Render());
        }

        public Surface Surface { get; }

        public HTMLElement Render() => _element;

        public void Dispose() => _root.Dispose();
    }
}

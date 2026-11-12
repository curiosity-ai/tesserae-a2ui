using System;
using Tesserae;

namespace Tesserae.A2UI
{
    /// <summary>
    /// The A2UI basic catalog (v1.0): Text, Image, Icon, Video, AudioPlayer, Row, Column, List, Card, Tabs, Divider,
    /// Modal, Button, CheckBox, TextField, DateTimeInput, ChoicePicker and Slider, and the functions that go with
    /// them, each drawn with the Tesserae component that already does the job.
    /// </summary>
    public static class BasicCatalog
    {
        public const string Id = "https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json";

        private static Catalog _instance;

        /// <summary>The shared basic catalog every <see cref="A2UIHost"/> starts with.</summary>
        public static Catalog Instance => _instance ?? (_instance = Create());

        /// <summary>A new basic catalog, for an application that wants to extend or replace parts of it.</summary>
        public static Catalog Create()
        {
            var c = new Catalog(Id);
            BasicFunctions.Register(c);

            c.Component("Text", BasicComponents.Text);
            c.Component("Image", BasicComponents.Image);
            c.Component("Icon", BasicComponents.Icon);
            c.Component("Video", BasicComponents.Video);
            c.Component("AudioPlayer", BasicComponents.AudioPlayer);
            c.Component("Row", cx => BasicComponents.Linear(cx, Stack.Orientation.Horizontal));
            c.Component("Column", cx => BasicComponents.Linear(cx, Stack.Orientation.Vertical));
            c.Component("List", BasicComponents.List);
            c.Component("Card", BasicComponents.Card);
            c.Component("Tabs", BasicComponents.Tabs);
            c.Component("Divider", BasicComponents.Divider);
            c.Component("Modal", BasicComponents.Modal);
            c.Component("Button", BasicComponents.Button);
            c.Component("CheckBox", BasicComponents.CheckBox);
            c.Component("TextField", BasicComponents.TextField);
            c.Component("DateTimeInput", BasicComponents.DateTimeInput);
            c.Component("ChoicePicker", BasicComponents.ChoicePicker);
            c.Component("Slider", BasicComponents.Slider);
            return c;
        }
    }
}

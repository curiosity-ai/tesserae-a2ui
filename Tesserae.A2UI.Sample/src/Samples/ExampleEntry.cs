using System.Collections.Generic;

namespace Tesserae.A2UI.Sample
{
    /// <summary>One row of <c>assets/examples/index.json</c>: an A2UI example file and the group the gallery files it under.</summary>
    internal sealed class ExampleEntry
    {
        public string Id, Name, Description, Group;
        public List<string> Components = new List<string>();

        public static List<ExampleEntry> Parse(string json)
        {
            var list = new List<ExampleEntry>();
            foreach (var row in JsonValue.Parse(json).Items)
            {
                var e = new ExampleEntry
                {
                    Id          = row.GetString("id"),
                    Name        = row.GetString("name"),
                    Description = row.GetString("description"),
                    Group       = row.GetString("group")
                };
                var comps = row.Get("components");
                if (comps is object) foreach (var c in comps.Items) e.Components.Add(c.StringValue);
                list.Add(e);
            }
            return list;
        }

        public static UIcons IconFor(string group)
        {
            switch (group)
            {
                case "Basics":               return UIcons.Apps;
                case "Forms and validation": return UIcons.Form;
                case "Lists and templates":  return UIcons.List;
                case "Streaming":            return UIcons.Waveform;
                case "Media":                return UIcons.Film;
                default:                     return UIcons.Square;
            }
        }
    }
}

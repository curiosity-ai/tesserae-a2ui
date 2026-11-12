using System;
using System.Collections.Generic;

namespace Tesserae.A2UI
{
    public enum A2UIMessageKind
    {
        CreateSurface,
        UpdateComponents,
        UpdateDataModel,
        DeleteSurface,
        CallRendererFunction,
        AgentFunctionResponse
    }

    /// <summary>One agent-to-renderer envelope (protocol v1.0), parsed from JSON.</summary>
    public sealed class A2UIMessage
    {
        public A2UIMessageKind Kind { get; private set; }
        public string Version { get; private set; }
        public string SurfaceId { get; private set; }

        // createSurface
        public string CatalogId { get; private set; }
        public bool SendDataModel { get; private set; }
        public JsonValue Metadata { get; private set; }

        /// <summary>createSurface (optional) and updateComponents: the component definitions.</summary>
        public JsonValue Components { get; private set; }

        /// <summary>createSurface: the initial data model object (optional).</summary>
        public JsonValue InitialDataModel { get; private set; }

        // updateDataModel
        public string Path { get; private set; }
        public JsonValue Value { get; private set; }

        // function calls / responses
        public string FunctionCallId { get; private set; }
        public JsonValue CallFunction { get; private set; }
        public JsonValue ResponseValue { get; private set; }
        public JsonValue ResponseError { get; private set; }

        /// <summary>The envelope as received.</summary>
        public JsonValue Json { get; private set; }

        /// <summary>Parses one envelope; throws <see cref="A2UIProtocolException"/> when it is not a valid v1.0 message.</summary>
        public static A2UIMessage Parse(JsonValue json)
        {
            if (json is null || !json.IsObject) throw new A2UIProtocolException("A message must be a JSON object");

            var m = new A2UIMessage { Json = json, Version = json.GetString("version") };

            if (json.Has("createSurface"))
            {
                var b = Body(json, "createSurface");
                m.Kind          = A2UIMessageKind.CreateSurface;
                m.SurfaceId     = Required(b, "surfaceId", "createSurface");
                m.CatalogId     = b.GetString("catalogId");
                m.SendDataModel = b.GetBool("sendDataModel");
                m.Metadata      = b.Get("metadata");
                if (b.Has("theme")) throw new A2UIProtocolException("A v1.0 createSurface must not carry 'theme' (styling comes from the host's Tesserae theme)");
                var comps = b.Get("components");
                if (comps is object && !comps.IsArray) throw new A2UIProtocolException("createSurface.components must be an array");
                m.Components = comps;
                var dm = b.Get("dataModel");
                if (dm is object && !dm.IsObject) throw new A2UIProtocolException("createSurface.dataModel must be an object");
                m.InitialDataModel = dm;
            }
            else if (json.Has("updateComponents"))
            {
                var b = Body(json, "updateComponents");
                m.Kind       = A2UIMessageKind.UpdateComponents;
                m.SurfaceId  = Required(b, "surfaceId", "updateComponents");
                var comps    = b.Get("components");
                if (comps is null || !comps.IsArray) throw new A2UIProtocolException("updateComponents.components must be an array");
                m.Components = comps;
            }
            else if (json.Has("updateDataModel"))
            {
                var b = Body(json, "updateDataModel");
                m.Kind      = A2UIMessageKind.UpdateDataModel;
                m.SurfaceId = Required(b, "surfaceId", "updateDataModel");
                m.Path      = b.GetString("path") ?? "/";
                if (!b.Has("value")) throw new A2UIProtocolException("updateDataModel requires 'value'");
                m.Value = b.Get("value");
            }
            else if (json.Has("deleteSurface"))
            {
                var b = Body(json, "deleteSurface");
                m.Kind      = A2UIMessageKind.DeleteSurface;
                m.SurfaceId = Required(b, "surfaceId", "deleteSurface");
            }
            else if (json.Has("callRendererFunction"))
            {
                var b = Body(json, "callRendererFunction");
                m.Kind           = A2UIMessageKind.CallRendererFunction;
                m.SurfaceId      = b.GetString("surfaceId");
                m.FunctionCallId = Required(b, "functionCallId", "callRendererFunction");
                m.CallFunction   = b.Get("callFunction");
                if (m.CallFunction is null || !m.CallFunction.IsObject) throw new A2UIProtocolException("callRendererFunction.callFunction must be an object");
            }
            else if (json.Has("agentFunctionResponse"))
            {
                var b = Body(json, "agentFunctionResponse");
                m.Kind           = A2UIMessageKind.AgentFunctionResponse;
                m.FunctionCallId = Required(b, "functionCallId", "agentFunctionResponse");
                m.ResponseValue  = b.Get("value");
                m.ResponseError  = b.Get("error");
                if (m.ResponseValue is null && m.ResponseError is null) throw new A2UIProtocolException("agentFunctionResponse needs 'value' or 'error'");
            }
            else
            {
                throw new A2UIProtocolException("Unknown message: expected createSurface, updateComponents, updateDataModel, deleteSurface, callRendererFunction or agentFunctionResponse");
            }

            return m;
        }

        private static JsonValue Body(JsonValue json, string key)
        {
            var b = json.Get(key);
            if (b is null || !b.IsObject) throw new A2UIProtocolException(key + " must be an object");
            return b;
        }

        private static string Required(JsonValue o, string key, string owner)
        {
            var s = o.GetString(key);
            if (string.IsNullOrEmpty(s)) throw new A2UIProtocolException(owner + "." + key + " is required");
            return s;
        }

        /// <summary>
        /// Reads a stream in any shape an agent or a file may hand over: JSON Lines (one envelope per line), one
        /// envelope, an array of envelopes, or a sample file <c>{ "messages": [ ... ] }</c>. A line that is not valid
        /// JSON is returned in <paramref name="errors"/>; the rest still parse.
        /// </summary>
        public static List<JsonValue> ReadStream(string text, List<string> errors = null)
        {
            var result = new List<JsonValue>();
            text = (text ?? "").Trim();
            if (text.Length == 0) return result;

            if (JsonValue.TryParse(text, out var whole, out var wholeError))
            {
                Flatten(whole, result);
                return result;
            }

            var lineNo = 0;
            foreach (var line in text.Split('\n'))
            {
                lineNo++;
                var t = line.Trim();
                if (t.Length == 0) continue;
                if (JsonValue.TryParse(t, out var v, out var err)) Flatten(v, result);
                else errors?.Add("line " + lineNo + ": " + err);
            }

            if (result.Count == 0) errors?.Add(wholeError);
            return result;
        }

        private static void Flatten(JsonValue v, List<JsonValue> into)
        {
            if (v.IsArray) { foreach (var i in v.Items) Flatten(i, into); }
            else if (v.IsObject && v.Get("messages") is JsonValue msgs && msgs.IsArray && !v.Has("version")) { foreach (var i in msgs.Items) into.Add(i); }
            else into.Add(v);
        }
    }

    public sealed class A2UIProtocolException : Exception
    {
        public A2UIProtocolException(string message) : base(message) { }
    }
}

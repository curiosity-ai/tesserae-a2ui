using System;

namespace Tesserae.A2UI
{
    /// <summary>A user action that the agent asked to hear about (a Button's <c>action.event</c>).</summary>
    public sealed class A2UIAction
    {
        public string Name { get; set; }
        public string SurfaceId { get; set; }
        public string SourceComponentId { get; set; }
        public string Timestamp { get; set; }
        public string UserMessage { get; set; }

        /// <summary>The action's context after every binding in it was resolved.</summary>
        public JsonValue Context { get; set; }

        /// <summary>The surface's whole data model when the surface was created with <c>sendDataModel</c>, else null (transport metadata, not part of the envelope).</summary>
        public JsonValue DataModel { get; set; }

        /// <summary>The renderer-to-agent envelope for this action.</summary>
        public JsonValue ToEnvelope()
        {
            var a = JsonValue.NewObject()
               .Set("name", JsonValue.String(Name))
               .Set("surfaceId", JsonValue.String(SurfaceId))
               .Set("sourceComponentId", JsonValue.String(SourceComponentId))
               .Set("timestamp", JsonValue.String(Timestamp));
            if (!string.IsNullOrEmpty(UserMessage)) a.Set("userMessage", JsonValue.String(UserMessage));
            a.Set("context", Context ?? JsonValue.NewObject());
            return Envelope("action", a);
        }

        /// <summary>The transport metadata that carries the data model (<c>renderer_data_model.json</c>), or null.</summary>
        public JsonValue ToMetadata()
        {
            if (DataModel is null) return null;
            return JsonValue.NewObject().Set("version", JsonValue.String("1.0"))
               .Set("surfaces", JsonValue.NewObject().Set(SurfaceId, DataModel));
        }

        internal static JsonValue Envelope(string key, JsonValue body) =>
            JsonValue.NewObject().Set("version", JsonValue.String("1.0")).Set(key, body);
    }

    /// <summary>A renderer-side error the agent should hear about (codes from the protocol).</summary>
    public sealed class A2UIError
    {
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string UnallowedParent  = "UNALLOWED_PARENT";
        public const string UnallowedChild   = "UNALLOWED_CHILD";
        public const string IntegrityError   = "INTEGRITY_ERROR";
        public const string InvalidFunction  = "INVALID_FUNCTION_CALL";
        public const string ExecutionError   = "EXECUTION_ERROR";
        public const string ExpressionError  = "EXPRESSION_ERROR";
        public const string CatalogError     = "CATALOG_ERROR";

        public A2UIError(string code, string message, string surfaceId = null, string path = null, string functionCallId = null)
        {
            Code           = code;
            Message        = message;
            SurfaceId      = surfaceId;
            Path           = path;
            FunctionCallId = functionCallId;
        }

        public string Code { get; }
        public string Message { get; }
        public string SurfaceId { get; }
        public string Path { get; }
        public string FunctionCallId { get; }

        public JsonValue ToEnvelope()
        {
            var e = JsonValue.NewObject().Set("code", JsonValue.String(Code)).Set("message", JsonValue.String(Message));
            if (SurfaceId is object) e.Set("surfaceId", JsonValue.String(SurfaceId));
            if (Path is object) e.Set("path", JsonValue.String(Path));
            if (FunctionCallId is object) e.Set("functionCallId", JsonValue.String(FunctionCallId));
            return A2UIAction.Envelope("error", e);
        }

        public override string ToString() => Code + ": " + Message + (SurfaceId is object ? " (surface " + SurfaceId + ")" : "");
    }
}

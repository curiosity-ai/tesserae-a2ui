using System;

namespace Tesserae.A2UI
{
    /// <summary>Who may invoke a catalog function (the catalog's <c>allowedCallers</c>).</summary>
    public enum AllowedCallers
    {
        RendererOnly,
        AgentOnly,
        RendererOrAgent
    }

    /// <summary>An exception a function raises to report a protocol error code (EXPRESSION_ERROR, EXECUTION_ERROR, ...).</summary>
    public sealed class A2UIFunctionException : Exception
    {
        public A2UIFunctionException(string code, string message) : base(message) { Code = code; }
        public string Code { get; }
    }

    /// <summary>
    /// A renderer-side function of a catalog. <see cref="Implementation"/> receives the arguments with every
    /// binding and nested call already resolved, so a function body is plain code; formatString is the one that reads
    /// the context too, because it resolves expressions embedded in a string.
    /// </summary>
    public sealed class FunctionDefinition
    {
        public FunctionDefinition(string name, Func<EvalContext, JsonValue, JsonValue> implementation)
        {
            Name           = name;
            Implementation = implementation;
        }

        public string Name { get; }
        public Func<EvalContext, JsonValue, JsonValue> Implementation { get; }
        public AllowedCallers AllowedCallers { get; set; } = AllowedCallers.RendererOnly;

        /// <summary>When true the function only runs inside an action handler (a user gesture), never in a binding or a check.</summary>
        public bool RequiresUserActivation { get; set; }

        /// <summary>The catalog's declared return type: string, number, boolean, array, object, validationResult, any or void.</summary>
        public string ReturnType { get; set; } = "any";
    }
}

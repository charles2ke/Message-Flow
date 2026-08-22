using System.Diagnostics;

namespace MessageFlow;

/// <summary>
/// The diagnostic primitives the library exposes to tracing infrastructure such as OpenTelemetry.
/// </summary>
public static class ChainDiagnostics
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> the library emits activities on.
    /// Subscribe to it to collect chain traces, for example with
    /// <c>builder.AddSource(ChainDiagnostics.ActivitySourceName)</c>.
    /// </summary>
    public const string ActivitySourceName = "MessageFlow";

    /// <summary>The default name of the activity created by <c>UseTracing</c>.</summary>
    public const string ExecuteActivityName = "MessageFlow.Execute";

    /// <summary>The tag carrying the request type of the chain.</summary>
    public const string RequestTypeTag = "messageflow.request_type";

    /// <summary>The tag carrying the response type of the chain.</summary>
    public const string ResponseTypeTag = "messageflow.response_type";

    /// <summary>The version reported by <see cref="ActivitySource"/>.</summary>
    public const string ActivitySourceVersion = "1.0.0";

    /// <summary>
    /// Gets the <see cref="System.Diagnostics.ActivitySource"/> the library emits activities on.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName, ActivitySourceVersion);
}

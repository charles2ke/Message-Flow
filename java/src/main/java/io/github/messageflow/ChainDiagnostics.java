package io.github.messageflow;

/**
 * The diagnostic primitives the library exposes to tracing infrastructure such as OpenTelemetry.
 */
public final class ChainDiagnostics {

    /** The name identifying the library as the source of the emitted spans. */
    public static final String TRACER_NAME = "MessageFlow";

    /** The version reported alongside {@link #TRACER_NAME}. */
    public static final String TRACER_VERSION = "1.0.0";

    /** The default name of the span created by {@code useTracing}. */
    public static final String EXECUTE_SPAN_NAME = "MessageFlow.Execute";

    /** The attribute carrying the request type of the chain. */
    public static final String REQUEST_TYPE_ATTRIBUTE = "messageflow.request_type";

    /** The attribute carrying the response type of the chain. */
    public static final String RESPONSE_TYPE_ATTRIBUTE = "messageflow.response_type";

    private ChainDiagnostics() {
    }
}

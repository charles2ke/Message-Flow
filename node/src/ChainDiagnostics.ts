/**
 * The diagnostic primitives the library exposes to tracing infrastructure such as OpenTelemetry.
 */
export class ChainDiagnostics {
  /**
   * The name identifying the library as the source of the emitted spans.
   */
  static readonly TRACER_NAME = 'MessageFlow';

  /**
   * The version reported alongside {@link TRACER_NAME}.
   */
  static readonly TRACER_VERSION = '1.0.0';

  /**
   * The default name of the span created by `useTracing`.
   */
  static readonly EXECUTE_SPAN_NAME = 'MessageFlow.Execute';

  /**
   * The attribute carrying the request type of the chain.
   */
  static readonly REQUEST_TYPE_ATTRIBUTE = 'messageflow.request_type';

  /**
   * The attribute carrying the response type of the chain.
   */
  static readonly RESPONSE_TYPE_ATTRIBUTE = 'messageflow.response_type';

  private constructor() {}
}

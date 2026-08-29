import type { ChainSpan } from './ChainSpan.js';

/**
 * Creates the spans emitted by the tracing middleware.
 *
 * The interface is intentionally minimal so the library stays dependency-free:
 * an adapter over OpenTelemetry, or over any other tracing framework, is a few lines of code.
 */
export interface ChainTracer {
  /**
   * Starts a span covering the execution of the remainder of the chain.
   *
   * @param spanName - the name of the span
   * @param requestType - the type of the request flowing through the chain
   * @param responseType - the type of the response produced by the chain
   * @returns the started span, never `null`
   */
  startSpan(
    spanName: string,
    requestType: string | null,
    responseType: string | null
  ): ChainSpan;
}

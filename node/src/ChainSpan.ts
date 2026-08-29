/**
 * A unit of tracing work covering the execution of the remainder of a chain.
 */
export interface ChainSpan {
  /**
   * Marks the span as successfully completed.
   */
  setOk(): void;

  /**
   * Marks the span as failed.
   *
   * @param error - the error that failed the request
   */
  setError(error: unknown): void;

  /**
   * Ends the span.
   */
  close(): void;
}

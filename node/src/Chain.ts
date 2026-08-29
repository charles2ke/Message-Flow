import type { CancellationToken } from './CancellationToken.js';

/**
 * An immutable, pre-compiled chain of responsibility.
 *
 * @typeParam TRequest - the type of the request flowing through the chain
 * @typeParam TResponse - the type of the response produced by the chain
 */
export interface Chain<TRequest, TResponse> {
  /**
   * Gets the number of handlers in the chain, excluding the terminal fallback.
   *
   * @returns the number of handlers in the chain
   */
  readonly count: number;

  /**
   * Sends a request through the chain.
   *
   * When no handler accepts the request and no fallback was configured, the returned promise
   * rejects with an {@link UnhandledRequestError}.
   *
   * @param request - the request to process
   * @param cancellationToken - a token used to cancel the operation (defaults to {@link CancellationToken.none})
   * @returns the response produced by the first handler that accepted the request
   */
  execute(
    request: TRequest,
    cancellationToken?: CancellationToken
  ): Promise<TResponse>;
}

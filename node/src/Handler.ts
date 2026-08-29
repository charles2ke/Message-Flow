import type { CancellationToken } from './CancellationToken.js';
import type { NextHandler } from './NextHandler.js';

/**
 * A single link of a chain of responsibility.
 *
 * @typeParam TRequest - the type of the request flowing through the chain
 * @typeParam TResponse - the type of the response produced by the chain
 */
export interface Handler<TRequest, TResponse> {
  /**
   * Processes the request, optionally delegating to the next handler of the chain.
   *
   * @param request - the request to process
   * @param next - the next handler of the chain
   * @param cancellationToken - a token used to cancel the operation
   * @returns the response for the request
   */
  handle(
    request: TRequest,
    next: NextHandler<TRequest, TResponse>,
    cancellationToken: CancellationToken
  ): Promise<TResponse>;
}

import type { CancellationToken } from './CancellationToken.js';
import type { Handler } from './Handler.js';
import type { NextHandler } from './NextHandler.js';

/**
 * Convenience base class for handlers that either fully handle a request or pass it on.
 *
 * @typeParam TRequest - the type of the request flowing through the chain
 * @typeParam TResponse - the type of the response produced by the chain
 */
export abstract class HandlerBase<TRequest, TResponse>
  implements Handler<TRequest, TResponse>
{
  /**
   * Determines whether this handler is responsible for the given request.
   *
   * @param request - the request to inspect
   * @returns `true` when this handler should process the request
   */
  protected abstract canHandle(request: TRequest): boolean;

  /**
   * Processes a request this handler is responsible for.
   *
   * @param request - the request to process
   * @param cancellationToken - a token used to cancel the operation
   * @returns the response for the request
   */
  protected abstract process(
    request: TRequest,
    cancellationToken: CancellationToken
  ): Promise<TResponse>;

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
  ): Promise<TResponse> {
    if (next === null || next === undefined) {
      throw new TypeError('next must not be null or undefined');
    }

    return this.canHandle(request)
      ? this.process(request, cancellationToken)
      : next(request, cancellationToken);
  }
}

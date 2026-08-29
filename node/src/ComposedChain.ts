import { CancellationToken } from './CancellationToken.js';
import type { Chain } from './Chain.js';
import type { NextHandler } from './NextHandler.js';
import { UnhandledRequestError } from './UnhandledRequestError.js';

/**
 * Default {@link Chain} implementation. The handler pipeline is composed once, at build time, so
 * execution is a simple delegate call.
 *
 * @typeParam TRequest - the type of the request flowing through the chain
 * @typeParam TResponse - the type of the response produced by the chain
 */
export class ComposedChain<TRequest, TResponse>
  implements Chain<TRequest, TResponse>
{
  private readonly _composer: (
    terminal: NextHandler<TRequest, TResponse>
  ) => NextHandler<TRequest, TResponse>;
  private readonly pipeline: NextHandler<TRequest, TResponse>;
  readonly count: number;

  /** @internal */
  constructor(
    composer: (
      terminal: NextHandler<TRequest, TResponse>
    ) => NextHandler<TRequest, TResponse>,
    count: number
  ) {
    this._composer = composer;
    this.count = count;
    this.pipeline = composer(ComposedChain.unhandled);
  }

  /**
   * Gets the open composition of the chain: it turns the step invoked when no handler of this chain
   * accepted the request into the executable pipeline. It allows the chain to be merged into another
   * chain without re-running its builder.
   *
   * @internal
   * @returns the open composition of the chain
   */
  composer(): (
    terminal: NextHandler<TRequest, TResponse>
  ) => NextHandler<TRequest, TResponse> {
    return this._composer;
  }

  /**
   * Sends a request through the chain.
   *
   * @param request - the request to process
   * @param cancellationToken - a token used to cancel the operation
   * @returns the response produced by the first handler that accepted the request
   */
  execute(
    request: TRequest,
    cancellationToken: CancellationToken = CancellationToken.none()
  ): Promise<TResponse> {
    if (cancellationToken === null || cancellationToken === undefined) {
      throw new TypeError('cancellationToken must not be null or undefined');
    }
    return this.pipeline(request, cancellationToken);
  }

  private static unhandled<TRequest, TResponse>(
    _request: TRequest,
    _cancellationToken: CancellationToken
  ): Promise<TResponse> {
    return Promise.reject(new UnhandledRequestError());
  }
}

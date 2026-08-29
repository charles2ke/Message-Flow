import type { CancellationToken } from './CancellationToken.js';

/**
 * Represents the next step of a chain of responsibility.
 *
 * @typeParam TRequest - the type of the request flowing through the chain
 * @typeParam TResponse - the type of the response produced by the chain
 */
export type NextHandler<TRequest, TResponse> = (
  request: TRequest,
  cancellationToken: CancellationToken
) => Promise<TResponse>;

import { ChainBuilder } from './ChainBuilder.js';

/**
 * Creates a builder for a chain that turns a request into a response.
 *
 * @typeParam TRequest - the type of the request flowing through the chain
 * @typeParam TResponse - the type of the response produced by the chain
 * @returns a new, empty builder
 */
export function createChain<TRequest, TResponse>(): ChainBuilder<
  TRequest,
  TResponse
> {
  return new ChainBuilder<TRequest, TResponse>();
}

// Re-export all public APIs
export { CancellationToken } from './CancellationToken.js';
export { CancellationTokenSource } from './CancellationTokenSource.js';
export type { Chain } from './Chain.js';
export { ChainBuilder } from './ChainBuilder.js';
export { ChainDiagnostics } from './ChainDiagnostics.js';
export { ChainLogLevel } from './ChainLogLevel.js';
export type { ChainLogger } from './ChainLogger.js';
export type { ChainSpan } from './ChainSpan.js';
export type { ChainTracer } from './ChainTracer.js';
export { ComposedChain } from './ComposedChain.js';
export type { Handler } from './Handler.js';
export { HandlerBase } from './HandlerBase.js';
export type { NextHandler } from './NextHandler.js';
export { UnhandledRequestError } from './UnhandledRequestError.js';

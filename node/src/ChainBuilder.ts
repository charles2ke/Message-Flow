import { CancellationToken } from './CancellationToken.js';
import type { Chain } from './Chain.js';
import { ChainDiagnostics } from './ChainDiagnostics.js';
import { ChainLogLevel } from './ChainLogLevel.js';
import type { ChainLogger } from './ChainLogger.js';
import type { ChainTracer } from './ChainTracer.js';
import { ComposedChain } from './ComposedChain.js';
import type { Handler } from './Handler.js';
import type { NextHandler } from './NextHandler.js';

type Composer<TRequest, TResponse> = (
  terminal: NextHandler<TRequest, TResponse>
) => NextHandler<TRequest, TResponse>;

/**
 * Builds an immutable {@link Chain} from an ordered set of handlers.
 *
 * @typeParam TRequest - the type of the request flowing through the chain
 * @typeParam TResponse - the type of the response produced by the chain
 */
export class ChainBuilder<TRequest, TResponse> {
  private readonly steps: Composer<TRequest, TResponse>[] = [];
  private fallback: NextHandler<TRequest, TResponse> | null = null;

  /**
   * Appends a handler to the end of the chain.
   *
   * @param handler - the handler to append (a Handler object or a plain function)
   * @returns the same builder instance, for chaining
   * @throws {TypeError} `handler` is `null` or `undefined`
   */
  use(
    handler:
      | Handler<TRequest, TResponse>
      | ((
          request: TRequest,
          next: NextHandler<TRequest, TResponse>,
          cancellationToken: CancellationToken
        ) => Promise<TResponse>)
      | ChainBuilder<TRequest, TResponse>
      | Chain<TRequest, TResponse>
  ): this {
    if (handler === null || handler === undefined) {
      throw new TypeError('handler must not be null or undefined');
    }

    if (handler instanceof ChainBuilder) {
      this.steps.push(handler.createComposer());
      return this;
    }

    if (this.isChain(handler)) {
      if (handler instanceof ComposedChain) {
        this.steps.push(handler.composer());
      } else {
        this.steps.push((_terminal) => (request, cancellationToken) =>
          handler.execute(request, cancellationToken)
        );
      }
      return this;
    }

    const handlerFn = this.isHandlerObject(handler)
      ? (
          request: TRequest,
          next: NextHandler<TRequest, TResponse>,
          cancellationToken: CancellationToken
        ) => handler.handle(request, next, cancellationToken)
      : handler;

    this.steps.push((nextHandler) => (request, cancellationToken) =>
      handlerFn(request, nextHandler, cancellationToken)
    );
    return this;
  }

  /**
   * Appends a handler that only runs when `predicate` matches the request; otherwise the
   * request flows to the next handler.
   *
   * @param predicate - decides whether the handler is responsible for the request
   * @param handler - produces the response for accepted requests
   * @returns the same builder instance, for chaining
   * @throws {TypeError} `predicate` or `handler` is `null` or `undefined`
   */
  useWhen(
    predicate: (request: TRequest) => boolean,
    handler: NextHandler<TRequest, TResponse>
  ): this {
    if (predicate === null || predicate === undefined) {
      throw new TypeError('predicate must not be null or undefined');
    }
    if (handler === null || handler === undefined) {
      throw new TypeError('handler must not be null or undefined');
    }

    return this.use((request, next, cancellationToken) =>
      predicate(request)
        ? handler(request, cancellationToken)
        : next(request, cancellationToken)
    );
  }

  /**
   * Appends a nested sub-chain that only runs when `predicate` matches the request.
   *
   * When the predicate does not match, the request skips the branch entirely and flows to the
   * next handler of the parent chain. When it does match but no handler of the branch accepts the
   * request, the request falls through to the next handler of the parent chain as well — unless the
   * branch configures its own fallback, which then becomes the terminal step of the branch. The
   * branch is configured immediately and composed at {@link build} time, so it costs a single
   * extra call per request. It counts as one handler towards {@link Chain.count}, regardless of
   * how many handlers it contains.
   *
   * @param predicate - decides whether the request enters the branch
   * @param configure - adds the handlers of the branch to the supplied builder
   * @returns the same builder instance, for chaining
   * @throws {TypeError} `predicate` or `configure` is `null` or `undefined`
   */
  useBranch(
    predicate: (request: TRequest) => boolean,
    configure: (builder: ChainBuilder<TRequest, TResponse>) => void
  ): this {
    if (predicate === null || predicate === undefined) {
      throw new TypeError('predicate must not be null or undefined');
    }
    if (configure === null || configure === undefined) {
      throw new TypeError('configure must not be null or undefined');
    }

    const branch = new ChainBuilder<TRequest, TResponse>();
    configure(branch);
    const branchComposer = branch.createComposer();

    this.steps.push((nextHandler) => {
      const branchPipeline = branchComposer(nextHandler);
      return (request, cancellationToken) =>
        predicate(request)
          ? branchPipeline(request, cancellationToken)
          : nextHandler(request, cancellationToken);
    });

    return this;
  }

  /**
   * Appends a middleware that logs the start, the completion and the failure of every request
   * flowing through the remainder of the chain.
   *
   * Only the chain name and durations are logged; the request itself is never written to the log,
   * so no payload can leak into log storage. Failures are logged at {@link ChainLogLevel.Error} and
   * the exception is propagated unchanged.
   *
   * @param logger - the logger receiving the entries
   * @param level - the level used for the start and completion entries (defaults to {@link ChainLogLevel.Debug})
   * @param chainName - the name identifying the chain in the log entries (defaults to "MessageFlow")
   * @returns the same builder instance, for chaining
   * @throws {TypeError} any argument is `null` or `undefined`
   */
  useLogging(
    logger: ChainLogger,
    level: ChainLogLevel = ChainLogLevel.Debug,
    chainName: string = ChainDiagnostics.TRACER_NAME
  ): this {
    if (logger === null || logger === undefined) {
      throw new TypeError('logger must not be null or undefined');
    }
    if (level === null || level === undefined) {
      throw new TypeError('level must not be null or undefined');
    }
    if (chainName === null || chainName === undefined) {
      throw new TypeError('chainName must not be null or undefined');
    }

    return this.use(async (request, next, cancellationToken) => {
      if (logger.isEnabled(level)) {
        logger.log(level, `Executing chain ${chainName}.`, null);
      }

      const timestamp = Date.now();

      try {
        const response = await next(request, cancellationToken);

        if (logger.isEnabled(level)) {
          logger.log(
            level,
            `Executed chain ${chainName} in ${elapsed(timestamp)} ms.`,
            null
          );
        }

        return response;
      } catch (error: unknown) {
        if (logger.isEnabled(ChainLogLevel.Error)) {
          logger.log(
            ChainLogLevel.Error,
            `Chain ${chainName} failed after ${elapsed(timestamp)} ms.`,
            error
          );
        }
        throw error;
      }
    });
  }

  /**
   * Appends a middleware that wraps the remainder of the chain in a {@link ChainSpan}.
   *
   * Failures mark the span as failed and propagate the exception unchanged. The span is always
   * ended, whether the request succeeded or not.
   *
   * @param tracer - the tracer creating the spans
   * @param spanName - the name of the created span (defaults to {@link ChainDiagnostics.EXECUTE_SPAN_NAME})
   * @param requestType - the request type reported on the span, may be `null`
   * @param responseType - the response type reported on the span, may be `null`
   * @returns the same builder instance, for chaining
   * @throws {TypeError} `tracer` or `spanName` is `null` or `undefined`
   * @throws {TypeError} `spanName` is empty
   */
  useTracing(
    tracer: ChainTracer,
    spanName: string = ChainDiagnostics.EXECUTE_SPAN_NAME,
    requestType: string | null = null,
    responseType: string | null = null
  ): this {
    if (tracer === null || tracer === undefined) {
      throw new TypeError('tracer must not be null or undefined');
    }
    if (spanName === null || spanName === undefined) {
      throw new TypeError('spanName must not be null or undefined');
    }
    if (spanName === '') {
      throw new TypeError('spanName must not be empty');
    }

    return this.use(async (request, next, cancellationToken) => {
      const span = tracer.startSpan(spanName, requestType, responseType);

      if (span === null || span === undefined) {
        throw new TypeError('ChainTracer.startSpan must not return null');
      }

      try {
        const response = await next(request, cancellationToken);
        span.setOk();
        return response;
      } catch (error: unknown) {
        span.setError(error);
        throw error;
      } finally {
        span.close();
      }
    });
  }

  /**
   * Sets the terminal step invoked when no handler accepted the request. Without a fallback the
   * chain rejects with {@link UnhandledRequestError} instead.
   *
   * @param fallback - the terminal handler
   * @returns the same builder instance, for chaining
   * @throws {TypeError} `fallback` is `null` or `undefined`
   */
  withFallback(fallback: NextHandler<TRequest, TResponse>): this {
    if (fallback === null || fallback === undefined) {
      throw new TypeError('fallback must not be null or undefined');
    }

    this.fallback = fallback;
    return this;
  }

  /**
   * Composes the configured handlers into an immutable chain.
   *
   * @returns the composed chain
   */
  build(): Chain<TRequest, TResponse> {
    return new ComposedChain(this.createComposer(), this.steps.length);
  }

  /**
   * Snapshots the configured handlers into an open composition: a function turning the step invoked
   * when no handler accepted the request into the composed pipeline.
   *
   * @internal
   * @returns the open composition of the configured handlers
   */
  private createComposer(): Composer<TRequest, TResponse> {
    const snapshot = [...this.steps];
    const snapshotFallback = this.fallback;

    return (terminal) => {
      let pipeline: NextHandler<TRequest, TResponse> =
        snapshotFallback ?? terminal;

      for (let i = snapshot.length - 1; i >= 0; i--) {
        pipeline = snapshot[i](pipeline);
      }

      return pipeline;
    };
  }

  private isHandlerObject(
    value: unknown
  ): value is Handler<TRequest, TResponse> {
    return (
      typeof value === 'object' &&
      value !== null &&
      'handle' in value &&
      typeof value.handle === 'function'
    );
  }

  private isChain(value: unknown): value is Chain<TRequest, TResponse> {
    return (
      typeof value === 'object' &&
      value !== null &&
      'execute' in value &&
      typeof value.execute === 'function' &&
      'count' in value
    );
  }
}

function elapsed(timestamp: number): string {
  const elapsedMilliseconds = Date.now() - timestamp;
  return elapsedMilliseconds.toFixed(3);
}

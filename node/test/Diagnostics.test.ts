import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  createChain,
  CancellationToken,
  CancellationTokenSource,
  ChainLogLevel,
  ChainDiagnostics,
  type ChainLogger,
  type ChainTracer,
  type ChainSpan,
} from '../src/index.js';

describe('Cancellation', () => {
  it('cancellation token none is never cancelled', () => {
    const token = CancellationToken.none();
    assert.equal(token.isCancellationRequested(), false);
  });

  it('cancellation token source creates cancellable token', () => {
    const source = new CancellationTokenSource();
    const token = source.token();

    assert.equal(token.isCancellationRequested(), false);

    source.cancel();

    assert.equal(token.isCancellationRequested(), true);
  });

  it('throwIfCancellationRequested throws when cancelled', () => {
    const source = new CancellationTokenSource();
    const token = source.token();

    source.cancel();

    assert.throws(
      () => token.throwIfCancellationRequested(),
      (error: unknown) => {
        assert.ok(error instanceof Error);
        assert.equal(error.name, 'CancellationError');
        assert.match(error.message, /cancelled/i);
        return true;
      }
    );
  });

  it('throwIfCancellationRequested does not throw when not cancelled', () => {
    const token = CancellationToken.none();
    assert.doesNotThrow(() => token.throwIfCancellationRequested());
  });

  it('handlers receive cancellation token', async () => {
    const source = new CancellationTokenSource();
    let receivedToken: CancellationToken | null = null;

    const chain = createChain<number, string>()
      .useWhen(
        (_request) => true,
        (_request, token) => {
          receivedToken = token;
          return Promise.resolve('handled');
        }
      )
      .build();

    await chain.execute(1, source.token());

    assert.ok(receivedToken !== null);
    assert.equal((receivedToken as CancellationToken).isCancellationRequested(), false);

    source.cancel();
    assert.equal((receivedToken as CancellationToken).isCancellationRequested(), true);
  });

  it('execute without token uses none', async () => {
    let receivedToken: CancellationToken | null = null;

    const chain = createChain<number, string>()
      .useWhen(
        (_request) => true,
        (_request, token) => {
          receivedToken = token;
          return Promise.resolve('handled');
        }
      )
      .build();

    await chain.execute(1);

    assert.ok(receivedToken !== null);
    assert.equal((receivedToken as CancellationToken).isCancellationRequested(), false);
  });
});

describe('Logging', () => {
  class TestLogger implements ChainLogger {
    public entries: Array<{
      level: ChainLogLevel;
      message: string;
      error: unknown;
    }> = [];

    isEnabled(_level: ChainLogLevel): boolean {
      return true;
    }

    log(level: ChainLogLevel, message: string, error: unknown): void {
      this.entries.push({ level, message, error });
    }
  }

  it('logs execution start and completion', async () => {
    const logger = new TestLogger();

    const chain = createChain<number, string>()
      .useLogging(logger, ChainLogLevel.Information, 'test-chain')
      .withFallback((_request, _token) => Promise.resolve('handled'))
      .build();

    await chain.execute(1);

    assert.equal(logger.entries.length, 2);
    assert.equal(logger.entries[0].level, ChainLogLevel.Information);
    assert.match(logger.entries[0].message, /Executing chain test-chain/);
    assert.equal(logger.entries[0].error, null);

    assert.equal(logger.entries[1].level, ChainLogLevel.Information);
    assert.match(logger.entries[1].message, /Executed chain test-chain in .* ms/);
    assert.equal(logger.entries[1].error, null);
  });

  it('logs failure at error level', async () => {
    const logger = new TestLogger();
    const testError = new Error('test error');

    const chain = createChain<number, string>()
      .useLogging(logger, ChainLogLevel.Information, 'test-chain')
      .use((_request, _next, _token) => Promise.reject(testError))
      .build();

    await assert.rejects(async () => chain.execute(1));

    assert.equal(logger.entries.length, 2);
    assert.equal(logger.entries[0].level, ChainLogLevel.Information);
    assert.match(logger.entries[0].message, /Executing chain test-chain/);

    assert.equal(logger.entries[1].level, ChainLogLevel.Error);
    assert.match(logger.entries[1].message, /Chain test-chain failed after .* ms/);
    assert.equal(logger.entries[1].error, testError);
  });

  it('uses default level and name when not specified', async () => {
    const logger = new TestLogger();

    const chain = createChain<number, string>()
      .useLogging(logger)
      .withFallback((_request, _token) => Promise.resolve('handled'))
      .build();

    await chain.execute(1);

    assert.equal(logger.entries.length, 2);
    assert.equal(logger.entries[0].level, ChainLogLevel.Debug);
    assert.match(logger.entries[0].message, /MessageFlow/);
  });

  it('respects isEnabled check', async () => {
    class SelectiveLogger implements ChainLogger {
      public entries: Array<{
        level: ChainLogLevel;
        message: string;
        error: unknown;
      }> = [];

      isEnabled(level: ChainLogLevel): boolean {
        return level >= ChainLogLevel.Warning;
      }

      log(level: ChainLogLevel, message: string, error: unknown): void {
        this.entries.push({ level, message, error });
      }
    }

    const logger = new SelectiveLogger();

    const chain = createChain<number, string>()
      .useLogging(logger, ChainLogLevel.Information, 'test-chain')
      .withFallback((_request, _token) => Promise.resolve('handled'))
      .build();

    await chain.execute(1);

    assert.equal(logger.entries.length, 0);
  });

  it('throws on null logger', () => {
    const builder = createChain<number, string>();
    assert.throws(
      () => builder.useLogging(null as unknown as ChainLogger),
      TypeError
    );
  });
});

describe('Tracing', () => {
  class TestSpan implements ChainSpan {
    public status: 'ok' | 'error' | 'none' = 'none';
    public error: unknown = null;
    public closed = false;

    setOk(): void {
      this.status = 'ok';
    }

    setError(error: unknown): void {
      this.status = 'error';
      this.error = error;
    }

    close(): void {
      this.closed = true;
    }
  }

  class TestTracer implements ChainTracer {
    public spans: Array<{
      spanName: string;
      requestType: string | null;
      responseType: string | null;
      span: TestSpan;
    }> = [];

    startSpan(
      spanName: string,
      requestType: string | null,
      responseType: string | null
    ): ChainSpan {
      const span = new TestSpan();
      this.spans.push({ spanName, requestType, responseType, span });
      return span;
    }
  }

  it('creates span for successful execution', async () => {
    const tracer = new TestTracer();

    const chain = createChain<number, string>()
      .useTracing(tracer, 'test-span', 'number', 'string')
      .withFallback((_request, _token) => Promise.resolve('handled'))
      .build();

    await chain.execute(1);

    assert.equal(tracer.spans.length, 1);
    assert.equal(tracer.spans[0].spanName, 'test-span');
    assert.equal(tracer.spans[0].requestType, 'number');
    assert.equal(tracer.spans[0].responseType, 'string');
    assert.equal(tracer.spans[0].span.status, 'ok');
    assert.equal(tracer.spans[0].span.closed, true);
  });

  it('marks span as error on failure', async () => {
    const tracer = new TestTracer();
    const testError = new Error('test error');

    const chain = createChain<number, string>()
      .useTracing(tracer, 'test-span', 'number', 'string')
      .use((_request, _next, _token) => Promise.reject(testError))
      .build();

    await assert.rejects(async () => chain.execute(1));

    assert.equal(tracer.spans.length, 1);
    assert.equal(tracer.spans[0].span.status, 'error');
    assert.equal(tracer.spans[0].span.error, testError);
    assert.equal(tracer.spans[0].span.closed, true);
  });

  it('uses default span name when not specified', async () => {
    const tracer = new TestTracer();

    const chain = createChain<number, string>()
      .useTracing(tracer)
      .withFallback((_request, _token) => Promise.resolve('handled'))
      .build();

    await chain.execute(1);

    assert.equal(tracer.spans.length, 1);
    assert.equal(tracer.spans[0].spanName, ChainDiagnostics.EXECUTE_SPAN_NAME);
    assert.equal(tracer.spans[0].requestType, null);
    assert.equal(tracer.spans[0].responseType, null);
  });

  it('throws on null tracer', () => {
    const builder = createChain<number, string>();
    assert.throws(
      () => builder.useTracing(null as unknown as ChainTracer),
      TypeError
    );
  });

  it('throws on empty span name', () => {
    class EmptyTracer implements ChainTracer {
      startSpan(): ChainSpan {
        return new TestSpan();
      }
    }

    const builder = createChain<number, string>();
    assert.throws(() => builder.useTracing(new EmptyTracer(), ''), TypeError);
  });

  it('throws when tracer returns null', async () => {
    class NullTracer implements ChainTracer {
      startSpan(): ChainSpan {
        return null as unknown as ChainSpan;
      }
    }

    const chain = createChain<number, string>()
      .useTracing(new NullTracer())
      .withFallback((_request, _token) => Promise.resolve('handled'))
      .build();

    await assert.rejects(async () => chain.execute(1), TypeError);
  });
});

describe('ChainDiagnostics', () => {
  it('exposes diagnostic constants', () => {
    assert.equal(ChainDiagnostics.TRACER_NAME, 'MessageFlow');
    assert.equal(ChainDiagnostics.TRACER_VERSION, '1.0.0');
    assert.equal(ChainDiagnostics.EXECUTE_SPAN_NAME, 'MessageFlow.Execute');
    assert.equal(
      ChainDiagnostics.REQUEST_TYPE_ATTRIBUTE,
      'messageflow.request_type'
    );
    assert.equal(
      ChainDiagnostics.RESPONSE_TYPE_ATTRIBUTE,
      'messageflow.response_type'
    );
  });
});

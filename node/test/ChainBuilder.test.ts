import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  createChain,
  CancellationToken,
  CancellationTokenSource,
  HandlerBase,
  UnhandledRequestError,
  type Handler,
  type NextHandler,
} from '../src/index.js';

describe('ChainBuilder', () => {
  it('first matching handler produces response', async () => {
    const chain = createChain<number, string>()
      .useWhen(
        (request) => request < 0,
        (request, _token) => Promise.resolve(`negative:${request}`)
      )
      .useWhen(
        (request) => request === 0,
        (_request, _token) => Promise.resolve('zero')
      )
      .withFallback((request, _token) => Promise.resolve(`positive:${request}`))
      .build();

    assert.equal(await chain.execute(-7), 'negative:-7');
    assert.equal(await chain.execute(0), 'zero');
    assert.equal(await chain.execute(3), 'positive:3');
    assert.equal(chain.count, 2);
  });

  it('unhandled request fails when no fallback configured', async () => {
    const chain = createChain<number, string>()
      .useWhen(
        (request) => request < 0,
        (_request, _token) => Promise.resolve('negative')
      )
      .build();

    await assert.rejects(
      async () => chain.execute(1),
      (error: unknown) => {
        assert.ok(error instanceof UnhandledRequestError);
        return true;
      }
    );
  });

  it('middleware runs around the rest of the chain', async () => {
    const log: string[] = [];

    const chain = createChain<number, string>()
      .use(async (request, next, token) => {
        log.push('before');
        const response = await next(request, token);
        log.push('after');
        return response + '!';
      })
      .useWhen(
        (_request) => true,
        (_request, _token) => Promise.resolve('handled')
      )
      .build();

    const result = await chain.execute(1);
    assert.equal(result, 'handled!');
    assert.deepEqual(log, ['before', 'after']);
  });

  it('handler implementation is invoked', async () => {
    class EvenHandler extends HandlerBase<number, string> {
      protected canHandle(request: number): boolean {
        return request % 2 === 0;
      }

      protected process(
        request: number,
        _cancellationToken: CancellationToken
      ): Promise<string> {
        return Promise.resolve(`even:${request}`);
      }
    }

    const chain = createChain<number, string>()
      .use(new EvenHandler())
      .withFallback((_request, _token) => Promise.resolve('odd'))
      .build();

    assert.equal(await chain.execute(2), 'even:2');
    assert.equal(await chain.execute(3), 'odd');
  });

  it('branch is skipped when predicate does not match', async () => {
    const chain = createChain<number, string>()
      .useBranch(
        (request) => request > 10,
        (branch) =>
          branch.useWhen(
            (request) => request > 100,
            (_request, _token) => Promise.resolve('huge')
          )
      )
      .withFallback((_request, _token) => Promise.resolve('fallback'))
      .build();

    assert.equal(await chain.execute(1000), 'huge');
    assert.equal(await chain.execute(20), 'fallback');
    assert.equal(await chain.execute(1), 'fallback');
    assert.equal(chain.count, 1);
  });

  it('branch fallback terminates the chain', async () => {
    const chain = createChain<number, string>()
      .useBranch(
        (request) => request > 10,
        (branch) =>
          branch.withFallback((_request, _token) =>
            Promise.resolve('branch-fallback')
          )
      )
      .withFallback((_request, _token) => Promise.resolve('fallback'))
      .build();

    assert.equal(await chain.execute(20), 'branch-fallback');
    assert.equal(await chain.execute(1), 'fallback');
  });

  it('merged builder falls through to the parent chain', async () => {
    const fragment = createChain<number, string>().useWhen(
      (request) => request === 1,
      (_request, _token) => Promise.resolve('one')
    );

    const chain = createChain<number, string>()
      .use(fragment)
      .useWhen(
        (request) => request === 2,
        (_request, _token) => Promise.resolve('two')
      )
      .build();

    assert.equal(await chain.execute(1), 'one');
    assert.equal(await chain.execute(2), 'two');
    assert.equal(chain.count, 2);

    // Verify snapshot semantics
    fragment.useWhen(
      (request) => request === 3,
      (_request, _token) => Promise.resolve('three')
    );

    await assert.rejects(
      async () => chain.execute(3),
      (error: unknown) => {
        assert.ok(error instanceof UnhandledRequestError);
        return true;
      }
    );
  });

  it('merged chain is recomposed against the parent chain', async () => {
    const merged = createChain<number, string>()
      .useWhen(
        (request) => request === 1,
        (_request, _token) => Promise.resolve('one')
      )
      .build();

    const chain = createChain<number, string>()
      .use(merged)
      .useWhen(
        (request) => request === 2,
        (_request, _token) => Promise.resolve('two')
      )
      .build();

    assert.equal(await chain.execute(1), 'one');
    assert.equal(await chain.execute(2), 'two');

    await assert.rejects(
      async () => chain.execute(3),
      (error: unknown) => {
        assert.ok(error instanceof UnhandledRequestError);
        return true;
      }
    );
  });

  it('merged builder with fallback terminates the chain', async () => {
    const fragment = createChain<number, string>()
      .useWhen(
        (request) => request === 1,
        (_request, _token) => Promise.resolve('one')
      )
      .withFallback((_request, _token) => Promise.resolve('fragment-fallback'));

    const chain = createChain<number, string>()
      .use(fragment)
      .useWhen(
        (request) => request === 2,
        (_request, _token) => Promise.resolve('two')
      )
      .build();

    assert.equal(await chain.execute(1), 'one');
    assert.equal(await chain.execute(3), 'fragment-fallback');
    assert.equal(chain.count, 2); // fragment counts as 1, second handler counts as 1
  });

  it('merged chain with fallback terminates the chain', async () => {
    const merged = createChain<number, string>()
      .useWhen(
        (request) => request === 1,
        (_request, _token) => Promise.resolve('one')
      )
      .withFallback((_request, _token) => Promise.resolve('merged-fallback'))
      .build();

    const chain = createChain<number, string>()
      .use(merged)
      .useWhen(
        (request) => request === 2,
        (_request, _token) => Promise.resolve('two')
      )
      .build();

    assert.equal(await chain.execute(1), 'one');
    assert.equal(await chain.execute(3), 'merged-fallback');
  });

  it('custom chain implementation terminates the chain', async () => {
    const customChain = {
      count: 1,
      execute: (_request: number, _token?: CancellationToken) =>
        Promise.resolve('custom'),
    };

    const chain = createChain<number, string>()
      .use(customChain)
      .useWhen(
        (request) => request === 2,
        (_request, _token) => Promise.resolve('two')
      )
      .build();

    assert.equal(await chain.execute(1), 'custom');
    assert.equal(await chain.execute(2), 'custom');
  });

  it('handler can be a plain function', async () => {
    const handler: Handler<number, string> = {
      handle: (request, next, cancellationToken) => {
        if (request < 0) {
          return Promise.resolve('negative');
        }
        return next(request, cancellationToken);
      },
    };

    const chain = createChain<number, string>()
      .use(handler)
      .withFallback((_request, _token) => Promise.resolve('fallback'))
      .build();

    assert.equal(await chain.execute(-1), 'negative');
    assert.equal(await chain.execute(1), 'fallback');
  });

  it('cancellation token is propagated', async () => {
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

    const source = new CancellationTokenSource();
    await chain.execute(1, source.token());

    assert.ok(receivedToken !== null);
    assert.equal(receivedToken, source.token());
  });

  it('handler base requires non-null next handler', async () => {
    class TestHandler extends HandlerBase<number, string> {
      protected canHandle(_request: number): boolean {
        return true;
      }

      protected process(
        _request: number,
        _cancellationToken: CancellationToken
      ): Promise<string> {
        return Promise.resolve('handled');
      }
    }

    const handler = new TestHandler();

    assert.throws(
      () => handler.handle(1, null as unknown as NextHandler<number, string>, CancellationToken.none()),
      TypeError
    );
  });

  it('use throws on null handler', () => {
    const builder = createChain<number, string>();
    assert.throws(
      () => builder.use(null as unknown as Handler<number, string>),
      TypeError
    );
  });

  it('useWhen throws on null predicate', () => {
    const builder = createChain<number, string>();
    assert.throws(
      () =>
        builder.useWhen(
          null as unknown as (request: number) => boolean,
          (_request, _token) => Promise.resolve('handled')
        ),
      TypeError
    );
  });

  it('useWhen throws on null handler', () => {
    const builder = createChain<number, string>();
    assert.throws(
      () =>
        builder.useWhen(
          (_request) => true,
          null as unknown as NextHandler<number, string>
        ),
      TypeError
    );
  });

  it('useBranch throws on null predicate', () => {
    const builder = createChain<number, string>();
    assert.throws(
      () =>
        builder.useBranch(
          null as unknown as (request: number) => boolean,
          (_branch) => {}
        ),
      TypeError
    );
  });

  it('useBranch throws on null configure', () => {
    const builder = createChain<number, string>();
    assert.throws(
      () =>
        builder.useBranch(
          (_request) => true,
          null as unknown as (branch: typeof builder) => void
        ),
      TypeError
    );
  });

  it('withFallback throws on null fallback', () => {
    const builder = createChain<number, string>();
    assert.throws(
      () => builder.withFallback(null as unknown as NextHandler<number, string>),
      TypeError
    );
  });

  it('execute throws on null cancellation token', async () => {
    const chain = createChain<number, string>()
      .withFallback((_request, _token) => Promise.resolve('handled'))
      .build();

    await assert.rejects(
      async () => chain.execute(1, null as unknown as CancellationToken),
      TypeError
    );
  });

  it('merging a builder into itself is safe', async () => {
    const builder = createChain<number, string>().useWhen(
      (request) => request === 1,
      (_request, _token) => Promise.resolve('one')
    );

    builder.use(builder);

    const chain = builder.build();
    assert.equal(await chain.execute(1), 'one');
  });
});

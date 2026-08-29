"""Tests for ChainBuilder and Chain functionality."""

import asyncio
from typing import Optional

import pytest

from messageflow import (
    CancellationToken,
    CancellationTokenSource,
    Chain,
    ChainBuilder,
    HandlerBase,
    UnhandledRequestError,
)


class EvenHandler(HandlerBase[int, str]):
    """Test handler that accepts even numbers."""

    def can_handle(self, request: int) -> bool:
        return request % 2 == 0

    async def process(self, request: int, cancellation_token: CancellationToken) -> str:
        return f"even:{request}"


class GreaterThan10Handler(HandlerBase[int, str]):
    """Test handler that accepts numbers greater than 10."""

    def can_handle(self, request: int) -> bool:
        return request > 10

    async def process(self, request: int, cancellation_token: CancellationToken) -> str:
        return f"large:{request}"


def run_async(coro):
    """Helper to run async coroutines in tests."""
    return asyncio.run(coro)


async def simple_handler(value):
    """Create a simple async handler that returns a value."""
    return value


class TestChainBuilder:
    """Tests for ChainBuilder."""

    def test_first_matching_handler_produces_response(self):
        """Test that the first matching handler produces the response."""
        chain = (
            Chain.create()
            .use_when(lambda r: r < 0, lambda r, t: simple_handler(f"negative:{r}"))
            .use_when(lambda r: r == 0, lambda r, t: simple_handler("zero"))
            .with_fallback(lambda r, t: simple_handler(f"positive:{r}"))
            .build()
        )

        assert run_async(chain.execute(-7)) == "negative:-7"
        assert run_async(chain.execute(0)) == "zero"
        assert run_async(chain.execute(3)) == "positive:3"
        assert chain.count == 2

    def test_unhandled_request_raises_when_no_fallback(self):
        """Test that UnhandledRequestError is raised when no fallback is configured."""
        chain = (
            Chain.create()
            .use_when(lambda r: r < 0, lambda r, t: simple_handler("negative"))
            .build()
        )

        with pytest.raises(UnhandledRequestError) as exc_info:
            run_async(chain.execute(1))

        assert "No handler in the chain handled the request" in str(exc_info.value)

    def test_middleware_runs_around_rest_of_chain(self):
        """Test that middleware can run code before and after the rest of the chain."""
        log = []

        async def middleware(request, next_handler, token):
            log.append("before")
            response = await next_handler(request, token)
            log.append("after")
            return response + "!"

        chain = (
            Chain.create()
            .use(middleware)
            .use_when(lambda r: True, lambda r, t: simple_handler("handled"))
            .build()
        )

        result = run_async(chain.execute(1))
        assert result == "handled!"
        assert log == ["before", "after"]

    def test_handler_implementation_is_invoked(self):
        """Test that Handler implementation subclasses work correctly."""
        chain = (
            Chain.create()
            .use(EvenHandler())
            .with_fallback(lambda r, t: simple_handler("odd"))
            .build()
        )

        assert run_async(chain.execute(2)) == "even:2"
        assert run_async(chain.execute(3)) == "odd"

    def test_branch_is_skipped_when_predicate_does_not_match(self):
        """Test that branches are skipped when their predicate doesn't match."""
        chain = (
            Chain.create()
            .use_branch(
                lambda r: r > 10,
                lambda branch: branch.use_when(
                    lambda r: r > 100, lambda r, t: simple_handler("huge")
                ),
            )
            .with_fallback(lambda r, t: simple_handler("fallback"))
            .build()
        )

        assert run_async(chain.execute(1000)) == "huge"
        assert run_async(chain.execute(20)) == "fallback"
        assert run_async(chain.execute(5)) == "fallback"

    def test_branch_falls_through_when_no_handler_matches(self):
        """Test that branches fall through to parent chain when no handler matches."""
        chain = (
            Chain.create()
            .use_branch(
                lambda r: r > 10,
                lambda branch: branch.use_when(
                    lambda r: r > 100, lambda r, t: simple_handler("huge")
                ),
            )
            .use_when(lambda r: r > 5, lambda r, t: simple_handler("medium"))
            .with_fallback(lambda r, t: simple_handler("fallback"))
            .build()
        )

        assert run_async(chain.execute(50)) == "medium"

    def test_branch_with_fallback_terminates(self):
        """Test that a branch with its own fallback terminates without falling through."""
        chain = (
            Chain.create()
            .use_branch(
                lambda r: r > 10,
                lambda branch: branch.use_when(
                    lambda r: r > 100, lambda r, t: simple_handler("huge")
                ).with_fallback(lambda r, t: simple_handler("branch_fallback")),
            )
            .use_when(lambda r: True, lambda r, t: simple_handler("parent"))
            .build()
        )

        assert run_async(chain.execute(50)) == "branch_fallback"
        assert run_async(chain.execute(5)) == "parent"

    def test_merge_builder_composes_against_continuation(self):
        """Test that merging a builder composes its handlers against the continuation."""

        def create_fragment() -> ChainBuilder[int, str]:
            return Chain.create().use(EvenHandler())

        chain = (
            Chain.create()
            .use(create_fragment())
            .use_when(lambda r: r > 10, lambda r, t: simple_handler(f"large:{r}"))
            .with_fallback(lambda r, t: simple_handler("fallback"))
            .build()
        )

        assert run_async(chain.execute(2)) == "even:2"
        assert run_async(chain.execute(15)) == "large:15"
        assert run_async(chain.execute(3)) == "fallback"

    def test_merge_builder_with_fallback_terminates(self):
        """Test that a merged builder with fallback terminates the segment."""

        def create_fragment() -> ChainBuilder[int, str]:
            return (
                Chain.create()
                .use(EvenHandler())
                .with_fallback(lambda r, t: simple_handler("fragment_fallback"))
            )

        chain = (
            Chain.create()
            .use(create_fragment())
            .use_when(lambda r: True, lambda r, t: simple_handler("parent"))
            .build()
        )

        assert run_async(chain.execute(2)) == "even:2"
        assert run_async(chain.execute(3)) == "fragment_fallback"

    def test_merge_builder_snapshot_semantics(self):
        """Test that merging a builder snapshots its handlers at merge time."""
        fragment = Chain.create().use(EvenHandler())

        chain1 = Chain.create().use(fragment).build()

        # Modify fragment after merge
        fragment.use_when(lambda r: r > 100, lambda r, t: simple_handler("huge"))

        chain2 = Chain.create().use(fragment).build()

        # chain1 should only have the original handler
        assert chain1.count == 1
        # chain2 should have both handlers
        assert chain2.count == 1  # Still counts as 1 merged segment

    def test_merge_composed_chain_recomposes(self):
        """Test that merging a ComposedChain re-composes it."""
        built_chain = (
            Chain.create()
            .use(EvenHandler())
            .with_fallback(lambda r, t: simple_handler("built_fallback"))
            .build()
        )

        chain = (
            Chain.create()
            .use(built_chain)
            .use_when(lambda r: r > 10, lambda r, t: simple_handler(f"large:{r}"))
            .build()
        )

        # Built chain is re-composed WITH its fallback, so the fallback terminates that segment
        assert run_async(chain.execute(2)) == "even:2"
        # 15 is odd, not handled by EvenHandler, goes to built_fallback (which terminates)
        assert run_async(chain.execute(15)) == "built_fallback"

    def test_merge_custom_chain_terminates(self):
        """Test that merging a custom Chain implementation terminates."""

        class CustomChain(Chain[int, str]):
            @property
            def count(self) -> int:
                return 1

            async def execute(
                self, request: int, cancellation_token: Optional[CancellationToken] = None
            ) -> str:
                return "custom"

        chain = (
            Chain.create()
            .use(CustomChain())
            .use_when(lambda r: True, lambda r, t: simple_handler("parent"))
            .build()
        )

        # Custom chain terminates, parent handler never reached
        assert run_async(chain.execute(1)) == "custom"

    def test_merged_fragment_counts_as_one_handler(self):
        """Test that a merged fragment counts as one handler regardless of its size."""
        fragment = (
            Chain.create()
            .use(EvenHandler())
            .use(GreaterThan10Handler())
            .use_when(lambda r: r < 0, lambda r, t: simple_handler("negative"))
        )

        chain = Chain.create().use(fragment).use(EvenHandler()).build()

        # Fragment counts as 1, plus the additional handler = 2
        assert chain.count == 2

    def test_cancellation_token_propagation(self):
        """Test that cancellation tokens are propagated through the chain."""
        called = []

        async def handler(request, next_handler, token):
            called.append("handler")
            token.throw_if_cancellation_requested()
            return await next_handler(request, token)

        chain = (
            Chain.create().use(handler).with_fallback(lambda r, t: simple_handler("done")).build()
        )

        source = CancellationTokenSource()
        source.cancel()

        with pytest.raises(asyncio.CancelledError):
            run_async(chain.execute(1, source.token()))

        assert called == ["handler"]

    def test_cancellation_token_default_to_none(self):
        """Test that cancellation token defaults to CancellationToken.none()."""
        chain = Chain.create().with_fallback(lambda r, t: simple_handler("done")).build()

        # Should not raise
        result = run_async(chain.execute(1))
        assert result == "done"

    def test_use_validates_handler_not_none(self):
        """Test that use() validates handler is not None."""
        with pytest.raises(TypeError) as exc_info:
            Chain.create().use(None)

        assert "handler must not be None" in str(exc_info.value)

    def test_use_when_validates_predicate_not_none(self):
        """Test that use_when() validates predicate is not None."""
        with pytest.raises(TypeError) as exc_info:
            Chain.create().use_when(None, lambda r, t: simple_handler("test"))

        assert "predicate must not be None" in str(exc_info.value)

    def test_use_when_validates_handler_not_none(self):
        """Test that use_when() validates handler is not None."""
        with pytest.raises(TypeError) as exc_info:
            Chain.create().use_when(lambda r: True, None)

        assert "handler must not be None" in str(exc_info.value)

    def test_use_branch_validates_predicate_not_none(self):
        """Test that use_branch() validates predicate is not None."""
        with pytest.raises(TypeError) as exc_info:
            Chain.create().use_branch(None, lambda branch: None)

        assert "predicate must not be None" in str(exc_info.value)

    def test_use_branch_validates_configure_not_none(self):
        """Test that use_branch() validates configure is not None."""
        with pytest.raises(TypeError) as exc_info:
            Chain.create().use_branch(lambda r: True, None)

        assert "configure must not be None" in str(exc_info.value)

    def test_with_fallback_validates_not_none(self):
        """Test that with_fallback() validates fallback is not None."""
        with pytest.raises(TypeError) as exc_info:
            Chain.create().with_fallback(None)

        assert "fallback must not be None" in str(exc_info.value)

    def test_execute_validates_cancellation_token_not_none(self):
        """Test that execute() validates cancellation_token is not None if provided."""
        chain = Chain.create().with_fallback(lambda r, t: simple_handler("done")).build()

        # Passing None explicitly should still work (converts to CancellationToken.none())
        result = run_async(chain.execute(1, None))
        assert result == "done"

    def test_handler_base_validates_next_handler_not_none(self):
        """Test that HandlerBase validates next_handler is not None."""

        class TestHandler(HandlerBase[int, str]):
            def can_handle(self, request: int) -> bool:
                return True

            async def process(self, request: int, cancellation_token: CancellationToken) -> str:
                return "handled"

        handler = TestHandler()

        with pytest.raises(TypeError) as exc_info:
            run_async(handler.handle(1, None, CancellationToken.none()))

        assert "next_handler must not be None" in str(exc_info.value)

    def test_empty_chain_raises_unhandled_request_error(self):
        """Test that an empty chain with no fallback raises UnhandledRequestError."""
        chain = Chain.create().build()

        with pytest.raises(UnhandledRequestError):
            run_async(chain.execute(1))

    def test_chain_count_excludes_fallback(self):
        """Test that chain count excludes the fallback handler."""
        chain = (
            Chain.create()
            .use(EvenHandler())
            .use(GreaterThan10Handler())
            .with_fallback(lambda r, t: simple_handler("fallback"))
            .build()
        )

        assert chain.count == 2

    def test_chain_count_for_empty_chain(self):
        """Test that chain count is 0 for an empty chain."""
        chain = Chain.create().build()
        assert chain.count == 0

    def test_exception_propagates_through_chain(self):
        """Test that exceptions from handlers propagate correctly."""

        async def failing_handler(request, next_handler, token):
            raise ValueError("Handler failed")

        chain = Chain.create().use(failing_handler).build()

        with pytest.raises(ValueError) as exc_info:
            run_async(chain.execute(1))

        assert "Handler failed" in str(exc_info.value)

    def test_multiple_handlers_in_sequence(self):
        """Test multiple handlers in sequence with different conditions."""
        chain = (
            Chain.create()
            .use_when(lambda r: r < 0, lambda r, t: simple_handler("negative"))
            .use_when(lambda r: r == 0, lambda r, t: simple_handler("zero"))
            .use_when(lambda r: r < 10, lambda r, t: simple_handler("small"))
            .use_when(lambda r: r < 100, lambda r, t: simple_handler("medium"))
            .with_fallback(lambda r, t: simple_handler("large"))
            .build()
        )

        assert run_async(chain.execute(-5)) == "negative"
        assert run_async(chain.execute(0)) == "zero"
        assert run_async(chain.execute(5)) == "small"
        assert run_async(chain.execute(50)) == "medium"
        assert run_async(chain.execute(500)) == "large"

    def test_async_handler_with_await(self):
        """Test that async handlers with actual await work correctly."""

        async def async_handler(request, next_handler, token):
            await asyncio.sleep(0)  # Simulate async work
            return "async_result"

        chain = Chain.create().use(async_handler).build()

        result = run_async(chain.execute(1))
        assert result == "async_result"

    def test_create_chain_factory_function(self):
        """Test that create_chain() factory function works."""
        from messageflow import create_chain

        chain = (
            create_chain().use_when(lambda r: True, lambda r, t: simple_handler("handled")).build()
        )

        result = run_async(chain.execute(1))
        assert result == "handled"

    def test_merge_composed_chain_with_fallback_terminates(self):
        """Test that merging a ComposedChain with fallback terminates the segment."""
        built_chain = (
            Chain.create()
            .use(EvenHandler())
            .with_fallback(lambda r, t: simple_handler("built_fallback"))
            .build()
        )

        chain = (
            Chain.create()
            .use(built_chain)
            .use_when(lambda r: r > 10, lambda r, t: simple_handler(f"large:{r}"))
            .build()
        )

        # Built chain is re-composed WITH its fallback, which terminates
        assert run_async(chain.execute(2)) == "even:2"
        assert run_async(chain.execute(3)) == "built_fallback"  # Falls to merged chain fallback
        # Request  15 is odd so not handled by EvenHandler, goes to built_fallback

    def test_use_validates_invalid_callable(self):
        """Test that use() validates callable handlers correctly."""

        # This should work - it's a valid async function
        async def valid_handler(request, next_handler, token):
            return "handled"

        chain = Chain.create().use(valid_handler).build()
        assert chain.count == 1

    def test_tracing_handles_synchronous_exception(self):
        """Test that tracing middleware handles exceptions raised before awaiting."""
        from messageflow import ChainSpan, ChainTracer

        class MockSpan(ChainSpan):
            def __init__(self):
                self.status = None
                self.error = None
                self.closed = False

            def set_ok(self) -> None:
                self.status = "ok"

            def set_error(self, error: BaseException) -> None:
                self.status = "error"
                self.error = error

            def close(self) -> None:
                self.closed = True

        class MockTracer(ChainTracer):
            def __init__(self):
                self.span = MockSpan()

            def start_span(self, span_name, request_type=None, response_type=None):
                return self.span

        tracer = MockTracer()

        async def sync_failing_handler(request, next_handler, token):
            # Raise immediately before any await
            raise RuntimeError("Immediate failure")

        chain = Chain.create().use_tracing(tracer, "test").use(sync_failing_handler).build()

        with pytest.raises(RuntimeError):
            run_async(chain.execute(1))

        assert tracer.span.status == "error"
        assert tracer.span.closed

    def test_logging_handles_synchronous_exception(self):
        """Test that logging middleware handles exceptions raised before awaiting."""
        from messageflow import ChainLogger, ChainLogLevel

        class MockLogger(ChainLogger):
            def __init__(self):
                self.entries = []

            def is_enabled(self, level: ChainLogLevel) -> bool:
                return True

            def log(self, level: ChainLogLevel, message: str, error=None) -> None:
                self.entries.append({"level": level, "message": message, "error": error})

        logger = MockLogger()

        async def sync_failing_handler(request, next_handler, token):
            # Raise immediately before any await
            raise RuntimeError("Immediate failure")

        chain = (
            Chain.create()
            .use_logging(logger, ChainLogLevel.DEBUG, "test")
            .use(sync_failing_handler)
            .build()
        )

        with pytest.raises(RuntimeError):
            run_async(chain.execute(1))

        assert len(logger.entries) == 2
        assert logger.entries[1]["level"] == ChainLogLevel.ERROR

    def test_use_with_non_async_callable(self):
        """Test that use() with a synchronous callable raises."""

        def non_async_handler(request, next_handler, token):
            return "handled"

        with pytest.raises(TypeError) as exc_info:
            Chain.create().use(non_async_handler)

        assert "handler must be an async callable" in str(exc_info.value)

    def test_use_with_invalid_type(self):
        """Test that use() with an invalid type raises."""
        with pytest.raises(TypeError) as exc_info:
            Chain.create().use(42)  # Not a valid handler type

        assert "handler must be a Handler, callable, ChainBuilder, or Chain" in str(exc_info.value)

    def test_logging_failure_when_logger_disabled(self):
        """Test that logging failure path is executed even when ERROR level is disabled."""
        from messageflow import ChainLogger, ChainLogLevel

        class DisabledLogger(ChainLogger):
            def __init__(self):
                self.entries = []

            def is_enabled(self, level: ChainLogLevel) -> bool:
                # Disable ERROR level
                return level != ChainLogLevel.ERROR

            def log(self, level: ChainLogLevel, message: str, error=None) -> None:
                self.entries.append({"level": level, "message": message, "error": error})

        logger = DisabledLogger()

        async def failing_handler(request, next_handler, token):
            raise ValueError("Test error")

        chain = (
            Chain.create()
            .use_logging(logger, ChainLogLevel.DEBUG, "test")
            .use(failing_handler)
            .build()
        )

        with pytest.raises(ValueError):
            run_async(chain.execute(1))

        # Only the start log should be present (ERROR is disabled)
        assert len(logger.entries) == 1
        assert logger.entries[0]["level"] == ChainLogLevel.DEBUG

    def test_use_with_non_async_but_correct_signature(self):
        """Test that use() works with non-async callable that has correct signature."""

        # This is a callable object with __call__ that has the right signature
        class CallableHandler:
            async def __call__(self, request, next_handler, token):
                return "callable_handled"

        handler = CallableHandler()
        chain = Chain.create().use(handler).build()

        result = run_async(chain.execute(1))
        assert result == "callable_handled"

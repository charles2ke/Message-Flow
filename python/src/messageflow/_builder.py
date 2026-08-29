"""ChainBuilder implementation for MessageFlow."""

import inspect
import time
from collections.abc import Awaitable
from typing import Callable, Generic, Optional, TypeVar, Union

from ._cancellation import CancellationToken
from ._chain import Chain
from ._diagnostics import ChainLogger, ChainLogLevel, ChainTracer
from ._errors import UnhandledRequestError
from ._handlers import Handler, NextHandler

T = TypeVar("T")
R = TypeVar("R")

# Composer: function that takes a terminal NextHandler and returns a composed NextHandler
Composer = Callable[[NextHandler[T, R]], NextHandler[T, R]]


class ComposedChain(Chain[T, R]):
    """
    Default Chain implementation. The handler pipeline is composed once, at build time, so
    execution is a simple delegate call.

    Args:
        T: The type of the request flowing through the chain.
        R: The type of the response produced by the chain.
    """

    def __init__(self, composer: Composer[T, R], handler_count: int) -> None:
        """
        Create a composed chain.

        Args:
            composer: The open composition turning the terminal step into the executable pipeline.
            handler_count: The number of handlers in the chain.
        """
        self._composer = composer
        self._count = handler_count
        self._pipeline = composer(self._unhandled)

    def composer(self) -> Composer[T, R]:
        """
        Get the open composition of the chain.

        It turns the step invoked when no handler of this chain accepted the request into the
        executable pipeline. It allows the chain to be merged into another chain without re-running
        its builder.

        Returns:
            The open composition of the chain.
        """
        return self._composer

    @property
    def count(self) -> int:
        """
        Get the number of handlers in the chain, excluding the terminal fallback.

        Returns:
            The number of handlers in the chain.
        """
        return self._count

    async def execute(
        self,
        request: T,
        cancellation_token: Optional[CancellationToken] = None,
    ) -> R:
        """
        Send a request through the chain.

        Args:
            request: The request to process.
            cancellation_token: A token used to cancel the operation.

        Returns:
            The response produced by the first handler that accepted the request.

        Raises:
            UnhandledRequestError: No handler accepted the request and no fallback was configured.
        """
        if cancellation_token is None:
            cancellation_token = CancellationToken.none()
        return await self._pipeline(request, cancellation_token)

    @staticmethod
    async def _unhandled(request: T, cancellation_token: CancellationToken) -> R:
        """Terminal handler that raises UnhandledRequestError."""
        raise UnhandledRequestError()


class ChainBuilder(Generic[T, R]):
    """
    Builds an immutable Chain from an ordered set of handlers.

    Args:
        T: The type of the request flowing through the chain.
        R: The type of the response produced by the chain.
    """

    def __init__(self) -> None:
        """Create a new, empty chain builder."""
        self._steps: list[Composer[T, R]] = []
        self._fallback: Optional[NextHandler[T, R]] = None

    def use(
        self,
        handler: Union[
            Handler[T, R],
            Callable[[T, NextHandler[T, R], CancellationToken], Awaitable[R]],
            "ChainBuilder[T, R]",
            Chain[T, R],
        ],
    ) -> "ChainBuilder[T, R]":
        """
        Append a handler to the end of the chain.

        Accepts Handler instances, callable handlers (async functions),
        other ChainBuilder instances, or already built Chain instances.

        Args:
            handler: The handler, builder, or chain to append.

        Returns:
            The same builder instance, for chaining.

        Raises:
            TypeError: handler is None or invalid.
        """
        if handler is None:
            raise TypeError("handler must not be None")

        # Check if it's another ChainBuilder
        if isinstance(handler, ChainBuilder):
            self._steps.append(handler._create_composer())
            return self

        # Check if it's a Chain
        if isinstance(handler, Chain):
            if isinstance(handler, ComposedChain):
                self._steps.append(handler.composer())
            else:
                # Custom Chain implementation - execute as-is and terminate
                def custom_chain_composer(ignored_next: NextHandler[T, R]) -> NextHandler[T, R]:
                    async def wrapper(request: T, token: CancellationToken) -> R:
                        return await handler.execute(request, token)

                    return wrapper

                self._steps.append(custom_chain_composer)
            return self

        # Check if it's a Handler instance
        if isinstance(handler, Handler):

            def handler_composer(next_handler: NextHandler[T, R]) -> NextHandler[T, R]:
                async def wrapper(request: T, token: CancellationToken) -> R:
                    return await handler.handle(request, next_handler, token)

                return wrapper

            self._steps.append(handler_composer)
            return self

        # Check if it's a callable
        if callable(handler):
            # Verify it's async and has the right signature
            if not (inspect.iscoroutinefunction(handler) or inspect.isasyncgenfunction(handler)):
                # Try to call it to see if it's an async callable
                sig = inspect.signature(handler)
                if len(sig.parameters) != 3:
                    raise TypeError(
                        "handler must be a callable with 3 parameters "
                        "(request, next_handler, cancellation_token)"
                    )

            def callable_composer(next_handler: NextHandler[T, R]) -> NextHandler[T, R]:
                async def wrapper(request: T, token: CancellationToken) -> R:
                    return await handler(request, next_handler, token)

                return wrapper

            self._steps.append(callable_composer)
            return self

        raise TypeError("handler must be a Handler, callable, ChainBuilder, or Chain")

    def use_when(
        self,
        predicate: Callable[[T], bool],
        handler: NextHandler[T, R],
    ) -> "ChainBuilder[T, R]":
        """
        Append a handler that only runs when predicate matches the request.

        Otherwise the request flows to the next handler.

        Args:
            predicate: Decides whether the handler is responsible for the request.
            handler: Produces the response for accepted requests.

        Returns:
            The same builder instance, for chaining.

        Raises:
            TypeError: predicate or handler is None.
        """
        if predicate is None:
            raise TypeError("predicate must not be None")
        if handler is None:
            raise TypeError("handler must not be None")

        async def conditional_handler(
            request: T,
            next_handler: NextHandler[T, R],
            cancellation_token: CancellationToken,
        ) -> R:
            if predicate(request):
                return await handler(request, cancellation_token)
            else:
                return await next_handler(request, cancellation_token)

        return self.use(conditional_handler)

    def use_branch(
        self,
        predicate: Callable[[T], bool],
        configure: Callable[["ChainBuilder[T, R]"], None],
    ) -> "ChainBuilder[T, R]":
        """
        Append a nested sub-chain that only runs when predicate matches the request.

        When the predicate does not match, the request skips the branch entirely and flows to
        the next handler of the parent chain. When it does match but no handler of the branch
        accepts the request, the request falls through to the next handler of the parent chain
        as well — unless the branch configures its own fallback, which then becomes the
        terminal step of the branch.

        Args:
            predicate: Decides whether the request enters the branch.
            configure: Adds the handlers of the branch to the supplied builder.

        Returns:
            The same builder instance, for chaining.

        Raises:
            TypeError: predicate or configure is None.
        """
        if predicate is None:
            raise TypeError("predicate must not be None")
        if configure is None:
            raise TypeError("configure must not be None")

        branch = ChainBuilder[T, R]()
        configure(branch)
        branch_composer = branch._create_composer()

        def branch_step_composer(next_handler: NextHandler[T, R]) -> NextHandler[T, R]:
            branch_pipeline = branch_composer(next_handler)

            async def wrapper(request: T, token: CancellationToken) -> R:
                if predicate(request):
                    return await branch_pipeline(request, token)
                else:
                    return await next_handler(request, token)

            return wrapper

        self._steps.append(branch_step_composer)
        return self

    def use_logging(
        self,
        logger: ChainLogger,
        level: ChainLogLevel = ChainLogLevel.DEBUG,
        chain_name: str = "MessageFlow",
    ) -> "ChainBuilder[T, R]":
        """
        Append a middleware that logs the start, completion and failure of every request.

        Only the chain name and durations are logged; the request itself is never written to
        the log, so no payload can leak into log storage. Failures are logged at
        ChainLogLevel.ERROR and the exception is propagated unchanged.

        Args:
            logger: The logger receiving the entries.
            level: The level used for the start and completion entries.
            chain_name: The name identifying the chain in the log entries.

        Returns:
            The same builder instance, for chaining.

        Raises:
            TypeError: Any argument is None.
        """
        if logger is None:
            raise TypeError("logger must not be None")
        if level is None:
            raise TypeError("level must not be None")
        if chain_name is None:
            raise TypeError("chain_name must not be None")

        async def logging_handler(
            request: T,
            next_handler: NextHandler[T, R],
            cancellation_token: CancellationToken,
        ) -> R:
            if logger.is_enabled(level):
                logger.log(level, f"Executing chain {chain_name}.", None)

            timestamp = time.perf_counter()

            try:
                response = await next_handler(request, cancellation_token)
            except BaseException as exception:
                self._log_failure(logger, chain_name, timestamp, exception)
                raise

            if logger.is_enabled(level):
                elapsed_ms = (time.perf_counter() - timestamp) * 1000
                logger.log(level, f"Executed chain {chain_name} in {elapsed_ms:.3f} ms.", None)

            return response

        return self.use(logging_handler)

    def use_tracing(
        self,
        tracer: ChainTracer,
        span_name: str = "MessageFlow.Execute",
        request_type: Optional[str] = None,
        response_type: Optional[str] = None,
    ) -> "ChainBuilder[T, R]":
        """
        Append a middleware that wraps the remainder of the chain in a ChainSpan.

        Failures mark the span as failed and propagate the exception unchanged. The span is always
        ended, whether the request succeeded or not.

        Args:
            tracer: The tracer creating the spans.
            span_name: The name of the created span.
            request_type: The request type reported on the span, may be None.
            response_type: The response type reported on the span, may be None.

        Returns:
            The same builder instance, for chaining.

        Raises:
            TypeError: tracer or span_name is None.
            ValueError: span_name is empty.
        """
        if tracer is None:
            raise TypeError("tracer must not be None")
        if span_name is None:
            raise TypeError("span_name must not be None")
        if not span_name:
            raise ValueError("span_name must not be empty.")

        async def tracing_handler(
            request: T,
            next_handler: NextHandler[T, R],
            cancellation_token: CancellationToken,
        ) -> R:
            span = tracer.start_span(span_name, request_type, response_type)
            if span is None:
                raise TypeError("ChainTracer.start_span must not return None")

            try:
                response = await next_handler(request, cancellation_token)
                span.set_ok()
                return response
            except BaseException as exception:
                span.set_error(exception)
                raise
            finally:
                span.close()

        return self.use(tracing_handler)

    def with_fallback(self, fallback: NextHandler[T, R]) -> "ChainBuilder[T, R]":
        """
        Set the terminal step invoked when no handler accepted the request.

        Without a fallback the chain raises UnhandledRequestError instead.

        Args:
            fallback: The terminal handler.

        Returns:
            The same builder instance, for chaining.

        Raises:
            TypeError: fallback is None.
        """
        if fallback is None:
            raise TypeError("fallback must not be None")

        self._fallback = fallback
        return self

    def build(self) -> Chain[T, R]:
        """
        Compose the configured handlers into an immutable chain.

        Returns:
            The composed chain.
        """
        return ComposedChain(self._create_composer(), len(self._steps))

    def _create_composer(self) -> Composer[T, R]:
        """
        Snapshot the configured handlers into an open composition.

        Returns:
            The open composition of the configured handlers.
        """
        # Snapshot the current state
        snapshot = list(self._steps)
        snapshot_fallback = self._fallback

        def composer(terminal: NextHandler[T, R]) -> NextHandler[T, R]:
            pipeline = snapshot_fallback if snapshot_fallback is not None else terminal

            # Apply steps in reverse order
            for step in reversed(snapshot):
                pipeline = step(pipeline)

            return pipeline

        return composer

    @staticmethod
    def _log_failure(
        logger: ChainLogger,
        chain_name: str,
        timestamp: float,
        error: BaseException,
    ) -> None:
        """Log a chain failure."""
        if logger.is_enabled(ChainLogLevel.ERROR):
            elapsed_ms = (time.perf_counter() - timestamp) * 1000
            logger.log(
                ChainLogLevel.ERROR,
                f"Chain {chain_name} failed after {elapsed_ms:.3f} ms.",
                error,
            )


# Export a convenience factory function
def create_chain() -> ChainBuilder[T, R]:
    """
    Create a builder for a chain that turns a request into a response.

    Returns:
        A new, empty builder.
    """
    return ChainBuilder()


__all__ = ["ChainBuilder", "ComposedChain", "create_chain"]

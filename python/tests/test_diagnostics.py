"""Tests for diagnostic functionality."""

import asyncio
from typing import Optional

import pytest

from messageflow import (
    Chain,
    ChainDiagnostics,
    ChainLogger,
    ChainLogLevel,
    ChainSpan,
    ChainTracer,
)


async def simple_handler(value):
    """Create a simple async handler that returns a value."""
    return value


class MockLogger(ChainLogger):
    """Test logger implementation that records all log calls."""

    def __init__(self):
        self.entries = []
        self.enabled_levels = {ChainLogLevel.DEBUG, ChainLogLevel.INFORMATION, ChainLogLevel.ERROR}

    def is_enabled(self, level: ChainLogLevel) -> bool:
        return level in self.enabled_levels

    def log(
        self, level: ChainLogLevel, message: str, error: Optional[BaseException] = None
    ) -> None:
        self.entries.append({"level": level, "message": message, "error": error})


class MockSpan(ChainSpan):
    """Test span implementation that records all calls."""

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
    """Test tracer implementation that creates test spans."""

    def __init__(self):
        self.spans = []
        self.last_span = None

    def start_span(
        self,
        span_name: str,
        request_type: Optional[str] = None,
        response_type: Optional[str] = None,
    ) -> ChainSpan:
        span = MockSpan()
        self.spans.append(
            {
                "span_name": span_name,
                "request_type": request_type,
                "response_type": response_type,
                "span": span,
            }
        )
        self.last_span = span
        return span


def run_async(coro):
    """Helper to run async coroutines in tests."""
    return asyncio.run(coro)


class TestLogging:
    """Tests for logging middleware."""

    def test_logging_records_start_and_completion(self):
        """Test that logging middleware records start and completion."""
        logger = MockLogger()

        chain = (
            Chain.create()
            .use_logging(logger, ChainLogLevel.INFORMATION, "test_chain")
            .with_fallback(lambda r, t: simple_handler("result"))
            .build()
        )

        result = run_async(chain.execute(1))
        assert result == "result"

        assert len(logger.entries) == 2
        assert logger.entries[0]["level"] == ChainLogLevel.INFORMATION
        assert "Executing chain test_chain" in logger.entries[0]["message"]
        assert logger.entries[0]["error"] is None

        assert logger.entries[1]["level"] == ChainLogLevel.INFORMATION
        assert "Executed chain test_chain in" in logger.entries[1]["message"]
        assert "ms" in logger.entries[1]["message"]
        assert logger.entries[1]["error"] is None

    def test_logging_records_failure(self):
        """Test that logging middleware records failures."""
        logger = MockLogger()

        async def failing_handler(request, next_handler, token):
            raise ValueError("Test error")

        chain = (
            Chain.create()
            .use_logging(logger, ChainLogLevel.INFORMATION, "test_chain")
            .use(failing_handler)
            .build()
        )

        with pytest.raises(ValueError):
            run_async(chain.execute(1))

        assert len(logger.entries) == 2
        assert logger.entries[0]["level"] == ChainLogLevel.INFORMATION
        assert "Executing chain test_chain" in logger.entries[0]["message"]

        assert logger.entries[1]["level"] == ChainLogLevel.ERROR
        assert "Chain test_chain failed after" in logger.entries[1]["message"]
        assert "ms" in logger.entries[1]["message"]
        assert isinstance(logger.entries[1]["error"], ValueError)
        assert str(logger.entries[1]["error"]) == "Test error"

    def test_logging_respects_is_enabled(self):
        """Test that logging respects the is_enabled check."""
        logger = MockLogger()
        logger.enabled_levels = set()  # Disable all logging

        chain = (
            Chain.create()
            .use_logging(logger, ChainLogLevel.INFORMATION, "test_chain")
            .with_fallback(lambda r, t: simple_handler("result"))
            .build()
        )

        result = run_async(chain.execute(1))
        assert result == "result"

        # No entries should be logged
        assert len(logger.entries) == 0

    def test_logging_default_parameters(self):
        """Test that use_logging works with default parameters."""
        logger = MockLogger()

        chain = (
            Chain.create()
            .use_logging(logger)
            .with_fallback(lambda r, t: simple_handler("result"))
            .build()
        )

        result = run_async(chain.execute(1))
        assert result == "result"

        assert len(logger.entries) == 2
        assert logger.entries[0]["level"] == ChainLogLevel.DEBUG
        assert "MessageFlow" in logger.entries[0]["message"]

    def test_logging_validates_logger_not_none(self):
        """Test that use_logging validates logger is not None."""
        with pytest.raises(TypeError) as exc_info:
            Chain.create().use_logging(None)

        assert "logger must not be None" in str(exc_info.value)

    def test_logging_validates_level_not_none(self):
        """Test that use_logging validates level is not None."""
        logger = MockLogger()

        with pytest.raises(TypeError) as exc_info:
            Chain.create().use_logging(logger, None, "test")

        assert "level must not be None" in str(exc_info.value)

    def test_logging_validates_chain_name_not_none(self):
        """Test that use_logging validates chain_name is not None."""
        logger = MockLogger()

        with pytest.raises(TypeError) as exc_info:
            Chain.create().use_logging(logger, ChainLogLevel.DEBUG, None)

        assert "chain_name must not be None" in str(exc_info.value)


class TestTracing:
    """Tests for tracing middleware."""

    def test_tracing_creates_span_and_marks_ok(self):
        """Test that tracing middleware creates a span and marks it ok."""
        tracer = MockTracer()

        chain = (
            Chain.create()
            .use_tracing(tracer, "test_span", "int", "str")
            .with_fallback(lambda r, t: simple_handler("result"))
            .build()
        )

        result = run_async(chain.execute(1))
        assert result == "result"

        assert len(tracer.spans) == 1
        span_info = tracer.spans[0]
        assert span_info["span_name"] == "test_span"
        assert span_info["request_type"] == "int"
        assert span_info["response_type"] == "str"

        span = span_info["span"]
        assert span.status == "ok"
        assert span.error is None
        assert span.closed

    def test_tracing_marks_span_as_error_on_failure(self):
        """Test that tracing middleware marks span as error on failure."""
        tracer = MockTracer()

        async def failing_handler(request, next_handler, token):
            raise ValueError("Test error")

        chain = Chain.create().use_tracing(tracer, "test_span").use(failing_handler).build()

        with pytest.raises(ValueError):
            run_async(chain.execute(1))

        assert len(tracer.spans) == 1
        span = tracer.spans[0]["span"]
        assert span.status == "error"
        assert isinstance(span.error, ValueError)
        assert str(span.error) == "Test error"
        assert span.closed

    def test_tracing_closes_span_always(self):
        """Test that tracing middleware always closes the span."""
        tracer = MockTracer()

        async def failing_handler(request, next_handler, token):
            raise ValueError("Test error")

        chain = Chain.create().use_tracing(tracer, "test_span").use(failing_handler).build()

        with pytest.raises(ValueError):
            run_async(chain.execute(1))

        span = tracer.spans[0]["span"]
        assert span.closed

    def test_tracing_validates_tracer_not_none(self):
        """Test that use_tracing validates tracer is not None."""
        with pytest.raises(TypeError) as exc_info:
            Chain.create().use_tracing(None)

        assert "tracer must not be None" in str(exc_info.value)

    def test_tracing_validates_span_name_not_none(self):
        """Test that use_tracing validates span_name is not None."""
        tracer = MockTracer()

        with pytest.raises(TypeError) as exc_info:
            Chain.create().use_tracing(tracer, None)

        assert "span_name must not be None" in str(exc_info.value)

    def test_tracing_validates_span_name_not_empty(self):
        """Test that use_tracing validates span_name is not empty."""
        tracer = MockTracer()

        with pytest.raises(ValueError) as exc_info:
            Chain.create().use_tracing(tracer, "")

        assert "span_name must not be empty" in str(exc_info.value)

    def test_tracing_validates_span_not_none(self):
        """Test that tracer.start_span must not return None."""

        class BadTracer(ChainTracer):
            def start_span(self, span_name: str, request_type=None, response_type=None):
                return None

        tracer = BadTracer()

        chain = (
            Chain.create()
            .use_tracing(tracer, "test")
            .with_fallback(lambda r, t: simple_handler("result"))
            .build()
        )

        with pytest.raises(TypeError) as exc_info:
            run_async(chain.execute(1))

        assert "ChainTracer.start_span must not return None" in str(exc_info.value)

    def test_tracing_with_none_request_and_response_types(self):
        """Test that tracing works with None request and response types."""
        tracer = MockTracer()

        chain = (
            Chain.create()
            .use_tracing(tracer, "test_span", None, None)
            .with_fallback(lambda r, t: simple_handler("result"))
            .build()
        )

        result = run_async(chain.execute(1))
        assert result == "result"

        assert len(tracer.spans) == 1
        span_info = tracer.spans[0]
        assert span_info["request_type"] is None
        assert span_info["response_type"] is None


class TestChainDiagnostics:
    """Tests for ChainDiagnostics constants."""

    def test_diagnostics_constants(self):
        """Test that ChainDiagnostics has the expected constants."""
        assert ChainDiagnostics.TRACER_NAME == "MessageFlow"
        assert ChainDiagnostics.TRACER_VERSION == "1.0.0"
        assert ChainDiagnostics.EXECUTE_SPAN_NAME == "MessageFlow.Execute"
        assert ChainDiagnostics.REQUEST_TYPE_ATTRIBUTE == "messageflow.request_type"
        assert ChainDiagnostics.RESPONSE_TYPE_ATTRIBUTE == "messageflow.response_type"


class TestChainLogLevel:
    """Tests for ChainLogLevel enum."""

    def test_log_levels_exist(self):
        """Test that all log levels are defined."""
        assert ChainLogLevel.TRACE
        assert ChainLogLevel.DEBUG
        assert ChainLogLevel.INFORMATION
        assert ChainLogLevel.WARNING
        assert ChainLogLevel.ERROR

    def test_log_levels_are_distinct(self):
        """Test that all log levels are distinct."""
        levels = [
            ChainLogLevel.TRACE,
            ChainLogLevel.DEBUG,
            ChainLogLevel.INFORMATION,
            ChainLogLevel.WARNING,
            ChainLogLevel.ERROR,
        ]
        assert len(levels) == len(set(levels))

"""Diagnostic primitives for MessageFlow."""

from abc import ABC, abstractmethod
from enum import Enum
from typing import Optional


class ChainLogLevel(Enum):
    """The severity of an entry written by a chain to a ChainLogger."""

    TRACE = "TRACE"
    DEBUG = "DEBUG"
    INFORMATION = "INFORMATION"
    WARNING = "WARNING"
    ERROR = "ERROR"


class ChainLogger(ABC):
    """
    Receives the log entries a chain writes while executing a request.

    The interface is intentionally minimal so the library stays dependency-free: an adapter over
    the standard logging module or any other logging framework is a few lines of code.
    """

    @abstractmethod
    def is_enabled(self, level: ChainLogLevel) -> bool:
        """
        Determine whether entries of the given level are recorded.

        Args:
            level: The level to check.

        Returns:
            True when entries of level are recorded.
        """

    @abstractmethod
    def log(
        self, level: ChainLogLevel, message: str, error: Optional[BaseException] = None
    ) -> None:
        """
        Record a log entry.

        Args:
            level: The severity of the entry.
            message: The message describing what the chain did.
            error: The exception that failed the request, or None.
        """


class ChainSpan(ABC):
    """A unit of tracing work covering the execution of the remainder of a chain."""

    @abstractmethod
    def set_ok(self) -> None:
        """Mark the span as successfully completed."""

    @abstractmethod
    def set_error(self, error: BaseException) -> None:
        """
        Mark the span as failed.

        Args:
            error: The exception that failed the request.
        """

    @abstractmethod
    def close(self) -> None:
        """End the span."""


class ChainTracer(ABC):
    """
    Creates the spans emitted by the tracing middleware.

    The interface is intentionally minimal so the library stays dependency-free: an adapter over
    OpenTelemetry, or over any other tracing framework, is a few lines of code.
    """

    @abstractmethod
    def start_span(
        self,
        span_name: str,
        request_type: Optional[str] = None,
        response_type: Optional[str] = None,
    ) -> ChainSpan:
        """
        Start a span covering the execution of the remainder of the chain.

        Args:
            span_name: The name of the span.
            request_type: The type of the request flowing through the chain.
            response_type: The type of the response produced by the chain.

        Returns:
            The started span, never None.
        """


class ChainDiagnostics:
    """
    The diagnostic primitives the library exposes to tracing infrastructure
    such as OpenTelemetry.
    """

    TRACER_NAME = "MessageFlow"
    """The name identifying the library as the source of the emitted spans."""

    TRACER_VERSION = "1.0.0"
    """The version reported alongside TRACER_NAME."""

    EXECUTE_SPAN_NAME = "MessageFlow.Execute"
    """The default name of the span created by use_tracing."""

    REQUEST_TYPE_ATTRIBUTE = "messageflow.request_type"
    """The attribute carrying the request type of the chain."""

    RESPONSE_TYPE_ATTRIBUTE = "messageflow.response_type"
    """The attribute carrying the response type of the chain."""

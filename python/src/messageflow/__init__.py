"""
MessageFlow for Python.

A small, dependency-free Chain of Responsibility library.

MessageFlow lets you compose an ordered set of handlers into an immutable chain. A request travels
through the chain until a handler accepts it; unhandled requests either hit a configured fallback or
raise UnhandledRequestError. The pipeline is composed once at build time, so executing a request is
just a function call.
"""

from ._builder import ChainBuilder, ComposedChain, create_chain
from ._cancellation import CancellationToken, CancellationTokenSource
from ._chain import Chain
from ._diagnostics import (
    ChainDiagnostics,
    ChainLogger,
    ChainLogLevel,
    ChainSpan,
    ChainTracer,
)
from ._errors import UnhandledRequestError
from ._handlers import Handler, HandlerBase, NextHandler

__version__ = "1.0.0"

__all__ = [
    # Core chain types
    "Chain",
    "ChainBuilder",
    "ComposedChain",
    "create_chain",
    # Handler types
    "Handler",
    "HandlerBase",
    "NextHandler",
    # Cancellation
    "CancellationToken",
    "CancellationTokenSource",
    # Diagnostics
    "ChainLogger",
    "ChainLogLevel",
    "ChainTracer",
    "ChainSpan",
    "ChainDiagnostics",
    # Errors
    "UnhandledRequestError",
]

"""Chain abstraction for MessageFlow."""

from abc import ABC, abstractmethod
from typing import Generic, Optional, TypeVar

from ._cancellation import CancellationToken

T = TypeVar("T")
R = TypeVar("R")


class Chain(ABC, Generic[T, R]):
    """
    An immutable, pre-compiled chain of responsibility.

    Args:
        T: The type of the request flowing through the chain.
        R: The type of the response produced by the chain.
    """

    @property
    @abstractmethod
    def count(self) -> int:
        """
        Get the number of handlers in the chain, excluding the terminal fallback.

        Returns:
            The number of handlers in the chain.
        """

    @abstractmethod
    async def execute(
        self,
        request: T,
        cancellation_token: Optional[CancellationToken] = None,
    ) -> R:
        """
        Send a request through the chain.

        When no handler accepts the request and no fallback was configured,
        raises UnhandledRequestError.

        Args:
            request: The request to process.
            cancellation_token: A token used to cancel the operation,
                defaults to CancellationToken.none().

        Returns:
            The response produced by the first handler that accepted the request.

        Raises:
            UnhandledRequestError: No handler accepted the request and no fallback was configured.
        """

    @staticmethod
    def create() -> "ChainBuilder[T, R]":  # noqa: F821
        """
        Create a builder for a chain that turns a request into a response.

        Returns:
            A new, empty builder.
        """
        # Import here to avoid circular dependency
        from ._builder import ChainBuilder

        return ChainBuilder()


__all__ = ["Chain"]

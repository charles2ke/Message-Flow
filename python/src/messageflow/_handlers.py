"""Handler types for MessageFlow."""

from abc import ABC, abstractmethod
from collections.abc import Awaitable
from typing import Callable, Generic, TypeVar

from ._cancellation import CancellationToken

T = TypeVar("T")
R = TypeVar("R")

# NextHandler is a callable that takes a request and cancellation token
# and returns an awaitable response
NextHandler = Callable[[T, CancellationToken], Awaitable[R]]


class Handler(ABC, Generic[T, R]):
    """
    A single link of a chain of responsibility.

    Args:
        T: The type of the request flowing through the chain.
        R: The type of the response produced by the chain.
    """

    @abstractmethod
    async def handle(
        self,
        request: T,
        next_handler: NextHandler[T, R],
        cancellation_token: CancellationToken,
    ) -> R:
        """
        Process the request, optionally delegating to the next handler of the chain.

        Args:
            request: The request to process.
            next_handler: The next handler of the chain.
            cancellation_token: A token used to cancel the operation.

        Returns:
            The response for the request.
        """


class HandlerBase(Handler[T, R], ABC):
    """
    Convenience base class for handlers that either fully handle a request or pass it on.

    Args:
        T: The type of the request flowing through the chain.
        R: The type of the response produced by the chain.
    """

    @abstractmethod
    def can_handle(self, request: T) -> bool:
        """
        Determine whether this handler is responsible for the given request.

        Args:
            request: The request to inspect.

        Returns:
            True when this handler should process the request.
        """

    @abstractmethod
    async def process(self, request: T, cancellation_token: CancellationToken) -> R:
        """
        Process a request this handler is responsible for.

        Args:
            request: The request to process.
            cancellation_token: A token used to cancel the operation.

        Returns:
            The response for the request.
        """

    async def handle(
        self,
        request: T,
        next_handler: NextHandler[T, R],
        cancellation_token: CancellationToken,
    ) -> R:
        """
        Process the request if this handler is responsible for it,
        otherwise delegate to next_handler.

        Args:
            request: The request to process.
            next_handler: The next handler of the chain.
            cancellation_token: A token used to cancel the operation.

        Returns:
            The response for the request.
        """
        if next_handler is None:
            raise TypeError("next_handler must not be None")

        if self.can_handle(request):
            return await self.process(request, cancellation_token)
        else:
            return await next_handler(request, cancellation_token)

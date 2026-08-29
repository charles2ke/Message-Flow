"""Cancellation support for MessageFlow chains."""

import asyncio
from typing import Optional


class CancellationToken:
    """
    Propagates a cancellation request through a chain.

    Tokens are created by a CancellationTokenSource; none() returns a token that is never cancelled.
    """

    _NONE: Optional["CancellationToken"] = None

    def __init__(self, cancelled: Optional[list[bool]] = None) -> None:
        """
        Create a cancellation token.

        Args:
            cancelled: A mutable flag (list with one bool) indicating cancellation state.
        """
        self._cancelled = cancelled

    @classmethod
    def none(cls) -> "CancellationToken":
        """
        Return a token that is never cancelled.

        Returns:
            The non-cancellable token.
        """
        if cls._NONE is None:
            cls._NONE = cls(None)
        return cls._NONE

    def is_cancellation_requested(self) -> bool:
        """
        Indicate whether cancellation has been requested.

        Returns:
            True when the source of this token was cancelled.
        """
        return self._cancelled is not None and self._cancelled[0]

    def throw_if_cancellation_requested(self) -> None:
        """
        Raise CancelledError when cancellation has been requested.

        Raises:
            asyncio.CancelledError: Cancellation has been requested.
        """
        if self.is_cancellation_requested():
            raise asyncio.CancelledError("The operation was cancelled.")


class CancellationTokenSource:
    """Creates CancellationToken instances and signals their cancellation."""

    def __init__(self) -> None:
        """Create a new cancellation token source."""
        self._cancelled = [False]
        self._token = CancellationToken(self._cancelled)

    def token(self) -> CancellationToken:
        """
        Get the token signalled by this source.

        Returns:
            The token of this source.
        """
        return self._token

    def cancel(self) -> None:
        """Signal cancellation to every holder of the token."""
        self._cancelled[0] = True

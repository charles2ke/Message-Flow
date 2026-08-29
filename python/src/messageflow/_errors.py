"""Exception types for MessageFlow."""


class UnhandledRequestError(RuntimeError):
    """Raised when no handler of a chain accepted the request and no fallback was configured."""

    def __init__(self, message: str = "No handler in the chain handled the request.") -> None:
        """
        Create an exception with the given message.

        Args:
            message: The message describing the error.
        """
        super().__init__(message)

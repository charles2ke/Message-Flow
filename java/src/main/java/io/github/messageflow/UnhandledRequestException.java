package io.github.messageflow;

/**
 * Thrown when no handler of a chain accepted the request and no fallback was configured.
 */
public final class UnhandledRequestException extends IllegalStateException {

    private static final long serialVersionUID = 1L;

    /** Creates an exception with the default message. */
    public UnhandledRequestException() {
        super("No handler in the chain handled the request.");
    }

    /**
     * Creates an exception with the given message.
     *
     * @param message the message describing the error
     */
    public UnhandledRequestException(String message) {
        super(message);
    }

    /**
     * Creates an exception with the given message and cause.
     *
     * @param message the message describing the error
     * @param cause   the exception that caused this error
     */
    public UnhandledRequestException(String message, Throwable cause) {
        super(message, cause);
    }
}

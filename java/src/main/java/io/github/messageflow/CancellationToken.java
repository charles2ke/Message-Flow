package io.github.messageflow;

import java.util.concurrent.CancellationException;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Propagates a cancellation request through a chain.
 *
 * <p>Tokens are created by a {@link CancellationTokenSource}; {@link #none()} returns a token that is
 * never cancelled.
 */
public final class CancellationToken {

    private static final CancellationToken NONE = new CancellationToken(null);

    private final AtomicBoolean cancelled;

    CancellationToken(AtomicBoolean cancelled) {
        this.cancelled = cancelled;
    }

    /**
     * Returns a token that is never cancelled.
     *
     * @return the non-cancellable token
     */
    public static CancellationToken none() {
        return NONE;
    }

    /**
     * Indicates whether cancellation has been requested.
     *
     * @return {@code true} when the source of this token was cancelled
     */
    public boolean isCancellationRequested() {
        return cancelled != null && cancelled.get();
    }

    /**
     * Throws when cancellation has been requested.
     *
     * @throws CancellationException cancellation has been requested
     */
    public void throwIfCancellationRequested() {
        if (isCancellationRequested()) {
            throw new CancellationException("The operation was cancelled.");
        }
    }
}

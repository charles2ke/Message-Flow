package io.github.messageflow;

import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Creates {@link CancellationToken} instances and signals their cancellation.
 */
public final class CancellationTokenSource {

    private final AtomicBoolean cancelled = new AtomicBoolean();
    private final CancellationToken token = new CancellationToken(cancelled);

    /**
     * Gets the token signalled by this source.
     *
     * @return the token of this source
     */
    public CancellationToken token() {
        return token;
    }

    /** Signals cancellation to every holder of {@link #token()}. */
    public void cancel() {
        cancelled.set(true);
    }
}

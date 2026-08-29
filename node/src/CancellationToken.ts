/**
 * Propagates a cancellation request through a chain.
 *
 * Tokens are created by a {@link CancellationTokenSource}; {@link CancellationToken.none}
 * returns a token that is never cancelled.
 */
export class CancellationToken {
  private static readonly NONE = new CancellationToken(null);

  private constructor(private readonly cancelled: { value: boolean } | null) {}

  /**
   * Returns a token that is never cancelled.
   *
   * @returns the non-cancellable token
   */
  static none(): CancellationToken {
    return CancellationToken.NONE;
  }

  /**
   * Indicates whether cancellation has been requested.
   *
   * @returns `true` when the source of this token was cancelled
   */
  isCancellationRequested(): boolean {
    return this.cancelled !== null && this.cancelled.value;
  }

  /**
   * Throws when cancellation has been requested.
   *
   * @throws {Error} with name 'CancellationError' when cancellation has been requested
   */
  throwIfCancellationRequested(): void {
    if (this.isCancellationRequested()) {
      const error = new Error('The operation was cancelled.');
      error.name = 'CancellationError';
      throw error;
    }
  }

  /** @internal */
  static createFromSource(cancelled: { value: boolean }): CancellationToken {
    return new CancellationToken(cancelled);
  }
}

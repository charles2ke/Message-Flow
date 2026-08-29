import { CancellationToken } from './CancellationToken.js';

/**
 * Creates {@link CancellationToken} instances and signals their cancellation.
 */
export class CancellationTokenSource {
  private readonly cancelled = { value: false };
  private readonly _token: CancellationToken;

  constructor() {
    this._token = CancellationToken['createFromSource'](this.cancelled);
  }

  /**
   * Gets the token signalled by this source.
   *
   * @returns the token of this source
   */
  token(): CancellationToken {
    return this._token;
  }

  /**
   * Signals cancellation to every holder of {@link token}.
   */
  cancel(): void {
    this.cancelled.value = true;
  }
}

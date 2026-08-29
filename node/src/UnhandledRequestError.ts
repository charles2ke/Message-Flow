/**
 * Thrown when no handler of a chain accepted the request and no fallback was configured.
 */
export class UnhandledRequestError extends Error {
  /**
   * Creates an error with the default message.
   */
  constructor(message: string = 'No handler in the chain handled the request.') {
    super(message);
    this.name = 'UnhandledRequestError';
    Object.setPrototypeOf(this, UnhandledRequestError.prototype);
  }
}

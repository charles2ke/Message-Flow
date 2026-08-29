/**
 * The severity of an entry written by a chain to a {@link ChainLogger}.
 */
export enum ChainLogLevel {
  /** The most verbose level, used for step-by-step diagnostics. */
  Trace = 0,

  /** Diagnostic information useful while developing. */
  Debug = 1,

  /** The normal flow of the chain. */
  Information = 2,

  /** An abnormal but recoverable situation. */
  Warning = 3,

  /** A request that failed with an exception. */
  Error = 4,
}

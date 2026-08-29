import type { ChainLogLevel } from './ChainLogLevel.js';

/**
 * Receives the log entries a chain writes while executing a request.
 *
 * The interface is intentionally minimal so the library stays dependency-free:
 * an adapter over any logging framework is a few lines of code.
 */
export interface ChainLogger {
  /**
   * Determines whether entries of the given level are recorded.
   *
   * @param level - the level to check
   * @returns `true` when entries of `level` are recorded
   */
  isEnabled(level: ChainLogLevel): boolean;

  /**
   * Records a log entry.
   *
   * @param level - the severity of the entry
   * @param message - the message describing what the chain did
   * @param error - the error that failed the request, or `null`
   */
  log(level: ChainLogLevel, message: string, error: unknown): void;
}

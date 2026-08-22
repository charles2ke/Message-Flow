using System.Diagnostics;
using System.Globalization;

namespace MessageFlow;

/// <summary>
/// Adds logging and tracing middleware to a <see cref="ChainBuilder{TRequest, TResponse}"/>.
/// </summary>
/// <remarks>
/// Both middlewares wrap the remainder of the chain, so they observe every handler registered after
/// them — register them first to observe the whole chain.
/// </remarks>
public static class ChainBuilderDiagnosticsExtensions
{
    /// <summary>
    /// Appends a middleware that logs the start, the completion and the failure of every request
    /// flowing through the remainder of the chain.
    /// </summary>
    /// <remarks>
    /// Only type names and durations are logged; the request itself is never written to the log, so
    /// no payload can leak into log storage. Failures are logged at <see cref="ChainLogLevel.Error"/>
    /// and the exception is rethrown unchanged.
    /// </remarks>
    /// <typeparam name="TRequest">The type of the request flowing through the chain.</typeparam>
    /// <typeparam name="TResponse">The type of the response produced by the chain.</typeparam>
    /// <param name="builder">The builder to append the middleware to.</param>
    /// <param name="logger">The logger receiving the entries.</param>
    /// <param name="level">The level used for the start and completion entries.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public static ChainBuilder<TRequest, TResponse> UseLogging<TRequest, TResponse>(
        this ChainBuilder<TRequest, TResponse> builder,
        IChainLogger logger,
        ChainLogLevel level = ChainLogLevel.Debug)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(logger);

        var chainName = $"{typeof(TRequest).Name} -> {typeof(TResponse).Name}";

        return builder.Use(async (request, nextHandler, cancellationToken) =>
        {
            if (logger.IsEnabled(level))
            {
                logger.Log(level, $"Executing chain {chainName}.", null);
            }

            var timestamp = Stopwatch.GetTimestamp();

            try
            {
                var response = await nextHandler(request, cancellationToken).ConfigureAwait(false);

                if (logger.IsEnabled(level))
                {
                    logger.Log(level, $"Executed chain {chainName} in {Elapsed(timestamp)} ms.", null);
                }

                return response;
            }
            catch (Exception exception)
            {
                if (logger.IsEnabled(ChainLogLevel.Error))
                {
                    logger.Log(
                        ChainLogLevel.Error,
                        $"Chain {chainName} failed after {Elapsed(timestamp)} ms.",
                        exception);
                }

                throw;
            }
        });
    }

    /// <summary>
    /// Appends a middleware that wraps the remainder of the chain in an
    /// <see cref="Activity"/> emitted on <see cref="ChainDiagnostics.ActivitySource"/>.
    /// </summary>
    /// <remarks>
    /// The activity is only created when a listener — an OpenTelemetry exporter, for example — is
    /// subscribed to <see cref="ChainDiagnostics.ActivitySourceName"/>; otherwise the middleware is a
    /// single delegate call. Failures set the activity status to
    /// <see cref="ActivityStatusCode.Error"/>, record an exception event and rethrow the exception
    /// unchanged.
    /// </remarks>
    /// <typeparam name="TRequest">The type of the request flowing through the chain.</typeparam>
    /// <typeparam name="TResponse">The type of the response produced by the chain.</typeparam>
    /// <param name="builder">The builder to append the middleware to.</param>
    /// <param name="activityName">The name of the created activity.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="activityName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="activityName"/> is empty.</exception>
    public static ChainBuilder<TRequest, TResponse> UseTracing<TRequest, TResponse>(
        this ChainBuilder<TRequest, TResponse> builder,
        string activityName = ChainDiagnostics.ExecuteActivityName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(activityName);

        var requestType = typeof(TRequest).FullName;
        var responseType = typeof(TResponse).FullName;

        return builder.Use(async (request, nextHandler, cancellationToken) =>
        {
            using var activity = ChainDiagnostics.ActivitySource.StartActivity(activityName, ActivityKind.Internal);
            activity?.SetTag(ChainDiagnostics.RequestTypeTag, requestType);
            activity?.SetTag(ChainDiagnostics.ResponseTypeTag, responseType);

            try
            {
                var response = await nextHandler(request, cancellationToken).ConfigureAwait(false);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return response;
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity?.AddEvent(new ActivityEvent(
                    "exception",
                    tags: new ActivityTagsCollection
                    {
                        ["exception.type"] = exception.GetType().FullName,
                        ["exception.message"] = exception.Message,
                    }));

                throw;
            }
        });
    }

    private static string Elapsed(long timestamp)
        => Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture);
}

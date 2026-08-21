using MessageFlow;

namespace MessageFlow.Samples;

/// <summary>
/// The kind of a support ticket.
/// </summary>
public enum TicketKind
{
    /// <summary>A refund request.</summary>
    Refund,

    /// <summary>A password reset request.</summary>
    PasswordReset,

    /// <summary>Anything the automated handlers do not understand.</summary>
    Other,
}

/// <summary>
/// A support ticket flowing through the triage chain.
/// </summary>
/// <param name="id">The ticket identifier.</param>
/// <param name="kind">The kind of the ticket.</param>
public sealed class Ticket(int id, TicketKind kind)
{
    /// <summary>Gets the ticket identifier.</summary>
    public int Id { get; } = id;

    /// <summary>Gets the kind of the ticket.</summary>
    public TicketKind Kind { get; } = kind;
}

/// <summary>
/// Handles refund tickets.
/// </summary>
public sealed class RefundHandler : HandlerBase<Ticket, string>
{
    /// <inheritdoc />
    protected override bool CanHandle(Ticket request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Kind == TicketKind.Refund;
    }

    /// <inheritdoc />
    protected override ValueTask<string> ProcessAsync(Ticket request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ValueTask<string>($"refund issued for ticket {request.Id}");
    }
}

/// <summary>
/// Handles password reset tickets.
/// </summary>
public sealed class PasswordResetHandler : HandlerBase<Ticket, string>
{
    /// <inheritdoc />
    protected override bool CanHandle(Ticket request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Kind == TicketKind.PasswordReset;
    }

    /// <inheritdoc />
    protected override ValueTask<string> ProcessAsync(Ticket request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ValueTask<string>($"reset link sent for ticket {request.Id}");
    }
}

/// <summary>
/// Routes support tickets to reusable <see cref="HandlerBase{TRequest, TResponse}"/> implementations.
/// </summary>
public static class SupportTicketSample
{
    /// <summary>
    /// Builds the ticket triage chain, escalating anything the handlers do not accept.
    /// </summary>
    /// <returns>The composed chain.</returns>
    public static IChain<Ticket, string> BuildChain()
        => Chain.Create<Ticket, string>()
            .Use(new RefundHandler())
            .Use(new PasswordResetHandler())
            .WithFallback((request, _) => new ValueTask<string>($"escalated ticket {request.Id} to a human"))
            .Build();

    /// <summary>
    /// Runs the sample against one ticket of every kind.
    /// </summary>
    /// <param name="output">The writer receiving the sample output.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The triage decisions, in ticket order.</returns>
    public static async Task<IReadOnlyList<string>> RunAsync(
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var chain = BuildChain();
        var tickets = new[]
        {
            new Ticket(1, TicketKind.Refund),
            new Ticket(2, TicketKind.PasswordReset),
            new Ticket(3, TicketKind.Other),
        };

        var results = new List<string>();
        foreach (var ticket in tickets)
        {
            var response = await chain.ExecuteAsync(ticket, cancellationToken).ConfigureAwait(false);
            results.Add(response);
            await output.WriteLineAsync($"ticket {ticket.Id} ({ticket.Kind}) => {response}").ConfigureAwait(false);
        }

        return results;
    }
}

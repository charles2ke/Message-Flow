using MessageFlow;

namespace MessageFlow.Samples;

/// <summary>
/// Glues two independently authored chain fragments into a single chain.
/// </summary>
public static class MergedChainsSample
{
    /// <summary>
    /// Builds the fragment owned by the billing team.
    /// </summary>
    /// <returns>The billing fragment.</returns>
    public static ChainBuilder<Ticket, string> BuildBillingFragment()
        => Chain.Create<Ticket, string>()
            .Use(new RefundHandler());

    /// <summary>
    /// Builds the fragment owned by the accounts team.
    /// </summary>
    /// <returns>The accounts fragment.</returns>
    public static ChainBuilder<Ticket, string> BuildAccountsFragment()
        => Chain.Create<Ticket, string>()
            .Use(new PasswordResetHandler());

    /// <summary>
    /// Merges both fragments into one chain. Tickets neither fragment accepts fall through to the
    /// fallback of the merged chain.
    /// </summary>
    /// <returns>The composed chain.</returns>
    public static IChain<Ticket, string> BuildChain()
        => Chain.Create<Ticket, string>()
            .Use(BuildBillingFragment())
            .Use(BuildAccountsFragment())
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
            new Ticket(11, TicketKind.Refund),
            new Ticket(12, TicketKind.PasswordReset),
            new Ticket(13, TicketKind.Other),
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

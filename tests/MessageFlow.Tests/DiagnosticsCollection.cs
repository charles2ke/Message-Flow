namespace MessageFlow.Tests;

/// <summary>
/// Serializes the test classes that subscribe an <see cref="System.Diagnostics.ActivityListener"/>:
/// listeners are process-wide, so running them in parallel would let one class observe the
/// activities of another.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DiagnosticsCollection
{
    public const string Name = "diagnostics";
}

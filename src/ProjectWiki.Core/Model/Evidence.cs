namespace ProjectWiki.Core.Model;

/// <summary>
/// A single piece of deterministic evidence backing a <see cref="Relation"/>.
/// Line numbers are only ever populated when computed from a real syntax
/// tree position; they must never be estimated.
/// </summary>
public sealed class Evidence
{
    public required string File { get; init; }

    public int? StartLine { get; init; }

    public int? EndLine { get; init; }
}

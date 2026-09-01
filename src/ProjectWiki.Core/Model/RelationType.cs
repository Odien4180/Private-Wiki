using System.Text.Json.Serialization;
using ProjectWiki.Core.Persistence;

namespace ProjectWiki.Core.Model;

[JsonConverter(typeof(SnakeCaseEnumConverter<RelationType>))]
public enum RelationType
{
    Contains,
    BelongsTo,
    Uses,
    DependsOn,
    References,
    Inherits,
    Implements,
    Creates,
    Loads,
    Configures,
    InitializedBy,
    UsedInScene,
    UsesPrefab,
    RelatedTo,
}

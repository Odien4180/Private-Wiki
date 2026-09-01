using System.Text.Json.Serialization;
using ProjectWiki.Core.Persistence;

namespace ProjectWiki.Core.Model;

[JsonConverter(typeof(SnakeCaseEnumConverter<Confidence>))]
public enum Confidence
{
    High,
    Medium,
    Low,
}

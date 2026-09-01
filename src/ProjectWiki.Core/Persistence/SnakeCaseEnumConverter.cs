using System.Text.Json.Serialization;

namespace ProjectWiki.Core.Persistence;

/// <summary>
/// A <see cref="JsonStringEnumConverter{TEnum}"/> pre-configured with
/// <see cref="SnakeCaseNamingPolicy"/>, usable directly from a
/// <c>[JsonConverter]</c> attribute (which cannot pass constructor
/// arguments to the generic converter itself).
/// </summary>
public sealed class SnakeCaseEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(SnakeCaseNamingPolicy.Instance)
    {
    }
}

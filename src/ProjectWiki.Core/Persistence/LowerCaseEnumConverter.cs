using System.Text.Json.Serialization;

namespace ProjectWiki.Core.Persistence;

public sealed class LowerCaseEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    public LowerCaseEnumConverter()
        : base(LowerCaseNamingPolicy.Instance)
    {
    }
}

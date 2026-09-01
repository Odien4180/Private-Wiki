using System.Text;
using System.Text.Json;

namespace ProjectWiki.Core.Persistence;

/// <summary>
/// Converts PascalCase enum member names (e.g. <c>BelongsTo</c>) to
/// lower snake_case (<c>belongs_to</c>) for JSON serialization, matching
/// the wiring used in the entity/relation JSON schemas.
/// </summary>
public sealed class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public static readonly SnakeCaseNamingPolicy Instance = new();

    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}

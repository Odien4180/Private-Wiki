using System.Text.Json.Serialization;
using ProjectWiki.Core.Persistence;

namespace ProjectWiki.Core.Config;

[JsonConverter(typeof(LowerCaseEnumConverter<ProjectType>))]
public enum ProjectType
{
    Generic,
    DotNet,
    Unity,
}

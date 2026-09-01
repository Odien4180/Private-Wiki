using System.Text.Json.Serialization;
using ProjectWiki.Core.Persistence;

namespace ProjectWiki.Core.Model;

[JsonConverter(typeof(SnakeCaseEnumConverter<EntityType>))]
public enum EntityType
{
    Project,
    Architecture,
    System,
    Feature,
    Class,
    Struct,
    Enum,
    Interface,
    Service,
    Manager,
    Component,
    Scene,
    Prefab,
    Data,
    Config,
    Package,
    External,
    Tool,
}

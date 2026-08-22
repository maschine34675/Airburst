using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;

namespace AirburstServer;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.maschine.Airburst";
    public string Name { get; init; } = "Airburst";
    public string Author { get; init; } = "maschine";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.1.0");
    public Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        { "com.wtt.commonlib", new Range("~3.0.0") }
    };
    public string? Url { get; init; } = "https://github.com/maschine34675/Airburst";
    public string License { get; init; } = "MIT";
}

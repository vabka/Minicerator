using System.Text.Json.Serialization;

namespace Minicerator.CLI
{
    public class ServerStatus
    {
        [JsonPropertyName("description")] public ServerDescription Description { get; init; }
        [JsonPropertyName("players")] public ServerPlayers Players { get; init; }
        [JsonPropertyName("version")] public ServerVersion Version { get; init; }
    }

    public class ServerPlayers
    {
        [JsonPropertyName("max")] public int Max { get; init; }
        [JsonPropertyName("online")] public int Online { get; init; }
    }

    public class ServerVersion
    {
        [JsonPropertyName("name")] public string Name { get; init; }
        [JsonPropertyName("protocol")] public int Protocol { get; init; }
    }

    public class ServerDescription
    {
        [JsonPropertyName("text")] public string Text { get; init; }
    }
}

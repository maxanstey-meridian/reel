namespace Reel.Server.Domain;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FileNodeType
{
    File,
    Directory
}

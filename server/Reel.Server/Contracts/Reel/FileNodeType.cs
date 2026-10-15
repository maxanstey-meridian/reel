using System.Text.Json.Serialization;
using Rivet;

namespace Reel.Server.Contracts.Reel;

[JsonConverter(typeof(RivetCamelCaseEnumConverter<FileNodeType>))]
public enum FileNodeType
{
    File,
    Directory,
}

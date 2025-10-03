using System.Text.Json.Serialization;

namespace HostsFileEditor;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, bool>))]
internal partial class LocalSettingsJsonContext : JsonSerializerContext { }

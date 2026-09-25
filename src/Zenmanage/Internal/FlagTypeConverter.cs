using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zenmanage.Internal;

/// <summary>
/// Custom JSON converter for <see cref="FlagType"/>. The default
/// <see cref="JsonStringEnumConverter"/> throws on any string it doesn't recognize, which
/// would fail the entire rules payload if the API ever serves a flag type this SDK
/// version predates. This converter maps an unrecognized type to
/// <see cref="FlagType.Unknown"/> instead, so the rest of the payload still parses;
/// evaluation then treats that flag like a missing one.
/// </summary>
internal sealed class FlagTypeConverter : JsonConverter<FlagType>
{
    public override FlagType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return FlagType.Unknown;
        }

        return reader.GetString() switch
        {
            "boolean" => FlagType.Boolean,
            "string" => FlagType.String,
            "number" => FlagType.Number,
            "json" => FlagType.Json,
            _ => FlagType.Unknown
        };
    }

    public override void Write(Utf8JsonWriter writer, FlagType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            FlagType.Boolean => "boolean",
            FlagType.String => "string",
            FlagType.Number => "number",
            FlagType.Json => "json",
            _ => "unknown"
        });
    }
}

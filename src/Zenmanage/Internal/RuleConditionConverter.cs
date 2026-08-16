using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zenmanage.Internal;

/// <summary>
/// Custom JSON converter for <see cref="RuleCondition"/> that handles both the
/// CDN wire format (selector/selector_subtype/comparer/values) and the legacy
/// format (attribute/operator/value).
/// </summary>
internal sealed class RuleConditionConverter : JsonConverter<RuleCondition>
{
    public override RuleCondition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for RuleCondition");

        string? attribute = null;
        string? @operator = null;
        JsonElement? value = null;

        string? selector = null;
        string? selectorSubtype = null;
        string? comparer = null;
        JsonElement? values = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name");

            var propName = reader.GetString();
            reader.Read();

            switch (propName)
            {
                case "attribute":
                    attribute = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case "operator":
                    @operator = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case "value":
                    value = JsonElement.ParseValue(ref reader);
                    break;
                case "selector":
                    selector = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case "selector_subtype":
                    selectorSubtype = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case "comparer":
                    comparer = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case "values":
                    values = JsonElement.ParseValue(ref reader);
                    break;
                default:
                    // Skip unknown fields
                    JsonElement.ParseValue(ref reader);
                    break;
            }
        }

        // CDN format: selector/comparer present
        if (selector is not null && comparer is not null)
        {
            var attr = selector == "attribute"
                ? (selectorSubtype ?? selector)
                : selector;

            return new RuleCondition(attr, comparer, values.HasValue ? values.Value : null);
        }

        // Legacy format
        if (attribute is null)
            throw new JsonException("Missing required field 'attribute' in RuleCondition");
        if (@operator is null)
            throw new JsonException("Missing required field 'operator' in RuleCondition");

        return new RuleCondition(attribute, @operator, value.HasValue ? value.Value : null);
    }

    public override void Write(Utf8JsonWriter writer, RuleCondition value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("attribute", value.Attribute);
        writer.WriteString("operator", value.Operator);
        if (value.Value is not null)
        {
            writer.WritePropertyName("value");
            JsonSerializer.Serialize(writer, value.Value, options);
        }
        writer.WriteEndObject();
    }
}

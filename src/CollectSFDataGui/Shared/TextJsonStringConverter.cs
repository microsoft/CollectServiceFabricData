using System;
using System.Text.Json;

namespace CollectSFDataGui.Shared
{
    public class JsonHelpers
    {
        public static JsonSerializerOptions GetJsonSerializerOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            options.Converters.Add(new JsonHelpers.StringConverter());
            options.AllowTrailingCommas = true;
            options.MaxDepth = 5;
            //options.IncludeFields = true;
            options.PropertyNameCaseInsensitive = true;
            options.ReadCommentHandling = JsonCommentHandling.Skip;
            //options.ReferenceHandler = ReferenceHandler.Preserve;
            return options;
        }

        public class StringConverter : System.Text.Json.Serialization.JsonConverter<string>
        {
            public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Number)
                {
                    var stringValue = reader.GetInt32();
                    return stringValue.ToString();
                }
                else if (reader.TokenType == JsonTokenType.String)
                {
                    return reader.GetString();
                }

                throw new System.Text.Json.JsonException();
            }

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value);
            }
        }
    }
}
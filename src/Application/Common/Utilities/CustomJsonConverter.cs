using Newtonsoft.Json;

namespace Common.Utilities;

public class CustomJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        // Handle only boolean types.
        return objectType == typeof(bool);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        switch (reader.Value?.ToString()?.ToLower().Trim())
        {
            case "true":
            case "yes":
            case "y":
            case "1":
                return true;
            case "false":
            case "no":
            case "n":
            case "0":
                return false;
            case null:
                return null;
        }

        return new JsonSerializer().Deserialize(reader, objectType);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteValue((bool)value ? 1 : 0);
        }
    }
}
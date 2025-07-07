using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

/// <summary>
/// Custom JsonConverter to handle deserialization of the abstract SlitherInteractor class.
/// This allows for polymorphic deserialization based on a 'Type' property in the JSON.
/// </summary>
public class SlitherInteractorConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        // This converter only applies to the base SlitherInteractor type
        return typeof(SlitherInteractor).IsAssignableFrom(objectType) && !objectType.IsAbstract;
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JObject jo = JObject.Load(reader);

        // The 'Type' property in the JSON will determine which concrete class to instantiate
        string typeName = jo["Type"]?.Value<string>();

        if (string.IsNullOrEmpty(typeName))
        {
            throw new JsonSerializationException("SlitherInteractor JSON object is missing the 'Type' property.");
        }

        SlitherInteractor interactor;
        switch (typeName)
        {
            case nameof(ChainInteractor):
                interactor = new ChainInteractor();
                break;
            case nameof(CocoonInteractor):
                interactor = new CocoonInteractor();
                break;
            // Add other concrete interactor types here in the future
            default:
                throw new NotSupportedException($"The interactor type '{typeName}' is not supported.");
        }

        // Populate the created object with the rest of the JSON data
        serializer.Populate(jo.CreateReader(), interactor);
        return interactor;
    }

    // We don't need custom write logic because the 'Type' property is part of the base class,
    // and Newtonsoft's default serialization will handle it correctly.
    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        // This will never be called because CanWrite is false
        throw new NotImplementedException("This converter is only used for reading JSON and should not be used for writing.");
    }
}

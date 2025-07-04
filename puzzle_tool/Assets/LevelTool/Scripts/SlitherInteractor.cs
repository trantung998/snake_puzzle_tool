using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class JsonDataService
{
    private static readonly JsonSerializerSettings settings = new JsonSerializerSettings
    {
        Converters = new List<JsonConverter> { new SlitherInteractorConverter() },
        Formatting = Formatting.Indented // Giúp file JSON dễ đọc hơn
    };

    public static bool Save(LevelData data, string path)
    {
        try
        {
            string json = JsonConvert.SerializeObject(data, settings);
            File.WriteAllText(path, json);
            Debug.Log($"<color=green>Successfully saved level to {path}</color>");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save level to {path}. Error: {e.Message}");
            return false;
        }
    }

    public static LevelData Load(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"Cannot find file at {path}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            LevelData data = JsonConvert.DeserializeObject<LevelData>(json, settings);
            Debug.Log($"<color=green>Successfully loaded level from {path}</color>");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load level from {path}. Error: {e.Message}");
            return null;
        }
    }
}

// Custom Converter để xử lý việc đọc (deserialization) các đối tượng đa hình
public class SlitherInteractorConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(SlitherInteractor);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JObject jo = JObject.Load(reader);
        string typeName = jo["Type"]?.Value<string>();

        if (string.IsNullOrEmpty(typeName))
        {
            throw new JsonSerializationException("SlitherInteractor object is missing 'Type' property.");
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
            default:
                throw new NotSupportedException($"Interactor type '{typeName}' is not supported.");
        }

        serializer.Populate(jo.CreateReader(), interactor);
        return interactor;
    }

    // Chúng ta không cần custom logic khi ghi (serialization) vì base class đã có trường "Type"
    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException("This converter is only used for reading JSON.");
    }
}
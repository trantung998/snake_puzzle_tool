using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System;

/// <summary>
/// Handles saving and loading of level data using JSON serialization.
/// This service uses Newtonsoft.Json for more robust serialization.
/// </summary>
public static class JsonDataService
{
    private static readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented, // Makes the JSON file human-readable
        TypeNameHandling = TypeNameHandling.Auto, // Handles derived types like interactors
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore, // Prevents errors with circular references
        NullValueHandling = NullValueHandling.Ignore // Omits null properties from the JSON
    };

    /// <summary>
    /// Saves the level data to a specified file path.
    /// </summary>
    /// <param name="levelData">The level data to save.</param>
    /// <param name="path">The full path to the file.</param>
    /// <returns>True if saving was successful, false otherwise.</returns>
    public static bool Save(LevelData levelData, string path)
    {
        try
        {
            if (levelData == null)
            {
                Debug.LogError("Save failed: LevelData is null.");
                return false;
            }

            string jsonData = JsonConvert.SerializeObject(levelData, serializerSettings);
            File.WriteAllText(path, jsonData);
            return true;
        }
        catch (JsonException ex)
        {
            Debug.LogError($"JSON Serialization Error: Failed to save level to {path}. Reason: {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            Debug.LogError($"File I/O Error: Failed to write to file at {path}. Check permissions. Reason: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"An unexpected error occurred while saving to {path}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads level data from a specified file path.
    /// </summary>
    /// <param name="path">The full path to the file.</param>
    /// <returns>The loaded LevelData, or null if loading fails.</returns>
    public static LevelData Load(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"Load failed: File not found at {path}");
            return null;
        }

        try
        {
            string jsonData = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(jsonData))
            {
                Debug.LogWarning($"File at {path} is empty. Returning new LevelData.");
                return new LevelData();
            }
            LevelData levelData = JsonConvert.DeserializeObject<LevelData>(jsonData, serializerSettings);
            return levelData;
        }
        catch (JsonException ex)
        {
            Debug.LogError($"JSON Deserialization Error: Failed to parse level data from {path}. The file might be corrupted or not a valid JSON. Reason: {ex.Message}");
            return null;
        }
        catch (IOException ex)
        {
            Debug.LogError($"File I/O Error: Failed to read file at {path}. Check permissions. Reason: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"An unexpected error occurred while loading from {path}: {ex.Message}");
            return null;
        }
    }
}
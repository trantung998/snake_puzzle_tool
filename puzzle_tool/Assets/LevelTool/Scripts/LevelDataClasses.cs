using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

// --- ENUM MÀU SẮC ---
[Serializable]
public enum SlitherColor
{
    Red,
    Green,
    Blue,
    Yellow,
    Purple,
    Orange,
    Cyan,
    Magenta
}

// Helper class để chuyển đổi giữa enum SlitherColor và UnityEngine.Color
public static class SlitherColorUtility
{
    public static Color ToUnityColor(this SlitherColor color)
    {
        switch (color)
        {
            case SlitherColor.Red:     return Color.red;
            case SlitherColor.Green:   return Color.green;
            case SlitherColor.Blue:    return Color.blue;
            case SlitherColor.Yellow:  return Color.yellow;
            case SlitherColor.Purple:  return new Color(0.5f, 0, 0.5f);
            case SlitherColor.Orange:  return new Color(1, 0.5f, 0);
            case SlitherColor.Cyan:    return Color.cyan;
            case SlitherColor.Magenta: return Color.magenta;
            default: return Color.white;
        }
    }
    
    public static SlitherColor FromUnityColor(Color color)
    {
        // Tìm SlitherColor gần nhất với Color đã cho
        float minDistance = float.MaxValue;
        SlitherColor bestMatch = SlitherColor.Red;
        
        foreach (SlitherColor slitherColor in Enum.GetValues(typeof(SlitherColor)))
        {
            Color unityColor = slitherColor.ToUnityColor();
            float distance = ColorDistance(color, unityColor);
            
            if (distance < minDistance)
            {
                minDistance = distance;
                bestMatch = slitherColor;
            }
        }
        
        return bestMatch;
    }
    
    private static float ColorDistance(Color a, Color b)
    {
        return Mathf.Sqrt(
            Mathf.Pow(a.r - b.r, 2) + 
            Mathf.Pow(a.g - b.g, 2) + 
            Mathf.Pow(a.b - b.b, 2));
    }
}

// --- CÁC LỚP DATA CHÍNH ---

[Serializable]
public class LevelData
{
    public int gridWidth = 5;
    public int gridHeight = 8;
    
    public List<SlitherPlacementData> slithers = new List<SlitherPlacementData>();
    public List<HolePlacementData> holes = new List<HolePlacementData>();

    public void ClearCell(Vector2Int position)
    {
        holes.RemoveAll(h => h.position == position);
        slithers.RemoveAll(s => s.bodyPositions.Contains(position));
    }
}

[Serializable]
public class HolePlacementData
{
    public SlitherColor color;
    public Vector2Int position;
    public string slitherId; // ID của rắn tương ứng với hố này
}

[Serializable]
public class SlitherPlacementData
{
    public string id; // ID duy nhất cho mỗi con rắn
    public SlitherColor color;
    public List<Vector2Int> bodyPositions = new List<Vector2Int>();
    public List<SlitherInteractor> interactors = new List<SlitherInteractor>();
    
    public SlitherPlacementData()
    {
        // Tạo ID duy nhất khi khởi tạo
        id = Guid.NewGuid().ToString();
    }
}


// --- CẤU TRÚC INTERACTOR ĐA HÌNH ---

[Serializable]
public abstract class SlitherInteractor
{
    // Trường này giúp Newtonsoft.Json biết phải deserialize thành đối tượng nào khi đọc file.
    public string Type => GetType().Name; 
}

[Serializable]
public class ChainInteractor : SlitherInteractor
{
    [Tooltip("Số lần cần tương tác để phá xích.")]
    public int hitCount = 1;
}

[Serializable]
public class CocoonInteractor : SlitherInteractor
{
    [Tooltip("Số lần cần tương tác để nở.")]
    public int hitCount = 1;
}

// --- EXTENSION METHODS ---

public static class LevelDataExtensions
{
    // Kiểm tra tất cả các rắn có hố tương ứng chưa
    public static bool ValidateSlithersHaveHoles(this LevelData data, out string errorMessage)
    {
        errorMessage = string.Empty;
        List<string> errors = new List<string>();
        
        // Kiểm tra từng con rắn có hố tương ứng không
        foreach (var slither in data.slithers)
        {
            bool hasMatchingHole = data.holes.Any(h => h.slitherId == slither.id);
            if (!hasMatchingHole)
            {
                errors.Add($"Rắn #{data.slithers.IndexOf(slither) + 1} (màu {slither.color}) không có hố tương ứng");
            }
        }
        
        // Kiểm tra xem có hố nào không có rắn tương ứng không
        foreach (var hole in data.holes)
        {
            bool hasMatchingSlither = data.slithers.Any(s => s.id == hole.slitherId);
            if (!hasMatchingSlither)
            {
                errors.Add($"Hố tại vị trí {hole.position} (màu {hole.color}) không có rắn tương ứng");
            }
        }
        
        errorMessage = string.Join("\n", errors);
        return errors.Count == 0;
    }
}
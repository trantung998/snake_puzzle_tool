using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class LevelEditorWindow : EditorWindow
{
    private LevelData currentLevelData;
    private string currentPath = "";
    private Vector2 scrollPosition;

    // Tool state
    private enum Tool { None, Slither, Hole, Eraser }
    private Tool currentTool = Tool.None;

    // Tool-specific properties
    private SlitherColor selectedColor = SlitherColor.Red;

    // State for painting slithers
    private bool isPaintingSlither = false;
    private List<Vector2Int> currentSlitherPoints = new List<Vector2Int>();
    
    // Inspector state
    private SlitherPlacementData selectedSlither;
    private Vector2Int? currentlyHoveredCell = null;

    [MenuItem("Tools/Slither Level Editor")]
    public static void ShowWindow()
    {
        GetWindow<LevelEditorWindow>("Slither Level Editor");
    }

    private void OnGUI()
    {
        // --- File Management Toolbar ---
        DrawFileToolbar();
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (currentLevelData == null)
        {
            EditorGUILayout.HelpBox("Load a level file or create a new one to begin.", MessageType.Info);
            if (GUILayout.Button("Create New Level")) CreateNewLevel();
            EditorGUILayout.EndScrollView();
            return;
        }

        // --- Phần Tools và Grid/Inspector Layout ---
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        // --- Cột Trái: Tools và Grid ---
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f));
        DrawToolbar();
        EditorGUILayout.Space();
        DrawGrid();
        EditorGUILayout.EndVertical();

        // --- Cột Phải: Inspector ---
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));
        DrawInspector();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    private void DrawFileToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        if (GUILayout.Button("New", EditorStyles.toolbarButton))
        {
            CreateNewLevel();
        }
        
        if (GUILayout.Button("Load", EditorStyles.toolbarButton))
        {
            LoadLevel();
        }

        // Hiển thị tên file hiện tại
        string fileName = string.IsNullOrEmpty(currentPath) ? "Untitled" : Path.GetFileName(currentPath);
        GUILayout.TextField(fileName, EditorStyles.toolbarTextField);

        if (GUILayout.Button("Save", EditorStyles.toolbarButton))
        {
            SaveCurrentLevel();
        }
        
        if (GUILayout.Button("Save As...", EditorStyles.toolbarButton))
        {
            SaveLevelAs();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
        currentTool = (Tool)GUILayout.Toolbar((int)currentTool, System.Enum.GetNames(typeof(Tool)));

        if (currentTool == Tool.Slither || currentTool == Tool.Hole)
        {
            // Thay ColorField bằng Popup với enum SlitherColor
            selectedColor = (SlitherColor)EditorGUILayout.EnumPopup("Color", selectedColor);
            
            // Hiển thị mẫu màu
            EditorGUILayout.ColorField(GUIContent.none, selectedColor.ToUnityColor(), false, false, false, GUILayout.Width(40));
        }
    }

    private void DrawGrid()
    {
        EditorGUILayout.LabelField("Grid Editor", EditorStyles.boldLabel);
        float buttonSize = Mathf.Min(40f, (position.width * 0.5f - 20) / currentLevelData.gridWidth);

        // Thêm tooltip hiển thị tọa độ hiện tại
        Vector2 mousePos = Event.current.mousePosition;
        Vector2Int? localHoveredCell = null; // Use a local variable for calculation
        
        for (int y = currentLevelData.gridHeight - 1; y >= 0; y--)
        {
            GUILayout.BeginHorizontal();
            for (int x = 0; x < currentLevelData.gridWidth; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                string buttonText;
                Color buttonColor = GetCellDisplayColor(pos, out buttonText);

                Rect buttonRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                    GUILayout.Width(buttonSize), GUILayout.Height(buttonSize));
                
                // Kiểm tra nếu chuột đang hover qua ô này
                if (buttonRect.Contains(mousePos))
                {
                    localHoveredCell = pos;
                    Repaint(); // Repaint to show info immediately
                }

                // Vẽ viền cho ô đang được hover
                if (currentlyHoveredCell.HasValue && currentlyHoveredCell.Value == pos)
                {
                    EditorGUI.DrawRect(buttonRect, new Color(1, 1, 1, 0.3f));
                }

                // Vẽ button
                GUI.backgroundColor = buttonColor;
                if (GUI.Button(buttonRect, buttonText))
                {
                    HandleGridClick(pos);
                }
            }
            GUILayout.EndHorizontal();
        }
        GUI.backgroundColor = Color.white;

        // Only update the stored hovered cell during the Layout event
        if (Event.current.type == EventType.Layout)
        {
            currentlyHoveredCell = localHoveredCell;
        }

        // Hiển thị thông tin chi tiết về ô đang hover
        if (currentlyHoveredCell.HasValue)
        {
            DrawCellInfo(currentlyHoveredCell.Value);
        }
    }
    
    private void DrawCellInfo(Vector2Int pos)
    {
        // Hiển thị thông tin tọa độ
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"Position: ({pos.x}, {pos.y})", EditorStyles.boldLabel);

        // Kiểm tra xem ô có chứa Slither không
        var slither = currentLevelData.slithers.FirstOrDefault(s => s.bodyPositions.Contains(pos));
        if (slither != null)
        {
            int index = slither.bodyPositions.IndexOf(pos);
            int slitherIndex = currentLevelData.slithers.IndexOf(slither);
            
            string segmentType = index == 0 ? "Head" : 
                               (index == slither.bodyPositions.Count - 1 ? "Tail" : $"Body ({index + 1})");
                               
            EditorGUILayout.LabelField($"Slither #{slitherIndex + 1}, {segmentType}");
            EditorGUILayout.LabelField($"Color: {slither.color}");
            
            // Kiểm tra interactors
            var interactors = slither.interactors;
            if (interactors.Count > 0)
            {
                EditorGUILayout.LabelField("Interactors:", EditorStyles.boldLabel);
                foreach (var interactor in interactors)
                {
                    if (interactor is ChainInteractor chain)
                        EditorGUILayout.LabelField($"Chain (Hit Count: {chain.hitCount})");
                    else if (interactor is CocoonInteractor cocoon)
                        EditorGUILayout.LabelField($"Cocoon (Hit Count: {cocoon.hitCount})");
                }
            }
        }
        
        // Kiểm tra xem ô có chứa Hole không
        var hole = currentLevelData.holes.FirstOrDefault(h => h.position == pos);
        if (hole != null)
        {
            EditorGUILayout.LabelField($"Hole (Color: {hole.color})");
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void HandleGridClick(Vector2Int pos)
    {
        EditorUtility.SetDirty(this); // Mark window as dirty to ensure repaint

        // Logic chọn Sâu
        var slitherAtPos = currentLevelData.slithers.FirstOrDefault(s => s.bodyPositions.Contains(pos));
        if (slitherAtPos != null && currentTool != Tool.Slither) // Don't select if in slither painting mode
        {
            selectedSlither = slitherAtPos;
            isPaintingSlither = false;
            currentSlitherPoints.Clear();
            Repaint();
            return;
        }

        // Logic công cụ
        switch (currentTool)
        {
            case Tool.Eraser:
                currentLevelData.ClearCell(pos);
                break;
            case Tool.Hole:
                currentLevelData.ClearCell(pos);
                currentLevelData.holes.Add(new HolePlacementData { position = pos, color = selectedColor });
                break;
            case Tool.Slither:
                HandleSlitherPainting(pos);
                break;
        }
        
        // Bỏ chọn nếu click vào ô trống
        if (slitherAtPos == null)
        {
            selectedSlither = null;
        }

        Repaint();
    }
    
    private void HandleSlitherPainting(Vector2Int pos)
    {
        // Không cho vẽ đè lên các đối tượng khác.
        // Sửa lỗi: Bỏ đi phép so sánh không hợp lệ giữa SlitherPlacementData và List<Vector2Int>.
        // Khi đang vẽ, con rắn mới chưa có trong list `slithers` nên chỉ cần kiểm tra sự tồn tại là đủ.
        if (currentLevelData.holes.Any(h => h.position == pos) ||
            currentLevelData.slithers.Any(s => s.bodyPositions.Contains(pos)))
        {
             if (isPaintingSlither) FinishSlitherPainting();
             return;
        }

        if (!isPaintingSlither) // Bắt đầu vẽ
        {
            isPaintingSlither = true;
            currentSlitherPoints.Clear();
            currentSlitherPoints.Add(pos);
            selectedSlither = null; // Clear selection
        }
        else // Tiếp tục vẽ
        {
            Vector2Int lastPoint = currentSlitherPoints.Last();
            if (Mathf.Abs(pos.x - lastPoint.x) + Mathf.Abs(pos.y - lastPoint.y) == 1 && !currentSlitherPoints.Contains(pos))
            {
                currentSlitherPoints.Add(pos);
            }
            else // Click không hợp lệ, kết thúc vẽ
            {
                FinishSlitherPainting();
            }
        }
    }

    private void FinishSlitherPainting()
    {
        if (currentSlitherPoints.Count < 2) {
            Debug.LogWarning("A slither needs at least 2 points.");
            isPaintingSlither = false; // Reset painting state
            currentSlitherPoints.Clear();
            return;
        }

        if (currentSlitherPoints.Count > 0)
        {
            // Tạo rắn mới với ID ngẫu nhiên
            var newSlither = new SlitherPlacementData
            {
                bodyPositions = new List<Vector2Int>(currentSlitherPoints),
                color = selectedColor,
                interactors = new List<SlitherInteractor>()
            };
            currentLevelData.slithers.Add(newSlither);
            selectedSlither = newSlither;
            
            // Tạo hố tương ứng với cùng màu và ID của rắn
            CreateMatchingHole(selectedColor, newSlither.id);
        }
        isPaintingSlither = false;
        currentSlitherPoints.Clear();
    }
    
    // Create a matching hole for the slither ID
    private void CreateMatchingHole(SlitherColor color, string slitherId)
    {
        // Find an empty position to place the hole
        Vector2Int holePosition = FindEmptyPosition();
        
        // Create a new hole
        currentLevelData.holes.Add(new HolePlacementData 
        { 
            position = holePosition, 
            color = color,
            slitherId = slitherId
        });
        
        Debug.Log($"Created a hole with color {color} at {holePosition} for slither ID: {slitherId}");
    }
    
    // Find an empty position on the grid
    private Vector2Int FindEmptyPosition()
    {
        // Create a list of occupied cells
        HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();
        
        // Thêm vị trí của tất cả các rắn
        foreach (var slither in currentLevelData.slithers)
        {
            foreach (var pos in slither.bodyPositions)
            {
                occupiedPositions.Add(pos);
            }
        }
        
        // Thêm vị trí của tất cả các hố
        foreach (var hole in currentLevelData.holes)
        {
            occupiedPositions.Add(hole.position);
        }
        
        // Tìm vị trí trống
        for (int x = 0; x < currentLevelData.gridWidth; x++)
        {
            for (int y = 0; y < currentLevelData.gridHeight; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!occupiedPositions.Contains(pos))
                {
                    return pos;
                }
            }
        }
        
        // Nếu không tìm thấy vị trí trống, đặt ở góc trái trên
        Debug.LogWarning("No empty position found for the hole. Placing at (0,0).");
        return new Vector2Int(0, 0);
    }


    private Color GetCellDisplayColor(Vector2Int pos, out string text)
    {
        text = "";
        
        if (isPaintingSlither && currentSlitherPoints.Contains(pos))
        {
            // Show order while drawing the slither
            int index = currentSlitherPoints.IndexOf(pos);
            if (index == 0)
                text = "H"; // Head
            else if (index == currentSlitherPoints.Count - 1)
                text = "T"; // Tail
            else
                text = (index + 1).ToString();
                
            return selectedColor.ToUnityColor();
        }
        
        var hole = currentLevelData.holes.FirstOrDefault(h => h.position == pos);
        if (hole != null) return hole.color.ToUnityColor();
        
        var slither = currentLevelData.slithers.FirstOrDefault(s => s.bodyPositions.Contains(pos));
        if (slither != null)
        {
            int index = slither.bodyPositions.IndexOf(pos);
            bool isHead = (index == 0);
            bool isTail = (index == slither.bodyPositions.Count - 1);
            
            // Xác định interactors
            var cocoon = slither.interactors.OfType<CocoonInteractor>().FirstOrDefault();
            if (cocoon != null)
            {
                text = cocoon.hitCount.ToString();
                return Color.white;
            }
            
            var chain = slither.interactors.OfType<ChainInteractor>().FirstOrDefault();
            if (chain != null && isHead)
            {
                text = chain.hitCount.ToString();
            }
            else if (isHead)
            {
                text = "H"; // Head
            }
            else if (isTail)
            {
                text = "T"; // Tail
            }
            else 
            {
                // Tùy chọn: hiển thị thứ tự các phần thân
                // text = (index + 1).ToString();
            }

            // Hiển thị màu khác khi đầu rắn được chọn
            if (slither == selectedSlither)
            {
                // Đầu/đuôi sáng hơn, thân tối hơn khi được chọn
                if (isHead || isTail)
                    return Color.yellow;
                else
                    return Color.Lerp(slither.color.ToUnityColor(), Color.yellow, 0.3f);
            }
            
            // Đầu/đuôi sáng hơn để dễ phân biệt
            if (isHead)
                return Color.Lerp(slither.color.ToUnityColor(), Color.white, 0.2f);
            else if (isTail)
                return Color.Lerp(slither.color.ToUnityColor(), Color.black, 0.2f);
            else
                return slither.color.ToUnityColor();
        }

        return new Color(0.8f, 0.8f, 0.8f);
    }
    
    // --- INSPECTOR LOGIC ---
    
    private void DrawInspector()
    {
        EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

        if (selectedSlither == null)
        {
            EditorGUILayout.LabelField("Select a slither to edit.");
            return;
        }
        
        // Hiển thị thông tin chi tiết về con rắn
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"Slither #{currentLevelData.slithers.IndexOf(selectedSlither) + 1}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"ID: {selectedSlither.id}");
        EditorGUILayout.LabelField($"Length: {selectedSlither.bodyPositions.Count} segments");
        EditorGUILayout.LabelField($"Head position: {selectedSlither.bodyPositions[0]}");
        EditorGUILayout.LabelField($"Tail position: {selectedSlither.bodyPositions[selectedSlither.bodyPositions.Count - 1]}");
        EditorGUILayout.EndVertical();
        
        // Chọn màu và cập nhật hố tương ứng
        SlitherColor oldColor = selectedSlither.color;
        SlitherColor newColor = (SlitherColor)EditorGUILayout.EnumPopup("Color", selectedSlither.color);
        
        // Nếu màu thay đổi, cập nhật màu của hố tương ứng
        if (newColor != oldColor)
        {
            selectedSlither.color = newColor;
            UpdateMatchingHole(oldColor, newColor);
        }
        
        EditorGUILayout.ColorField(GUIContent.none, selectedSlither.color.ToUnityColor(), false, false, false, GUILayout.Width(40));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Interactors", EditorStyles.boldLabel);

        if (EditorGUILayout.DropdownButton(new GUIContent("Add Interactor"), FocusType.Keyboard))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Chain"), false, () => selectedSlither.interactors.Add(new ChainInteractor()));
            menu.AddItem(new GUIContent("Cocoon"), false, () => selectedSlither.interactors.Add(new CocoonInteractor()));
            menu.ShowAsContext();
        }

        for (int i = selectedSlither.interactors.Count - 1; i >= 0; i--)
        {
            var interactor = selectedSlither.interactors[i];
            
            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(interactor.GetType().Name, EditorStyles.boldLabel);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                selectedSlither.interactors.RemoveAt(i);
                Repaint();
                continue;
            }
            EditorGUILayout.EndHorizontal();

            if (interactor is ChainInteractor chain) chain.hitCount = EditorGUILayout.IntField("Hit Count", chain.hitCount);
            if (interactor is CocoonInteractor cocoon) cocoon.hitCount = EditorGUILayout.IntField("Hit Count", cocoon.hitCount);

            EditorGUILayout.EndVertical();
        }
    }

    // --- FILE MANAGEMENT ---

    private void CreateNewLevel()
    {
        currentLevelData = new LevelData();
        currentPath = "";
        selectedSlither = null;
        Debug.Log("New level created. Edit and use 'Save As...' to create a file.");
    }
    
    private void LoadLevel()
    {
        string path = EditorUtility.OpenFilePanel("Load Level JSON", Application.streamingAssetsPath, "json");
        if (!string.IsNullOrEmpty(path))
        {
            currentLevelData = JsonDataService.Load(path);
            currentPath = path;
            selectedSlither = null;
            
            // Check and show a warning if the level is invalid
            string errorMessage;
            if (!currentLevelData.ValidateSlithersHaveHoles(out errorMessage))
            {
                EditorUtility.DisplayDialog(
                    "Warning: Invalid Level", 
                    $"This level has some issues that need to be resolved:\n\n{errorMessage}\n\nPlease fix these issues before saving.",
                    "Understood");
                
                // Automatically fix issues if possible
                FixLevelIssues();
            }
            
            Repaint();
        }
    }
    
    // Automatically fix issues in the level
    private void FixLevelIssues()
    {
        // Ensure every slither has an ID
        foreach (var slither in currentLevelData.slithers)
        {
            if (string.IsNullOrEmpty(slither.id))
            {
                slither.id = Guid.NewGuid().ToString();
                Debug.Log($"Created a new ID for a slither that had none: {slither.id}");
            }
        }

        // 1. Create holes for slithers that don't have one
        foreach (var slither in currentLevelData.slithers)
        {
            if (!currentLevelData.holes.Any(h => h.slitherId == slither.id))
            {
                Debug.Log($"Automatically creating a hole for slither ID: {slither.id}, color: {slither.color}");
                CreateMatchingHole(slither.color, slither.id);
            }
        }
        
        // 2. Remove holes that don't have a corresponding slither
        foreach (var hole in currentLevelData.holes.ToList())
        {
            if (string.IsNullOrEmpty(hole.slitherId) || !currentLevelData.slithers.Any(s => s.id == hole.slitherId))
            {
                Debug.Log($"Removing hole at {hole.position} because it has no corresponding slither.");
                currentLevelData.holes.Remove(hole);
            }
        }
        
        // 3. Handle cases where a slither has multiple holes (keep the first one)
        var slitherIds = currentLevelData.slithers.Select(s => s.id).ToList();
        foreach (var slitherId in slitherIds)
        {
            var matchingHoles = currentLevelData.holes.Where(h => h.slitherId == slitherId).ToList();
            if (matchingHoles.Count > 1)
            {
                // Keep the first hole, remove the rest
                for (int i = 1; i < matchingHoles.Count; i++)
                {
                    Debug.Log($"Removing extra hole at {matchingHoles[i].position} for slither ID: {slitherId}");
                    currentLevelData.holes.Remove(matchingHoles[i]);
                }
            }
        }
    }
    
    private void SaveLevelAs()
    {
        string path = EditorUtility.SaveFilePanel("Save Level JSON", Application.streamingAssetsPath, "level_new.json", "json");
        if (!string.IsNullOrEmpty(path))
        {
            PerformSave(path);
        }
    }
    
    private void SaveCurrentLevel()
    {
        if (string.IsNullOrEmpty(currentPath))
        {
            SaveLevelAs();
        }
        else
        {
            PerformSave(currentPath);
        }
    }

    private void PerformSave(string path)
    {
        // Validate before saving
        string errorMessage;
        if (!currentLevelData.ValidateSlithersHaveHoles(out errorMessage))
        {
            // Show confirmation dialog
            bool proceed = EditorUtility.DisplayDialog(
                "Warning: Invalid Level",
                $"The level has some issues that need to be resolved:\n\n{errorMessage}\n\nDo you still want to save?",
                "Save", "Cancel");

            if (!proceed)
            {
                Debug.Log("Level save cancelled.");
                return;
            }
        }

        if (JsonDataService.Save(currentLevelData, path))
        {
            currentPath = path;
            AssetDatabase.Refresh();
            Debug.Log($"Level saved to: {path}");
        }
        else
        {
            Debug.LogError($"Failed to save level to: {path}");
        }
    }
    
    // Update hole color when snake color changes
    private void UpdateMatchingHole(SlitherColor oldColor, SlitherColor newColor)
    {
        // Find the hole based on the ID of the selected slither
        var matchingHole = currentLevelData.holes.FirstOrDefault(h => h.slitherId == selectedSlither.id);
        if (matchingHole != null)
        {
            matchingHole.color = newColor; // Chỉ cập nhật màu, ID không đổi
            Debug.Log($"Đã cập nhật màu hố từ {oldColor} thành {newColor} cho rắn ID: {selectedSlither.id}");
        }
        else
        {
            // Nếu không tìm thấy hố tương ứng, tạo mới
            Debug.LogWarning($"Không tìm thấy hố tương ứng với rắn ID: {selectedSlither.id}. Tạo hố mới.");
            CreateMatchingHole(newColor, selectedSlither.id);
        }
    }
}
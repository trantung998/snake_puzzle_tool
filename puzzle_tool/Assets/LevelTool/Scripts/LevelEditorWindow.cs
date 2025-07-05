using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class LevelEditorWindow : EditorWindow
{
    // Constants
    private const float BUTTON_SIZE = 40f;
    private const int MIN_GRID_SIZE = 3;
    private const int MAX_GRID_SIZE = 20;
    private const float LAYOUT_WIDTH_RATIO = 0.5f;
    
    private LevelData currentLevelData;
    private string currentPath = "";
    private Vector2 scrollPosition;

    // Tool state
    private enum Tool { None, Slither, Hole, Eraser, Move }
    private Tool currentTool = Tool.None;

    // Tool-specific properties
    private SlitherColor selectedColor = SlitherColor.Red;

    // Grid resize properties
    private int pendingWidth;
    private int pendingHeight;
    private bool hasInitializedGridSize = false;

    // State for painting slithers
    private bool isPaintingSlither = false;
    private List<Vector2Int> currentSlitherPoints = new List<Vector2Int>();
    
    // Inspector state
    private SlitherPlacementData selectedSlither;
    private Vector2Int? currentlyHoveredCell = null;
    
    // State for slither position editing
    private bool isDraggingHandle = false;
    private bool isDraggingHead = false; // true for head, false for tail
    private SlitherPlacementData draggingSlither = null;
    private List<Vector2Int> previewPositions = new List<Vector2Int>();

    [MenuItem("Tools/Slither Level Editor")]
    public static void ShowWindow()
    {
        GetWindow<LevelEditorWindow>("Slither Level Editor");
    }

    private void OnGUI()
    {
        // Handle keyboard shortcuts
        HandleKeyboardInput();
        
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

        // Initialize pending grid size if not done yet
        if (!hasInitializedGridSize)
        {
            pendingWidth = currentLevelData.gridWidth;
            pendingHeight = currentLevelData.gridHeight;
            hasInitializedGridSize = true;
        }

        // --- Phần Tools và Grid/Inspector Layout ---
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        // --- Cột Trái: Tools và Grid ---
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * LAYOUT_WIDTH_RATIO));
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
        
        // Grid Size Controls
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid Size", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Width:", GUILayout.Width(45));
        pendingWidth = EditorGUILayout.IntField(pendingWidth, GUILayout.Width(50));
        EditorGUILayout.LabelField("Height:", GUILayout.Width(50));
        pendingHeight = EditorGUILayout.IntField(pendingHeight, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
        
        // Validate grid size for display but don't clamp the pending values yet
        int validWidth = Mathf.Clamp(pendingWidth, MIN_GRID_SIZE, MAX_GRID_SIZE);
        int validHeight = Mathf.Clamp(pendingHeight, MIN_GRID_SIZE, MAX_GRID_SIZE);
        
        // Show validation info
        bool isValidInput = (pendingWidth == validWidth && pendingHeight == validHeight);
        if (!isValidInput)
        {
            EditorGUILayout.HelpBox($"Values will be clamped to range {MIN_GRID_SIZE}-{MAX_GRID_SIZE}", MessageType.Warning);
        }
        
        // Hiển thị thông tin giới hạn
        EditorGUILayout.LabelField($"Range: {MIN_GRID_SIZE}-{MAX_GRID_SIZE}", EditorStyles.miniLabel);
        
        // Hiển thị nút resize và trạng thái
        bool hasChanges = validWidth != currentLevelData.gridWidth || validHeight != currentLevelData.gridHeight;
        
        EditorGUILayout.BeginHorizontal();
        if (hasChanges)
        {
            GUI.backgroundColor = Color.yellow;
            string buttonText = isValidInput ? 
                $"Resize Grid to {validWidth}x{validHeight}" : 
                $"Resize Grid to {validWidth}x{validHeight} (clamped)";
            
            if (GUILayout.Button(buttonText, GUILayout.Height(25)))
            {
                ResizeGrid(validWidth, validHeight);
                // Update pending values to match actual values after resize
                pendingWidth = validWidth;
                pendingHeight = validHeight;
            }
            GUI.backgroundColor = Color.white;
            
            // Reset button when there are changes
            if (GUILayout.Button("Reset", GUILayout.Width(60), GUILayout.Height(25)))
            {
                pendingWidth = currentLevelData.gridWidth;
                pendingHeight = currentLevelData.gridHeight;
            }
        }
        else
        {
            GUI.enabled = false;
            GUILayout.Button($"Current: {currentLevelData.gridWidth}x{currentLevelData.gridHeight}", GUILayout.Height(25));
            GUI.enabled = true;
        }
        EditorGUILayout.EndHorizontal();
        
        // Help text for shortcuts
        EditorGUILayout.LabelField("Shortcuts: Ctrl+Plus/Minus to resize", EditorStyles.miniLabel);
        
        EditorGUILayout.EndVertical();
    }

    private void DrawGrid()
    {
        EditorGUILayout.LabelField("Grid Editor", EditorStyles.boldLabel);
        
        // Hiển thị thông tin grid hiện tại
        EditorGUILayout.LabelField($"Current Grid: {currentLevelData.gridWidth} x {currentLevelData.gridHeight}", EditorStyles.miniLabel);
        
        // Show instructions for Move tool
        if (currentTool == Tool.Move)
        {
            // Bold and colored instructions to make them more noticeable
            GUIStyle instructionStyle = new GUIStyle(EditorStyles.boldLabel);
            instructionStyle.normal.textColor = Color.blue;
            EditorGUILayout.LabelField("Click the head (◉H) or tail (◉T) handles to move slithers", instructionStyle);
            EditorGUILayout.LabelField("Green checkmarks (✓) show valid positions where you can move", EditorStyles.miniLabel);
        }
        
        float buttonSize = Mathf.Min(BUTTON_SIZE, (position.width * LAYOUT_WIDTH_RATIO - 20) / currentLevelData.gridWidth);

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
                    
                    // Update drag preview if dragging handle
                    if (isDraggingHandle)
                    {
                        UpdateHandleDragPreview(pos);
                    }
                    
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

        // Handle escape key to cancel dragging
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            if (isDraggingHandle)
            {
                CancelHandleDrag();
                Event.current.Use();
            }
        }

        // Only update the stored hovered cell during the Layout event
        if (Event.current.type == EventType.Layout)
        {
            currentlyHoveredCell = localHoveredCell;
        }

        // Show dragging status
        if (isDraggingHandle)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            
            // Create a more attention-grabbing header
            GUIStyle dragHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            dragHeaderStyle.normal.textColor = Color.blue;
            dragHeaderStyle.fontSize = 12;
            
            // Create a custom style for the box
            GUI.backgroundColor = Color.Lerp(Color.white, Color.cyan, 0.2f);
            
            EditorGUILayout.LabelField($"DRAGGING {(isDraggingHead ? "HEAD" : "TAIL")} HANDLE", dragHeaderStyle);
            
            // More informative instructions
            EditorGUILayout.LabelField("• Look for green checkmarks (✓) showing valid destinations", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Click on a green cell to move there", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Press Escape key to cancel", EditorStyles.miniLabel);
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }
        // Hiển thị thông tin chi tiết về ô đang hover
        else if (currentlyHoveredCell.HasValue)
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

        // Kiểm tra position có hợp lệ không
        if (!IsValidPosition(pos))
        {
            return;
        }

        // Handle dragging finish
        if (isDraggingHandle)
        {
            FinishHandleDrag(pos);
            return;
        }

        // Logic chọn Sâu
        var slitherAtPos = currentLevelData.slithers.FirstOrDefault(s => s.bodyPositions.Contains(pos));
        if (slitherAtPos != null && currentTool != Tool.Slither) // Don't select if in slither painting mode
        {
            selectedSlither = slitherAtPos;
            isPaintingSlither = false;
            currentSlitherPoints.Clear();
            
            // Handle Move tool - start dragging if clicking on head or tail
            if (currentTool == Tool.Move)
            {
                int segmentIndex = slitherAtPos.bodyPositions.IndexOf(pos);
                bool isHead = (segmentIndex == 0);
                bool isTail = (segmentIndex == slitherAtPos.bodyPositions.Count - 1);
                
                if (isHead || isTail)
                {
                    StartHandleDrag(slitherAtPos, isHead);
                }
            }
            
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
            case Tool.Move:
                // Move tool doesn't create new objects, only moves existing ones
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
        // Kiểm tra position có hợp lệ không
        if (!IsValidPosition(pos))
        {
            if (isPaintingSlither) FinishSlitherPainting();
            return;
        }
        
        // Không cho vẽ đè lên các đối tượng khác.
        // Sửa lỗi: Bỏ đi phép so sánh không hợp lệ giữa SlitherPlacementData và List<Vector2Int>.
        // Khi đang vẽ, con rắn mới chưa có trong list `slithers` nên chỉ cần kiểm tra sự tồn tại là đủ.
        if (IsPositionOccupied(pos))
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
        // Tìm vị trí trống
        for (int x = 0; x < currentLevelData.gridWidth; x++)
        {
            for (int y = 0; y < currentLevelData.gridHeight; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (IsValidPosition(pos) && !IsPositionOccupied(pos))
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
        
        // Show preview while dragging handle
        if (isDraggingHandle)
        {
            // Show cells that are valid destinations with a highlight
            if (draggingSlither != null)
            {
                // For head dragging, check if the position is adjacent to current head
                if (isDraggingHead && IsAdjacent(pos, draggingSlither.bodyPositions[0]) && 
                    IsValidPosition(pos) && !IsPositionOccupiedByOther(pos, draggingSlither))
                {
                    // Valid destination for head
                    if (!previewPositions.Contains(pos))
                    {
                        text = "✓";
                        return Color.Lerp(Color.green, Color.white, 0.7f);
                    }
                }
                // For tail dragging, check if the position is adjacent to current tail
                else if (!isDraggingHead && IsAdjacent(pos, draggingSlither.bodyPositions[draggingSlither.bodyPositions.Count - 1]) && 
                         IsValidPosition(pos) && !IsPositionOccupiedByOther(pos, draggingSlither))
                {
                    // Valid destination for tail
                    if (!previewPositions.Contains(pos))
                    {
                        text = "✓";
                        return Color.Lerp(Color.green, Color.white, 0.7f);
                    }
                }
            }
            
            // Show actual preview of slither
            if (previewPositions.Contains(pos))
            {
                int index = previewPositions.IndexOf(pos);
                if (index == 0)
                    text = "H"; // Head
                else if (index == previewPositions.Count - 1)
                    text = "T"; // Tail
                else
                    text = (index + 1).ToString();
                
                // Make preview more visible and distinct
                return Color.Lerp(draggingSlither.color.ToUnityColor(), Color.cyan, 0.4f);
            }
        }
        
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
                // Show more visible handle indicator when Move tool is selected and slither is selected
                if (currentTool == Tool.Move && slither == selectedSlither)
                {
                    text = "◉H"; // Handle indicator (larger and more visible)
                }
                else
                {
                    text = "H"; // Regular head indicator
                }
            }
            else if (isTail)
            {
                // Show more visible handle indicator when Move tool is selected and slither is selected
                if (currentTool == Tool.Move && slither == selectedSlither)
                {
                    text = "◉T"; // Handle indicator (larger and more visible)
                }
                else
                {
                    text = "T"; // Regular tail indicator
                }
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
                {
                    // Make handles much more prominent and obvious when in Move mode
                    if (currentTool == Tool.Move)
                    {
                        // Use a pulsating effect for the handles to draw attention
                        float pulse = Mathf.PingPong(Time.realtimeSinceStartup * 2f, 1f);
                        return Color.Lerp(Color.cyan, Color.white, pulse * 0.3f);
                    }
                    else
                        return Color.yellow;
                }
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
        
        // Position editing controls
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Position Controls", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Move Head:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↑", GUILayout.Width(30), GUILayout.Height(25)))
        {
            MoveSlitherHead(Vector2Int.up);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("←", GUILayout.Width(30), GUILayout.Height(25)))
        {
            MoveSlitherHead(Vector2Int.left);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("→", GUILayout.Width(30), GUILayout.Height(25)))
        {
            MoveSlitherHead(Vector2Int.right);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↓", GUILayout.Width(30), GUILayout.Height(25)))
        {
            MoveSlitherHead(Vector2Int.down);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Move Tail:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↑", GUILayout.Width(30), GUILayout.Height(25)))
        {
            MoveSlitherTail(Vector2Int.up);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("←", GUILayout.Width(30), GUILayout.Height(25)))
        {
            MoveSlitherTail(Vector2Int.left);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("→", GUILayout.Width(30), GUILayout.Height(25)))
        {
            MoveSlitherTail(Vector2Int.right);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↓", GUILayout.Width(30), GUILayout.Height(25)))
        {
            MoveSlitherTail(Vector2Int.down);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.LabelField("Tip: Use Move tool to drag handles directly on grid", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
        
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
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Snake Management", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical("box");
        
        // Length increase controls
        EditorGUILayout.LabelField("Increase Length:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Add Head Segment", GUILayout.Height(25)))
        {
            AddSlitherSegment(true);
        }
        
        if (GUILayout.Button("Add Tail Segment", GUILayout.Height(25)))
        {
            AddSlitherSegment(false);
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Keyboard: Insert = Add Head, Page Down = Add Tail", EditorStyles.miniLabel);
        
        EditorGUILayout.Space();
        
        // Length reduction controls
        EditorGUILayout.LabelField("Reduce Length:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        
        bool disableSegmentButtons = selectedSlither.bodyPositions.Count <= 2;
        EditorGUI.BeginDisabledGroup(disableSegmentButtons);
        
        if (GUILayout.Button("Remove Head Segment", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Remove Head Segment", 
                "Are you sure you want to remove the head segment of this snake?", "Yes", "Cancel"))
            {
                RemoveSlitherSegment(true);
            }
        }
        
        if (GUILayout.Button("Remove Tail Segment", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Remove Tail Segment", 
                "Are you sure you want to remove the tail segment of this snake?", "Yes", "Cancel"))
            {
                RemoveSlitherSegment(false);
            }
        }
        
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        
        if (disableSegmentButtons)
        {
            EditorGUILayout.HelpBox("Cannot reduce length: snake is already at minimum length (2 segments).", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("Keyboard: Home = Remove Head, End = Remove Tail", EditorStyles.miniLabel);
        }
        
        EditorGUILayout.Space();
        
        // Delete snake control
        EditorGUILayout.LabelField("Delete Snake:", EditorStyles.miniLabel);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        Color defaultBgColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f); // Red color for delete button
        
        if (GUILayout.Button("Delete Entire Snake", GUILayout.Height(30), GUILayout.Width(200)))
        {
            if (EditorUtility.DisplayDialog("Delete Snake", 
                "Are you sure you want to delete this snake and its matching hole?\nThis action cannot be undone!", "Delete", "Cancel"))
            {
                DeleteSelectedSlither();
            }
        }
        
        GUI.backgroundColor = defaultBgColor;
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.LabelField("Keyboard: Shift+Delete = Delete Snake", EditorStyles.miniLabel);
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
    }

    // --- FILE MANAGEMENT ---

    private void CreateNewLevel()
    {
        currentLevelData = new LevelData();
        currentPath = "";
        selectedSlither = null;
        
        // Reset grid size pending values
        pendingWidth = currentLevelData.gridWidth;
        pendingHeight = currentLevelData.gridHeight;
        hasInitializedGridSize = true;
        
        // Reset all editing states
        isPaintingSlither = false;
        currentSlitherPoints.Clear();
        CancelHandleDrag();
        
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
            
            // Reset grid size pending values
            pendingWidth = currentLevelData.gridWidth;
            pendingHeight = currentLevelData.gridHeight;
            hasInitializedGridSize = true;
            
            // Reset all editing states
            isPaintingSlither = false;
            currentSlitherPoints.Clear();
            CancelHandleDrag();
            
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
    
    // --- GRID RESIZING ---
    
    private void ResizeGrid(int newWidth, int newHeight)
    {
        // Nếu kích thước không thay đổi, không cần làm gì
        if (newWidth == currentLevelData.gridWidth && newHeight == currentLevelData.gridHeight)
        {
            Debug.Log("Grid size unchanged.");
            return;
        }
        
        // Kiểm tra xem có đối tượng nào sẽ bị ảnh hưởng không
        List<string> affectedItems = GetAffectedItemsByResize(newWidth, newHeight);
        
        bool proceed = true;
        if (affectedItems.Count > 0)
        {
            string message = $"Resizing the grid from {currentLevelData.gridWidth}x{currentLevelData.gridHeight} to {newWidth}x{newHeight} will affect the following items:\n\n{string.Join("\n", affectedItems)}\n\nThese items will be removed. Do you want to continue?";
            proceed = EditorUtility.DisplayDialog(
                "Grid Resize Warning",
                message,
                "Continue", "Cancel");
        }
        else
        {
            // Không có items bị ảnh hưởng, chỉ cần confirm đơn giản
            proceed = EditorUtility.DisplayDialog(
                "Confirm Grid Resize",
                $"Resize grid from {currentLevelData.gridWidth}x{currentLevelData.gridHeight} to {newWidth}x{newHeight}?",
                "Yes", "Cancel");
        }
        
        if (proceed)
        {
            // Thực hiện resize
            int oldWidth = currentLevelData.gridWidth;
            int oldHeight = currentLevelData.gridHeight;
            
            currentLevelData.gridWidth = newWidth;
            currentLevelData.gridHeight = newHeight;
            
            // Loại bỏ các đối tượng nằm ngoài grid mới (nếu có)
            if (affectedItems.Count > 0)
            {
                RemoveItemsOutsideGrid();
            }
            
            // Reset selection nếu slither đã chọn bị xóa
            if (selectedSlither != null && !currentLevelData.slithers.Contains(selectedSlither))
            {
                selectedSlither = null;
            }
            
            // Reset painting state
            isPaintingSlither = false;
            currentSlitherPoints.Clear();
            
            Debug.Log($"Grid resized from {oldWidth}x{oldHeight} to {newWidth}x{newHeight}. Affected items: {affectedItems.Count}");
            Repaint();
        }
    }
    
    private List<string> GetAffectedItemsByResize(int newWidth, int newHeight)
    {
        List<string> affectedItems = new List<string>();
        
        // Kiểm tra holes
        foreach (var hole in currentLevelData.holes)
        {
            if (hole.position.x >= newWidth || hole.position.y >= newHeight)
            {
                affectedItems.Add($"Hole at ({hole.position.x}, {hole.position.y}) - Color: {hole.color}");
            }
        }
        
        // Kiểm tra slithers
        for (int i = 0; i < currentLevelData.slithers.Count; i++)
        {
            var slither = currentLevelData.slithers[i];
            var invalidPositions = slither.bodyPositions.Where(pos => 
                pos.x >= newWidth || pos.y >= newHeight).ToList();
                
            if (invalidPositions.Count > 0)
            {
                if (invalidPositions.Count == slither.bodyPositions.Count)
                {
                    // Toàn bộ rắn bị ảnh hưởng
                    affectedItems.Add($"Slither #{i + 1} (Color: {slither.color}) - Entire slither");
                }
                else
                {
                    // Một phần rắn bị ảnh hưởng
                    string positions = string.Join(", ", invalidPositions.Select(p => $"({p.x},{p.y})"));
                    affectedItems.Add($"Slither #{i + 1} (Color: {slither.color}) - Segments at: {positions}");
                }
            }
        }
        
        return affectedItems;
    }
    
    private void RemoveItemsOutsideGrid()
    {
        // Loại bỏ holes nằm ngoài grid
        currentLevelData.holes.RemoveAll(hole => 
            hole.position.x >= currentLevelData.gridWidth || 
            hole.position.y >= currentLevelData.gridHeight);
        
        // Xử lý slithers
        for (int i = currentLevelData.slithers.Count - 1; i >= 0; i--)
        {
            var slither = currentLevelData.slithers[i];
            
            // Loại bỏ các segments nằm ngoài grid
            slither.bodyPositions.RemoveAll(pos => 
                pos.x >= currentLevelData.gridWidth || 
                pos.y >= currentLevelData.gridHeight);
            
            // Nếu rắn không còn segments nào, xóa toàn bộ rắn và hole tương ứng
            if (slither.bodyPositions.Count == 0)
            {
                // Xóa hole tương ứng
                currentLevelData.holes.RemoveAll(h => h.slitherId == slither.id);
                currentLevelData.slithers.RemoveAt(i);
            }
            else if (slither.bodyPositions.Count == 1)
            {
                // Nếu chỉ còn 1 segment, cũng xóa vì rắn cần ít nhất 2 segments
                currentLevelData.holes.RemoveAll(h => h.slitherId == slither.id);
                currentLevelData.slithers.RemoveAt(i);
            }
        }
    }
    
    // --- UTILITY METHODS ---
    
    private bool IsValidPosition(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < currentLevelData.gridWidth && 
               pos.y >= 0 && pos.y < currentLevelData.gridHeight;
    }
    
    private bool IsPositionOccupied(Vector2Int pos)
    {
        return currentLevelData.holes.Any(h => h.position == pos) ||
               currentLevelData.slithers.Any(s => s.bodyPositions.Contains(pos));
    }
    
    // --- KEYBOARD INPUT ---
    
    private void HandleKeyboardInput()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown && currentLevelData != null)
        {
            // Shortcuts for quick grid resizing
            if (e.control)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Plus:
                    case KeyCode.KeypadPlus:
                        // Tăng kích thước grid
                        int newWidth = Mathf.Min(currentLevelData.gridWidth + 1, MAX_GRID_SIZE);
                        int newHeight = Mathf.Min(currentLevelData.gridHeight + 1, MAX_GRID_SIZE);
                        if (newWidth != currentLevelData.gridWidth || newHeight != currentLevelData.gridHeight)
                        {
                            ResizeGrid(newWidth, newHeight);
                            // Update pending values
                            pendingWidth = newWidth;
                            pendingHeight = newHeight;
                            e.Use();
                        }
                        break;
                        
                    case KeyCode.Minus:
                    case KeyCode.KeypadMinus:
                        // Giảm kích thước grid
                        int shrinkWidth = Mathf.Max(currentLevelData.gridWidth - 1, MIN_GRID_SIZE);
                        int shrinkHeight = Mathf.Max(currentLevelData.gridHeight - 1, MIN_GRID_SIZE);
                        if (shrinkWidth != currentLevelData.gridWidth || shrinkHeight != currentLevelData.gridHeight)
                        {
                            ResizeGrid(shrinkWidth, shrinkHeight);
                            // Update pending values
                            pendingWidth = shrinkWidth;
                            pendingHeight = shrinkHeight;
                            e.Use();
                        }
                        break;
                }
            }
            
            // Process additional keyboard shortcuts for snake management
            ProcessKeyboardShortcuts();
        }
    }
    
    // --- SLITHER POSITION EDITING ---
    
    private void StartHandleDrag(SlitherPlacementData slither, bool isDraggingHeadHandle)
    {
        isDraggingHandle = true;
        isDraggingHead = isDraggingHeadHandle;
        draggingSlither = slither;
        
        // Initialize preview with current positions
        previewPositions = new List<Vector2Int>(slither.bodyPositions);
        
        string handleType = isDraggingHead ? "head" : "tail";
        string slitherInfo = $"#{currentLevelData.slithers.IndexOf(slither) + 1} (Color: {slither.color})";
        Debug.Log($"Started dragging {handleType} of slither {slitherInfo}");
        
        Repaint();
    }
    
    private void FinishHandleDrag(Vector2Int newPos)
    {
        if (!isDraggingHandle || draggingSlither == null)
            return;
            
        // Validate the new position
        if (!IsValidPosition(newPos) || IsPositionOccupiedByOther(newPos, draggingSlither))
        {
            Debug.LogWarning("Cannot move handle to invalid or occupied position");
            CancelHandleDrag();
            return;
        }
        
        // Apply the movement
        if (isDraggingHead)
        {
            // Check if new head position is adjacent to current head
            Vector2Int currentHead = draggingSlither.bodyPositions[0];
            if (IsAdjacent(newPos, currentHead))
            {
                // Store length for feedback message
                int oldLength = draggingSlither.bodyPositions.Count;
                
                // Add new head segment
                draggingSlither.bodyPositions.Insert(0, newPos);
                
                // Get the direction of movement for logging
                string direction = GetDirectionName(newPos - currentHead);
                
                // Remove tail segment to maintain length (optional behavior)
                if (draggingSlither.bodyPositions.Count > 3) // Keep minimum length
                {
                    Vector2Int removedTail = draggingSlither.bodyPositions[draggingSlither.bodyPositions.Count - 1];
                    draggingSlither.bodyPositions.RemoveAt(draggingSlither.bodyPositions.Count - 1);
                    
                    // Log the change instead of showing dialog
                    Debug.Log($"Head moved {direction} to ({newPos.x}, {newPos.y}). Tail segment at ({removedTail.x}, {removedTail.y}) was removed to maintain length.");
                }
                else
                {
                    // Log the change instead of showing dialog
                    Debug.Log($"Head moved {direction} to ({newPos.x}, {newPos.y}). Slither length increased from {oldLength} to {draggingSlither.bodyPositions.Count}.");
                }
            }
            else
            {
                Debug.LogWarning("New head position must be adjacent to current head");
                CancelHandleDrag();
                return;
            }
        }
        else
        {
            // Dragging tail
            Vector2Int currentTail = draggingSlither.bodyPositions[draggingSlither.bodyPositions.Count - 1];
            if (IsAdjacent(newPos, currentTail))
            {
                // Store length for feedback message
                int oldLength = draggingSlither.bodyPositions.Count;
                
                // Add new tail segment
                draggingSlither.bodyPositions.Add(newPos);
                
                // Get the direction of movement for logging
                string direction = GetDirectionName(newPos - currentTail);
                
                // Remove head segment to maintain length (optional behavior)
                if (draggingSlither.bodyPositions.Count > 3) // Keep minimum length
                {
                    Vector2Int removedHead = draggingSlither.bodyPositions[0];
                    draggingSlither.bodyPositions.RemoveAt(0);
                    
                    // Log the change instead of showing dialog
                    Debug.Log($"Tail moved {direction} to ({newPos.x}, {newPos.y}). Head segment at ({removedHead.x}, {removedHead.y}) was removed to maintain length.");
                }
                else
                {
                    // Log the change instead of showing dialog
                    Debug.Log($"Tail moved {direction} to ({newPos.x}, {newPos.y}). Slither length increased from {oldLength} to {draggingSlither.bodyPositions.Count}.");
                }
            }
            else
            {
                Debug.LogWarning("New tail position must be adjacent to current tail");
                CancelHandleDrag();
                return;
            }
        }
        
        Debug.Log($"Successfully moved {(isDraggingHead ? "head" : "tail")} to {newPos}");
        CancelHandleDrag();
    }
    
    private void CancelHandleDrag()
    {
        if (isDraggingHandle)
        {
            // Show feedback when drag is cancelled
            Debug.Log("Handle drag operation cancelled");
        }
        
        isDraggingHandle = false;
        isDraggingHead = false;
        draggingSlither = null;
        previewPositions.Clear();
        Repaint();
    }
    
    private void UpdateHandleDragPreview(Vector2Int mousePos)
    {
        if (!isDraggingHandle || draggingSlither == null)
            return;
            
        // Validate the position
        if (!IsValidPosition(mousePos) || IsPositionOccupiedByOther(mousePos, draggingSlither))
        {
            // Show invalid preview (optional: use current positions)
            previewPositions = new List<Vector2Int>(draggingSlither.bodyPositions);
            return;
        }
        
        // Calculate preview positions
        previewPositions = new List<Vector2Int>(draggingSlither.bodyPositions);
        
        if (isDraggingHead)
        {
            Vector2Int currentHead = draggingSlither.bodyPositions[0];
            if (IsAdjacent(mousePos, currentHead))
            {
                previewPositions.Insert(0, mousePos);
                if (previewPositions.Count > 3) // Maintain length
                {
                    previewPositions.RemoveAt(previewPositions.Count - 1);
                }
            }
        }
        else
        {
            Vector2Int currentTail = draggingSlither.bodyPositions[draggingSlither.bodyPositions.Count - 1];
            if (IsAdjacent(mousePos, currentTail))
            {
                previewPositions.Add(mousePos);
                if (previewPositions.Count > 3) // Maintain length
                {
                    previewPositions.RemoveAt(0);
                }
            }
        }
    }
    
    private bool IsAdjacent(Vector2Int pos1, Vector2Int pos2)
    {
        int dx = Mathf.Abs(pos1.x - pos2.x);
        int dy = Mathf.Abs(pos1.y - pos2.y);
        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
    }
    
    private bool IsPositionOccupiedByOther(Vector2Int pos, SlitherPlacementData excludeSlither)
    {
        // Check holes
        if (currentLevelData.holes.Any(h => h.position == pos))
            return true;
            
        // Check other slithers
        foreach (var slither in currentLevelData.slithers)
        {
            if (slither != excludeSlither && slither.bodyPositions.Contains(pos))
                return true;
        }
        
        return false;
    }

    private void MoveSlitherHead(Vector2Int direction)
    {
        if (selectedSlither == null) return;
        
        Vector2Int currentHead = selectedSlither.bodyPositions[0];
        Vector2Int newHead = currentHead + direction;
        
        // Validate new position
        if (!IsValidPosition(newHead))
        {
            Debug.LogWarning("Cannot move head: position out of bounds");
            return;
        }
        
        if (IsPositionOccupiedByOther(newHead, selectedSlither))
        {
            Debug.LogWarning("Cannot move head: position occupied");
            return;
        }
        
        // Check if moving to its own body (except tail which will be removed)
        if (selectedSlither.bodyPositions.Take(selectedSlither.bodyPositions.Count - 1).Contains(newHead))
        {
            Debug.LogWarning("Cannot move head: would overlap with own body");
            return;
        }
        
        // Add new head
        selectedSlither.bodyPositions.Insert(0, newHead);
        
        // Remove tail to maintain length (keep minimum of 2 segments)
        if (selectedSlither.bodyPositions.Count > 2)
        {
            selectedSlither.bodyPositions.RemoveAt(selectedSlither.bodyPositions.Count - 1);
        }
        
        Repaint();
    }
    
    private void MoveSlitherTail(Vector2Int direction)
    {
        if (selectedSlither == null) return;
        
        Vector2Int currentTail = selectedSlither.bodyPositions[selectedSlither.bodyPositions.Count - 1];
        Vector2Int newTail = currentTail + direction;
        
        // Validate new position
        if (!IsValidPosition(newTail))
        {
            Debug.LogWarning("Cannot move tail: position out of bounds");
            return;
        }
        
        if (IsPositionOccupiedByOther(newTail, selectedSlither))
        {
            Debug.LogWarning("Cannot move tail: position occupied");
            return;
        }
        
        // Check if moving to its own body (except head which will be removed)
        if (selectedSlither.bodyPositions.Skip(1).Contains(newTail))
        {
            Debug.LogWarning("Cannot move tail: would overlap with own body");
            return;
        }
        
        // Add new tail
        selectedSlither.bodyPositions.Add(newTail);
        
        // Remove head to maintain length (keep minimum of 2 segments)
        if (selectedSlither.bodyPositions.Count > 2)
        {
            selectedSlither.bodyPositions.RemoveAt(0);
        }
        
        Repaint();
    }

    private string GetDirectionName(Vector2Int direction)
    {
        if (direction.x == 1 && direction.y == 0) return "right";
        if (direction.x == -1 && direction.y == 0) return "left";
        if (direction.x == 0 && direction.y == 1) return "up";
        if (direction.x == 0 && direction.y == -1) return "down";
        
        return "in a new direction"; // Fallback for unexpected cases
    }

    private void DeleteSelectedSlither()
    {
        if (selectedSlither == null) return;
        
        // Find matching hole to delete as well
        var matchingHole = currentLevelData.holes.FirstOrDefault(h => h.slitherId == selectedSlither.id);
        if (matchingHole != null)
        {
            currentLevelData.holes.Remove(matchingHole);
        }
        
        // Remove the slither
        currentLevelData.slithers.Remove(selectedSlither);
        selectedSlither = null;
        
        Repaint();
    }
    
    private void RemoveSlitherSegment(bool removeHead)
    {
        if (selectedSlither == null) return;
        
        // Can't reduce below minimum length of 2 segments
        if (selectedSlither.bodyPositions.Count <= 2)
        {
            Debug.LogWarning("Cannot reduce length: snake already at minimum length (2 segments)");
            return;
        }
        
        if (removeHead)
        {
            // Remove head (first segment)
            selectedSlither.bodyPositions.RemoveAt(0);
        }
        else
        {
            // Remove tail (last segment)
            selectedSlither.bodyPositions.RemoveAt(selectedSlither.bodyPositions.Count - 1);
        }
        
        Repaint();
    }
    
    private void AddSlitherSegment(bool addToHead)
    {
        if (selectedSlither == null) return;
        
        Vector2Int newPosition;
        
        if (addToHead)
        {
            // Add segment to head
            Vector2Int currentHead = selectedSlither.bodyPositions[0];
            Vector2Int direction = Vector2Int.zero;
            
            // Try to determine direction based on the second segment
            if (selectedSlither.bodyPositions.Count > 1)
            {
                Vector2Int secondSegment = selectedSlither.bodyPositions[1];
                direction = currentHead - secondSegment; // Direction from second to head
            }
            else
            {
                // Default direction if only one segment
                direction = Vector2Int.up;
            }
            
            newPosition = currentHead + direction;
        }
        else
        {
            // Add segment to tail
            Vector2Int currentTail = selectedSlither.bodyPositions[selectedSlither.bodyPositions.Count - 1];
            Vector2Int direction = Vector2Int.zero;
            
            // Try to determine direction based on the second-to-last segment
            if (selectedSlither.bodyPositions.Count > 1)
            {
                Vector2Int secondLastSegment = selectedSlither.bodyPositions[selectedSlither.bodyPositions.Count - 2];
                direction = currentTail - secondLastSegment; // Direction from second-to-last to tail
            }
            else
            {
                // Default direction if only one segment
                direction = Vector2Int.down;
            }
            
            newPosition = currentTail + direction;
        }
        
        // Validate new position
        if (!IsValidPosition(newPosition))
        {
            Debug.LogWarning($"Cannot add segment: position ({newPosition.x}, {newPosition.y}) is out of bounds");
            return;
        }
        
        if (IsPositionOccupiedByOther(newPosition, selectedSlither))
        {
            Debug.LogWarning($"Cannot add segment: position ({newPosition.x}, {newPosition.y}) is occupied");
            return;
        }
        
        // Check if the new position would overlap with the snake's own body
        if (selectedSlither.bodyPositions.Contains(newPosition))
        {
            Debug.LogWarning($"Cannot add segment: position ({newPosition.x}, {newPosition.y}) overlaps with snake's own body");
            return;
        }
        
        // Add the segment
        if (addToHead)
        {
            selectedSlither.bodyPositions.Insert(0, newPosition);
            Debug.Log($"Added head segment at ({newPosition.x}, {newPosition.y}). Snake length increased to {selectedSlither.bodyPositions.Count}.");
        }
        else
        {
            selectedSlither.bodyPositions.Add(newPosition);
            Debug.Log($"Added tail segment at ({newPosition.x}, {newPosition.y}). Snake length increased to {selectedSlither.bodyPositions.Count}.");
        }
        
        Repaint();
    }

    private void ProcessKeyboardShortcuts()
    {
        Event e = Event.current;
        
        // Only process keyboard events
        if (e.type != EventType.KeyDown) return;
        
        // Grid resizing shortcuts
        if (e.control && e.keyCode == KeyCode.Plus || e.control && e.keyCode == KeyCode.KeypadPlus)
        {
            // Ctrl+Plus: Increase grid size
            pendingWidth = Mathf.Min(pendingWidth + 1, 20);
            pendingHeight = Mathf.Min(pendingHeight + 1, 20);
            Repaint();
            e.Use();
        }
        else if (e.control && e.keyCode == KeyCode.Minus || e.control && e.keyCode == KeyCode.KeypadMinus)
        {
            // Ctrl+Minus: Decrease grid size
            pendingWidth = Mathf.Max(pendingWidth - 1, 3);
            pendingHeight = Mathf.Max(pendingHeight - 1, 3);
            Repaint();
            e.Use();
        }
        
        // Snake editing shortcuts (only if a snake is selected)
        if (selectedSlither != null)
        {
            // Delete entire snake with Delete key + Shift
            if (e.shift && e.keyCode == KeyCode.Delete)
            {
                if (EditorUtility.DisplayDialog("Delete Snake", 
                    "Are you sure you want to delete this snake and its matching hole?\nThis action cannot be undone!", "Delete", "Cancel"))
                {
                    DeleteSelectedSlither();
                    e.Use();
                }
            }
            // Remove head segment with Home key
            else if (e.keyCode == KeyCode.Home && selectedSlither.bodyPositions.Count > 2)
            {
                if (EditorUtility.DisplayDialog("Remove Head Segment", 
                    "Are you sure you want to remove the head segment of this snake?", "Yes", "Cancel"))
                {
                    RemoveSlitherSegment(true);
                    e.Use();
                }
            }
            // Remove tail segment with End key
            else if (e.keyCode == KeyCode.End && selectedSlither.bodyPositions.Count > 2)
            {
                if (EditorUtility.DisplayDialog("Remove Tail Segment", 
                    "Are you sure you want to remove the tail segment of this snake?", "Yes", "Cancel"))
                {
                    RemoveSlitherSegment(false);
                    e.Use();
                }
            }
            // Add head segment with Insert key
            else if (e.keyCode == KeyCode.Insert)
            {
                AddSlitherSegment(true);
                e.Use();
            }
            // Add tail segment with Page Down key
            else if (e.keyCode == KeyCode.PageDown)
            {
                AddSlitherSegment(false);
                e.Use();
            }
            // Escape key to cancel drag
            else if (e.keyCode == KeyCode.Escape && isDraggingHandle)
            {
                CancelHandleDrag();
                e.Use();
            }
        }
    }
}
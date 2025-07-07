using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class LevelEditorWindow : EditorWindow
{
    // Constants
    private const float LAYOUT_WIDTH_RATIO = 0.5f;
    private const string LAST_LEVEL_PATH_KEY = "SlitherLevelEditor_LastLevelPath";

    // Core components
    private LevelEditorGrid gridRenderer;
    private LevelEditorInspector inspector;
    private LevelEditorToolbar toolbar;

    // Data and state
    private LevelData currentLevelData;
    private string currentPath = "";
    private Vector2 scrollPosition;

    // Component initialization tracking
    private bool needsComponentReinitialization = false;

    // State management
    private SlitherPlacementData selectedSlither;
    private Vector2Int? currentlyHoveredCell = null;

    // State for painting slithers
    private bool isPaintingSlither = false;
    private List<Vector2Int> currentSlitherPoints = new List<Vector2Int>();

    // State for slither position editing
    private bool isDraggingHandle = false;
    private bool isDraggingHead = false; // true for head, false for tail
    private SlitherPlacementData draggingSlither = null;
    private List<Vector2Int> previewPositions = new List<Vector2Int>();

    // State for hole dragging
    private bool isDraggingHole = false;
    private HolePlacementData draggingHole = null;
    private Vector2Int originalHolePosition;
    private Vector2Int? previewHolePosition = null;

    // Level browser state
    private List<string> availableLevelPaths = new List<string>();
    private Vector2 levelBrowserScrollPosition;
    private float lastLevelScanTime = 0f;
    private const float LEVEL_SCAN_INTERVAL = 2f; // Scan every 2 seconds

    [MenuItem("Tools/Slither Level Editor")]
    public static void ShowWindow()
    {
        GetWindow<LevelEditorWindow>("Slither Level Editor");
    }

    private void OnEnable()
    {
        // Initialize components
        gridRenderer = new LevelEditorGrid();
        inspector = new LevelEditorInspector();
        toolbar = new LevelEditorToolbar();

        // Subscribe to events
        SetupEventHandlers();

        // Attempt to load the last opened level
        string lastPath = EditorPrefs.GetString(LAST_LEVEL_PATH_KEY, "");
        if (!string.IsNullOrEmpty(lastPath))
        {
            LoadLevelFromPath(lastPath);
        }

        // Initialize level browser
        ScanForAvailableLevels();
    }

    private void OnDisable()
    {
        // Clean up event handlers
        CleanupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        // Grid events
        if (gridRenderer != null)
        {
            gridRenderer.OnCellClicked += HandleGridClick;
            gridRenderer.OnCellHovered += HandleCellHovered;
            gridRenderer.OnCellRightClicked += HandleGridRightClick;
            gridRenderer.OnHoleDragStarted += HandleHoleDragStarted;
            gridRenderer.OnHoleDragUpdated += HandleHoleDragUpdated;
            gridRenderer.OnHoleDragEnded += HandleHoleDragEnded;
        }

        // Toolbar events
        if (toolbar != null)
        {
            toolbar.OnNewLevel += CreateNewLevel;
            toolbar.OnLoadLevel += LoadLevel;
            toolbar.OnSaveLevel += SaveCurrentLevel;
            toolbar.OnSaveAsLevel += SaveLevelAs;
            toolbar.OnToolChanged += HandleToolChanged;
            toolbar.OnColorChanged += HandleColorChanged;
            toolbar.OnGridResizeRequested += ResizeGrid;
        }        // Inspector events
        if (inspector != null)
        {
            inspector.OnSlitherColorChanged += UpdateMatchingHole;
            inspector.OnSlitherDeleted += DeleteSelectedSlither;
            inspector.OnSegmentRemoved += RemoveSlitherSegment;
            inspector.OnSegmentAdded += AddSlitherSegment;
            inspector.OnInteractorAdded += HandleInteractorAdded;
            inspector.OnInteractorRemoved += HandleInteractorRemoved;
            inspector.OnMoveHead += MoveSlitherHead;
            inspector.OnMoveTail += MoveSlitherTail;
        }
    }

    private void CleanupEventHandlers()
    {
        // Grid events cleanup
        if (gridRenderer != null)
        {
            gridRenderer.OnCellClicked -= HandleGridClick;
            gridRenderer.OnCellHovered -= HandleCellHovered;
            gridRenderer.OnCellRightClicked -= HandleGridRightClick;
            gridRenderer.OnHoleDragStarted -= HandleHoleDragStarted;
            gridRenderer.OnHoleDragUpdated -= HandleHoleDragUpdated;
            gridRenderer.OnHoleDragEnded -= HandleHoleDragEnded;
        }

        // Toolbar events
        if (toolbar != null)
        {
            toolbar.OnNewLevel -= CreateNewLevel;
            toolbar.OnLoadLevel -= LoadLevel;
            toolbar.OnSaveLevel -= SaveCurrentLevel;
            toolbar.OnSaveAsLevel -= SaveLevelAs;
            toolbar.OnToolChanged -= HandleToolChanged;
            toolbar.OnColorChanged -= HandleColorChanged;
            toolbar.OnGridResizeRequested -= ResizeGrid;
        }

        // Inspector events
        if (inspector != null)
        {
            inspector.OnSlitherColorChanged -= UpdateMatchingHole;
            inspector.OnSlitherDeleted -= DeleteSelectedSlither;
            inspector.OnSegmentRemoved -= RemoveSlitherSegment;
            inspector.OnSegmentAdded -= AddSlitherSegment;
            inspector.OnInteractorAdded -= HandleInteractorAdded;
            inspector.OnInteractorRemoved -= HandleInteractorRemoved;
            inspector.OnMoveHead -= MoveSlitherHead;
            inspector.OnMoveTail -= MoveSlitherTail;
        }
    }

    private void OnGUI()
    {
        // Handle keyboard shortcuts
        HandleKeyboardInput();

        // Draw level editor interface
        if (currentLevelData == null)
        {
            EditorGUILayout.HelpBox("Load a level file or create a new one to begin.", MessageType.Info);
            
            // Draw level browser when no level is loaded
            DrawLevelBrowser();
            
            if (GUILayout.Button("Create New Level"))
            {
                CreateNewLevel();
            }
            return;
        }

        // Initialize components with current data (only when needed)
        if (needsComponentReinitialization || gridRenderer == null || toolbar == null)
        {
            if (gridRenderer != null)
            {
                gridRenderer.Initialize(currentLevelData);
            }
            if (toolbar != null)
            {
                toolbar.Initialize(currentLevelData);
            }
            needsComponentReinitialization = false;
        }

        // Enhanced file toolbar with better styling
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(25));

        // File operations with icons
        if (GUILayout.Button("📄 New", EditorStyles.toolbarButton, GUILayout.Width(60))) CreateNewLevel();
        if (GUILayout.Button("📁 Load", EditorStyles.toolbarButton, GUILayout.Width(60))) LoadLevel();

        // File name display with enhanced styling
        string fileName = string.IsNullOrEmpty(currentPath) ? "Untitled" : Path.GetFileName(currentPath);
        GUIStyle fileNameStyle = new GUIStyle(EditorStyles.toolbarTextField);
        fileNameStyle.fontStyle = FontStyle.Bold;
        GUILayout.TextField($"📝 {fileName}", fileNameStyle, GUILayout.ExpandWidth(true));

        if (GUILayout.Button("💾 Save", EditorStyles.toolbarButton, GUILayout.Width(60))) SaveCurrentLevel();
        if (GUILayout.Button("💾 Save As...", EditorStyles.toolbarButton, GUILayout.Width(80))) SaveLevelAs();

        EditorGUILayout.EndHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Main layout
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        // Left column: Tools and Grid
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * LAYOUT_WIDTH_RATIO));

        // Toolbar section
        if (toolbar != null)
        {
            toolbar.DrawToolbar(currentLevelData);
        }

        EditorGUILayout.Space(5);

        // Enhanced grid section with better visual hierarchy
        GUIStyle gridHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
        gridHeaderStyle.fontSize = 14;
        EditorGUILayout.LabelField("🎯 Grid Editor", gridHeaderStyle);

        GUIStyle gridInfoStyle = new GUIStyle(EditorStyles.miniLabel);
        gridInfoStyle.normal.textColor = Color.gray;
        EditorGUILayout.LabelField($"📐 Current Grid: {currentLevelData.gridWidth} x {currentLevelData.gridHeight}", gridInfoStyle);

        EditorGUILayout.Space(3);

        // Calculate grid area for rendering
        Rect gridRect = GUILayoutUtility.GetRect(0, 400, GUILayout.ExpandWidth(true));
        if (gridRenderer != null)
        {
            // Update painting preview
            if (isPaintingSlither)
            {
                gridRenderer.SetPaintingPositions(currentSlitherPoints);
            }
            else
            {
                gridRenderer.ClearPaintingPositions();
            }

            gridRenderer.DrawGrid(gridRect, selectedSlither, isDraggingHandle, isDraggingHead, draggingSlither, previewPositions, isDraggingHole, previewHolePosition);
        }

        // Grid overlay information
        if (gridRenderer != null)
        {
            gridRenderer.DrawOverlay();
        }

        EditorGUILayout.EndVertical();

        // Right column: Inspector
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));

        if (inspector != null)
        {
            Vector2Int? hoveredCell = gridRenderer?.GetHoveredCell();
            inspector.DrawInspector(currentLevelData, selectedSlither, hoveredCell);
        }
        else
        {
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Inspector component failed to initialize. Please restart the Level Editor.", MessageType.Error);

            if (GUILayout.Button("Reinitialize Inspector"))
            {
                inspector = new LevelEditorInspector();
                SetupEventHandlers();
                Debug.Log("Inspector reinitialized");
            }
        }

        EditorGUILayout.Space();
        
        // Add Level Browser at the bottom of the inspector
        DrawLevelBrowserCompact();

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();

        // Enhanced status bar for better information display
        DrawStatusBar();
    }

    /// <summary>
    /// Draw an enhanced status bar with current tool, color, and level information
    /// </summary>
    private void DrawStatusBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Current tool information with icon
        var currentTool = toolbar?.GetCurrentTool() ?? LevelEditorToolbar.Tool.None;
        string toolIcon = GetToolIcon(currentTool);
        EditorGUILayout.LabelField($"{toolIcon} Tool: {currentTool}", GUILayout.Width(120));

        EditorGUILayout.LabelField("|", GUILayout.Width(10)); // Separator

        // Current color for relevant tools
        if (currentTool == LevelEditorToolbar.Tool.Slither || currentTool == LevelEditorToolbar.Tool.Hole)
        {
            var selectedColor = toolbar?.GetSelectedColor() ?? SlitherColor.Red;
            Color colorPreview = GetEnhancedSlitherColor(selectedColor);

            // Create a small colored rect to show the current color
            Rect colorRect = GUILayoutUtility.GetRect(15, 15, GUILayout.Width(15), GUILayout.Height(15));
            EditorGUI.DrawRect(colorRect, colorPreview);

            EditorGUILayout.LabelField($"Color: {selectedColor}", GUILayout.Width(100));
            EditorGUILayout.LabelField("|", GUILayout.Width(10)); // Separator
        }

        // Level statistics
        if (currentLevelData != null)
        {
            EditorGUILayout.LabelField($"🐍 Slithers: {currentLevelData.slithers.Count}", GUILayout.Width(90));
            EditorGUILayout.LabelField($"⚫ Holes: {currentLevelData.holes.Count}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"📐 Grid: {currentLevelData.gridWidth}x{currentLevelData.gridHeight}", GUILayout.Width(80));

            EditorGUILayout.LabelField("|", GUILayout.Width(10)); // Separator
        }

        // Selected snake information
        if (selectedSlither != null)
        {
            int slitherIndex = currentLevelData.slithers.IndexOf(selectedSlither) + 1;
            EditorGUILayout.LabelField($"✓ Snake #{slitherIndex} ({selectedSlither.bodyPositions.Count} segments)", GUILayout.Width(140));

            // Show available shortcuts when snake is selected
            if (selectedSlither.bodyPositions.Count > 2)
            {
                EditorGUILayout.LabelField("⌨️ Ins/PgDn=Add, Home/End=Remove", EditorStyles.miniLabel, GUILayout.Width(160));
            }
            else
            {
                EditorGUILayout.LabelField("⌨️ Ins/PgDn=Add", EditorStyles.miniLabel, GUILayout.Width(100));
            }

            EditorGUILayout.LabelField("|", GUILayout.Width(10)); // Separator
        }

        // Mouse position information
        if (currentlyHoveredCell.HasValue)
        {
            EditorGUILayout.LabelField($"📍 Pos: ({currentlyHoveredCell.Value.x}, {currentlyHoveredCell.Value.y})", GUILayout.Width(90));
        }

        GUILayout.FlexibleSpace(); // Push content to the left

        // Show painting state
        if (isPaintingSlither)
        {
            EditorGUILayout.LabelField($"🎨 Painting... ({currentSlitherPoints.Count} points)", GUILayout.Width(150));
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Get tool icon for status bar
    /// </summary>
    private string GetToolIcon(LevelEditorToolbar.Tool tool)
    {
        switch (tool)
        {
            case LevelEditorToolbar.Tool.Slither: return "🐍";
            case LevelEditorToolbar.Tool.Hole: return "⚫";
            case LevelEditorToolbar.Tool.Eraser: return "🗑️";
            case LevelEditorToolbar.Tool.Move: return "✋";
            case LevelEditorToolbar.Tool.None: return "👆";
            default: return "";
        }
    }

    /// <summary>
    /// Get enhanced color for status bar (matches grid and toolbar)
    /// </summary>
    private Color GetEnhancedSlitherColor(SlitherColor slitherColor)
    {
        switch (slitherColor)
        {
            case SlitherColor.Red:
                return new Color(0.9f, 0.2f, 0.2f, 1f);
            case SlitherColor.Green:
                return new Color(0.2f, 0.8f, 0.3f, 1f);
            case SlitherColor.Blue:
                return new Color(0.2f, 0.4f, 0.9f, 1f);
            case SlitherColor.Yellow:
                return new Color(1f, 0.9f, 0.2f, 1f);
            case SlitherColor.Purple:
                return new Color(0.7f, 0.3f, 0.9f, 1f);
            case SlitherColor.Orange:
                return new Color(1f, 0.6f, 0.1f, 1f);
            case SlitherColor.Cyan:
                return new Color(0.2f, 0.8f, 0.9f, 1f);
            case SlitherColor.Magenta:
                return new Color(1f, 0.4f, 0.7f, 1f);
            default:
                return slitherColor.ToUnityColor();
        }
    }

    // --- FILE MANAGEMENT ---

    private void CreateNewLevel()
    {
        currentLevelData = new LevelData();
        currentPath = "";
        selectedSlither = null;
        needsComponentReinitialization = true;

        // Reset all editing states
        isPaintingSlither = false;
        currentSlitherPoints.Clear();
        CancelHandleDrag();

        // Clear the last path preference when creating a new unsaved level
        EditorPrefs.DeleteKey(LAST_LEVEL_PATH_KEY);

        Debug.Log("New level created. Edit and use 'Save As...' to create a file.");
    }

    private void LoadLevel()
    {
        string path = EditorUtility.OpenFilePanel("Load Level JSON", Application.streamingAssetsPath, "json");
        if (!string.IsNullOrEmpty(path))
        {
            LoadLevelFromPath(path);
        }
    }

    private void LoadLevelFromPath(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Could not auto-load level. File not found at: {path}");
            EditorPrefs.DeleteKey(LAST_LEVEL_PATH_KEY); // Clean up invalid path
            return;
        }

        LevelData loadedData = JsonDataService.Load(path);
        if (loadedData != null)
        {
            currentLevelData = loadedData;
            currentPath = path;
            selectedSlither = null;
            needsComponentReinitialization = true;

            // Reset all editing states
            isPaintingSlither = false;
            currentSlitherPoints.Clear();
            CancelHandleDrag();

            // Store the valid path for the next session
            EditorPrefs.SetString(LAST_LEVEL_PATH_KEY, path);

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

            Debug.Log($"<color=cyan>Successfully loaded level: {Path.GetFileName(path)}</color>");
            Repaint();
        }
        else
        {
            // If loading failed (e.g., corrupted file), clear the pref
            EditorPrefs.DeleteKey(LAST_LEVEL_PATH_KEY);
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
                // TODO: Create matching hole implementation
                // CreateMatchingHole(slither.color, slither.id);
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
            EditorPrefs.SetString(LAST_LEVEL_PATH_KEY, path); // Remember this path for next session
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
            // TODO: Update matching hole implementation
            // CreateMatchingHole(newColor, selectedSlither.id);
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

            // Update toolbar pending size to match the new grid size
            if (toolbar != null)
            {
                toolbar.SetPendingGridSize(newWidth, newHeight);
            }

            // Mark components for reinitialization to reflect the new grid size
            needsComponentReinitialization = true;

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
            // Handle escape to cancel operations
            if (e.keyCode == KeyCode.Escape)
            {
                if (isDraggingHandle)
                {
                    CancelHandleDrag();
                    e.Use();
                }
                if (isPaintingSlither)
                {
                    CancelPainting();
                    e.Use();
                }
            }

            // Handle snake segment modification shortcuts (when a snake is selected)
            if (selectedSlither != null)
            {
                if (e.keyCode == KeyCode.Insert)
                {
                    AddSlitherSegment(true); // Add head segment
                    e.Use();
                }
                else if (e.keyCode == KeyCode.PageDown)
                {
                    AddSlitherSegment(false); // Add tail segment
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Home && selectedSlither.bodyPositions.Count > 2)
                {
                    if (EditorUtility.DisplayDialog("Remove Head Segment",
                        "Are you sure you want to remove the head segment of this snake?", "Yes", "Cancel"))
                    {
                        RemoveSlitherSegment(true); // Remove head segment
                    }
                    e.Use();
                }
                else if (e.keyCode == KeyCode.End && selectedSlither.bodyPositions.Count > 2)
                {
                    if (EditorUtility.DisplayDialog("Remove Tail Segment",
                        "Are you sure you want to remove the tail segment of this snake?", "Yes", "Cancel"))
                    {
                        RemoveSlitherSegment(false); // Remove tail segment
                    }
                    e.Use();
                }
            }
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

    // REMOVED: ProcessKeyboardShortcuts() - will be handled by components

    // Event handlers for component communication

    private void HandleToolChanged(LevelEditorToolbar.Tool tool)
    {
        // Cancel any ongoing operations when tool changes
        CancelPainting();
        CancelHandleDrag();

        Debug.Log($"Tool changed to: {tool}");
        Repaint();
    }

    private void HandleColorChanged(SlitherColor color)
    {
        Debug.Log($"Color changed to: {color}");
        Repaint();
    }

    private void HandleInteractorAdded(SlitherInteractor interactor)
    {
        Debug.Log($"Added interactor: {interactor.GetType().Name}");
        Repaint();
    }

    private void HandleInteractorRemoved(SlitherInteractor interactor)
    {
        Debug.Log($"Removed interactor: {interactor.GetType().Name}");
        Repaint();
    }

    private void CancelPainting()
    {
        if (isPaintingSlither)
        {
            isPaintingSlither = false;
            currentSlitherPoints.Clear();
        }
    }

    private void HandleHoleDragStarted(HolePlacementData hole, Vector2Int startPosition)
    {
        if (hole != null)
        {
            isDraggingHole = true;
            draggingHole = hole;
            originalHolePosition = hole.position;
            previewHolePosition = startPosition;

            Debug.Log($"Started dragging hole at ({startPosition.x}, {startPosition.y})");
            Repaint();
        }
    }

    private void HandleHoleDragUpdated(Vector2Int newPosition)
    {
        if (isDraggingHole && draggingHole != null)
        {
            // Check if the new position is valid (not occupied by slither or another hole)
            bool isValidPosition = IsValidHolePosition(newPosition, draggingHole);

            if (isValidPosition)
            {
                previewHolePosition = newPosition;

                // Update grid preview
                if (gridRenderer != null)
                {
                    gridRenderer.UpdateHolePreview(newPosition);
                }
            }

            Repaint();
        }
    }

    private void HandleHoleDragEnded()
    {
        if (isDraggingHole && draggingHole != null)
        {
            // Check if the preview position is valid
            if (previewHolePosition.HasValue && IsValidHolePosition(previewHolePosition.Value, draggingHole))
            {
                Vector2Int oldPosition = draggingHole.position;
                draggingHole.position = previewHolePosition.Value;

                Debug.Log($"Moved hole from ({oldPosition.x}, {oldPosition.y}) to ({previewHolePosition.Value.x}, {previewHolePosition.Value.y})");
            }
            else
            {
                Debug.LogWarning("Cannot place hole at invalid position - drag cancelled");
            }

            // End dragging state
            isDraggingHole = false;
            draggingHole = null;
            previewHolePosition = null;

            if (gridRenderer != null)
            {
                gridRenderer.EndHoleDragging();
            }

            Repaint();
        }
    }

    private bool IsValidHolePosition(Vector2Int position, HolePlacementData excludeHole = null)
    {
        // Check if position is within grid bounds
        if (position.x < 0 || position.x >= currentLevelData.gridWidth ||
            position.y < 0 || position.y >= currentLevelData.gridHeight)
        {
            return false;
        }

        // Check if position is occupied by any slither
        foreach (var slither in currentLevelData.slithers)
        {
            if (slither.bodyPositions.Contains(position))
            {
                return false;
            }
        }

        // Check if position is occupied by another hole (excluding the one being moved)
        foreach (var hole in currentLevelData.holes)
        {
            if (hole != excludeHole && hole.position == position)
            {
                return false;
            }
        }

        return true;
    }    // Grid event handlers
    private void HandleGridClick(Vector2Int cellPosition)
    {
        if (currentLevelData == null) return;

        // Find slither at this position
        SlitherPlacementData clickedSlither = FindSlitherAtPosition(cellPosition);

        if (clickedSlither != null)
        {
            // Select the slither
            selectedSlither = clickedSlither;
        }
        else
        {
            // Deselect if clicking on empty space
            selectedSlither = null;
        }

        Repaint();
    }

    private void HandleCellHovered(Vector2Int cellPosition)
    {
        // The grid renderer already tracks hovered cells internally via GetHoveredCell()
        // This event handler can be used for additional logic if needed
        currentlyHoveredCell = cellPosition;
        Repaint();
    }

    private void HandleGridRightClick(Vector2Int cellPosition)
    {
        // TODO: Implement context menu for right-click
        Debug.Log($"Right-clicked on cell {cellPosition}");
    }

    private SlitherPlacementData FindSlitherAtPosition(Vector2Int position)
    {
        if (currentLevelData == null) return null;

        return currentLevelData.slithers.FirstOrDefault(slither =>
            slither.bodyPositions.Contains(position));
    }

    // --- LEVEL BROWSER ---

    /// <summary>
    /// Scan for available level files in the Resources and StreamingAssets folders
    /// </summary>
    private void ScanForAvailableLevels()
    {
        availableLevelPaths.Clear();

        // Scan StreamingAssets folder (common location for level files)
        string streamingAssetsPath = Application.streamingAssetsPath;
        if (Directory.Exists(streamingAssetsPath))
        {
            var jsonFiles = Directory.GetFiles(streamingAssetsPath, "*.json", SearchOption.AllDirectories);
            availableLevelPaths.AddRange(jsonFiles);
        }

        // Scan Assets/Resources folder
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (Directory.Exists(resourcesPath))
        {
            var jsonFiles = Directory.GetFiles(resourcesPath, "*.json", SearchOption.AllDirectories);
            availableLevelPaths.AddRange(jsonFiles);
        }

        // Sort by last write time (most recent first)
        availableLevelPaths.Sort((a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

        lastLevelScanTime = (float)EditorApplication.timeSinceStartup;
    }

    /// <summary>
    /// Draw the level browser when no level is loaded (full view)
    /// </summary>
    private void DrawLevelBrowser()
    {
        // Periodically refresh the level list
        if (EditorApplication.timeSinceStartup - lastLevelScanTime > LEVEL_SCAN_INTERVAL)
        {
            ScanForAvailableLevels();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("📁 Available Levels", EditorStyles.boldLabel);

        if (availableLevelPaths.Count == 0)
        {
            EditorGUILayout.HelpBox("No level files found in Resources or StreamingAssets folders.", MessageType.Info);
            if (GUILayout.Button("🔄 Refresh"))
            {
                ScanForAvailableLevels();
            }
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Found {availableLevelPaths.Count} level(s)", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("🔄 Refresh", GUILayout.Width(70)))
        {
            ScanForAvailableLevels();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Scrollable list of levels
        levelBrowserScrollPosition = EditorGUILayout.BeginScrollView(levelBrowserScrollPosition, GUILayout.Height(200));

        for (int i = 0; i < availableLevelPaths.Count; i++)
        {
            DrawLevelListItem(availableLevelPaths[i], i);
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Draw the compact level browser in the inspector panel
    /// </summary>
    private void DrawLevelBrowserCompact()
    {
        // Periodically refresh the level list
        if (EditorApplication.timeSinceStartup - lastLevelScanTime > LEVEL_SCAN_INTERVAL)
        {
            ScanForAvailableLevels();
        }

        EditorGUILayout.LabelField("📁 Available Levels", EditorStyles.boldLabel);

        if (availableLevelPaths.Count == 0)
        {
            EditorGUILayout.LabelField("No levels found", EditorStyles.miniLabel);
            if (GUILayout.Button("🔄 Refresh", GUILayout.Height(20)))
            {
                ScanForAvailableLevels();
            }
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{availableLevelPaths.Count} level(s)", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("🔄", GUILayout.Width(25), GUILayout.Height(18)))
        {
            ScanForAvailableLevels();
        }
        EditorGUILayout.EndHorizontal();

        // Compact scrollable list (max 3 items visible)
        levelBrowserScrollPosition = EditorGUILayout.BeginScrollView(levelBrowserScrollPosition, GUILayout.Height(80));

        for (int i = 0; i < Math.Min(availableLevelPaths.Count, 10); i++) // Show max 10 items
        {
            DrawLevelListItemCompact(availableLevelPaths[i], i);
        }

        EditorGUILayout.EndScrollView();

        if (availableLevelPaths.Count > 10)
        {
            EditorGUILayout.LabelField($"... and {availableLevelPaths.Count - 10} more", EditorStyles.miniLabel);
        }
    }

    /// <summary>
    /// Draw a single level item in the list (full view)
    /// </summary>
    private void DrawLevelListItem(string levelPath, int index)
    {
        string fileName = Path.GetFileNameWithoutExtension(levelPath);
        string relativePath = GetRelativePath(levelPath);
        DateTime lastModified = File.GetLastWriteTime(levelPath);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        // Level info
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(fileName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(relativePath, EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Modified: {lastModified:MMM dd, yyyy HH:mm}", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        // Load button
        if (GUILayout.Button("📂 Load", GUILayout.Width(60), GUILayout.Height(40)))
        {
            LoadLevelFromPath(levelPath);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
    }

    /// <summary>
    /// Draw a single level item in the list (compact view)
    /// </summary>
    private void DrawLevelListItemCompact(string levelPath, int index)
    {
        string fileName = Path.GetFileNameWithoutExtension(levelPath);
        DateTime lastModified = File.GetLastWriteTime(levelPath);

        EditorGUILayout.BeginHorizontal("box");

        // Level info
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(fileName, EditorStyles.miniLabel);
        EditorGUILayout.LabelField(lastModified.ToString("MMM dd, HH:mm"), EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        // Load button
        if (GUILayout.Button("📂", GUILayout.Width(25), GUILayout.Height(25)))
        {
            LoadLevelFromPath(levelPath);
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Get relative path from the project root
    /// </summary>
    private string GetRelativePath(string fullPath)
    {
        string dataPath = Application.dataPath;
        if (fullPath.StartsWith(dataPath))
        {
            return "Assets" + fullPath.Substring(dataPath.Length).Replace('\\', '/');
        }
        
        string streamingAssetsPath = Application.streamingAssetsPath;
        if (fullPath.StartsWith(streamingAssetsPath))
        {
            return "StreamingAssets" + fullPath.Substring(streamingAssetsPath.Length).Replace('\\', '/');
        }

        return Path.GetFileName(fullPath);
    }
}
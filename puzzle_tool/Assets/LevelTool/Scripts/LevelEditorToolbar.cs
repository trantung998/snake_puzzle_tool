using UnityEngine;
using UnityEditor;

/// <summary>
/// Handles the toolbar rendering and file operations for the Level Editor.
/// Manages tool selection, file operations, and grid controls.
/// </summary>
public class LevelEditorToolbar
{
    // Tool definitions
    public enum Tool { None, Slither, Hole, Eraser, Move }

    // Events for communication with main window
    public System.Action OnNewLevel;
    public System.Action OnLoadLevel;
    public System.Action OnSaveLevel;
    public System.Action OnSaveAsLevel;
    public System.Action<Tool> OnToolChanged;
    public System.Action<SlitherColor> OnColorChanged;
    public System.Action<int, int> OnGridResizeRequested;

    // Current state
    private Tool currentTool = Tool.None;
    private SlitherColor selectedColor = SlitherColor.Red;
    private int pendingWidth = 5;
    private int pendingHeight = 8;

    /// <summary>
    /// Initialize toolbar with default values
    /// </summary>
    public void Initialize(LevelData levelData)
    {
        if (levelData != null)
        {
            pendingWidth = levelData.gridWidth;
            pendingHeight = levelData.gridHeight;
        }
    }

    /// <summary>
    /// Draw the file toolbar
    /// </summary>
    public void DrawFileToolbar(string currentPath = "")
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New", EditorStyles.toolbarButton))
        {
            OnNewLevel?.Invoke();
        }

        if (GUILayout.Button("Load", EditorStyles.toolbarButton))
        {
            OnLoadLevel?.Invoke();
        }

        // Display current file name
        string fileName = string.IsNullOrEmpty(currentPath) ? "Untitled" : System.IO.Path.GetFileName(currentPath);
        GUILayout.TextField(fileName, EditorStyles.toolbarTextField);

        if (GUILayout.Button("Save", EditorStyles.toolbarButton))
        {
            OnSaveLevel?.Invoke();
        }

        if (GUILayout.Button("Save As...", EditorStyles.toolbarButton))
        {
            OnSaveAsLevel?.Invoke();
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draw the tool selection toolbar
    /// </summary>
    public void DrawToolbar(LevelData levelData)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        // Tool selection
        EditorGUILayout.BeginHorizontal();

        if (DrawToolButton("None", Tool.None, "No tool selected"))
            SetCurrentTool(Tool.None);

        if (DrawToolButton("Slither", Tool.Slither, "Draw slithers"))
            SetCurrentTool(Tool.Slither);

        if (DrawToolButton("Hole", Tool.Hole, "Place holes"))
            SetCurrentTool(Tool.Hole);

        if (DrawToolButton("Eraser", Tool.Eraser, "Remove objects"))
            SetCurrentTool(Tool.Eraser);

        if (DrawToolButton("Move", Tool.Move, "Move and edit slithers"))
            SetCurrentTool(Tool.Move);

        EditorGUILayout.EndHorizontal();

        // Color selection for slither and hole tools
        if (currentTool == Tool.Slither || currentTool == Tool.Hole)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Color:", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            SlitherColor newColor = (SlitherColor)EditorGUILayout.EnumPopup(selectedColor);
            if (newColor != selectedColor)
            {
                selectedColor = newColor;
                OnColorChanged?.Invoke(selectedColor);
            }

            // Enhanced color preview with better visibility
            EditorGUILayout.BeginVertical(GUILayout.Width(50));
            EditorGUILayout.LabelField("Preview:", EditorStyles.miniLabel, GUILayout.Width(50));

            Color enhancedColor = GetEnhancedSlitherColor(selectedColor);
            EditorGUILayout.ColorField(GUIContent.none, enhancedColor, false, false, false, GUILayout.Width(40), GUILayout.Height(25));
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();

        // Grid controls
        DrawGridControls(levelData);
    }

    /// <summary>
    /// Draw grid size controls
    /// </summary>
    private void DrawGridControls(LevelData levelData)
    {
        if (levelData == null) return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Grid Controls", EditorStyles.boldLabel);

        // Current grid size display
        EditorGUILayout.LabelField($"Current Grid: {levelData.gridWidth} x {levelData.gridHeight}", EditorStyles.miniLabel);

        // Grid size input
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("New Size:", GUILayout.Width(65));

        // Color input fields red if they are out of range
        bool widthOutOfRange = pendingWidth < 3 || pendingWidth > 20;
        bool heightOutOfRange = pendingHeight < 3 || pendingHeight > 20;

        if (widthOutOfRange) GUI.color = Color.red;
        int newWidth = EditorGUILayout.IntField(pendingWidth, GUILayout.Width(50));
        if (widthOutOfRange) GUI.color = Color.white;

        EditorGUILayout.LabelField("x", GUILayout.Width(15));

        if (heightOutOfRange) GUI.color = Color.red;
        int newHeight = EditorGUILayout.IntField(pendingHeight, GUILayout.Width(50));
        if (heightOutOfRange) GUI.color = Color.white;

        // Clamp values and update pending values only if they changed
        int validWidth = Mathf.Clamp(newWidth, 3, 20);
        int validHeight = Mathf.Clamp(newHeight, 3, 20);

        // Update pending values only if there's actual change to avoid unnecessary updates
        if (newWidth != pendingWidth) pendingWidth = validWidth;
        if (newHeight != pendingHeight) pendingHeight = validHeight;

        EditorGUILayout.EndHorizontal();

        // Validation and buttons
        bool isValidInput = pendingWidth >= 3 && pendingWidth <= 20 &&
                           pendingHeight >= 3 && pendingHeight <= 20;
        bool hasChanges = pendingWidth != levelData.gridWidth || pendingHeight != levelData.gridHeight;

        if (!isValidInput)
        {
            EditorGUILayout.HelpBox("Grid size must be between 3x3 and 20x20", MessageType.Warning);
        }

        if (hasChanges)
        {
            string buttonText = $"Resize to {pendingWidth}x{pendingHeight}";

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(buttonText, GUILayout.Height(25)))
            {
                OnGridResizeRequested?.Invoke(pendingWidth, pendingHeight);
            }
            if (GUILayout.Button("Reset", GUILayout.Width(60), GUILayout.Height(25)))
            {
                pendingWidth = levelData.gridWidth;
                pendingHeight = levelData.gridHeight;
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Button($"Current: {levelData.gridWidth}x{levelData.gridHeight}", GUILayout.Height(25));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Keyboard: Ctrl+Plus/Minus to resize", EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Draw a tool button with enhanced visual feedback and icons
    /// </summary>
    private bool DrawToolButton(string label, Tool tool, string tooltip)
    {
        Color originalColor = GUI.backgroundColor;
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);

        // Enhanced styling for selected tool
        if (currentTool == tool)
        {
            GUI.backgroundColor = new Color(0.2f, 0.8f, 1f, 0.9f); // Bright cyan
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.normal.textColor = Color.white;
        }
        else
        {
            // Subtle hover effect for non-selected tools
            buttonStyle.normal.textColor = Color.black;
        }

        // Add icons to tools for better visual identification
        string icon = GetToolIcon(tool);
        string buttonText = string.IsNullOrEmpty(icon) ? label : $"{icon} {label}";

        bool clicked = GUILayout.Button(new GUIContent(buttonText, tooltip), buttonStyle, GUILayout.Height(35), GUILayout.MinWidth(80));

        GUI.backgroundColor = originalColor;

        return clicked;
    }

    /// <summary>
    /// Get icon for each tool to improve visual identification
    /// </summary>
    private string GetToolIcon(Tool tool)
    {
        switch (tool)
        {
            case Tool.Slither: return "🐍";
            case Tool.Hole: return "⚫";
            case Tool.Eraser: return "🗑️";
            case Tool.Move: return "✋";
            case Tool.None: return "👆";
            default: return "";
        }
    }

    /// <summary>
    /// Set the current tool and notify listeners
    /// </summary>
    private void SetCurrentTool(Tool tool)
    {
        if (currentTool != tool)
        {
            currentTool = tool;
            OnToolChanged?.Invoke(tool);

            // Debug feedback
            string toolName = tool == Tool.None ? "None" : tool.ToString();
            Debug.Log($"Tool changed to: {toolName}");
        }
    }

    /// <summary>
    /// Get the current tool
    /// </summary>
    public Tool GetCurrentTool()
    {
        return currentTool;
    }

    /// <summary>
    /// Get the selected color
    /// </summary>
    public SlitherColor GetSelectedColor()
    {
        return selectedColor;
    }

    /// <summary>
    /// Set the selected color programmatically
    /// </summary>
    public void SetSelectedColor(SlitherColor color)
    {
        if (selectedColor != color)
        {
            selectedColor = color;
            OnColorChanged?.Invoke(selectedColor);
        }
    }

    /// <summary>
    /// Update pending grid size values
    /// </summary>
    public void SetPendingGridSize(int width, int height)
    {
        pendingWidth = Mathf.Clamp(width, 3, 20);
        pendingHeight = Mathf.Clamp(height, 3, 20);
    }

    /// <summary>
    /// Get pending grid size
    /// </summary>
    public (int width, int height) GetPendingGridSize()
    {
        return (pendingWidth, pendingHeight);
    }

    /// <summary>
    /// Get enhanced color with better accessibility (matches LevelEditorGrid implementation)
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
}

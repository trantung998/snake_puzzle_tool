using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Handles all grid rendering and mouse interaction logic for the Level Editor.
/// Follows Single Responsibility Principle by focusing only on grid-related operations.
/// </summary>
public class LevelEditorGrid
{
    // Constants for grid rendering
    private const float BUTTON_SIZE = 45f; // Slightly larger for better visibility
    private const float GRID_LINE_WIDTH = 1f;
    private const float HOVER_ANIMATION_SPEED = 8f;

    // Enhanced color system
    private static readonly Color GRID_LINE_COLOR = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    private static readonly Color GRID_BACKGROUND = new Color(0.15f, 0.15f, 0.15f, 1f);
    private static readonly Color SELECTED_CELL_COLOR = new Color(1f, 0.8f, 0.2f, 0.9f); // Warm yellow
    private static readonly Color PREVIEW_COLOR = new Color(0.2f, 0.8f, 1f, 0.7f); // Bright cyan
    private static readonly Color HOVER_COLOR = new Color(1f, 1f, 1f, 0.2f);
    private static readonly Color VALID_DROP_COLOR = new Color(0.2f, 1f, 0.2f, 0.6f);
    private static readonly Color INVALID_DROP_COLOR = new Color(1f, 0.2f, 0.2f, 0.6f);

    // Visual feedback
    private float hoverIntensity = 0f;
    private Vector2Int? lastHoveredCell = null;

    // Current state
    private LevelData levelData;
    private Vector2Int? hoveredCell;
    private List<Vector2Int> previewPositions = new List<Vector2Int>();

    // State for painting preview
    private List<Vector2Int> paintingPositions = new List<Vector2Int>();

    // State for hole dragging
    private bool isDraggingHole = false;
    private HolePlacementData draggingHole = null;
    private Vector2Int? holePreviewPosition = null;

    // Events for communication with main window
    public System.Action<Vector2Int> OnCellClicked;
    public System.Action<Vector2Int> OnCellHovered;
    public System.Action<Vector2Int> OnCellRightClicked;

    // Events for hole dragging
    public System.Action<HolePlacementData, Vector2Int> OnHoleDragStarted;
    public System.Action<Vector2Int> OnHoleDragUpdated;
    public System.Action OnHoleDragEnded;

    /// <summary>
    /// Initialize the grid with level data
    /// </summary>
    public void Initialize(LevelData data)
    {
        levelData = data;
        hoveredCell = null;
        previewPositions.Clear();
    }

    /// <summary>
    /// Main drawing method called from OnGUI
    /// </summary>
    public void DrawGrid(Rect area, SlitherPlacementData selectedSlither, bool isDraggingHandle, bool isDraggingHead, SlitherPlacementData draggingSlither, List<Vector2Int> previewPositions = null, bool isHoleDragging = false, Vector2Int? holePreview = null)
    {
        if (levelData == null)
        {
            EditorGUI.LabelField(area, "No level data loaded");
            return;
        }

        if (previewPositions != null)
        {
            this.previewPositions = previewPositions;
        }

        // Update hole dragging state
        if (isHoleDragging && holePreview.HasValue)
        {
            this.isDraggingHole = isHoleDragging;
            this.holePreviewPosition = holePreview;
        }

        // Calculate grid rendering parameters
        float buttonSize = BUTTON_SIZE;
        float gridWidth = levelData.gridWidth * buttonSize;
        float gridHeight = levelData.gridHeight * buttonSize;

        // Center the grid in the available area - using relative coordinates
        float offsetX = (area.width - gridWidth) * 0.5f;
        float offsetY = (area.height - gridHeight) * 0.5f;

        // Calculate absolute positions for drawing
        float absoluteOffsetX = area.x + offsetX;
        float absoluteOffsetY = area.y + offsetY;

        // Draw enhanced grid background with proper alignment
        Rect gridBackgroundRect = new Rect(absoluteOffsetX - 5, absoluteOffsetY - 5, gridWidth + 10, gridHeight + 10);
        EditorGUI.DrawRect(gridBackgroundRect, GRID_BACKGROUND);

        // Draw grid border with proper alignment
        Rect gridBorderRect = new Rect(absoluteOffsetX - 2, absoluteOffsetY - 2, gridWidth + 4, gridHeight + 4);
        EditorGUI.DrawRect(gridBorderRect, GRID_LINE_COLOR);

        // Handle mouse events for both slither and hole dragging
        HandleMouseEvents(area, buttonSize, absoluteOffsetX, absoluteOffsetY);

        // Draw grid lines for better visual separation
        DrawGridLines(absoluteOffsetX, absoluteOffsetY, gridWidth, gridHeight, buttonSize);

        // Draw grid cells with corrected positioning
        for (int y = 0; y < levelData.gridHeight; y++)
        {
            for (int x = 0; x < levelData.gridWidth; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                Rect cellRect = new Rect(
                    absoluteOffsetX + x * buttonSize,
                    absoluteOffsetY + (levelData.gridHeight - 1 - y) * buttonSize,
                    buttonSize,
                    buttonSize
                );

                DrawGridCell(pos, cellRect, selectedSlither, isDraggingHandle, isDraggingHead, draggingSlither);
            }
        }

        // Optional: Draw debug information to verify alignment
        if (Application.isPlaying && Event.current.control)
        {
            // Show grid bounds for debugging when holding Ctrl
            EditorGUI.DrawRect(new Rect(absoluteOffsetX, absoluteOffsetY, gridWidth, gridHeight),
                new Color(1f, 0f, 0f, 0.1f)); // Semi-transparent red overlay
        }
    }

    /// <summary>
    /// Draw a single grid cell with enhanced visual feedback
    /// </summary>
    private void DrawGridCell(Vector2Int pos, Rect cellRect, SlitherPlacementData selectedSlither, bool isDraggingHandle, bool isDraggingHead, SlitherPlacementData draggingSlither)
    {
        // Update hover animation
        bool isHovered = (hoveredCell.HasValue && hoveredCell.Value == pos);
        if (isHovered && lastHoveredCell != pos)
        {
            hoverIntensity = 0f;
            lastHoveredCell = pos;
        }

        if (isHovered)
        {
            hoverIntensity = Mathf.Min(1f, hoverIntensity + Time.deltaTime * HOVER_ANIMATION_SPEED);
        }
        else if (lastHoveredCell == pos)
        {
            hoverIntensity = Mathf.Max(0f, hoverIntensity - Time.deltaTime * HOVER_ANIMATION_SPEED);
            if (hoverIntensity <= 0f)
                lastHoveredCell = null;
        }

        // Determine cell content and color
        Color cellColor = GetCellColor(pos, selectedSlither, isDraggingHandle, isDraggingHead, draggingSlither);
        string cellContent = GetCellContent(pos);

        // Apply hover effect
        if (isHovered || (lastHoveredCell == pos && hoverIntensity > 0f))
        {
            cellColor = Color.Lerp(cellColor, HOVER_COLOR, hoverIntensity * 0.3f);
        }

        // Apply cell color with enhanced visual feedback
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = cellColor;

        // Enhanced border for better grid definition
        if (isHovered)
        {
            EditorGUI.DrawRect(new Rect(cellRect.x - 1, cellRect.y - 1, cellRect.width + 2, cellRect.height + 2), GRID_LINE_COLOR);
        }

        // Create button with enhanced styling
        GUIStyle cellStyle = new GUIStyle(GUI.skin.button);
        cellStyle.fontSize = 16; // Larger font for better visibility
        cellStyle.fontStyle = FontStyle.Bold;
        cellStyle.alignment = TextAnchor.MiddleCenter;

        // Add subtle gradient effect
        if (cellContent != "")
        {
            cellStyle.normal.textColor = GetTextColor(cellColor);
        }

        if (GUI.Button(cellRect, cellContent, cellStyle))
        {
            // Check if user clicked on a hole to start dragging
            var hole = levelData.holes.FirstOrDefault(h => h.position == pos);
            if (hole != null && !isDraggingHole)
            {
                draggingHole = hole;
                OnHoleDragStarted?.Invoke(hole, pos);
            }
            else
            {
                OnCellClicked?.Invoke(pos);
            }
        }

        // Handle mouse drag for holes
        if (Event.current.type == EventType.MouseDrag && Event.current.button == 0)
        {
            if (cellRect.Contains(Event.current.mousePosition))
            {
                var hole = levelData.holes.FirstOrDefault(h => h.position == pos);
                if (hole != null && !isDraggingHole)
                {
                    draggingHole = hole;
                    OnHoleDragStarted?.Invoke(hole, pos);
                }
            }
        }

        // Handle mouse up to end hole dragging
        if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && isDraggingHole)
        {
            if (cellRect.Contains(Event.current.mousePosition))
            {
                OnHoleDragEnded?.Invoke();
            }
        }

        // Handle right-click
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            if (cellRect.Contains(Event.current.mousePosition))
            {
                OnCellRightClicked?.Invoke(pos);
                Event.current.Use();
            }
        }

        // Restore original color
        GUI.backgroundColor = originalColor;
    }

    /// <summary>
    /// Determine the appropriate color for a grid cell with enhanced accessibility
    /// </summary>
    private Color GetCellColor(Vector2Int pos, SlitherPlacementData selectedSlither, bool isDraggingHandle, bool isDraggingHead, SlitherPlacementData draggingSlither)
    {
        // Check if this is a preview position (during drag)
        if (previewPositions.Contains(pos))
        {
            return PREVIEW_COLOR;
        }

        // Check if this is a position being painted
        if (paintingPositions.Contains(pos))
        {
            return Color.Lerp(PREVIEW_COLOR, Color.white, 0.2f);
        }

        // Check if this is the hole preview position (during drag)
        if (isDraggingHole && pos == holePreviewPosition)
        {
            return VALID_DROP_COLOR;
        }

        // Check if this cell contains a hole
        var hole = levelData.holes.FirstOrDefault(h => h.position == pos);
        if (hole != null)
        {
            Color holeColor = GetEnhancedSlitherColor(hole.color);

            // If this hole is being dragged, make it semi-transparent
            if (isDraggingHole && hole == draggingHole)
            {
                return Color.Lerp(holeColor, Color.white, 0.7f);
            }

            // Add subtle darkening for holes to differentiate from slithers
            return Color.Lerp(holeColor, Color.black, 0.2f);
        }

        // Check if this cell contains a slither segment
        var slither = levelData.slithers.FirstOrDefault(s => s.bodyPositions.Contains(pos));
        if (slither != null)
        {
            Color baseColor = GetEnhancedSlitherColor(slither.color);
            int segmentIndex = slither.bodyPositions.IndexOf(pos);
            bool isHead = segmentIndex == 0;
            bool isTail = segmentIndex == slither.bodyPositions.Count - 1;

            // Add visual distinction for head and tail
            if (isHead)
            {
                baseColor = Color.Lerp(baseColor, Color.white, 0.15f); // Slightly brighter for head
            }
            else if (isTail)
            {
                baseColor = Color.Lerp(baseColor, Color.black, 0.1f); // Slightly darker for tail
            }

            // Highlight selected slither with animated pulse
            if (slither == selectedSlither)
            {
                float pulseIntensity = 0.3f + 0.2f * Mathf.Sin(Time.realtimeSinceStartup * 3f);
                baseColor = Color.Lerp(baseColor, SELECTED_CELL_COLOR, pulseIntensity);
            }

            // Special highlight for dragging slither
            if (isDraggingHandle && slither == draggingSlither)
            {
                baseColor = Color.Lerp(baseColor, PREVIEW_COLOR, 0.4f);
            }

            return baseColor;
        }

        // Enhanced empty cell appearance
        return GRID_BACKGROUND;
    }

    /// <summary>
    /// Get enhanced color with better accessibility and visual distinction
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

    /// <summary>
    /// Get the text content to display in a grid cell with enhanced icons
    /// </summary>
    private string GetCellContent(Vector2Int pos)
    {
        // Check for painting preview
        if (paintingPositions.Contains(pos))
        {
            int index = paintingPositions.IndexOf(pos);
            if (index == 0)
                return "🐍"; // Snake head emoji
            else if (index == paintingPositions.Count - 1)
                return "◉"; // Tail symbol
            else
                return "●"; // Body segment
        }

        // Check for hole preview position
        if (isDraggingHole && pos == holePreviewPosition)
        {
            return "🕳️"; // Hole emoji for preview
        }

        // Check for hole
        var hole = levelData.holes.FirstOrDefault(h => h.position == pos);
        if (hole != null)
        {
            return "⚫"; // Black circle for hole
        }

        // Check for slither
        var slither = levelData.slithers.FirstOrDefault(s => s.bodyPositions.Contains(pos));
        if (slither != null)
        {
            int segmentIndex = slither.bodyPositions.IndexOf(pos);

            // Check for special interactors first
            var cocoon = slither.interactors.OfType<CocoonInteractor>().FirstOrDefault();
            if (cocoon != null)
            {
                return $"🛡{cocoon.hitCount}"; // Shield with hit count
            }

            var chain = slither.interactors.OfType<ChainInteractor>().FirstOrDefault();
            if (chain != null && segmentIndex == 0) // Only show on head
            {
                return $"⛓{chain.hitCount}"; // Chain with hit count
            }

            // Head
            if (segmentIndex == 0)
            {
                return "🐍"; // Snake head emoji
            }
            // Tail
            else if (segmentIndex == slither.bodyPositions.Count - 1)
            {
                return "◉"; // Circle with center dot for tail
            }
            // Body
            else
            {
                return "●"; // Filled circle for body
            }
        }

        // Empty cell
        return "";
    }

    /// <summary>
    /// Handle mouse events over the grid area
    /// </summary>
    private void HandleMouseEvents(Rect area, float buttonSize, float offsetX, float offsetY)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDown)
        {
            Vector2 mousePos = e.mousePosition;

            // Check if mouse is over grid area
            if (area.Contains(mousePos))
            {
                // Calculate grid cell position
                float relativeX = mousePos.x - offsetX;
                float relativeY = mousePos.y - offsetY;

                int gridX = Mathf.FloorToInt(relativeX / buttonSize);
                int gridY = levelData.gridHeight - 1 - Mathf.FloorToInt(relativeY / buttonSize);

                Vector2Int cellPos = new Vector2Int(gridX, gridY);

                // Validate position
                if (IsValidPosition(cellPos))
                {
                    // Update hovered cell
                    if (hoveredCell != cellPos)
                    {
                        hoveredCell = cellPos;
                        OnCellHovered?.Invoke(cellPos);
                    }

                    // If dragging a hole, update preview position
                    if (isDraggingHole && draggingHole != null)
                    {
                        OnHoleDragUpdated?.Invoke(cellPos);
                    }
                }
                else
                {
                    hoveredCell = null;
                }
            }
            else
            {
                hoveredCell = null;
            }
        }
    }

    /// <summary>
    /// Update preview positions for drag operations
    /// </summary>
    public void SetPreviewPositions(List<Vector2Int> positions)
    {
        previewPositions = new List<Vector2Int>(positions);
    }

    /// <summary>
    /// Clear preview positions
    /// </summary>
    public void ClearPreview()
    {
        previewPositions.Clear();
        paintingPositions.Clear();
    }

    /// <summary>
    /// Start hole dragging
    /// </summary>
    public void StartHoleDragging(HolePlacementData hole, Vector2Int position)
    {
        isDraggingHole = true;
        draggingHole = hole;
        holePreviewPosition = position;
    }

    /// <summary>
    /// Update hole preview position during drag
    /// </summary>
    public void UpdateHolePreview(Vector2Int position)
    {
        if (isDraggingHole)
        {
            holePreviewPosition = position;
        }
    }

    /// <summary>
    /// Set painting positions for slither preview
    /// </summary>
    public void SetPaintingPositions(List<Vector2Int> positions)
    {
        paintingPositions = new List<Vector2Int>(positions);
    }

    /// <summary>
    /// Clear painting positions
    /// </summary>
    public void ClearPaintingPositions()
    {
        paintingPositions.Clear();
    }

    /// <summary>
    /// Get currently hovered cell
    /// </summary>
    public Vector2Int? GetHoveredCell()
    {
        return hoveredCell;
    }

    /// <summary>
    /// Set hole dragging state
    /// </summary>
    public void SetHoleDragging(bool dragging, HolePlacementData hole = null, Vector2Int previewPos = default)
    {
        isDraggingHole = dragging;
        draggingHole = hole;
        holePreviewPosition = previewPos;
    }

    /// <summary>
    /// End hole dragging
    /// </summary>
    public void EndHoleDragging()
    {
        isDraggingHole = false;
        draggingHole = null;
        holePreviewPosition = null;
    }

    /// <summary>
    /// Check if a position is valid within the grid
    /// </summary>
    private bool IsValidPosition(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < levelData.gridWidth &&
               pos.y >= 0 && pos.y < levelData.gridHeight;
    }

    /// <summary>
    /// Draw additional information overlay (like coordinates, debug info)
    /// </summary>
    public void DrawOverlay()
    {
        if (hoveredCell.HasValue)
        {
            Vector2Int pos = hoveredCell.Value;
            EditorGUILayout.LabelField($"Hovered: ({pos.x}, {pos.y})", EditorStyles.miniLabel);

            // Show cell information
            DrawCellInfo(pos);
        }
    }

    /// <summary>
    /// Draw detailed information about a specific cell
    /// </summary>
    private void DrawCellInfo(Vector2Int pos)
    {
        // Check for slither at this position
        var slither = levelData.slithers.FirstOrDefault(s => s.bodyPositions.Contains(pos));
        if (slither != null)
        {
            int slitherIndex = levelData.slithers.IndexOf(slither);
            int segmentIndex = slither.bodyPositions.IndexOf(pos);
            string segmentType = segmentIndex == 0 ? "Head" :
                               segmentIndex == slither.bodyPositions.Count - 1 ? "Tail" : "Body";

            EditorGUILayout.LabelField($"Slither #{slitherIndex + 1} - {segmentType} (Color: {slither.color})", EditorStyles.miniLabel);

            // Show interactors if any
            if (slither.interactors != null && slither.interactors.Count > 0)
            {
                foreach (var interactor in slither.interactors)
                {
                    if (interactor is ChainInteractor chain)
                    {
                        EditorGUILayout.LabelField($"Chain (Hit Count: {chain.hitCount})", EditorStyles.miniLabel);
                    }
                    else if (interactor is CocoonInteractor cocoon)
                    {
                        EditorGUILayout.LabelField($"Cocoon (Hit Count: {cocoon.hitCount})", EditorStyles.miniLabel);
                    }
                }
            }
        }

        // Check for hole at this position
        var hole = levelData.holes.FirstOrDefault(h => h.position == pos);
        if (hole != null)
        {
            EditorGUILayout.LabelField($"Hole (Color: {hole.color}, Slither ID: {hole.slitherId})", EditorStyles.miniLabel);
        }

        // Show if empty
        if (slither == null && hole == null)
        {
            EditorGUILayout.LabelField("Empty cell", EditorStyles.miniLabel);
        }
    }

    /// <summary>
    /// Get appropriate text color based on background color for better contrast
    /// </summary>
    private Color GetTextColor(Color backgroundColor)
    {
        // Calculate luminance of background color
        float luminance = 0.299f * backgroundColor.r + 0.587f * backgroundColor.g + 0.114f * backgroundColor.b;

        // Return white text for dark backgrounds, black for light backgrounds
        return luminance > 0.5f ? Color.black : Color.white;
    }

    /// <summary>
    /// Draw grid lines for better visual separation with proper alignment
    /// </summary>
    private void DrawGridLines(float offsetX, float offsetY, float gridWidth, float gridHeight, float cellSize)
    {
        Color lineColor = new Color(GRID_LINE_COLOR.r, GRID_LINE_COLOR.g, GRID_LINE_COLOR.b, 0.3f);

        // Draw vertical lines
        for (int x = 0; x <= levelData.gridWidth; x++)
        {
            float xPos = offsetX + x * cellSize;
            Rect lineRect = new Rect(xPos - GRID_LINE_WIDTH * 0.5f, offsetY, GRID_LINE_WIDTH, gridHeight);
            EditorGUI.DrawRect(lineRect, lineColor);
        }

        // Draw horizontal lines
        for (int y = 0; y <= levelData.gridHeight; y++)
        {
            float yPos = offsetY + y * cellSize;
            Rect lineRect = new Rect(offsetX, yPos - GRID_LINE_WIDTH * 0.5f, gridWidth, GRID_LINE_WIDTH);
            EditorGUI.DrawRect(lineRect, lineColor);
        }
    }
}

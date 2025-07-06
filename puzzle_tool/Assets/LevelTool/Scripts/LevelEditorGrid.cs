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
    private const float BUTTON_SIZE = 40f;
    private static readonly Color GRID_LINE_COLOR = Color.gray;
    private static readonly Color SELECTED_CELL_COLOR = Color.yellow;
    private static readonly Color PREVIEW_COLOR = Color.cyan;
    
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
        
        // Center the grid in the available area
        float offsetX = (area.width - gridWidth) * 0.5f;
        float offsetY = (area.height - gridHeight) * 0.5f;
        
        // Handle mouse events for both slither and hole dragging
        HandleMouseEvents(area, buttonSize, offsetX, offsetY);
        
        // Draw grid cells
        for (int y = 0; y < levelData.gridHeight; y++)
        {
            for (int x = 0; x < levelData.gridWidth; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                Rect cellRect = new Rect(
                    area.x + offsetX + x * buttonSize,
                    area.y + offsetY + (levelData.gridHeight - 1 - y) * buttonSize,
                    buttonSize,
                    buttonSize
                );
                
                DrawGridCell(pos, cellRect, selectedSlither, isDraggingHandle, isDraggingHead, draggingSlither);
            }
        }
    }
    
    /// <summary>
    /// Draw a single grid cell with all its contents
    /// </summary>
    private void DrawGridCell(Vector2Int pos, Rect cellRect, SlitherPlacementData selectedSlither, bool isDraggingHandle, bool isDraggingHead, SlitherPlacementData draggingSlither)
    {
        // Determine cell content and color
        Color cellColor = GetCellColor(pos, selectedSlither, isDraggingHandle, isDraggingHead, draggingSlither);
        string cellContent = GetCellContent(pos);
        
        // Apply cell color
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = cellColor;
        
        // Create the button using manual positioning
        if (GUI.Button(cellRect, cellContent))
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
    /// Determine the appropriate color for a grid cell
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
            return Color.Lerp(Color.cyan, Color.white, 0.3f);
        }
        
        // Check if this is the hole preview position (during drag)
        if (isDraggingHole && pos == holePreviewPosition)
        {
            return Color.Lerp(PREVIEW_COLOR, Color.white, 0.3f);
        }
        
        // Check if this cell contains a hole
        var hole = levelData.holes.FirstOrDefault(h => h.position == pos);
        if (hole != null)
        {
            // If this hole is being dragged, make it semi-transparent
            if (isDraggingHole && hole == draggingHole)
            {
                return Color.Lerp(hole.color.ToUnityColor(), Color.white, 0.7f);
            }
            return Color.Lerp(hole.color.ToUnityColor(), Color.black, 0.3f);
        }
        
        // Check if this cell contains a slither segment
        var slither = levelData.slithers.FirstOrDefault(s => s.bodyPositions.Contains(pos));
        if (slither != null)
        {
            Color baseColor = slither.color.ToUnityColor();
            
            // Highlight selected slither
            if (slither == selectedSlither)
            {
                baseColor = Color.Lerp(baseColor, SELECTED_CELL_COLOR, 0.4f);
            }
            
            // Special highlight for dragging slither
            if (isDraggingHandle && slither == draggingSlither)
            {
                baseColor = Color.Lerp(baseColor, PREVIEW_COLOR, 0.4f);
            }
            
            return baseColor;
        }
        
        // Highlight hovered cell
        if (hoveredCell.HasValue && hoveredCell.Value == pos)
        {
            return Color.Lerp(Color.white, Color.gray, 0.2f);
        }
        
        // Default cell color
        return Color.white;
    }
    
    /// <summary>
    /// Get the text content to display in a grid cell
    /// </summary>
    private string GetCellContent(Vector2Int pos)
    {
        // Check for painting preview
        if (paintingPositions.Contains(pos))
        {
            int index = paintingPositions.IndexOf(pos);
            if (index == 0)
                return "H"; // Head
            else if (index == paintingPositions.Count - 1)
                return "T"; // Tail
            else
                return (index + 1).ToString(); // Body segment number
        }
        
        // Check for hole preview position
        if (isDraggingHole && pos == holePreviewPosition)
        {
            return "◐"; // Half-filled circle for hole preview
        }
        
        // Check for hole
        var hole = levelData.holes.FirstOrDefault(h => h.position == pos);
        if (hole != null)
        {
            return "●"; // Filled circle for hole
        }
        
        // Check for slither
        var slither = levelData.slithers.FirstOrDefault(s => s.bodyPositions.Contains(pos));
        if (slither != null)
        {
            int segmentIndex = slither.bodyPositions.IndexOf(pos);
            
            // Head
            if (segmentIndex == 0)
            {
                return "◉"; // Filled circle with ring for head
            }
            // Tail
            else if (segmentIndex == slither.bodyPositions.Count - 1)
            {
                return "◎"; // Circle with center dot for tail
            }
            // Body
            else
            {
                return "○"; // Empty circle for body
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
}

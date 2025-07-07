using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Handles the inspector panel rendering for the Level Editor.
/// Displays detailed information and controls for selected objects.
/// </summary>
public class LevelEditorInspector
{
    // Events for communication with main window
    public System.Action<SlitherColor, SlitherColor> OnSlitherColorChanged;
    public System.Action OnSlitherDeleted;
    public System.Action<bool> OnSegmentRemoved; // true for head, false for tail
    public System.Action<bool> OnSegmentAdded; // true for head, false for tail
    public System.Action<SlitherInteractor> OnInteractorAdded;
    public System.Action<SlitherInteractor> OnInteractorRemoved;

    // Events for position movement
    public System.Action<Vector2Int> OnMoveHead; // direction vector
    public System.Action<Vector2Int> OnMoveTail; // direction vector

    /// <summary>
    /// Draw the inspector panel
    /// </summary>
    public void DrawInspector(LevelData levelData, SlitherPlacementData selectedSlither, Vector2Int? hoveredCell)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

        if (selectedSlither != null)
        {
            DrawSlitherInspector(levelData, selectedSlither);
        }
        else if (hoveredCell.HasValue)
        {
            DrawCellInspector(levelData, hoveredCell.Value);
        }
        else
        {
            DrawGeneralInspector(levelData);
        }

        EditorGUILayout.EndVertical();
    }    /// <summary>
         /// Draw inspector when a slither is selected
         /// </summary>
    private void DrawSlitherInspector(LevelData levelData, SlitherPlacementData selectedSlither)
    {
        EditorGUILayout.LabelField("Selected Slither", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Basic information
        EditorGUILayout.LabelField($"Slither #{levelData.slithers.IndexOf(selectedSlither) + 1}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"ID: {selectedSlither.id}");
        EditorGUILayout.LabelField($"Length: {selectedSlither.bodyPositions.Count} segments");
        EditorGUILayout.LabelField($"Head position: {selectedSlither.bodyPositions[0]}");
        EditorGUILayout.LabelField($"Tail position: {selectedSlither.bodyPositions[selectedSlither.bodyPositions.Count - 1]}");

        EditorGUILayout.Space();

        // Color management
        EditorGUILayout.LabelField("Color:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();

        SlitherColor oldColor = selectedSlither.color;
        SlitherColor newColor = (SlitherColor)EditorGUILayout.EnumPopup("Color", selectedSlither.color);

        if (newColor != oldColor)
        {
            selectedSlither.color = newColor;
            OnSlitherColorChanged?.Invoke(oldColor, newColor);
        }

        // Color preview
        EditorGUILayout.ColorField(GUIContent.none, selectedSlither.color.ToUnityColor(), false, false, false, GUILayout.Width(40));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Position editing controls
        DrawPositionControls(selectedSlither);

        EditorGUILayout.Space();

        // Interactor management
        DrawInteractorSection(selectedSlither);

        EditorGUILayout.Space();

        // Snake management
        DrawSnakeManagement(selectedSlither);
    }

    /// <summary>
    /// Draw inspector when hovering over a cell
    /// </summary>
    private void DrawCellInspector(LevelData levelData, Vector2Int cellPos)
    {
        EditorGUILayout.LabelField("Cell Information", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Position: ({cellPos.x}, {cellPos.y})");

        // Check what's in this cell
        var slither = levelData.slithers.FirstOrDefault(s => s.bodyPositions.Contains(cellPos));
        var hole = levelData.holes.FirstOrDefault(h => h.position == cellPos);

        if (slither != null)
        {
            int slitherIndex = levelData.slithers.IndexOf(slither);
            int segmentIndex = slither.bodyPositions.IndexOf(cellPos);
            string segmentType = segmentIndex == 0 ? "Head" :
                               segmentIndex == slither.bodyPositions.Count - 1 ? "Tail" : "Body";

            EditorGUILayout.LabelField($"Contains: Slither #{slitherIndex + 1}");
            EditorGUILayout.LabelField($"Segment: {segmentType}");
            EditorGUILayout.LabelField($"Color: {slither.color}");

            if (slither.interactors.Count > 0)
            {
                EditorGUILayout.LabelField("Interactors:");
                foreach (var interactor in slither.interactors)
                {
                    if (interactor is ChainInteractor chain)
                        EditorGUILayout.LabelField($"  • Chain (Hits: {chain.hitCount})");
                    else if (interactor is CocoonInteractor cocoon)
                        EditorGUILayout.LabelField($"  • Cocoon (Hits: {cocoon.hitCount})");
                }
            }
        }

        if (hole != null)
        {
            EditorGUILayout.LabelField($"Contains: Hole");
            EditorGUILayout.LabelField($"Color: {hole.color}");
            EditorGUILayout.LabelField($"Slither ID: {hole.slitherId}");
        }

        if (slither == null && hole == null)
        {
            EditorGUILayout.LabelField("Contains: Empty");
        }
    }

    /// <summary>
    /// Draw general inspector when nothing is selected
    /// </summary>
    private void DrawGeneralInspector(LevelData levelData)
    {
        EditorGUILayout.LabelField("Level Information", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Grid Size: {levelData.gridWidth} x {levelData.gridHeight}");
        EditorGUILayout.LabelField($"Slithers: {levelData.slithers.Count}");
        EditorGUILayout.LabelField($"Holes: {levelData.holes.Count}");

        EditorGUILayout.Space();

        // Level validation
        if (GUILayout.Button("Validate Level"))
        {
            bool isValid = levelData.ValidateSlithersHaveHoles(out string errorMessage);
            if (isValid)
            {
                EditorUtility.DisplayDialog("Validation Result", "Level is valid!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Validation Errors", errorMessage, "OK");
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Usage Instructions", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("• Click on a snake to select it and see segment management options\n• Use keyboard shortcuts: Insert/PageDown to add segments, Home/End to remove\n• Hover over cells to see detailed information", MessageType.Info);

        if (levelData.slithers.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Available Snakes:", EditorStyles.miniLabel);
            for (int i = 0; i < levelData.slithers.Count; i++)
            {
                var slither = levelData.slithers[i];
                EditorGUILayout.LabelField($"  #{i + 1}: {slither.color} ({slither.bodyPositions.Count} segments)");
            }
        }
    }

    /// <summary>
    /// Draw the interactor management section
    /// </summary>
    private void DrawInteractorSection(SlitherPlacementData selectedSlither)
    {
        EditorGUILayout.LabelField("Interactors", EditorStyles.boldLabel);

        if (selectedSlither.interactors.Count == 0)
        {
            EditorGUILayout.LabelField("No interactors", EditorStyles.miniLabel);
        }
        else
        {
            // Display existing interactors
            for (int i = selectedSlither.interactors.Count - 1; i >= 0; i--)
            {
                var interactor = selectedSlither.interactors[i];

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                if (interactor is ChainInteractor chain)
                {
                    EditorGUILayout.LabelField("Chain Interactor", EditorStyles.boldLabel);
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        selectedSlither.interactors.RemoveAt(i);
                        OnInteractorRemoved?.Invoke(interactor);
                    }
                    EditorGUILayout.EndHorizontal();
                    chain.hitCount = EditorGUILayout.IntField("Hit Count", chain.hitCount);
                }
                else if (interactor is CocoonInteractor cocoon)
                {
                    EditorGUILayout.LabelField("Cocoon Interactor", EditorStyles.boldLabel);
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        selectedSlither.interactors.RemoveAt(i);
                        OnInteractorRemoved?.Invoke(interactor);
                    }
                    EditorGUILayout.EndHorizontal();
                    cocoon.hitCount = EditorGUILayout.IntField("Hit Count", cocoon.hitCount);
                }

                EditorGUILayout.EndVertical();
            }
        }

        // Add new interactor button
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Interactor"))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Chain"), false, () =>
            {
                var newInteractor = new ChainInteractor();
                selectedSlither.interactors.Add(newInteractor);
                OnInteractorAdded?.Invoke(newInteractor);
            });
            menu.AddItem(new GUIContent("Cocoon"), false, () =>
            {
                var newInteractor = new CocoonInteractor();
                selectedSlither.interactors.Add(newInteractor);
                OnInteractorAdded?.Invoke(newInteractor);
            });
            menu.ShowAsContext();
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draw the snake management section
    /// </summary>
    private void DrawSnakeManagement(SlitherPlacementData selectedSlither)
    {
        EditorGUILayout.LabelField("Snake Management", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box");

        // Length increase controls
        EditorGUILayout.LabelField("Increase Length:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal(); if (GUILayout.Button("Add Head Segment", GUILayout.Height(25)))
        {
            OnSegmentAdded?.Invoke(true);
        }

        if (GUILayout.Button("Add Tail Segment", GUILayout.Height(25)))
        {
            OnSegmentAdded?.Invoke(false);
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
                OnSegmentRemoved?.Invoke(true);
            }
        }

        if (GUILayout.Button("Remove Tail Segment", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Remove Tail Segment",
                "Are you sure you want to remove the tail segment of this snake?", "Yes", "Cancel"))
            {
                OnSegmentRemoved?.Invoke(false);
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
                OnSlitherDeleted?.Invoke();
            }
        }

        GUI.backgroundColor = defaultBgColor;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Keyboard: Shift+Delete = Delete Snake", EditorStyles.miniLabel);
    }

    /// <summary>
    /// Draw position editing controls for head and tail
    /// </summary>
    private void DrawPositionControls(SlitherPlacementData selectedSlither)
    {
        EditorGUILayout.LabelField("Position Controls", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Move Head:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↑", GUILayout.Width(30), GUILayout.Height(25)))
        {
            OnMoveHead?.Invoke(Vector2Int.up);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("←", GUILayout.Width(30), GUILayout.Height(25)))
        {
            OnMoveHead?.Invoke(Vector2Int.left);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("→", GUILayout.Width(30), GUILayout.Height(25)))
        {
            OnMoveHead?.Invoke(Vector2Int.right);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↓", GUILayout.Width(30), GUILayout.Height(25)))
        {
            OnMoveHead?.Invoke(Vector2Int.down);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Move Tail:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↑", GUILayout.Width(30), GUILayout.Height(25)))
        {
            OnMoveTail?.Invoke(Vector2Int.up);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("←", GUILayout.Width(30), GUILayout.Height(25)))
        {
            OnMoveTail?.Invoke(Vector2Int.left);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("→", GUILayout.Width(30), GUILayout.Height(25)))
        {
            OnMoveTail?.Invoke(Vector2Int.right);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↓", GUILayout.Width(30), GUILayout.Height(25)))
        {
            OnMoveTail?.Invoke(Vector2Int.down);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Tip: Use Move tool to drag handles directly on grid", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }
}

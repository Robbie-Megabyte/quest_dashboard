using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public sealed class HudPresetWindowPlacement
{
    [Tooltip("Must match HudWindow.windowId exactly.")]
    public string windowId = "window";

    [Min(0)]
    public int column = 0;

    [Min(0)]
    public int row = 0;

    [Min(1)]
    public int columnSpan = 1;

    [Min(1)]
    public int rowSpan = 1;
}


[Serializable]
public sealed class HudPresetDefinition
{
    public string presetId = "preset";

    public string displayName = "Preset";

    [Tooltip(
        "Built-in presets will later be protected from deletion."
    )]
    public bool builtIn = true;

    public List<HudPresetWindowPlacement> windows =
        new List<HudPresetWindowPlacement>();
}


[DisallowMultipleComponent]
public sealed class HudPresetManager : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField]
    private Transform windowLayer;

    [SerializeField]
    private HudGridManager gridManager;

    [SerializeField]
    private HudModeManager modeManager;


    [Header("Built-In Presets")]
    [SerializeField]
    private List<HudPresetDefinition> builtInPresets =
        new List<HudPresetDefinition>();


    public IReadOnlyList<HudPresetDefinition> BuiltInPresets
    {
        get
        {
            return builtInPresets;
        }
    }


    private void Awake()
    {
        ResolveReferences();
    }


    private void OnValidate()
    {
        ResolveReferences();
    }


    private void ResolveReferences()
    {
        if (gridManager == null)
        {
            gridManager =
                UnityEngine.Object.FindAnyObjectByType
                <HudGridManager>();
        }

        if (modeManager == null)
        {
            modeManager =
                UnityEngine.Object.FindAnyObjectByType
                <HudModeManager>();
        }

        if (windowLayer == null)
        {
            Transform candidate =
                transform.Find("HUD_WindowLayer");

            if (candidate == null &&
                transform.parent != null)
            {
                candidate =
                    transform.parent.Find(
                        "HUD_WindowLayer"
                    );
            }

            if (candidate != null)
            {
                windowLayer = candidate;
            }
        }
    }


    // ========================================================
    // PUBLIC API
    // ========================================================

    public bool ApplyBuiltInPreset(
        int presetIndex)
    {
        ResolveReferences();

        if (
            presetIndex < 0 ||
            presetIndex >= builtInPresets.Count
        )
        {
            Debug.LogError(
                $"HUD PRESET: index {presetIndex} is invalid."
            );

            return false;
        }

        return ApplyPreset(
            builtInPresets[presetIndex]
        );
    }


    public bool ApplyPreset(
        HudPresetDefinition preset)
    {
        ResolveReferences();

        if (modeManager != null &&
            !modeManager.IsEditMode)
        {
            Debug.LogWarning(
                "HUD PRESET: presets can only be loaded " +
                "while the HUD is in Edit mode."
            );

            return false;
        }

        if (!TryValidatePreset(
                preset,
                out Dictionary<string, HudWindow>
                    availableWindows,
                out string validationError))
        {
            Debug.LogError(
                "HUD PRESET: preset was not applied: " +
                validationError
            );

            return false;
        }

        /*
         * Nothing is closed until the complete preset has
         * passed validation.
         */
        CloseAllManagedWindows();

        foreach (
            HudPresetWindowPlacement placement
            in preset.windows)
        {
            string windowId =
                placement.windowId.Trim();

            HudWindow window =
                availableWindows[windowId];

            GameObject windowObject =
                window.gameObject;

            HudGridWindowController controller =
                windowObject.GetComponent
                    <HudGridWindowController>();

            windowObject.SetActive(true);

            controller.SetHudGridEnabled(true);

            if (window.sphereConstraint != null)
            {
                window.sphereConstraint
                    .EnableHudConstraint();
            }

            bool placed =
                gridManager.TryPlaceWindowExact(
                    controller,
                    placement.column,
                    placement.row,
                    placement.columnSpan,
                    placement.rowSpan
                );

            if (!placed)
            {
                Debug.LogError(
                    $"HUD PRESET: unexpected placement " +
                    $"failure for window '{windowId}'."
                );

                CloseAllManagedWindows();

                return false;
            }

            HudWindowModePresentation presentation =
                windowObject.GetComponent
                    <HudWindowModePresentation>();

            if (presentation != null &&
                modeManager != null)
            {
                presentation.ApplyMode(
                    modeManager.CurrentMode
                );
            }
        }

        Debug.Log(
            $"HUD PRESET: applied '{preset.displayName}'."
        );

        return true;
    }


    // ========================================================
    // VALIDATION
    // ========================================================

    private bool TryValidatePreset(
        HudPresetDefinition preset,
        out Dictionary<string, HudWindow>
            availableWindows,
        out string error)
    {
        availableWindows =
            BuildWindowLookup();

        error = null;

        if (gridManager == null)
        {
            error = "HudGridManager is missing.";
            return false;
        }

        if (windowLayer == null)
        {
            error = "HUD_WindowLayer is missing.";
            return false;
        }

        if (preset == null)
        {
            error = "Preset is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                preset.presetId))
        {
            error = "Preset ID is empty.";
            return false;
        }

        if (preset.windows == null ||
            preset.windows.Count == 0)
        {
            error = "Preset contains no windows.";
            return false;
        }

        bool[,] reserved =
            new bool[
                gridManager.columns,
                gridManager.rows
            ];

        var usedWindowIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (
            HudPresetWindowPlacement placement
            in preset.windows)
        {
            if (placement == null)
            {
                error =
                    "Preset contains a null placement.";

                return false;
            }

            string windowId =
                (
                    placement.windowId ??
                    string.Empty
                ).Trim();

            if (windowId.Length == 0)
            {
                error =
                    "A placement has an empty window ID.";

                return false;
            }

            if (!availableWindows.ContainsKey(
                    windowId))
            {
                error =
                    $"Window '{windowId}' does not exist.";

                return false;
            }

            if (!usedWindowIds.Add(windowId))
            {
                error =
                    $"Window '{windowId}' appears more than once.";

                return false;
            }

            if (
                placement.column < 0 ||
                placement.row < 0 ||
                placement.columnSpan < 1 ||
                placement.rowSpan < 1 ||
                placement.column +
                    placement.columnSpan >
                    gridManager.columns ||
                placement.row +
                    placement.rowSpan >
                    gridManager.rows
            )
            {
                error =
                    $"Window '{windowId}' is outside the " +
                    $"{gridManager.columns}x" +
                    $"{gridManager.rows} grid.";

                return false;
            }

            for (
                int row = placement.row;
                row <
                    placement.row +
                    placement.rowSpan;
                ++row)
            {
                for (
                    int column = placement.column;
                    column <
                        placement.column +
                        placement.columnSpan;
                    ++column)
                {
                    if (reserved[column, row])
                    {
                        error =
                            $"Window '{windowId}' overlaps " +
                            "another preset window.";

                        return false;
                    }

                    reserved[column, row] =
                        true;
                }
            }
        }

        return true;
    }


    private Dictionary<string, HudWindow>
        BuildWindowLookup()
    {
        var result =
            new Dictionary<string, HudWindow>(
                StringComparer.Ordinal
            );

        if (windowLayer == null)
            return result;

        HudWindow[] windows =
            windowLayer.GetComponentsInChildren
                <HudWindow>(true);

        foreach (HudWindow window in windows)
        {
            if (window == null ||
                string.IsNullOrWhiteSpace(
                    window.windowId))
            {
                continue;
            }

            string windowId =
                window.windowId.Trim();

            if (result.ContainsKey(windowId))
            {
                Debug.LogError(
                    $"HUD PRESET: duplicate HudWindow ID " +
                    $"'{windowId}'."
                );

                continue;
            }

            result.Add(
                windowId,
                window
            );
        }

        return result;
    }


    // ========================================================
    // REPLACE CURRENT LAYOUT
    // ========================================================

    private void CloseAllManagedWindows()
    {
        if (windowLayer == null)
            return;

        HudWindow[] windows =
            windowLayer.GetComponentsInChildren
                <HudWindow>(true);

        foreach (HudWindow window in windows)
        {
            if (window == null)
                continue;

            HudGridWindowController controller =
                window.GetComponent
                    <HudGridWindowController>();

            if (controller != null)
            {
                if (gridManager != null)
                {
                    gridManager.UnregisterWindow(
                        controller
                    );
                }

                controller.SetGridPlaced(false);

                controller.SetHudGridEnabled(false);
            }

            window.gameObject.SetActive(false);
        }
    }


    // ========================================================
    // INSPECTOR TESTING
    // ========================================================

    [ContextMenu("Validate Built-In Presets")]
    private void ValidateBuiltInPresets()
    {
        ResolveReferences();

        if (builtInPresets.Count == 0)
        {
            Debug.LogWarning(
                "HUD PRESET: no built-in presets exist."
            );

            return;
        }

        for (
            int index = 0;
            index < builtInPresets.Count;
            ++index)
        {
            HudPresetDefinition preset =
                builtInPresets[index];

            if (TryValidatePreset(
                    preset,
                    out _,
                    out string error))
            {
                Debug.Log(
                    $"HUD PRESET: [{index}] " +
                    $"'{preset.displayName}' is valid."
                );
            }
            else
            {
                Debug.LogError(
                    $"HUD PRESET: [{index}] is invalid: " +
                    error
                );
            }
        }
    }


    [ContextMenu("Apply First Built-In Preset")]
    private void ApplyFirstBuiltInPreset()
    {
        ApplyBuiltInPreset(0);
    }
}
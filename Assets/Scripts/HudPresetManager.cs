using System;
using System.Collections.Generic;
using System.IO;
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
        "Built-in presets cannot be renamed or deleted."
    )]
    public bool builtIn = true;

    public List<HudPresetWindowPlacement> windows =
        new List<HudPresetWindowPlacement>();
}


[Serializable]
public sealed class HudPresetSaveFile
{
    public int schemaVersion = 1;

    public List<HudPresetDefinition> customPresets =
        new List<HudPresetDefinition>();
}


[DisallowMultipleComponent]
public sealed class HudPresetManager : MonoBehaviour
{
    private const int CurrentSchemaVersion = 1;
    public const int MaximumPresetNameLength = 20;

    private const string PresetFileName =
        "hud_presets_v1.json";


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


    [Header("Custom Preset Storage")]
    [Min(1)]
    [SerializeField]
    private int maximumCustomPresets = 10;


    private readonly List<HudPresetDefinition>
        customPresets =
            new List<HudPresetDefinition>();

    private readonly List<HudPresetDefinition>
        allPresets =
            new List<HudPresetDefinition>();


    public event Action PresetsChanged;


    public IReadOnlyList<HudPresetDefinition>
        BuiltInPresets
    {
        get
        {
            return builtInPresets;
        }
    }


    public IReadOnlyList<HudPresetDefinition>
        CustomPresets
    {
        get
        {
            return customPresets;
        }
    }


    public IReadOnlyList<HudPresetDefinition>
        AllPresets
    {
        get
        {
            return allPresets;
        }
    }


    public int MaximumCustomPresets
    {
        get
        {
            return maximumCustomPresets;
        }
    }


    public int CustomPresetCount
    {
        get
        {
            return customPresets.Count;
        }
    }


    public int RemainingCustomPresetSlots
    {
        get
        {
            return Mathf.Max(
                0,
                maximumCustomPresets -
                customPresets.Count
            );
        }
    }


    public bool CanCreateCustomPreset
    {
        get
        {
            return
                customPresets.Count <
                maximumCustomPresets;
        }
    }


    public string CustomPresetFilePath
    {
        get
        {
            return Path.Combine(
                Application.persistentDataPath,
                PresetFileName
            );
        }
    }


    private string CustomPresetBackupPath
    {
        get
        {
            return CustomPresetFilePath + ".bak";
        }
    }


    private void Awake()
    {
        maximumCustomPresets =
            Mathf.Max(
                1,
                maximumCustomPresets
            );

        ResolveReferences();
        NormalizeBuiltInPresets();
        ReloadCustomPresets();
    }


    private void OnValidate()
    {
        maximumCustomPresets =
            Mathf.Max(
                1,
                maximumCustomPresets
            );

        ResolveReferences();
        NormalizeBuiltInPresets();
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

    public static bool TryNormalizePresetName(
        string rawName,
        out string normalizedName,
        out string error)
    {
        normalizedName =
            (
                rawName ??
                string.Empty
            ).Trim();

        error = null;

        if (normalizedName.Length == 0)
        {
            error = "Preset name is empty.";
            return false;
        }

        if (
            normalizedName.Length >
            MaximumPresetNameLength
        )
        {
            error =
                $"Preset names can contain at most " +
                $"{MaximumPresetNameLength} characters.";

            return false;
        }

        /*
        * Spaces inside the name are deliberately allowed.
        * Only leading and trailing whitespace is removed.
        */
        return true;
    }


    // ========================================================
    // PUBLIC PRESET API
    // ========================================================

    /*
     * Kept for compatibility with the existing Operations
     * preset button.
     */
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
                $"HUD PRESET: built-in index " +
                $"{presetIndex} is invalid."
            );

            return false;
        }

        return ApplyPreset(
            builtInPresets[presetIndex]
        );
    }


    public bool ApplyPresetById(
        string presetId)
    {
        if (!TryGetPresetById(
                presetId,
                out HudPresetDefinition preset))
        {
            Debug.LogError(
                $"HUD PRESET: preset ID " +
                $"'{presetId}' was not found."
            );

            return false;
        }

        return ApplyPreset(preset);
    }


    public bool TryGetPresetById(
        string presetId,
        out HudPresetDefinition preset)
    {
        preset = null;

        string wantedId =
            (
                presetId ??
                string.Empty
            ).Trim();

        if (wantedId.Length == 0)
            return false;

        foreach (
            HudPresetDefinition candidate
            in allPresets)
        {
            if (candidate == null)
                continue;

            if (string.Equals(
                    candidate.presetId,
                    wantedId,
                    StringComparison.Ordinal))
            {
                preset = candidate;
                return true;
            }
        }

        return false;
    }


    public bool TryAddCustomPreset(
        HudPresetDefinition preset,
        out string error)
    {
        ResolveReferences();

        error = null;

        if (!CanCreateCustomPreset)
        {
            error =
                $"The custom preset limit of " +
                $"{maximumCustomPresets} has been reached.";

            return false;
        }

        if (preset == null)
        {
            error = "Preset is null.";
            return false;
        }

        HudPresetDefinition storedPreset =
            ClonePreset(preset);

        storedPreset.builtIn = false;

        storedPreset.presetId =
            (
                storedPreset.presetId ??
                string.Empty
            ).Trim();

        if (!TryNormalizePresetName(
                storedPreset.displayName,
                out string normalizedName,
                out error))
        {
            return false;
        }

        storedPreset.displayName =
            normalizedName;

        if (storedPreset.presetId.Length == 0)
        {
            error = "Preset ID is empty.";
            return false;
        }

        if (TryGetPresetById(
                storedPreset.presetId,
                out _))
        {
            error =
                $"Preset ID '{storedPreset.presetId}' " +
                "already exists.";

            return false;
        }

        if (!TryValidatePreset(
                storedPreset,
                out _,
                out error))
        {
            return false;
        }

        customPresets.Add(storedPreset);
        RebuildAllPresets();

        if (!TryWriteCustomPresets(
                out string saveError))
        {
            customPresets.Remove(storedPreset);
            RebuildAllPresets();

            error =
                "The preset could not be saved: " +
                saveError;

            return false;
        }

        Debug.Log(
            $"HUD PRESET STORAGE: saved custom preset " +
            $"'{storedPreset.displayName}' " +
            $"({customPresets.Count}/" +
            $"{maximumCustomPresets})."
        );

        PresetsChanged?.Invoke();

        return true;
    }


    public bool TryDeleteCustomPreset(
        string presetId,
        out string error)
    {
        error = null;

        string wantedId =
            (
                presetId ??
                string.Empty
            ).Trim();

        if (wantedId.Length == 0)
        {
            error = "Preset ID is empty.";
            return false;
        }

        int presetIndex = -1;

        for (
            int index = 0;
            index < customPresets.Count;
            ++index)
        {
            HudPresetDefinition candidate =
                customPresets[index];

            if (candidate != null &&
                string.Equals(
                    candidate.presetId,
                    wantedId,
                    StringComparison.Ordinal))
            {
                presetIndex = index;
                break;
            }
        }

        if (presetIndex < 0)
        {
            if (TryGetPresetById(
                    wantedId,
                    out HudPresetDefinition existing) &&
                existing.builtIn)
            {
                error =
                    "Built-in presets cannot be deleted.";
            }
            else
            {
                error =
                    $"Custom preset '{wantedId}' " +
                    "was not found.";
            }

            return false;
        }

        HudPresetDefinition removedPreset =
            customPresets[presetIndex];

        customPresets.RemoveAt(presetIndex);
        RebuildAllPresets();

        if (!TryWriteCustomPresets(
                out string saveError))
        {
            customPresets.Insert(
                presetIndex,
                removedPreset
            );

            RebuildAllPresets();

            error =
                "The preset could not be deleted: " +
                saveError;

            return false;
        }

        Debug.Log(
            $"HUD PRESET STORAGE: deleted custom preset " +
            $"'{removedPreset.displayName}'."
        );

        PresetsChanged?.Invoke();

        return true;
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
    // PERSISTENT STORAGE
    // ========================================================

    public void ReloadCustomPresets()
    {
        ResolveReferences();
        NormalizeBuiltInPresets();

        customPresets.Clear();

        string primaryPath =
            CustomPresetFilePath;

        string backupPath =
            CustomPresetBackupPath;

        bool primaryExists =
            File.Exists(primaryPath);

        bool backupExists =
            File.Exists(backupPath);

        if (!primaryExists &&
            !backupExists)
        {
            RebuildAllPresets();

            Debug.Log(
                "HUD PRESET STORAGE: no custom preset " +
                "file exists yet; using built-in presets " +
                $"only. Custom capacity is " +
                $"{maximumCustomPresets}."
            );

            PresetsChanged?.Invoke();

            return;
        }

        HudPresetSaveFile saveFile = null;
        string loadSource = null;
        string primaryError = null;

        if (primaryExists &&
            TryReadSaveFile(
                primaryPath,
                out saveFile,
                out primaryError))
        {
            loadSource = primaryPath;
        }
        else
        {
            if (primaryExists)
            {
                Debug.LogWarning(
                    "HUD PRESET STORAGE: primary file " +
                    $"could not be loaded: {primaryError}"
                );
            }

            string backupError = null;

            if (backupExists &&
                TryReadSaveFile(
                    backupPath,
                    out saveFile,
                    out backupError))
            {
                loadSource = backupPath;

                Debug.LogWarning(
                    "HUD PRESET STORAGE: recovered custom " +
                    "presets from the backup file."
                );
            }
            else if (backupExists)
            {
                Debug.LogError(
                    "HUD PRESET STORAGE: backup file " +
                    $"could not be loaded: {backupError}"
                );
            }
        }

        if (saveFile == null)
        {
            RebuildAllPresets();

            Debug.LogError(
                "HUD PRESET STORAGE: no valid custom " +
                "preset file could be loaded. Built-in " +
                "presets remain available."
            );

            PresetsChanged?.Invoke();

            return;
        }

        var usedPresetIds =
            BuildBuiltInIdSet();

        if (saveFile.customPresets != null)
        {
            foreach (
                HudPresetDefinition loadedPreset
                in saveFile.customPresets)
            {
                if (
                    customPresets.Count >=
                    maximumCustomPresets
                )
                {
                    Debug.LogWarning(
                        "HUD PRESET STORAGE: the saved " +
                        "custom preset count exceeds the " +
                        $"limit of {maximumCustomPresets}. " +
                        "Additional presets were ignored."
                    );

                    break;
                }

                if (!TryPrepareLoadedCustomPreset(
                        loadedPreset,
                        usedPresetIds,
                        out HudPresetDefinition prepared,
                        out string presetError))
                {
                    Debug.LogWarning(
                        "HUD PRESET STORAGE: ignored an " +
                        "invalid saved preset: " +
                        presetError
                    );

                    continue;
                }

                customPresets.Add(prepared);

                usedPresetIds.Add(
                    prepared.presetId
                );
            }
        }

        RebuildAllPresets();

        Debug.Log(
            $"HUD PRESET STORAGE: loaded " +
            $"{customPresets.Count}/" +
            $"{maximumCustomPresets} custom presets " +
            $"from '{loadSource}'."
        );

        PresetsChanged?.Invoke();
    }


    private bool TryReadSaveFile(
        string path,
        out HudPresetSaveFile saveFile,
        out string error)
    {
        saveFile = null;
        error = null;

        try
        {
            string json =
                File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "The file is empty.";
                return false;
            }

            saveFile =
                JsonUtility.FromJson
                    <HudPresetSaveFile>(json);

            if (saveFile == null)
            {
                error =
                    "The JSON data could not be parsed.";

                return false;
            }

            if (
                saveFile.schemaVersion !=
                CurrentSchemaVersion
            )
            {
                error =
                    $"Unsupported schema version " +
                    $"{saveFile.schemaVersion}; expected " +
                    $"{CurrentSchemaVersion}.";

                saveFile = null;

                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error =
                exception.GetType().Name +
                ": " +
                exception.Message;

            saveFile = null;

            return false;
        }
    }


    private bool TryWriteCustomPresets(
        out string error)
    {
        error = null;

        string primaryPath =
            CustomPresetFilePath;

        string temporaryPath =
            primaryPath + ".tmp";

        string backupPath =
            CustomPresetBackupPath;

        var saveFile =
            new HudPresetSaveFile
            {
                schemaVersion =
                    CurrentSchemaVersion,

                customPresets =
                    new List<HudPresetDefinition>()
            };

        foreach (
            HudPresetDefinition preset
            in customPresets)
        {
            saveFile.customPresets.Add(
                ClonePreset(preset)
            );
        }

        try
        {
            Directory.CreateDirectory(
                Application.persistentDataPath
            );

            string json =
                JsonUtility.ToJson(
                    saveFile,
                    true
                );

            File.WriteAllText(
                temporaryPath,
                json
            );

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            if (File.Exists(primaryPath))
            {
                File.Move(
                    primaryPath,
                    backupPath
                );
            }

            try
            {
                File.Move(
                    temporaryPath,
                    primaryPath
                );
            }
            catch
            {
                if (
                    !File.Exists(primaryPath) &&
                    File.Exists(backupPath)
                )
                {
                    File.Move(
                        backupPath,
                        primaryPath
                    );
                }

                throw;
            }

            return true;
        }
        catch (Exception exception)
        {
            error =
                exception.GetType().Name +
                ": " +
                exception.Message;

            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
                // Preserve the original storage error.
            }

            return false;
        }
    }


    private bool TryPrepareLoadedCustomPreset(
        HudPresetDefinition loadedPreset,
        HashSet<string> usedPresetIds,
        out HudPresetDefinition preparedPreset,
        out string error)
    {
        preparedPreset = null;
        error = null;

        if (loadedPreset == null)
        {
            error = "Preset is null.";
            return false;
        }

        preparedPreset =
            ClonePreset(loadedPreset);

        preparedPreset.builtIn = false;

        preparedPreset.presetId =
            (
                preparedPreset.presetId ??
                string.Empty
            ).Trim();

        if (!TryNormalizePresetName(
                preparedPreset.displayName,
                out string normalizedName,
                out error))
        {
            return false;
        }

        preparedPreset.displayName =
            normalizedName;

        if (preparedPreset.presetId.Length == 0)
        {
            error = "Preset ID is empty.";
            return false;
        }


        if (usedPresetIds.Contains(
                preparedPreset.presetId))
        {
            error =
                $"Preset ID '{preparedPreset.presetId}' " +
                "is duplicated.";

            return false;
        }

        if (!TryValidatePreset(
                preparedPreset,
                out _,
                out error))
        {
            return false;
        }

        return true;
    }


    // ========================================================
    // PRESET COLLECTION
    // ========================================================

    private void NormalizeBuiltInPresets()
    {
        if (builtInPresets == null)
        {
            builtInPresets =
                new List<HudPresetDefinition>();

            return;
        }

        foreach (
            HudPresetDefinition preset
            in builtInPresets)
        {
            if (preset == null)
                continue;

            preset.builtIn = true;

            preset.presetId =
                (
                    preset.presetId ??
                    string.Empty
                ).Trim();

            preset.displayName =
                (
                    preset.displayName ??
                    string.Empty
                ).Trim();
        }
    }


    private HashSet<string> BuildBuiltInIdSet()
    {
        var ids =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (
            HudPresetDefinition preset
            in builtInPresets)
        {
            if (preset == null ||
                string.IsNullOrWhiteSpace(
                    preset.presetId))
            {
                continue;
            }

            ids.Add(
                preset.presetId.Trim()
            );
        }

        return ids;
    }


    private void RebuildAllPresets()
    {
        allPresets.Clear();

        var usedIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (
            HudPresetDefinition preset
            in builtInPresets)
        {
            AddPresetToCombinedList(
                preset,
                true,
                usedIds
            );
        }

        foreach (
            HudPresetDefinition preset
            in customPresets)
        {
            AddPresetToCombinedList(
                preset,
                false,
                usedIds
            );
        }
    }


    private void AddPresetToCombinedList(
        HudPresetDefinition preset,
        bool builtIn,
        HashSet<string> usedIds)
    {
        if (preset == null)
            return;

        preset.builtIn = builtIn;

        string presetId =
            (
                preset.presetId ??
                string.Empty
            ).Trim();

        if (presetId.Length == 0)
        {
            Debug.LogError(
                "HUD PRESET: a preset has an empty ID " +
                "and was excluded from the preset list."
            );

            return;
        }

        if (!usedIds.Add(presetId))
        {
            Debug.LogError(
                $"HUD PRESET: duplicate preset ID " +
                $"'{presetId}' was excluded."
            );

            return;
        }

        preset.presetId = presetId;

        if (string.IsNullOrWhiteSpace(
                preset.displayName))
        {
            preset.displayName = presetId;
        }
        else
        {
            preset.displayName =
                preset.displayName.Trim();
        }

        allPresets.Add(preset);
    }


    private static HudPresetDefinition ClonePreset(
        HudPresetDefinition source)
    {
        if (source == null)
            return null;

        var clone =
            new HudPresetDefinition
            {
                presetId =
                    source.presetId,

                displayName =
                    source.displayName,

                builtIn =
                    source.builtIn,

                windows =
                    new List
                        <HudPresetWindowPlacement>()
            };

        if (source.windows == null)
            return clone;

        foreach (
            HudPresetWindowPlacement placement
            in source.windows)
        {
            if (placement == null)
            {
                clone.windows.Add(null);
                continue;
            }

            clone.windows.Add(
                new HudPresetWindowPlacement
                {
                    windowId =
                        placement.windowId,

                    column =
                        placement.column,

                    row =
                        placement.row,

                    columnSpan =
                        placement.columnSpan,

                    rowSpan =
                        placement.rowSpan
                }
            );
        }

        return clone;
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

        if (!TryNormalizePresetName(
                preset.displayName,
                out _,
                out error))
        {
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
                    $"Window '{windowId}' appears more " +
                    "than once.";

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


    [ContextMenu("Log Preset Storage Status")]
    private void LogPresetStorageStatus()
    {
        Debug.Log(
            "HUD PRESET STORAGE: " +
            $"built-in={builtInPresets.Count}, " +
            $"custom={customPresets.Count}/" +
            $"{maximumCustomPresets}, " +
            $"remaining={RemainingCustomPresetSlots}, " +
            $"path='{CustomPresetFilePath}'."
        );
    }


    [ContextMenu("Apply First Built-In Preset")]
    private void ApplyFirstBuiltInPreset()
    {
        ApplyBuiltInPreset(0);
    }
}
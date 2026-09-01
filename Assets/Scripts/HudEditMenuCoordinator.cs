using UnityEngine;

[DisallowMultipleComponent]
public sealed class HudEditMenuCoordinator : MonoBehaviour
{
    [Header("Shared Menu")]
    [SerializeField] private HudWindowPaletteController paletteController;
    [SerializeField] private HudWindow sharedMenuWindow;
    [SerializeField] private GameObject paletteItems;
    [SerializeField] private GameObject presetItems;

    [Header("Managers")]
    [SerializeField] private HudPresetManager presetManager;
    [SerializeField] private HudModeManager modeManager;

    [Header("Titles")]
    [SerializeField] private string paletteTitle = "Windows";
    [SerializeField] private string presetsTitle = "Presets";

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();

        // The normal window palette remains the default menu page.
        SetContentVisibility(showPalette: true);

        if (sharedMenuWindow != null)
            sharedMenuWindow.SetTitle(paletteTitle);
    }

    public void TogglePaletteMenu()
    {
        ResolveReferences();

        if (!CanOpenEditMenus())
            return;

        bool paletteAlreadyOpen =
            IsSharedMenuOpen() &&
            paletteItems != null &&
            paletteItems.activeSelf;

        if (paletteAlreadyOpen)
        {
            CloseMenus();
            return;
        }

        SetContentVisibility(showPalette: true);

        if (sharedMenuWindow != null)
            sharedMenuWindow.SetTitle(paletteTitle);

        if (paletteController != null)
            paletteController.OpenMenu();
    }

    public void TogglePresetsMenu()
    {
        ResolveReferences();

        if (!CanOpenEditMenus())
            return;

        bool presetsAlreadyOpen =
            IsSharedMenuOpen() &&
            presetItems != null &&
            presetItems.activeSelf;

        if (presetsAlreadyOpen)
        {
            CloseMenus();
            return;
        }

        SetContentVisibility(showPalette: false);

        if (sharedMenuWindow != null)
            sharedMenuWindow.SetTitle(presetsTitle);

        if (paletteController != null)
            paletteController.OpenMenu();
    }

    public void ApplyPresetAndClose(int presetIndex)
    {
        ResolveReferences();

        if (!CanOpenEditMenus())
            return;

        if (presetManager == null)
        {
            Debug.LogError(
                "[HUD Presets] HudPresetManager is not assigned.",
                this);
            return;
        }

        if (presetManager.ApplyBuiltInPreset(presetIndex))
            CloseMenus();
    }

    public void CloseMenus()
    {
        if (paletteController != null)
            paletteController.CloseMenu();
    }

    private bool CanOpenEditMenus()
    {
        return modeManager == null || modeManager.IsEditMode;
    }

    private bool IsSharedMenuOpen()
    {
        return
            paletteController != null &&
            paletteController.menuPanel != null &&
            paletteController.menuPanel.activeSelf;
    }

    private void SetContentVisibility(bool showPalette)
    {
        if (paletteItems != null)
            paletteItems.SetActive(showPalette);

        if (presetItems != null)
            presetItems.SetActive(!showPalette);
    }

    private void ResolveReferences()
    {
        if (paletteController == null)
            paletteController =
                UnityEngine.Object.FindAnyObjectByType<HudWindowPaletteController>();

        if (presetManager == null)
            presetManager =
                UnityEngine.Object.FindAnyObjectByType<HudPresetManager>();

        if (modeManager == null)
            modeManager =
                UnityEngine.Object.FindAnyObjectByType<HudModeManager>();

        if (sharedMenuWindow == null &&
            paletteController != null &&
            paletteController.menuPanel != null)
        {
            sharedMenuWindow =
                paletteController.menuPanel.GetComponent<HudWindow>();
        }

        if (sharedMenuWindow == null)
            return;

        Transform menuTransform = sharedMenuWindow.transform;

        if (paletteItems == null)
        {
            Transform found = menuTransform.Find("PaletteItems");

            if (found != null)
                paletteItems = found.gameObject;
        }

        if (presetItems == null)
        {
            Transform found = menuTransform.Find("PresetItems");

            if (found != null)
                presetItems = found.gameObject;
        }
    }
}
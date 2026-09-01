using UnityEngine;

[DisallowMultipleComponent]
public sealed class HudPresetButton : MonoBehaviour
{
    [SerializeField] private HudEditMenuCoordinator menuCoordinator;

    [Min(0)]
    [SerializeField] private int presetIndex;

    public void Activate()
    {
        ResolveReferences();

        if (menuCoordinator == null)
        {
            Debug.LogError(
                "[HUD Presets] Menu coordinator was not found.",
                this);
            return;
        }

        menuCoordinator.ApplyPresetAndClose(presetIndex);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (menuCoordinator == null)
        {
            menuCoordinator =
                GetComponentInParent<HudEditMenuCoordinator>(true);
        }

        if (menuCoordinator == null)
        {
            menuCoordinator =
                UnityEngine.Object.FindAnyObjectByType<HudEditMenuCoordinator>();
        }
    }
}
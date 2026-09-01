using UnityEngine;

[DisallowMultipleComponent]
public sealed class HudLiveViewControlsToggle : MonoBehaviour
{
    [SerializeField]
    private HudModeManager modeManager;

    [SerializeField]
    private GameObject buttonRoot;

    [SerializeField]
    private HudButtonLabelCanvas buttonLabel;

    [SerializeField]
    private GameObject[] viewControls;

    private bool controlsVisible = true;
    private HudModeManager.HudMode lastMode;
    private bool initialized;

    private void Awake()
    {
        if (modeManager == null)
        {
            modeManager =
                Object.FindAnyObjectByType<HudModeManager>();
        }
        if (buttonLabel == null &&
            buttonRoot != null)
        {
            buttonLabel =
                buttonRoot.GetComponent
                <HudButtonLabelCanvas>();
        }
    }

    private void Start()
    {
        ApplyState();
    }

    private void LateUpdate()
    {
        if (modeManager == null)
            return;

        if (!initialized ||
            modeManager.CurrentMode != lastMode)
        {
            ApplyState();
        }
    }

    public void ToggleControls()
    {
        if (modeManager == null ||
            !modeManager.IsLiveMode)
        {
            return;
        }

        controlsVisible = !controlsVisible;
        ApplyState();
    }

    private void ApplyState()
    {
        if (modeManager == null)
            return;

        bool liveMode = modeManager.IsLiveMode;

        if (buttonRoot != null)
        {
            buttonRoot.SetActive(liveMode);
        }

        // Always show them in Edit mode. The toggle only controls Live mode.
        bool showControls =
            !liveMode || controlsVisible;

        if (viewControls != null)
        {
            foreach (GameObject control in viewControls)
            {
                if (control != null)
                {
                    control.SetActive(showControls);
                }
            }
        }

        if (buttonLabel != null)
        {
            buttonLabel.SetText(
                controlsVisible ? "HIDE UI" : "SHOW UI"
            );
        }

        lastMode = modeManager.CurrentMode;
        initialized = true;
    }
}
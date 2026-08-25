using UnityEngine;

[DisallowMultipleComponent]
public class HudWindowPaletteController : MonoBehaviour
{
    [Header("Menu")]
    public GameObject menuPanel;

    [Header("Placement")]
    public HudSphereWindowConstraint sphereConstraint;

    [Tooltip(
        "Horizontal visor angle used whenever the menu opens."
    )]
    public float openYawDegrees = 0.0f;

    [Tooltip(
        "Vertical visor angle used whenever the menu opens. " +
        "Negative moves it lower in the field of view."
    )]
    public float openPitchDegrees = -5.0f;

    [Tooltip(
        "Return the palette to its preset position every time " +
        "it is opened."
    )]
    public bool resetPositionWhenOpened = true;

    [Header("Startup")]
    public bool startOpen = true;


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        ResolveReferences();
    }


    void Start()
    {
        ResolveReferences();

        if (startOpen)
        {
            OpenMenu();
        }
        else
        {
            CloseMenu();
        }
    }


    // ========================================================
    // REFERENCES
    // ========================================================

    private void ResolveReferences()
    {
        if (menuPanel != null &&
            sphereConstraint == null)
        {
            sphereConstraint =
                menuPanel.GetComponent
                <HudSphereWindowConstraint>();
        }
    }


    // ========================================================
    // MENU
    // ========================================================

    public void ToggleMenu()
    {
        if (menuPanel == null)
            return;


        if (menuPanel.activeSelf)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }


    public void OpenMenu()
    {
        if (menuPanel == null)
            return;


        menuPanel.SetActive(
            true
        );


        ResolveReferences();


        if (resetPositionWhenOpened &&
            sphereConstraint != null)
        {
            sphereConstraint.SetVisorAngles(
                openYawDegrees,
                openPitchDegrees
            );
        }
    }


    public void CloseMenu()
    {
        if (menuPanel == null)
            return;


        menuPanel.SetActive(
            false
        );
    }
}
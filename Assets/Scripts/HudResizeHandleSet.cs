using UnityEngine;

[DisallowMultipleComponent]
public class HudResizeHandleSet : MonoBehaviour
{
    [Header("Handle Policy")]
    [Tooltip(
        "Only TopLeft, TopRight, BottomLeft and BottomRight " +
        "are enabled."
    )]
    public bool cornersOnly = true;


    [Header("Visibility")]
    [Tooltip(
        "This will later be controlled by Edit / Live mode."
    )]
    public bool handlesVisible = true;


    void Awake()
    {
        ApplyVisibility();
    }


    void OnEnable()
    {
        /*
         * Avoid modifying the hierarchy from editor
         * validation callbacks. Only apply during runtime.
         */
        if (Application.isPlaying)
        {
            ApplyVisibility();
        }
    }


    public void SetHandlesVisible(bool visible)
    {
        handlesVisible = visible;
        ApplyVisibility();
    }


    public void SetCornersOnly(bool enabled)
    {
        cornersOnly = enabled;
        ApplyVisibility();
    }


    public void ApplyVisibility()
    {
        HudGridResizeHandle[] handles =
            GetComponentsInChildren<HudGridResizeHandle>(true);


        foreach (HudGridResizeHandle handle in handles)
        {
            if (handle == null)
                continue;


            bool isCorner =
                handle.resizeEdge ==
                    HudGridManager.ResizeEdge.TopLeft
                ||
                handle.resizeEdge ==
                    HudGridManager.ResizeEdge.TopRight
                ||
                handle.resizeEdge ==
                    HudGridManager.ResizeEdge.BottomLeft
                ||
                handle.resizeEdge ==
                    HudGridManager.ResizeEdge.BottomRight;


            bool shouldShow =
                handlesVisible &&
                (!cornersOnly || isCorner);


            if (handle.gameObject.activeSelf != shouldShow)
            {
                handle.gameObject.SetActive(shouldShow);
            }
        }
    }
}
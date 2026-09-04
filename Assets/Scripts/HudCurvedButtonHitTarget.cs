using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class HudCurvedButtonHitTarget :
    MonoBehaviour
{
    [Header("Physical Hit Target")]

    [SerializeField]
    private float colliderDepthMeters = 0.016f;

    [SerializeField]
    private float edgePaddingMeters = 0.003f;

    [Tooltip(
        "Distance from the curved window surface toward the user."
    )]
    [SerializeField]
    private float surfaceOffsetTowardUser = 0.018f;


    private HudWindow hudWindow;
    private Button targetButton;
    private RectTransform sourceRect;

    private GameObject hitObject;
    private BoxCollider hitCollider;
    private XRSimpleInteractable interactable;

    private bool eventsSubscribed;

    private readonly Vector3[] worldCorners =
        new Vector3[4];


    private void Awake()
    {
        ResolveReferences();
        DisableFlatCanvasRaycasts();
        EnsureHitObject();
        SynchronizeHitTarget();
    }


    private void OnEnable()
    {
        ResolveReferences();
        DisableFlatCanvasRaycasts();
        EnsureHitObject();
        SubscribeEvents();
        SynchronizeHitTarget();
    }


    private void Start()
    {
        SynchronizeHitTarget();
    }


    private void LateUpdate()
    {
        SynchronizeHitTarget();
    }


    private void OnDisable()
    {
        UnsubscribeEvents();
        SetHovered(false);

        if (hitObject != null)
            hitObject.SetActive(false);
    }


    private void OnDestroy()
    {
        UnsubscribeEvents();

        if (hitObject != null)
            Destroy(hitObject);
    }


    private void ResolveReferences()
    {
        if (targetButton == null)
            targetButton = GetComponent<Button>();

        if (sourceRect == null)
            sourceRect = transform as RectTransform;

        if (hudWindow == null)
        {
            hudWindow =
                GetComponentInParent<HudWindow>(true);
        }
    }


    /*
     * Prevent the old flat Canvas geometry from competing
     * with the physical curved hit target.
     */
    private void DisableFlatCanvasRaycasts()
    {
        Graphic[] graphics =
            GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (graphic != null)
                graphic.raycastTarget = false;
        }
    }


    private void EnsureHitObject()
    {
        if (hitObject != null ||
            hudWindow == null)
        {
            return;
        }

        hitObject =
            new GameObject(
                gameObject.name + "_CurvedHit",
                typeof(BoxCollider),
                typeof(XRSimpleInteractable));

        hitObject.layer = gameObject.layer;

        hitObject.transform.SetParent(
            hudWindow.transform,
            false);

        hitCollider =
            hitObject.GetComponent<BoxCollider>();

        hitCollider.center = Vector3.zero;
        hitCollider.isTrigger = false;

        interactable =
            hitObject.GetComponent<XRSimpleInteractable>();

        interactable.colliders.Clear();
        interactable.colliders.Add(hitCollider);

        SubscribeEvents();
    }


    private void SubscribeEvents()
    {
        if (eventsSubscribed ||
            interactable == null)
        {
            return;
        }

        interactable.selectEntered.AddListener(
            HandleSelected);

        interactable.hoverEntered.AddListener(
            HandleHoverEntered);

        interactable.hoverExited.AddListener(
            HandleHoverExited);

        eventsSubscribed = true;
    }


    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed ||
            interactable == null)
        {
            return;
        }

        interactable.selectEntered.RemoveListener(
            HandleSelected);

        interactable.hoverEntered.RemoveListener(
            HandleHoverEntered);

        interactable.hoverExited.RemoveListener(
            HandleHoverExited);

        eventsSubscribed = false;
    }


    private void SynchronizeHitTarget()
    {
        ResolveReferences();
        EnsureHitObject();

        if (hitObject == null ||
            hitCollider == null ||
            sourceRect == null ||
            hudWindow == null ||
            targetButton == null)
        {
            return;
        }

        bool shouldBeActive =
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            targetButton.isActiveAndEnabled &&
            targetButton.interactable &&
            sourceRect.rect.width > 0f &&
            sourceRect.rect.height > 0f;

        if (hitObject.activeSelf != shouldBeActive)
            hitObject.SetActive(shouldBeActive);

        if (!shouldBeActive)
            return;

        sourceRect.GetWorldCorners(worldCorners);

        Vector2 bottomLeft =
            GetNormalizedPosition(worldCorners[0]);

        Vector2 topLeft =
            GetNormalizedPosition(worldCorners[1]);

        Vector2 topRight =
            GetNormalizedPosition(worldCorners[2]);

        Vector2 bottomRight =
            GetNormalizedPosition(worldCorners[3]);

        Vector2 center =
            (
                bottomLeft +
                topLeft +
                topRight +
                bottomRight
            ) * 0.25f;

        Vector2 left =
            (bottomLeft + topLeft) * 0.5f;

        Vector2 right =
            (bottomRight + topRight) * 0.5f;

        Vector2 bottom =
            (bottomLeft + bottomRight) * 0.5f;

        Vector2 top =
            (topLeft + topRight) * 0.5f;

        Pose centerPose =
            GetSurfacePose(center);

        float widthMeters =
            Vector3.Distance(
                GetSurfacePose(left).position,
                GetSurfacePose(right).position);

        float heightMeters =
            Vector3.Distance(
                GetSurfacePose(bottom).position,
                GetSurfacePose(top).position);

        hitObject.transform.localScale =
            Vector3.one;

        hitObject.transform.SetPositionAndRotation(
            centerPose.position,
            centerPose.rotation);

        hitCollider.size =
            new Vector3(
                Mathf.Max(
                    0.005f,
                    widthMeters +
                    edgePaddingMeters * 2f),
                Mathf.Max(
                    0.005f,
                    heightMeters +
                    edgePaddingMeters * 2f),
                Mathf.Max(
                    0.005f,
                    colliderDepthMeters));
    }


    private Vector2 GetNormalizedPosition(
        Vector3 worldPosition)
    {
        Vector3 localPosition =
            hudWindow.transform.InverseTransformPoint(
                worldPosition);

        return new Vector2(
            localPosition.x /
            Mathf.Max(
                0.001f,
                hudWindow.WidthMeters),
            localPosition.y /
            Mathf.Max(
                0.001f,
                hudWindow.HeightMeters));
    }


    private Pose GetSurfacePose(
        Vector2 normalizedPosition)
    {
        return HudSphereGeometry.GetSurfacePose(
            hudWindow,
            normalizedPosition.x,
            normalizedPosition.y,
            surfaceOffsetTowardUser);
    }


    private void HandleSelected(
        SelectEnterEventArgs eventArguments)
    {
        if (targetButton == null ||
            !targetButton.isActiveAndEnabled ||
            !targetButton.interactable)
        {
            return;
        }

        /*
         * Preserve the button's existing On Click event.
         */
        targetButton.onClick.Invoke();
    }


    private void HandleHoverEntered(
        HoverEnterEventArgs eventArguments)
    {
        SetHovered(true);
    }


    private void HandleHoverExited(
        HoverExitEventArgs eventArguments)
    {
        SetHovered(false);
    }


    private void SetHovered(bool hovered)
    {
        if (targetButton == null ||
            targetButton.targetGraphic == null)
        {
            return;
        }

        ColorBlock colors =
            targetButton.colors;

        Color tint =
            hovered
                ? colors.highlightedColor
                : colors.normalColor;

        targetButton.targetGraphic.CrossFadeColor(
            tint * colors.colorMultiplier,
            colors.fadeDuration,
            true,
            true);
    }
}
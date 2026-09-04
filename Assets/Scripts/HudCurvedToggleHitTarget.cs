using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class HudCurvedToggleHitTarget : MonoBehaviour
{
    [Header("Automatically Resolved")]
    [SerializeField] private HudWindow hudWindow;
    [SerializeField] private Toggle targetToggle;

    [Header("Physical Hit Target")]
    [SerializeField] private float colliderDepthMeters = 0.016f;
    [SerializeField] private float edgePaddingMeters = 0.001f;

    [Tooltip("Matches the offset used by the physical close button.")]
    [SerializeField] private float surfaceOffsetTowardUser = 0.018f;

    private RectTransform sourceRect;
    private GameObject hitObject;
    private BoxCollider hitCollider;
    private XRSimpleInteractable interactable;

    private readonly Vector3[] worldCorners =
        new Vector3[4];

    private bool eventsSubscribed;


    public void Configure(
        HudWindow owner,
        Toggle toggle)
    {
        hudWindow = owner;
        targetToggle = toggle;
        sourceRect = transform as RectTransform;

        EnsureHitObject();
        SynchronizeHitTarget();
    }


    private void Awake()
    {
        ResolveReferences();
        EnsureHitObject();
    }


    private void OnEnable()
    {
        ResolveReferences();
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
        if (sourceRect == null)
            sourceRect = transform as RectTransform;

        if (targetToggle == null)
            targetToggle = GetComponent<Toggle>();

        if (hudWindow == null)
        {
            hudWindow =
                GetComponentInParent<HudWindow>(true);
        }
    }


    private void EnsureHitObject()
    {
        if (hitObject != null ||
            hudWindow == null)
        {
            return;
        }

        hitObject = new GameObject(
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
            hudWindow == null)
        {
            return;
        }

        bool shouldBeActive =
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
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
            (bottomLeft +
             topLeft +
             topRight +
             bottomRight) * 0.25f;

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

        Vector3 leftPosition =
            GetSurfacePose(left).position;

        Vector3 rightPosition =
            GetSurfacePose(right).position;

        Vector3 bottomPosition =
            GetSurfacePose(bottom).position;

        Vector3 topPosition =
            GetSurfacePose(top).position;

        float widthMeters =
            Vector3.Distance(
                leftPosition,
                rightPosition);

        float heightMeters =
            Vector3.Distance(
                bottomPosition,
                topPosition);

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
        Vector3 windowLocal =
            hudWindow.transform.InverseTransformPoint(
                worldPosition);

        return new Vector2(
            windowLocal.x /
            Mathf.Max(0.001f, hudWindow.WidthMeters),
            windowLocal.y /
            Mathf.Max(0.001f, hudWindow.HeightMeters));
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
        if (targetToggle == null ||
            !targetToggle.isActiveAndEnabled ||
            !targetToggle.interactable)
        {
            return;
        }

        targetToggle.isOn =
            !targetToggle.isOn;
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
        if (targetToggle == null ||
            targetToggle.targetGraphic == null)
        {
            return;
        }

        ColorBlock colors =
            targetToggle.colors;

        Color tint =
            hovered
                ? colors.highlightedColor
                : colors.normalColor;

        targetToggle.targetGraphic.CrossFadeColor(
            tint * colors.colorMultiplier,
            colors.fadeDuration,
            true,
            true);
    }
}
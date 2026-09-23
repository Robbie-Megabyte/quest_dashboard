using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public sealed class G1RobotWindowView :
    MonoBehaviour,
    IDragHandler,
    IScrollHandler
{
    private const int RobotRenderLayer = 29;

    [Header("References")]
    public HudWindow hudWindow;
    public Transform robotContentRoot;
    public RawImage targetImage;
    public TMP_Text dragModeLabel;

    [Header("Rendering")]
    [Min(256)]
    public int baseTextureResolution = 768;

    public Color backgroundColor =
        new Color(0f, 0f, 0f, 0f);

    [Header("View")]
    public float initialYaw = 135f;
    public float initialPitch = 10f;

    [Range(-80f, 80f)]
    public float minimumPitch = -60f;

    [Range(-80f, 80f)]
    public float maximumPitch = 60f;

    [Header("Interaction")]
    public float rotationSensitivity = 0.18f;

    [Min(0.1f)]
    public float panSensitivity = 1f;

    public bool startInPanMode = false;

    [Header("Capped Zoom")]
    [Tooltip("Smallest multiplier is maximum zoom-in.")]
    public float minimumZoom = 0.45f;

    [Tooltip("Largest multiplier is maximum zoom-out.")]
    public float maximumZoom = 2f;

    public float zoomStep = 1.2f;

    private Camera renderCamera;
    private RenderTexture renderTexture;
    private GameObject cameraObject;
    private GameObject lightRoot;

    private Bounds modelLocalBounds;
    private bool hasBounds;

    private Vector3 panOffsetLocal;
    private float yaw;
    private float pitch;
    private float zoom = 1f;
    private bool panMode;
    private bool interactionEnabled = true;

    private Vector2 lastViewportSize;

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<RawImage>();

        if (hudWindow == null)
            hudWindow = GetComponentInParent<HudWindow>(true);

        panMode = startInPanMode;

        PutRobotOnRenderLayer();
        HideRobotFromMainCamera();
        CalculateModelBounds();
        CreateRenderCamera();
        CreateBalancedLighting();
        ApplyDashboardControlTheme();
        ResetView();
    }

    private void LateUpdate()
    {
        if (renderCamera == null ||
            robotContentRoot == null ||
            !hasBounds)
        {
            return;
        }

        UpdateRenderTexture();
        UpdateCameraPose();
    }

    private void OnDestroy()
    {
        if (renderCamera != null)
            renderCamera.targetTexture = null;

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (cameraObject != null)
            Destroy(cameraObject);

        if (lightRoot != null)
            Destroy(lightRoot);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
    }

    public void ZoomIn()
    {
        if (!interactionEnabled)
            return;

        zoom = Mathf.Clamp(
            zoom / Mathf.Max(1.01f, zoomStep),
            minimumZoom,
            maximumZoom
        );
    }

    public void ZoomOut()
    {
        if (!interactionEnabled)
            return;

        zoom = Mathf.Clamp(
            zoom * Mathf.Max(1.01f, zoomStep),
            minimumZoom,
            maximumZoom
        );
    }

    public void ToggleDragMode()
    {
        if (!interactionEnabled)
            return;

        panMode = !panMode;
        UpdateDragModeLabel();
    }

    public void ResetView()
    {
        yaw = initialYaw;

        pitch = Mathf.Clamp(
            initialPitch,
            minimumPitch,
            maximumPitch
        );

        zoom = 1f;
        panOffsetLocal = Vector3.zero;

        UpdateDragModeLabel();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!interactionEnabled)
            return;

        if (eventData.scrollDelta.y > 0f)
            ZoomIn();
        else if (eventData.scrollDelta.y < 0f)
            ZoomOut();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!interactionEnabled ||
            renderCamera == null ||
            hudWindow == null)
        {
            return;
        }

        if (panMode)
        {
            ApplyPan(eventData.delta);
            return;
        }

        yaw +=
            eventData.delta.x *
            rotationSensitivity;

        pitch = Mathf.Clamp(
            pitch +
            eventData.delta.y *
            rotationSensitivity,
            minimumPitch,
            maximumPitch
        );
    }

    private void ApplyPan(Vector2 pointerDelta)
    {
        float viewportHeight =
            Mathf.Max(
                1f,
                targetImage.rectTransform.rect.height
            );

        float worldPerPixel =
            2f *
            renderCamera.orthographicSize /
            viewportHeight;

        Vector3 worldDelta =
            (
                -renderCamera.transform.right *
                pointerDelta.x
                -
                renderCamera.transform.up *
                pointerDelta.y
            ) *
            worldPerPixel *
            panSensitivity;

        panOffsetLocal +=
            hudWindow.transform.InverseTransformVector(
                worldDelta
            );
    }

    private void UpdateDragModeLabel()
    {
        if (dragModeLabel != null)
        {
            dragModeLabel.text =
                panMode ? "PAN" : "ROTATE";
        }
    }

    private void ApplyDashboardControlTheme()
    {
        if (dragModeLabel == null ||
            dragModeLabel.transform.parent == null ||
            dragModeLabel.transform.parent.parent == null)
        {
            return;
        }

        Transform controlsRoot =
            dragModeLabel.transform.parent.parent;

        foreach (Button button in
                 controlsRoot.GetComponentsInChildren<Button>(true))
        {
            if (button.transform.parent != controlsRoot)
                continue;

            string buttonName = button.gameObject.name;

            if (buttonName != "RobotDragModeButton" &&
                buttonName != "ZoomInButton" &&
                buttonName != "ZoomOutButton")
            {
                continue;
            }

            HudDashboardTheme.StyleDarkGreenButton(button);
        }
    }

    private void CreateRenderCamera()
    {
        cameraObject =
            new GameObject("G1_Robot_RenderCamera");

        cameraObject.transform.SetParent(
            hudWindow.transform,
            false
        );

        renderCamera =
            cameraObject.AddComponent<Camera>();

        renderCamera.enabled = true;
        renderCamera.orthographic = true;
        renderCamera.clearFlags =
            CameraClearFlags.SolidColor;

        renderCamera.backgroundColor =
            backgroundColor;

        renderCamera.cullingMask =
            1 << RobotRenderLayer;

        renderCamera.nearClipPlane = 0.01f;
        renderCamera.allowHDR = false;
        renderCamera.allowMSAA = true;

        UpdateRenderTexture(true);
    }

    private void CreateBalancedLighting()
    {
        if (hudWindow == null || lightRoot != null)
            return;

        lightRoot =
            new GameObject("G1_Robot_FillLights");

        lightRoot.hideFlags = HideFlags.DontSave;

        lightRoot.transform.SetParent(
            hudWindow.transform,
            false
        );

        CreateFillLight("FrontLeft", 35.0f, -45.0f);
        CreateFillLight("FrontRight", 35.0f, 45.0f);
        CreateFillLight("RearRight", 35.0f, 135.0f);
        CreateFillLight("RearLeft", 35.0f, -135.0f);
    }

    private void CreateFillLight(
        string lightName,
        float pitchDegrees,
        float yawDegrees)
    {
        GameObject lightObject =
            new GameObject(lightName);

        lightObject.transform.SetParent(
            lightRoot.transform,
            false
        );

        lightObject.transform.localRotation =
            Quaternion.Euler(
                pitchDegrees,
                yawDegrees,
                0.0f
            );

        Light fillLight =
            lightObject.AddComponent<Light>();

        fillLight.type = LightType.Directional;
        fillLight.color = Color.white;
        fillLight.intensity = 0.45f;
        fillLight.shadows = LightShadows.None;
        fillLight.cullingMask = 1 << RobotRenderLayer;
    }

    private void UpdateRenderTexture(bool force = false)
    {
        if (targetImage == null ||
            renderCamera == null)
        {
            return;
        }

        Vector2 size =
            targetImage.rectTransform.rect.size;

        if (size.x < 1f || size.y < 1f)
            return;

        if (!force &&
            Vector2.Distance(size, lastViewportSize) < 2f)
        {
            return;
        }

        lastViewportSize = size;

        /*
        * The RenderTexture must retain exactly the same aspect
        * ratio as the RawImage. Otherwise Unity stretches the
        * rendered robot when the window becomes narrow or wide.
        */
        float aspect =
            Mathf.Clamp(
                size.x / size.y,
                0.1f,
                10f
            );

        /*
        * Use the longest texture dimension as the fixed resolution.
        * The shorter dimension is calculated from the aspect ratio.
        *
        * This keeps the ratio intact instead of independently
        * clamping width and height.
        */
        int longestSide =
            Mathf.Clamp(
                baseTextureResolution,
                256,
                1536
            );

        int width;
        int height;

        if (aspect >= 1f)
        {
            width = longestSide;

            height = Mathf.Max(
                64,
                Mathf.RoundToInt(
                    width / aspect
                )
            );
        }
        else
        {
            height = longestSide;

            width = Mathf.Max(
                64,
                Mathf.RoundToInt(
                    height * aspect
                )
            );
        }

        if (renderTexture != null)
        {
            renderCamera.targetTexture = null;

            renderTexture.Release();
            Destroy(renderTexture);
        }

        renderTexture =
            new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32
            );

        renderTexture.name =
            "G1_Robot_Window_RenderTexture";

        renderTexture.Create();

        renderCamera.targetTexture =
            renderTexture;

        /*
        * Use the final integer texture dimensions so the camera
        * projection precisely matches the generated texture.
        */
        renderCamera.aspect =
            width / (float)height;

        targetImage.texture =
            renderTexture;
    }

    private void UpdateCameraPose()
    {
        float scale =
            Mathf.Max(
                Mathf.Abs(robotContentRoot.lossyScale.x),
                Mathf.Abs(robotContentRoot.lossyScale.y),
                Mathf.Abs(robotContentRoot.lossyScale.z)
            );

        float verticalExtent =
            modelLocalBounds.extents.y *
            scale;

        float horizontalExtent =
            Mathf.Sqrt(
                modelLocalBounds.extents.x *
                modelLocalBounds.extents.x
                +
                modelLocalBounds.extents.z *
                modelLocalBounds.extents.z
            ) *
            scale;

        float fittedSize =
            Mathf.Max(
                verticalExtent,
                horizontalExtent /
                Mathf.Max(0.1f, renderCamera.aspect)
            ) *
            1.08f;

        renderCamera.orthographicSize =
            Mathf.Max(
                0.01f,
                fittedSize * zoom
            );

        Vector3 target =
            robotContentRoot.TransformPoint(
                modelLocalBounds.center
            );

        target +=
            hudWindow.transform.TransformVector(
                panOffsetLocal
            );

        Quaternion orbit =
            hudWindow.transform.rotation *
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );

        float radius =
            modelLocalBounds.extents.magnitude *
            scale;

        float distance =
            Mathf.Max(2f, radius * 4f);

        Vector3 cameraPosition =
            target +
            orbit *
            Vector3.forward *
            distance;

        renderCamera.transform.SetPositionAndRotation(
            cameraPosition,
            Quaternion.LookRotation(
                target - cameraPosition,
                orbit * Vector3.up
            )
        );

        renderCamera.farClipPlane =
            distance + radius * 4f + 10f;
    }

    private void CalculateModelBounds()
    {
        if (robotContentRoot == null)
            return;

        Renderer[] renderers =
            robotContentRoot.GetComponentsInChildren
            <Renderer>(true);

        bool initialized = false;
        Bounds combined = new Bounds();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Bounds bounds = renderer.localBounds;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner =
                            bounds.center +
                            Vector3.Scale(
                                bounds.extents,
                                new Vector3(x, y, z)
                            );

                        Vector3 worldCorner =
                            renderer.transform.TransformPoint(
                                corner
                            );

                        Vector3 rootCorner =
                            robotContentRoot.InverseTransformPoint(
                                worldCorner
                            );

                        if (!initialized)
                        {
                            combined =
                                new Bounds(
                                    rootCorner,
                                    Vector3.zero
                                );

                            initialized = true;
                        }
                        else
                        {
                            combined.Encapsulate(rootCorner);
                        }
                    }
                }
            }
        }

        modelLocalBounds = combined;
        hasBounds = initialized;
    }

    private void PutRobotOnRenderLayer()
    {
        if (robotContentRoot == null)
            return;

        Transform[] children =
            robotContentRoot.GetComponentsInChildren
            <Transform>(true);

        foreach (Transform child in children)
        {
            child.gameObject.layer =
                RobotRenderLayer;
        }
    }

    private void HideRobotFromMainCamera()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            mainCamera.cullingMask &=
                ~(1 << RobotRenderLayer);
        }
    }
}
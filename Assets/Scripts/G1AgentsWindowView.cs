using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;


[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public sealed class G1AgentsWindowView :
    MonoBehaviour,
    IDragHandler,
    IScrollHandler,
    IPointerClickHandler
{
    private const int AgentsRenderLayer = 28;


    [Header("References")]
    [SerializeField]
    private HudWindow hudWindow;

    [SerializeField]
    private RawImage targetImage;

    [SerializeField]
    private GameObject vehiclePrefab;

    [Tooltip("The live G1 model used by the Robot and SLAM views.")]
    [SerializeField]
    private Transform robotModelSource;


    [Header("Vehicle Model")]
    [SerializeField]
    private float vehicleModelYawOffsetDegrees = -90.0f;


    [Header("Render Texture")]
    [SerializeField]
    [Range(256, 1536)]
    private int baseTextureResolution = 1280;

    [SerializeField]
    private Color backgroundColor =
        new Color32(6, 16, 25, 255);


    [Header("View")]
    [SerializeField]
    private float cameraYawDegrees = -35.0f;

    [SerializeField]
    private float cameraPitchDegrees = 55.0f;

    [SerializeField]
    private float minimumViewSize = 2.5f;

    [SerializeField]
    private float maximumViewSize = 15.0f;


    [Header("Static Point Cloud")]
    [SerializeField]
    [Range(1.0f, 6.0f)]
    private float staticPointSize = 2.5f;

    [SerializeField]
    private float staticPointMinimumHeight = -0.10f;

    [SerializeField]
    private float staticPointMaximumHeight = 2.50f;


    [Header("Interaction")]
    [SerializeField]
    private float rotationSensitivity = 0.18f;

    [SerializeField]
    private float zoomSensitivity = 0.08f;

    [SerializeField]
    [Min(0.1f)]
    private float panSensitivity = 1.0f;

    [SerializeField]
    [Range(5.0f, 85.0f)]
    private float minimumCameraPitch = 10.0f;

    [SerializeField]
    [Range(5.0f, 85.0f)]
    private float maximumCameraPitch = 85.0f;

    [SerializeField]
    private float minimumZoom = 0.35f;

    [SerializeField]
    private float maximumZoom = 3.0f;


    [Header("Preview")]
    [Tooltip(
        "Move both agents along test paths until the live " +
        "network client is connected."
    )]
    [SerializeField]
    private bool animatePreview = true;


    private GameObject runtimeRoot;
    private Transform robotRoot;
    private Transform vehicleRoot;
    private Transform vehicleVisual;
    private GameObject robotReplica;

    private Renderer[] robotVisualRenderers =
        System.Array.Empty<Renderer>();

    private Renderer[] vehicleVisualRenderers =
        System.Array.Empty<Renderer>();

    private Transform[] sourcePoseTransforms =
        System.Array.Empty<Transform>();

    private Transform[] replicaPoseTransforms =
        System.Array.Empty<Transform>();

    private TextMeshPro robotNameplate;
    private TextMeshPro vehicleNameplate;

    private GameObject renderCameraObject;
    private Camera renderCamera;
    private RenderTexture renderTexture;

    private Material robotMaterial;
    private Material floorMaterial;
    private Material gridMaterial;
    private Material worldMapMaterial;
    private Material staticPointCloudMaterial;
    private Material vehicleLidarMaterial;
    private Material plannerPathMaterial;
    private Material nameplateBackgroundMaterial;
    private Material robotStatusDotMaterial;
    private Material vehicleStatusDotMaterial;

    private readonly List<Material> nameplateTextMaterials =
        new List<Material>();

    private Mesh gridMesh;
    private Mesh worldMapMesh;
    private Mesh staticPointCloudMesh;
    private Mesh vehicleLidarMesh;
    private Mesh plannerPathMesh;
    private GameObject worldMapObject;
    private GameObject staticPointCloudObject;
    private GameObject vehicleLidarObject;
    private GameObject plannerPathObject;
    private Vector2 lastViewportSize;

    private bool worldMapVisible = true;
    private bool staticPointCloudVisible = true;

    private Transform goalMarkerRoot;
    private Material goalMarkerMaterial;

    private bool worldMapBoundsValid;
    private Vector2 worldMapMinimum;
    private Vector2 worldMapMaximum;

    private bool interactionEnabled = true;
    private bool panMode;
    private float orbitYawOffset;
    private float orbitPitchOffset;
    private float viewZoom = 1.0f;
    private Vector3 viewPanOffset;

    public bool PanMode => panMode;
    public bool WorldMapVisible => worldMapVisible;
    public bool StaticPointCloudVisible =>
        staticPointCloudVisible;


    private void Awake()
    {
        ResolveReferences();
        BuildRuntimeScene();

        SetRobotMapPose(
            -1.2f,
            -0.35f,
            20.0f
        );

        SetVehicleMapPose(
            1.2f,
            0.65f,
            -25.0f
        );
    }


    private void OnEnable()
    {
        ResolveReferences();

        if (runtimeRoot == null)
        {
            BuildRuntimeScene();
        }

        if (renderCamera != null)
        {
            renderCamera.enabled = true;
        }

        HideRuntimeLayerFromOtherCameras();
    }


    private void OnDisable()
    {
        if (renderCamera != null)
        {
            renderCamera.enabled = false;
        }
    }


    private void Update()
    {
        if (!animatePreview ||
            robotRoot == null ||
            vehicleRoot == null)
        {
            return;
        }

        float travel =
            Mathf.Sin(
                Time.unscaledTime * 0.45f
            ) * 1.25f;

        /*
         * Both agents face map +X and translate along map X.
         * During the second half of the cycle they reverse
         * without turning, which makes model-axis errors easy
         * to see.
         */
        SetRobotMapPose(
            travel,
            -0.75f,
            0.0f
        );

        SetVehicleMapPose(
            travel,
            0.75f,
            0.0f
        );
    }


    private void LateUpdate()
    {
        if (runtimeRoot == null ||
            renderCamera == null)
        {
            return;
        }

        HideRuntimeLayerFromOtherCameras();
        MirrorRobotPose();

        if (vehicleVisual != null)
        {
            vehicleVisual.localRotation =
                Quaternion.Euler(
                    0.0f,
                    vehicleModelYawOffsetDegrees,
                    0.0f
                );
        }

        UpdateRenderTexture();
        UpdateCameraPose();
        UpdateNameplates();
    }


    private void OnDestroy()
    {
        if (renderCamera != null)
        {
            renderCamera.targetTexture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (runtimeRoot != null)
        {
            Destroy(runtimeRoot);
        }

        if (renderCameraObject != null)
        {
            Destroy(renderCameraObject);
        }

        if (gridMesh != null)
        {
            Destroy(gridMesh);
        }

        if (worldMapMesh != null)
        {
            Destroy(worldMapMesh);
        }

        if (staticPointCloudMesh != null)
        {
            Destroy(staticPointCloudMesh);
        }

        if (vehicleLidarMesh != null)
        {
            Destroy(vehicleLidarMesh);
        }

        if (plannerPathMesh != null)
        {
            Destroy(plannerPathMesh);
        }

        if (robotMaterial != null)
        {
            Destroy(robotMaterial);
        }

        if (floorMaterial != null)
        {
            Destroy(floorMaterial);
        }

        if (gridMaterial != null)
        {
            Destroy(gridMaterial);
        }

        if (worldMapMaterial != null)
        {
            Destroy(worldMapMaterial);
        }

        if (staticPointCloudMaterial != null)
        {
            Destroy(staticPointCloudMaterial);
        }

        if (vehicleLidarMaterial != null)
        {
            Destroy(vehicleLidarMaterial);
        }

        if (plannerPathMaterial != null)
        {
            Destroy(plannerPathMaterial);
        }

        if (goalMarkerMaterial != null)
        {
            Destroy(goalMarkerMaterial);
        }

        if (nameplateBackgroundMaterial != null)
        {
            Destroy(nameplateBackgroundMaterial);
        }

        foreach (Material material in nameplateTextMaterials)
        {
            if (material != null)
                Destroy(material);
        }

        nameplateTextMaterials.Clear();

        if (robotStatusDotMaterial != null)
            Destroy(robotStatusDotMaterial);

        if (vehicleStatusDotMaterial != null)
            Destroy(vehicleStatusDotMaterial);
    }


    public void SetPreviewAnimationEnabled(
        bool enabled)
    {
        animatePreview = enabled;
    }


    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
    }


    public void ToggleWorldMapVisibility()
    {
        worldMapVisible = !worldMapVisible;

        if (worldMapObject != null)
            worldMapObject.SetActive(worldMapVisible);
    }


    public void ToggleStaticPointCloudVisibility()
    {
        staticPointCloudVisible =
            !staticPointCloudVisible;

        if (staticPointCloudObject != null)
        {
            staticPointCloudObject.SetActive(
                staticPointCloudVisible
            );
        }
    }


    public void ToggleDragMode()
    {
        if (!interactionEnabled)
            return;

        panMode = !panMode;
    }


    public void ZoomIn()
    {
        if (!interactionEnabled)
            return;

        viewZoom = Mathf.Clamp(
            viewZoom / 1.20f,
            minimumZoom,
            maximumZoom
        );
    }


    public void ZoomOut()
    {
        if (!interactionEnabled)
            return;

        viewZoom = Mathf.Clamp(
            viewZoom * 1.20f,
            minimumZoom,
            maximumZoom
        );
    }


    public void ResetCameraView()
    {
        if (!interactionEnabled)
            return;

        panMode = false;
        orbitYawOffset = 0.0f;
        orbitPitchOffset = 0.0f;
        viewZoom = 1.0f;
        viewPanOffset = Vector3.zero;
    }


    public void OnDrag(PointerEventData eventData)
    {
        if (!interactionEnabled || renderCamera == null)
            return;

        if (panMode)
        {
            ApplyViewPan(eventData.delta);
        }
        else
        {
            orbitYawOffset +=
                eventData.delta.x * rotationSensitivity;

            float pitch = Mathf.Clamp(
                cameraPitchDegrees + orbitPitchOffset -
                    eventData.delta.y * rotationSensitivity,
                minimumCameraPitch,
                maximumCameraPitch
            );

            orbitPitchOffset = pitch - cameraPitchDegrees;
        }

        eventData.Use();
    }


    public void OnScroll(PointerEventData eventData)
    {
        if (!interactionEnabled)
            return;

        viewZoom = Mathf.Clamp(
            viewZoom * Mathf.Exp(
                -eventData.scrollDelta.y * zoomSensitivity
            ),
            minimumZoom,
            maximumZoom
        );

        eventData.Use();
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interactionEnabled)
            return;

        if (eventData.clickCount >= 2)
            ResetCameraView();
    }


    private void ApplyViewPan(Vector2 pointerDelta)
    {
        if (renderCamera == null ||
            targetImage == null ||
            runtimeRoot == null)
        {
            return;
        }

        float viewportHeight = Mathf.Max(
            1.0f,
            targetImage.rectTransform.rect.height
        );

        float worldPerPixel =
            2.0f * renderCamera.orthographicSize /
            viewportHeight;

        Vector3 worldDelta =
            (
                -renderCamera.transform.right * pointerDelta.x -
                renderCamera.transform.up * pointerDelta.y
            ) * worldPerPixel * panSensitivity;

        Vector3 localDelta =
            runtimeRoot.transform.InverseTransformVector(
                worldDelta
            );

        localDelta.y = 0.0f;
        viewPanOffset += localDelta;
    }


    public void SetRobotMapPose(
        float mapX,
        float mapY,
        float yawDegrees)
    {
        if (robotRoot == null)
            return;

        ApplyMapPose(
            robotRoot,
            mapX,
            mapY,
            yawDegrees
        );
    }


    public void SetVehicleMapPose(
        float mapX,
        float mapY,
        float yawDegrees)
    {
        if (vehicleRoot == null)
            return;

        ApplyMapPose(
            vehicleRoot,
            mapX,
            mapY,
            yawDegrees
        );
    }


    public void SetWorldMap(
        IReadOnlyList<Vector2> points,
        float resolution)
    {
        EnsureWorldMapObject();

        if (worldMapMesh == null)
            return;

        worldMapMesh.Clear();
        worldMapBoundsValid = false;

        if (points == null || points.Count == 0)
            return;

        float halfSize = Mathf.Clamp(
            resolution * 0.45f,
            0.02f,
            0.075f
        );

        List<Vector3> vertices =
            new List<Vector3>(points.Count * 4);
        List<int> indices =
            new List<int>(points.Count * 4);

        Vector2 minimum = points[0];
        Vector2 maximum = points[0];

        foreach (Vector2 point in points)
        {
            if (!IsFinite(point.x) || !IsFinite(point.y))
                continue;

            int first = vertices.Count;
            vertices.Add(new Vector3(
                point.x - halfSize, 0.018f, point.y
            ));
            vertices.Add(new Vector3(
                point.x + halfSize, 0.018f, point.y
            ));
            vertices.Add(new Vector3(
                point.x, 0.018f, point.y - halfSize
            ));
            vertices.Add(new Vector3(
                point.x, 0.018f, point.y + halfSize
            ));

            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(first + 2);
            indices.Add(first + 3);

            minimum = Vector2.Min(minimum, point);
            maximum = Vector2.Max(maximum, point);
        }

        worldMapMesh.indexFormat = IndexFormat.UInt32;
        worldMapMesh.SetVertices(vertices);
        worldMapMesh.SetIndices(
            indices,
            MeshTopology.Lines,
            0
        );
        worldMapMesh.RecalculateBounds();

        worldMapMinimum = minimum;
        worldMapMaximum = maximum;
        worldMapBoundsValid = vertices.Count > 0;
    }


    public void SetStaticPointCloud(
        IReadOnlyList<Vector3> points)
    {
        EnsureStaticPointCloudObject();

        if (staticPointCloudMesh == null)
            return;

        staticPointCloudMesh.Clear();

        if (points == null || points.Count == 0)
            return;

        List<Vector3> vertices =
            new List<Vector3>(points.Count);

        List<Color32> colors =
            new List<Color32>(points.Count);

        List<int> indices =
            new List<int>(points.Count);

        float minimumHeight =
            Mathf.Min(
                staticPointMinimumHeight,
                staticPointMaximumHeight - 0.01f
            );

        float maximumHeight =
            Mathf.Max(
                staticPointMaximumHeight,
                minimumHeight + 0.01f
            );

        foreach (Vector3 point in points)
        {
            if (!IsFinite(point.x) ||
                !IsFinite(point.y) ||
                !IsFinite(point.z))
            {
                continue;
            }

            // Network order is map X, map Y, height Z.
            vertices.Add(
                new Vector3(
                    point.x,
                    point.z + 0.02f,
                    point.y
                )
            );

            colors.Add(
                StaticPointColor(
                    point.z,
                    minimumHeight,
                    maximumHeight
                )
            );

            indices.Add(vertices.Count - 1);
        }

        staticPointCloudMesh.indexFormat = IndexFormat.UInt32;
        staticPointCloudMesh.SetVertices(vertices);
        staticPointCloudMesh.SetColors(colors);
        staticPointCloudMesh.SetIndices(
            indices,
            MeshTopology.Points,
            0,
            false
        );
        staticPointCloudMesh.RecalculateBounds();
    }


    private static Color32 StaticPointColor(
        float height,
        float minimumHeight,
        float maximumHeight)
    {
        float normalizedHeight =
            Mathf.InverseLerp(
                minimumHeight,
                maximumHeight,
                height
            );

        Color color =
            Color.HSVToRGB(
                0.72f - normalizedHeight * 0.72f,
                0.88f,
                1.0f
            );

        return (Color32)color;
    }


    public void SetVehicleLidarScan(
        IReadOnlyList<Vector2> points)
    {
        EnsureVehicleLidarObject();

        if (vehicleLidarMesh == null)
            return;

        vehicleLidarMesh.Clear();

        if (points == null || points.Count == 0)
            return;

        const float halfSize = 0.055f;

        List<Vector3> vertices =
            new List<Vector3>(points.Count * 4);

        List<int> indices =
            new List<int>(points.Count * 4);

        foreach (Vector2 point in points)
        {
            if (!IsFinite(point.x) || !IsFinite(point.y))
                continue;

            Vector3 centre = new Vector3(
                point.x,
                0.075f,
                point.y
            );

            int first = vertices.Count;

            vertices.Add(centre + Vector3.right * halfSize);
            vertices.Add(centre - Vector3.right * halfSize);
            vertices.Add(centre + Vector3.forward * halfSize);
            vertices.Add(centre - Vector3.forward * halfSize);

            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(first + 2);
            indices.Add(first + 3);
        }

        vehicleLidarMesh.indexFormat = IndexFormat.UInt32;
        vehicleLidarMesh.SetVertices(vertices);
        vehicleLidarMesh.SetIndices(
            indices,
            MeshTopology.Lines,
            0
        );
        vehicleLidarMesh.RecalculateBounds();
    }


    public void ClearVehicleLidarScan()
    {
        if (vehicleLidarMesh != null &&
            vehicleLidarMesh.vertexCount > 0)
        {
            vehicleLidarMesh.Clear();
        }
    }


    public void SetPlannerPath(
        IReadOnlyList<Vector2> points)
    {
        EnsurePlannerPathObject();

        if (plannerPathMesh == null)
            return;

        plannerPathMesh.Clear();

        if (points == null || points.Count < 2)
            return;

        const float halfWidth = 0.045f;
        List<Vector3> vertices =
            new List<Vector3>((points.Count - 1) * 4);
        List<int> triangles =
            new List<int>((points.Count - 1) * 6);

        for (int index = 0;
             index + 1 < points.Count;
             ++index)
        {
            Vector2 start = points[index];
            Vector2 end = points[index + 1];
            Vector2 delta = end - start;

            if (!IsFinite(start.x) || !IsFinite(start.y) ||
                !IsFinite(end.x) || !IsFinite(end.y) ||
                delta.sqrMagnitude < 0.000001f)
            {
                continue;
            }

            Vector2 side = new Vector2(
                -delta.y,
                delta.x
            ).normalized * halfWidth;

            int first = vertices.Count;
            vertices.Add(new Vector3(
                start.x + side.x, 0.045f, start.y + side.y
            ));
            vertices.Add(new Vector3(
                start.x - side.x, 0.045f, start.y - side.y
            ));
            vertices.Add(new Vector3(
                end.x - side.x, 0.045f, end.y - side.y
            ));
            vertices.Add(new Vector3(
                end.x + side.x, 0.045f, end.y + side.y
            ));

            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 1);
            triangles.Add(first);
            triangles.Add(first + 3);
            triangles.Add(first + 2);
        }

        plannerPathMesh.indexFormat = IndexFormat.UInt32;
        plannerPathMesh.SetVertices(vertices);
        plannerPathMesh.SetTriangles(triangles, 0);
        plannerPathMesh.RecalculateBounds();
    }


    public void ClearPlannerPath()
    {
        if (plannerPathMesh != null)
            plannerPathMesh.Clear();
    }


    private void EnsureWorldMapObject()
    {
        if (worldMapObject != null || runtimeRoot == null)
            return;

        worldMapObject = new GameObject("AgentsWorldMap");
        worldMapObject.transform.SetParent(
            runtimeRoot.transform,
            false
        );
        worldMapObject.layer = AgentsRenderLayer;
        worldMapObject.SetActive(worldMapVisible);

        MeshFilter filter =
            worldMapObject.AddComponent<MeshFilter>();
        MeshRenderer renderer =
            worldMapObject.AddComponent<MeshRenderer>();

        worldMapMesh = new Mesh
        {
            name = "G1 Agents World Map"
        };
        filter.sharedMesh = worldMapMesh;
        renderer.sharedMaterial = worldMapMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }


    private void EnsureStaticPointCloudObject()
    {
        if (staticPointCloudObject != null ||
            runtimeRoot == null)
        {
            return;
        }

        staticPointCloudObject =
            new GameObject("AgentsStaticPointCloud");

        staticPointCloudObject.transform.SetParent(
            runtimeRoot.transform,
            false
        );

        staticPointCloudObject.layer = AgentsRenderLayer;
        staticPointCloudObject.SetActive(
            staticPointCloudVisible
        );

        MeshFilter filter =
            staticPointCloudObject.AddComponent<MeshFilter>();

        MeshRenderer renderer =
            staticPointCloudObject.AddComponent<MeshRenderer>();

        staticPointCloudMesh = new Mesh
        {
            name = "G1 Agents Static Point Cloud"
        };

        filter.sharedMesh = staticPointCloudMesh;
        renderer.sharedMaterial = staticPointCloudMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }


    private void EnsureVehicleLidarObject()
    {
        if (vehicleLidarObject != null ||
            runtimeRoot == null)
        {
            return;
        }

        vehicleLidarObject =
            new GameObject("AgentsVehicleLiveLidar");

        vehicleLidarObject.transform.SetParent(
            runtimeRoot.transform,
            false
        );

        vehicleLidarObject.layer = AgentsRenderLayer;

        MeshFilter filter =
            vehicleLidarObject.AddComponent<MeshFilter>();

        MeshRenderer renderer =
            vehicleLidarObject.AddComponent<MeshRenderer>();

        vehicleLidarMesh = new Mesh
        {
            name = "G1 Agents Vehicle Live LiDAR"
        };

        filter.sharedMesh = vehicleLidarMesh;
        renderer.sharedMaterial = vehicleLidarMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }


    private void EnsurePlannerPathObject()
    {
        if (plannerPathObject != null || runtimeRoot == null)
            return;

        plannerPathObject = new GameObject("AgentsPlannerPath");
        plannerPathObject.transform.SetParent(
            runtimeRoot.transform,
            false
        );
        plannerPathObject.layer = AgentsRenderLayer;

        MeshFilter filter =
            plannerPathObject.AddComponent<MeshFilter>();
        MeshRenderer renderer =
            plannerPathObject.AddComponent<MeshRenderer>();

        plannerPathMesh = new Mesh
        {
            name = "G1 Agents Planner Path"
        };
        filter.sharedMesh = plannerPathMesh;
        renderer.sharedMaterial = plannerPathMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }


    public bool TryViewportScreenPointToMap(
        Vector2 screenPoint,
        Camera eventCamera,
        out Vector2 mapPoint)
    {
        mapPoint = Vector2.zero;

        if (targetImage == null ||
            renderCamera == null ||
            runtimeRoot == null)
        {
            return false;
        }

        RectTransform viewport =
            targetImage.rectTransform;

        if (!RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    viewport,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = viewport.rect;
        if (!rect.Contains(localPoint) ||
            rect.width <= 0.0f ||
            rect.height <= 0.0f)
        {
            return false;
        }

        float u = Mathf.InverseLerp(
            rect.xMin,
            rect.xMax,
            localPoint.x
        );

        float v = Mathf.InverseLerp(
            rect.yMin,
            rect.yMax,
            localPoint.y
        );

        Rect sourceUv = targetImage.uvRect;
        u = Mathf.Lerp(sourceUv.xMin, sourceUv.xMax, u);
        v = Mathf.Lerp(sourceUv.yMin, sourceUv.yMax, v);

        Ray ray = renderCamera.ViewportPointToRay(
            new Vector3(u, v, 0.0f)
        );

        Plane floorPlane = new Plane(
            runtimeRoot.transform.up,
            runtimeRoot.transform.position
        );

        if (!floorPlane.Raycast(ray, out float distance))
            return false;

        Vector3 localHit =
            runtimeRoot.transform.InverseTransformPoint(
                ray.GetPoint(distance)
            );

        if (!IsFinite(localHit.x) ||
            !IsFinite(localHit.z))
        {
            return false;
        }

        mapPoint = new Vector2(
            localHit.x,
            localHit.z
        );

        return true;
    }


    public bool TryViewportWorldRayToMap(
        Ray worldRay,
        Camera eventCamera,
        out Vector2 mapPoint)
    {
        mapPoint = Vector2.zero;

        if (targetImage == null ||
            hudWindow == null ||
            hudWindow.sphereConstraint == null ||
            hudWindow.sphereConstraint.hudRoot == null)
        {
            return false;
        }

        Transform hudRoot =
            hudWindow.sphereConstraint.hudRoot;

        float radius =
            HudSphereGeometry.GetRadius(hudWindow);

        float drawingRadius = Mathf.Max(
            0.01f,
            radius -
            hudWindow.visualSurfaceOffsetTowardUser
        );

        Vector3 sphereCenterLocal =
            new Vector3(0.0f, 0.0f, -radius);

        Vector3 rayOriginLocal =
            hudRoot.InverseTransformPoint(
                worldRay.origin
            );

        Vector3 rayDirectionLocal =
            hudRoot.InverseTransformDirection(
                worldRay.direction
            );

        if (rayDirectionLocal.sqrMagnitude < 0.000001f)
            return false;

        rayDirectionLocal.Normalize();

        Vector3 originFromCenter =
            rayOriginLocal - sphereCenterLocal;

        float halfB = Vector3.Dot(
            originFromCenter,
            rayDirectionLocal
        );

        float c =
            originFromCenter.sqrMagnitude -
            drawingRadius * drawingRadius;

        float discriminant =
            halfB * halfB - c;

        if (discriminant < 0.0f)
            return false;

        float squareRoot = Mathf.Sqrt(discriminant);
        float nearDistance = -halfB - squareRoot;
        float farDistance = -halfB + squareRoot;

        float hitDistance =
            nearDistance > 0.0001f
                ? nearDistance
                : farDistance;

        if (hitDistance <= 0.0001f)
            return false;

        Vector3 hitDirection =
            rayOriginLocal +
            rayDirectionLocal * hitDistance -
            sphereCenterLocal;

        Vector3 windowCenterDirection =
            hudRoot.InverseTransformPoint(
                hudWindow.transform.position
            ) - sphereCenterLocal;

        if (hitDirection.sqrMagnitude < 0.000001f ||
            windowCenterDirection.sqrMagnitude < 0.000001f)
        {
            return false;
        }

        hitDirection.Normalize();
        windowCenterDirection.Normalize();

        float hitYawDegrees = Mathf.Atan2(
            hitDirection.x,
            hitDirection.z
        ) * Mathf.Rad2Deg;

        float centerYawDegrees = Mathf.Atan2(
            windowCenterDirection.x,
            windowCenterDirection.z
        ) * Mathf.Rad2Deg;

        float hitPitchDegrees = Mathf.Asin(
            Mathf.Clamp(hitDirection.y, -1.0f, 1.0f)
        ) * Mathf.Rad2Deg;

        float centerPitchDegrees = Mathf.Asin(
            Mathf.Clamp(
                windowCenterDirection.y,
                -1.0f,
                1.0f
            )
        ) * Mathf.Rad2Deg;

        float widthDegrees =
            HudSphereGeometry.MetersToDegrees(
                hudWindow.WidthMeters,
                radius
            );

        float heightDegrees =
            HudSphereGeometry.MetersToDegrees(
                hudWindow.HeightMeters,
                radius
            );

        if (widthDegrees <= 0.0001f ||
            heightDegrees <= 0.0001f)
        {
            return false;
        }

        float normalizedWindowX = Mathf.DeltaAngle(
            centerYawDegrees,
            hitYawDegrees
        ) / widthDegrees;

        float normalizedWindowY =
            (hitPitchDegrees - centerPitchDegrees) /
            heightDegrees;

        Vector3 flatWorldPoint =
            hudWindow.transform.TransformPoint(
                new Vector3(
                    normalizedWindowX *
                    hudWindow.WidthMeters,
                    normalizedWindowY *
                    hudWindow.HeightMeters,
                    0.0f
                )
            );

        Vector2 correctedScreenPoint =
            RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                flatWorldPoint
            );

        return TryViewportScreenPointToMap(
            correctedScreenPoint,
            eventCamera,
            out mapPoint
        );
    }


    public void SetGoalMarker(
        float mapX,
        float mapY,
        float yawDegrees,
        Color color)
    {
        if (goalMarkerRoot == null)
            return;

        ApplyMapPose(
            goalMarkerRoot,
            mapX,
            mapY,
            yawDegrees
        );

        if (goalMarkerMaterial != null)
        {
            if (goalMarkerMaterial
                    .HasProperty("_BaseColor"))
            {
                goalMarkerMaterial.SetColor(
                    "_BaseColor",
                    color
                );
            }

            goalMarkerMaterial.color = color;
        }

        goalMarkerRoot.gameObject.SetActive(true);
    }


    public void ClearGoalMarker()
    {
        if (goalMarkerRoot != null)
            goalMarkerRoot.gameObject.SetActive(false);
    }


    public void SetAgentOnlineStates(
        bool robotOnline,
        bool vehicleOnline)
    {
        if (robotNameplate != null)
            robotNameplate.text = "G1 ROBOT";

        if (vehicleNameplate != null)
            vehicleNameplate.text = "SLAM VEHICLE";

        SetStatusDotColor(
            robotStatusDotMaterial,
            robotOnline
        );

        SetStatusDotColor(
            vehicleStatusDotMaterial,
            vehicleOnline
        );
    }


    private static void SetStatusDotColor(
        Material material,
        bool online)
    {
        if (material == null)
            return;

        Color color = online
            ? new Color32(53, 231, 122, 255)
            : new Color32(255, 77, 85, 255);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        material.color = color;
    }


    private static bool IsFinite(float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }


    private static void ApplyMapPose(
        Transform target,
        float mapX,
        float mapY,
        float yawDegrees)
    {
        target.localPosition =
            new Vector3(
                mapX,
                0.0f,
                mapY
            );

        /*
         * ROS/map yaw zero points along +X.
         * Unity forward points along +Z.
         */
        target.localRotation =
            Quaternion.Euler(
                0.0f,
                90.0f - yawDegrees,
                0.0f
            );
    }


    private void ResolveReferences()
    {
        if (targetImage == null)
        {
            targetImage =
                GetComponent<RawImage>();
        }

        if (hudWindow == null)
        {
            hudWindow =
                GetComponentInParent<HudWindow>(
                    true
                );
        }
    }


    private void BuildRuntimeScene()
    {
        if (runtimeRoot != null)
            return;

        if (hudWindow == null ||
            targetImage == null)
        {
            Debug.LogError(
                "[G1 Agents] HudWindow or target RawImage " +
                "is missing.",
                this
            );

            return;
        }

        Transform placeholder =
            transform.Find(
                "AgentsPlaceholderText"
            );

        if (placeholder != null)
        {
            placeholder.gameObject.SetActive(
                false
            );
        }

        targetImage.color = Color.white;

        runtimeRoot =
            new GameObject(
                "G1_Agents_Runtime"
            );

        runtimeRoot.transform.SetParent(
            hudWindow.transform,
            false
        );

        runtimeRoot.layer =
            AgentsRenderLayer;

        CreateMaterials();
        CreateFloor();
        CreateGoalMarker();
        CreateRobotProxy();
        CreateVehicle();
        CreateRenderCamera();
        CreateRenderLight();

        HideRuntimeLayerFromOtherCameras();
        SetLayerRecursively(
            runtimeRoot.transform
        );
    }


    private void CreateMaterials()
    {
        robotMaterial =
            CreateRuntimeMaterial(
                new Color32(
                    55,
                    211,
                    255,
                    255
                ),
                true
            );

        floorMaterial =
            CreateRuntimeMaterial(
                new Color32(
                    12,
                    27,
                    39,
                    255
                ),
                true
            );

        gridMaterial =
            CreateRuntimeMaterial(
                new Color32(
                    41,
                    83,
                    106,
                    255
                ),
                true
            );

        worldMapMaterial =
            CreateRuntimeMaterial(
                new Color32(
                    255,
                    76,
                    88,
                    255
                ),
                true
            );

        staticPointCloudMaterial =
            CreatePointCloudMaterial(staticPointSize);

        vehicleLidarMaterial =
            CreateRuntimeMaterial(
                new Color32(
                    255,
                    196,
                    80,
                    255
                ),
                true
            );

        plannerPathMaterial =
            CreateRuntimeMaterial(
                new Color32(
                    255,
                    190,
                    65,
                    255
                ),
                true
            );

        goalMarkerMaterial =
            CreateRuntimeMaterial(
                new Color32(
                    255,
                    196,
                    80,
                    255
                ),
                true
            );

        nameplateBackgroundMaterial =
            CreateNameplateBackgroundMaterial();
    }


    private static Shader ResolveNameplateOverlayShader()
    {
        Material overlayTemplate =
            Resources.Load<Material>(
                "Fonts & Materials/" +
                "LiberationSans SDF - Overlay"
            );

        if (overlayTemplate != null &&
            overlayTemplate.shader != null)
        {
            return overlayTemplate.shader;
        }

        return Shader.Find(
            "TextMeshPro/Mobile/Distance Field Overlay"
        );
    }


    private static Material CreateNameplateBackgroundMaterial()
    {
        Shader shader =
            Resources.Load<Shader>(
                "G1NameplateBackgroundOverlay"
            );

        if (shader == null)
        {
            Debug.LogWarning(
                "G1 nameplate overlay shader was not found; " +
                "using a normal depth-tested material."
            );

            return CreateRuntimeMaterial(Color.black, true);
        }

        Material material = new Material(shader)
        {
            name = "G1 Agents Nameplate Background",
            renderQueue = 3998
        };

        material.SetColor("_Color", Color.black);
        material.SetFloat("_Circle", 0.0f);
        return material;
    }


    private Material CreateStatusDotMaterial()
    {
        if (nameplateBackgroundMaterial == null)
            return null;

        Material material =
            new Material(nameplateBackgroundMaterial)
            {
                name = "G1 Agents Status Dot",
                renderQueue = 3999
            };

        material.SetFloat("_Circle", 1.0f);
        SetStatusDotColor(material, false);
        return material;
    }


    private Material CreateNameplateTextMaterial(
        Material source)
    {
        Shader shader = ResolveNameplateOverlayShader();

        if (source == null || shader == null)
            return null;

        Material material = new Material(source)
        {
            name = "G1 Agents Nameplate Text",
            shader = shader,
            renderQueue = 4000
        };

        nameplateTextMaterials.Add(material);
        return material;
    }


    private static Material CreatePointCloudMaterial(
        float pointSize)
    {
        Shader shader = Shader.Find("G1/PointCloud");

        if (shader == null)
        {
            Debug.LogWarning(
                "G1/PointCloud shader was not found; " +
                "falling back to the URP unlit shader."
            );

            return CreateRuntimeMaterial(Color.white, true);
        }

        Material material = new Material(shader)
        {
            name = "G1 Agents Static Point Cloud"
        };

        material.SetColor("_Tint", Color.white);
        material.SetFloat(
            "_PointSize",
            Mathf.Max(1.0f, pointSize)
        );

        return material;
    }


    private static Material CreateRuntimeMaterial(
        Color color,
        bool unlit)
    {
        Shader shader = null;

        if (unlit)
        {
            shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit"
                );
        }

        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit"
                );
        }

        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Unlit/Color"
                );
        }

        Material material =
            new Material(shader);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                color
            );
        }

        material.color = color;

        return material;
    }


    private void CreateFloor()
    {
        GameObject floor =
            CreatePrimitivePart(
                "AgentsFloor",
                PrimitiveType.Cube,
                runtimeRoot.transform,
                new Vector3(
                    0.0f,
                    -0.035f,
                    0.0f
                ),
                new Vector3(
                    30.0f,
                    0.05f,
                    30.0f
                ),
                floorMaterial
            );

        Renderer floorRenderer =
            floor.GetComponent<Renderer>();

        if (floorRenderer != null)
        {
            floorRenderer.shadowCastingMode =
                ShadowCastingMode.Off;

            floorRenderer.receiveShadows = false;
        }

        GameObject gridObject =
            new GameObject(
                "AgentsGrid"
            );

        gridObject.transform.SetParent(
            runtimeRoot.transform,
            false
        );

        gridObject.transform.localPosition =
            new Vector3(
                0.0f,
                0.002f,
                0.0f
            );

        MeshFilter filter =
            gridObject.AddComponent<MeshFilter>();

        MeshRenderer renderer =
            gridObject.AddComponent<MeshRenderer>();

        renderer.sharedMaterial =
            gridMaterial;

        renderer.shadowCastingMode =
            ShadowCastingMode.Off;

        renderer.receiveShadows = false;

        List<Vector3> vertices =
            new List<Vector3>();

        List<int> indices =
            new List<int>();

        const int halfExtent = 15;

        for (
            int coordinate = -halfExtent;
            coordinate <= halfExtent;
            ++coordinate)
        {
            int firstIndex =
                vertices.Count;

            vertices.Add(
                new Vector3(
                    coordinate,
                    0.0f,
                    -halfExtent
                )
            );

            vertices.Add(
                new Vector3(
                    coordinate,
                    0.0f,
                    halfExtent
                )
            );

            indices.Add(firstIndex);
            indices.Add(firstIndex + 1);


            firstIndex =
                vertices.Count;

            vertices.Add(
                new Vector3(
                    -halfExtent,
                    0.0f,
                    coordinate
                )
            );

            vertices.Add(
                new Vector3(
                    halfExtent,
                    0.0f,
                    coordinate
                )
            );

            indices.Add(firstIndex);
            indices.Add(firstIndex + 1);
        }

        gridMesh =
            new Mesh
            {
                name = "G1 Agents Runtime Grid"
            };

        gridMesh.SetVertices(
            vertices
        );

        gridMesh.SetIndices(
            indices,
            MeshTopology.Lines,
            0
        );

        gridMesh.bounds =
            new Bounds(
                Vector3.zero,
                new Vector3(
                    30.0f,
                    1.0f,
                    30.0f
                )
            );

        filter.sharedMesh =
            gridMesh;
    }


    private void CreateGoalMarker()
    {
        goalMarkerRoot =
            new GameObject(
                "AgentsGoalMarker"
            ).transform;

        goalMarkerRoot.SetParent(
            runtimeRoot.transform,
            false
        );

        CreatePrimitivePart(
            "GoalDisc",
            PrimitiveType.Cylinder,
            goalMarkerRoot,
            new Vector3(0.0f, 0.025f, 0.0f),
            new Vector3(0.18f, 0.012f, 0.18f),
            goalMarkerMaterial
        );

        CreatePrimitivePart(
            "GoalDirection",
            PrimitiveType.Cube,
            goalMarkerRoot,
            new Vector3(0.0f, 0.04f, 0.40f),
            new Vector3(0.055f, 0.025f, 0.72f),
            goalMarkerMaterial
        );

        GameObject tip =
            CreatePrimitivePart(
                "GoalArrowTip",
                PrimitiveType.Cube,
                goalMarkerRoot,
                new Vector3(0.0f, 0.04f, 0.78f),
                new Vector3(0.17f, 0.025f, 0.17f),
                goalMarkerMaterial
            );

        tip.transform.localRotation =
            Quaternion.Euler(0.0f, 45.0f, 0.0f);

        goalMarkerRoot.gameObject.SetActive(false);
    }


    private void CreateRobotProxy()
    {
        robotRoot =
            new GameObject(
                "G1Agent"
            ).transform;

        robotRoot.SetParent(
            runtimeRoot.transform,
            false
        );

        if (robotModelSource != null)
        {
            robotReplica =
                Instantiate(
                    robotModelSource.gameObject,
                    robotRoot,
                    false
                );

            robotReplica.name = "AgentsG1Replica";
            robotReplica.tag = "Untagged";
            robotReplica.hideFlags = HideFlags.DontSave;

            Transform replicaTransform =
                robotReplica.transform;

            replicaTransform.localPosition =
                Vector3.up * 0.79f;
            replicaTransform.localRotation = Quaternion.identity;
            replicaTransform.localScale =
                robotModelSource.localScale;

            ArticulationBody[] sourceBodies =
                robotModelSource.GetComponentsInChildren
                    <ArticulationBody>(true);

            ArticulationBody[] replicaBodies =
                robotReplica.GetComponentsInChildren
                    <ArticulationBody>(true);

            int poseCount =
                Mathf.Min(
                    sourceBodies.Length,
                    replicaBodies.Length
                );

            sourcePoseTransforms = new Transform[poseCount];
            replicaPoseTransforms = new Transform[poseCount];

            for (int index = 0;
                 index < poseCount;
                 ++index)
            {
                sourcePoseTransforms[index] =
                    sourceBodies[index].transform;

                replicaPoseTransforms[index] =
                    replicaBodies[index].transform;
            }

            foreach (Collider collider in
                     robotReplica.GetComponentsInChildren
                         <Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (ArticulationBody body in replicaBodies)
            {
                body.useGravity = false;
                body.enabled = false;
            }

            robotVisualRenderers =
                robotReplica.GetComponentsInChildren
                    <Renderer>(true);

            robotNameplate =
                CreateNameplate(
                    robotRoot,
                    "G1 ROBOT",
                    2.05f,
                    new Color32(110, 230, 255, 255),
                    out robotStatusDotMaterial
                );

            Debug.Log(
                "[G1 Agents] Live G1 pose mirror ready: " +
                poseCount + " articulated transforms.",
                this
            );

            return;
        }

        Debug.LogWarning(
            "[G1 Agents] Robot Model Source is not assigned; " +
            "using the lightweight proxy.",
            this
        );

        CreatePrimitivePart(
            "Torso",
            PrimitiveType.Cube,
            robotRoot,
            new Vector3(0.0f, 1.05f, 0.0f),
            new Vector3(0.46f, 0.62f, 0.27f),
            robotMaterial
        );

        CreatePrimitivePart(
            "Pelvis",
            PrimitiveType.Cube,
            robotRoot,
            new Vector3(0.0f, 0.66f, 0.0f),
            new Vector3(0.40f, 0.20f, 0.25f),
            robotMaterial
        );

        CreatePrimitivePart(
            "Head",
            PrimitiveType.Sphere,
            robotRoot,
            new Vector3(0.0f, 1.53f, 0.0f),
            new Vector3(0.28f, 0.28f, 0.28f),
            robotMaterial
        );

        CreatePrimitivePart(
            "LeftArm",
            PrimitiveType.Capsule,
            robotRoot,
            new Vector3(-0.33f, 0.96f, 0.0f),
            new Vector3(0.10f, 0.30f, 0.10f),
            robotMaterial
        );

        CreatePrimitivePart(
            "RightArm",
            PrimitiveType.Capsule,
            robotRoot,
            new Vector3(0.33f, 0.96f, 0.0f),
            new Vector3(0.10f, 0.30f, 0.10f),
            robotMaterial
        );

        CreatePrimitivePart(
            "LeftLeg",
            PrimitiveType.Capsule,
            robotRoot,
            new Vector3(-0.13f, 0.32f, 0.0f),
            new Vector3(0.12f, 0.32f, 0.12f),
            robotMaterial
        );

        CreatePrimitivePart(
            "RightLeg",
            PrimitiveType.Capsule,
            robotRoot,
            new Vector3(0.13f, 0.32f, 0.0f),
            new Vector3(0.12f, 0.32f, 0.12f),
            robotMaterial
        );

        robotVisualRenderers =
            robotRoot.GetComponentsInChildren
                <Renderer>(true);

        robotNameplate =
            CreateNameplate(
                robotRoot,
                "G1 ROBOT",
                2.05f,
                new Color32(
                    110,
                    230,
                    255,
                    255
                ),
                out robotStatusDotMaterial
            );
    }


    private void CreateVehicle()
    {
        vehicleRoot =
            new GameObject(
                "VehicleAgent"
            ).transform;

        vehicleRoot.SetParent(
            runtimeRoot.transform,
            false
        );

        if (vehiclePrefab != null)
        {
            GameObject instance =
                Instantiate(
                    vehiclePrefab,
                    vehicleRoot
                );

            instance.name =
                "SLAMVehicleVisual";

            instance.transform.localPosition =
                Vector3.zero;

            instance.transform.localScale =
                Vector3.one;

            vehicleVisual =
                instance.transform;
        }
        else
        {
            Debug.LogWarning(
                "[G1 Agents] Vehicle prefab is not assigned; " +
                "using a temporary cube.",
                this
            );

            GameObject fallback =
                CreatePrimitivePart(
                    "MissingVehiclePrefab",
                    PrimitiveType.Cube,
                    vehicleRoot,
                    new Vector3(
                        0.0f,
                        0.18f,
                        0.0f
                    ),
                    new Vector3(
                        0.75f,
                        0.36f,
                        0.55f
                    ),
                    robotMaterial
                );

            vehicleVisual =
                fallback.transform;
        }

        vehicleVisualRenderers =
            vehicleVisual != null
                ? vehicleVisual.GetComponentsInChildren
                    <Renderer>(true)
                : System.Array.Empty<Renderer>();

        vehicleNameplate =
            CreateNameplate(
                vehicleRoot,
                "SLAM VEHICLE",
                1.15f,
                new Color32(
                    255,
                    196,
                    80,
                    255
                ),
                out vehicleStatusDotMaterial
            );
    }


    private static GameObject CreatePrimitivePart(
        string objectName,
        PrimitiveType primitiveType,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject part =
            GameObject.CreatePrimitive(
                primitiveType
            );

        part.name = objectName;

        part.transform.SetParent(
            parent,
            false
        );

        part.transform.localPosition =
            localPosition;

        part.transform.localRotation =
            Quaternion.identity;

        part.transform.localScale =
            localScale;

        Collider collider =
            part.GetComponent<Collider>();

        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        Renderer renderer =
            part.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial =
                material;

            renderer.shadowCastingMode =
                ShadowCastingMode.Off;

            renderer.receiveShadows = false;
        }

        part.layer =
            AgentsRenderLayer;

        return part;
    }


    private TextMeshPro CreateNameplate(
        Transform parent,
        string label,
        float height,
        Color color,
        out Material statusDotMaterial)
    {
        GameObject objectInstance =
            new GameObject(
                label + "_Nameplate"
            );

        objectInstance.transform.SetParent(
            parent,
            false
        );

        objectInstance.transform.localPosition =
            new Vector3(
                0.0f,
                height,
                0.0f
            );

        objectInstance.transform.localScale =
            Vector3.one * 0.44f;

        objectInstance.layer =
            AgentsRenderLayer;

        GameObject background =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );

        background.name = label + "_Background";
        background.transform.SetParent(
            objectInstance.transform,
            false
        );
        background.transform.localPosition =
            new Vector3(0.0f, 0.0f, 0.035f);
        background.transform.localRotation =
            Quaternion.identity;
        background.transform.localScale =
            Vector3.one;
        background.layer = AgentsRenderLayer;

        Collider backgroundCollider =
            background.GetComponent<Collider>();

        if (backgroundCollider != null)
        {
            backgroundCollider.enabled = false;
            Destroy(backgroundCollider);
        }

        Renderer backgroundRenderer =
            background.GetComponent<Renderer>();

        if (backgroundRenderer != null)
        {
            backgroundRenderer.sharedMaterial =
                nameplateBackgroundMaterial;
            backgroundRenderer.sortingOrder = 9;
            backgroundRenderer.shadowCastingMode =
                ShadowCastingMode.Off;
            backgroundRenderer.receiveShadows = false;
        }

        TextMeshPro text =
            objectInstance.AddComponent<TextMeshPro>();

        text.text = label;
        text.fontSize = 4.0f;
        text.alignment =
            TextAlignmentOptions.Center;

        text.color = color;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.25f;

        text.textWrappingMode =
            TextWrappingModes.NoWrap;

        Material overlayTextMaterial =
            CreateNameplateTextMaterial(
                text.fontSharedMaterial
            );

        if (overlayTextMaterial != null)
        {
            text.fontSharedMaterial =
                overlayTextMaterial;
        }

        text.rectTransform.sizeDelta =
            new Vector2(
                6.0f,
                1.0f
            );

        text.ForceMeshUpdate();

        Vector2 renderedTextSize =
            text.GetRenderedValues(false);

        float dotDiameter = Mathf.Clamp(
            renderedTextSize.y * 0.322f,
            0.15f,
            0.30f
        );

        float dotGap = Mathf.Max(
            0.17f,
            dotDiameter * 1.75f
        );
        float statusWidth = dotDiameter + dotGap;

        text.rectTransform.localPosition =
            new Vector3(
                statusWidth * 0.5f,
                0.0f,
                0.0f
            );

        background.transform.localScale =
            new Vector3(
                Mathf.Max(
                    0.5f,
                    renderedTextSize.x +
                    statusWidth + 0.30f
                ),
                Mathf.Max(
                    0.35f,
                    renderedTextSize.y + 0.16f
                ),
                1.0f
            );

        GameObject statusDot =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );

        statusDot.name = label + "_StatusDot";
        statusDot.transform.SetParent(
            objectInstance.transform,
            false
        );
        statusDot.transform.localPosition =
            new Vector3(
                -(renderedTextSize.x + dotGap) * 0.5f,
                0.0f,
                0.0f
            );
        statusDot.transform.localRotation =
            Quaternion.identity;
        statusDot.transform.localScale =
            new Vector3(
                dotDiameter,
                dotDiameter,
                1.0f
            );
        statusDot.layer = AgentsRenderLayer;

        Collider statusDotCollider =
            statusDot.GetComponent<Collider>();

        if (statusDotCollider != null)
        {
            statusDotCollider.enabled = false;
            Destroy(statusDotCollider);
        }

        statusDotMaterial =
            CreateStatusDotMaterial();

        Renderer statusDotRenderer =
            statusDot.GetComponent<Renderer>();

        if (statusDotRenderer != null)
        {
            statusDotRenderer.sharedMaterial =
                statusDotMaterial;
            statusDotRenderer.sortingOrder = 10;
            statusDotRenderer.shadowCastingMode =
                ShadowCastingMode.Off;
            statusDotRenderer.receiveShadows = false;
        }

        Renderer renderer =
            text.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sortingOrder = 11;

            renderer.shadowCastingMode =
                ShadowCastingMode.Off;
        }

        return text;
    }


    private void CreateRenderCamera()
    {
        renderCameraObject =
            new GameObject(
                "G1_Agents_RenderCamera"
            );

        renderCameraObject.transform.SetParent(
            hudWindow.transform,
            false
        );

        renderCamera =
            renderCameraObject.AddComponent<Camera>();

        renderCamera.enabled = true;
        renderCamera.orthographic = true;

        renderCamera.clearFlags =
            CameraClearFlags.SolidColor;

        renderCamera.backgroundColor =
            backgroundColor;

        renderCamera.cullingMask =
            1 << AgentsRenderLayer;

        renderCamera.nearClipPlane = 0.01f;
        renderCamera.farClipPlane = 100.0f;
        renderCamera.allowHDR = false;
        renderCamera.allowMSAA = true;

        UpdateRenderTexture(
            true
        );
    }


    private void CreateRenderLight()
    {
        GameObject lightObject =
            new GameObject(
                "G1_Agents_RenderLight"
            );

        lightObject.transform.SetParent(
            runtimeRoot.transform,
            false
        );

        lightObject.transform.localRotation =
            Quaternion.Euler(
                45.0f,
                -35.0f,
                0.0f
            );

        lightObject.layer =
            AgentsRenderLayer;

        Light renderLight =
            lightObject.AddComponent<Light>();

        renderLight.type =
            LightType.Directional;

        renderLight.intensity = 1.35f;
        renderLight.shadows =
            LightShadows.None;

        renderLight.cullingMask =
            1 << AgentsRenderLayer;
    }


    private void UpdateRenderTexture(
        bool force = false)
    {
        if (targetImage == null ||
            renderCamera == null)
        {
            return;
        }

        Vector2 viewportSize =
            targetImage.rectTransform.rect.size;

        if (viewportSize.x < 1.0f ||
            viewportSize.y < 1.0f)
        {
            return;
        }

        if (!force &&
            Vector2.Distance(
                viewportSize,
                lastViewportSize
            ) < 2.0f)
        {
            return;
        }

        lastViewportSize =
            viewportSize;

        float aspect =
            Mathf.Clamp(
                viewportSize.x /
                viewportSize.y,
                0.1f,
                10.0f
            );

        int longestSide =
            Mathf.Clamp(
                baseTextureResolution,
                256,
                1536
            );

        int width;
        int height;

        if (aspect >= 1.0f)
        {
            width = longestSide;

            height =
                Mathf.Max(
                    64,
                    Mathf.RoundToInt(
                        width / aspect
                    )
                );
        }
        else
        {
            height = longestSide;

            width =
                Mathf.Max(
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
            )
            {
                name =
                    "G1 Agents Window RenderTexture",

                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };

        renderTexture.Create();

        renderCamera.targetTexture =
            renderTexture;

        renderCamera.aspect =
            width / (float)height;

        targetImage.texture =
            renderTexture;

        targetImage.color =
            Color.white;
    }


    private void UpdateCameraPose()
    {
        if (robotRoot == null ||
            vehicleRoot == null ||
            renderCamera == null)
        {
            return;
        }

        Vector3 robotPosition =
            robotRoot.localPosition;

        Vector3 vehiclePosition =
            vehicleRoot.localPosition;

        Vector2 minimum = new Vector2(
            Mathf.Min(robotPosition.x, vehiclePosition.x),
            Mathf.Min(robotPosition.z, vehiclePosition.z)
        );
        Vector2 maximum = new Vector2(
            Mathf.Max(robotPosition.x, vehiclePosition.x),
            Mathf.Max(robotPosition.z, vehiclePosition.z)
        );

        if (worldMapBoundsValid)
        {
            minimum = Vector2.Min(minimum, worldMapMinimum);
            maximum = Vector2.Max(maximum, worldMapMaximum);
        }

        Vector2 centre = (minimum + maximum) * 0.5f;
        Vector2 size = maximum - minimum;

        Vector3 localTarget = new Vector3(
            centre.x,
            0.55f,
            centre.y
        ) + viewPanOffset;

        float fittedSize = Mathf.Max(
            minimumViewSize,
            size.y * 0.60f + 0.75f,
            size.x * 0.60f /
                Mathf.Max(0.5f, renderCamera.aspect) +
                0.75f
        );

        fittedSize = Mathf.Clamp(
            fittedSize,
            minimumViewSize,
            maximumViewSize
        );

        renderCamera.orthographicSize = Mathf.Clamp(
            fittedSize * viewZoom,
            minimumViewSize * minimumZoom,
            maximumViewSize * maximumZoom
        );

        Quaternion viewOrbit =
            Quaternion.Euler(
                Mathf.Clamp(
                    cameraPitchDegrees + orbitPitchOffset,
                    minimumCameraPitch,
                    maximumCameraPitch
                ),
                cameraYawDegrees + orbitYawOffset,
                0.0f
            );

        Vector3 localCameraPosition =
            localTarget +
            viewOrbit *
            new Vector3(
                0.0f,
                0.0f,
                -20.0f
            );

        Quaternion localCameraRotation =
            Quaternion.LookRotation(
                localTarget -
                    localCameraPosition,
                Vector3.up
            );

        renderCamera.transform.SetPositionAndRotation(
            hudWindow.transform.TransformPoint(
                localCameraPosition
            ),
            hudWindow.transform.rotation *
                localCameraRotation
        );
    }


    private void UpdateNameplates()
    {
        PositionNameplateAboveRenderers(
            robotNameplate,
            robotVisualRenderers
        );

        PositionNameplateAboveRenderers(
            vehicleNameplate,
            vehicleVisualRenderers
        );
    }


    private void PositionNameplateAboveRenderers(
        TextMeshPro nameplate,
        Renderer[] visualRenderers)
    {
        if (nameplate == null ||
            renderCamera == null ||
            visualRenderers == null)
        {
            return;
        }

        bool hasBounds = false;
        Bounds combinedBounds = default;

        foreach (Renderer visualRenderer in visualRenderers)
        {
            if (visualRenderer == null ||
                !visualRenderer.enabled ||
                !visualRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = visualRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(
                    visualRenderer.bounds
                );
            }
        }

        if (!hasBounds)
        {
            FaceNameplateTowardCamera(nameplate);
            return;
        }

        Vector3 center = combinedBounds.center;
        Vector3 extents = combinedBounds.extents;

        float minimumViewportX = float.PositiveInfinity;
        float maximumViewportX = float.NegativeInfinity;
        float maximumViewportY = float.NegativeInfinity;
        bool hasVisibleCorner = false;

        for (int cornerIndex = 0;
             cornerIndex < 8;
             ++cornerIndex)
        {
            Vector3 worldCorner = center +
                new Vector3(
                    (cornerIndex & 1) == 0
                        ? -extents.x
                        : extents.x,
                    (cornerIndex & 2) == 0
                        ? -extents.y
                        : extents.y,
                    (cornerIndex & 4) == 0
                        ? -extents.z
                        : extents.z
                );

            Vector3 viewportCorner =
                renderCamera.WorldToViewportPoint(
                    worldCorner
                );

            if (viewportCorner.z <= 0.0f)
                continue;

            hasVisibleCorner = true;

            minimumViewportX = Mathf.Min(
                minimumViewportX,
                viewportCorner.x
            );

            maximumViewportX = Mathf.Max(
                maximumViewportX,
                viewportCorner.x
            );

            maximumViewportY = Mathf.Max(
                maximumViewportY,
                viewportCorner.y
            );
        }

        Vector3 centerViewport =
            renderCamera.WorldToViewportPoint(center);

        if (!hasVisibleCorner ||
            centerViewport.z <= 0.0f)
        {
            FaceNameplateTowardCamera(nameplate);
            return;
        }

        float elevatedViewportY =
            maximumViewportY + 0.035f;

        Vector3 gridViewport =
            renderCamera.WorldToViewportPoint(
                nameplate.transform.parent.position
            );

        float loweredViewportY =
            gridViewport.z > 0.0f
                ? Mathf.Lerp(
                    elevatedViewportY,
                    gridViewport.y,
                    0.20f
                )
                : elevatedViewportY;

        Vector3 nameplateViewport =
            new Vector3(
                (minimumViewportX +
                 maximumViewportX) * 0.5f,
                loweredViewportY,
                centerViewport.z
            );

        nameplate.transform.position =
            renderCamera.ViewportToWorldPoint(
                nameplateViewport
            );

        FaceNameplateTowardCamera(nameplate);
    }


    private void FaceNameplateTowardCamera(
        TextMeshPro nameplate)
    {
        if (nameplate == null ||
            renderCamera == null)
        {
            return;
        }

        Transform labelTransform =
            nameplate.transform;

        Vector3 forward =
            labelTransform.position -
            renderCamera.transform.position;

        if (forward.sqrMagnitude <
            0.000001f)
        {
            return;
        }

        labelTransform.rotation =
            Quaternion.LookRotation(
                forward.normalized,
                renderCamera.transform.up
            );
    }


    private void MirrorRobotPose()
    {
        int count =
            Mathf.Min(
                sourcePoseTransforms.Length,
                replicaPoseTransforms.Length
            );

        for (int index = 0;
             index < count;
             ++index)
        {
            Transform sourceTransform =
                sourcePoseTransforms[index];

            Transform replicaTransform =
                replicaPoseTransforms[index];

            if (sourceTransform == null ||
                replicaTransform == null)
            {
                continue;
            }

            replicaTransform.localPosition =
                sourceTransform.localPosition;

            replicaTransform.localRotation =
                sourceTransform.localRotation;

            replicaTransform.localScale =
                sourceTransform.localScale;
        }
    }


    private static void SetLayerRecursively(
        Transform root)
    {
        if (root == null)
            return;

        Transform[] descendants =
            root.GetComponentsInChildren
                <Transform>(true);

        foreach (Transform descendant
                 in descendants)
        {
            descendant.gameObject.layer =
                AgentsRenderLayer;

            Renderer renderer =
                descendant.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.shadowCastingMode =
                    ShadowCastingMode.Off;

                renderer.receiveShadows = false;
            }
        }
    }


    private void HideRuntimeLayerFromOtherCameras()
    {
        Camera[] cameras = Camera.allCameras;
        int agentsLayerMask = 1 << AgentsRenderLayer;

        foreach (Camera candidate in cameras)
        {
            if (candidate == null ||
                candidate == renderCamera)
            {
                continue;
            }

            candidate.cullingMask &= ~agentsLayerMask;
        }
    }
}

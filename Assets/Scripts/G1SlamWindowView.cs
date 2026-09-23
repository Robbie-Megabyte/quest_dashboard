using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public sealed class G1SlamWindowView :
    MonoBehaviour,
    IDragHandler,
    IScrollHandler,
    IPointerClickHandler
{
    private const int RenderLayer = 30;

    private static int nextRuntimeSlot = 0;

    private const float FloorOriginZ =
        -1.235811371f;

    private static readonly Quaternion FloorRotation =
        Quaternion.FromToRotation(
            new Vector3(
                0.092484490f,
                -0.025026605f,
                1.0f
            ).normalized,
            Vector3.forward
        );

    [Header("Dashboard")]
    [SerializeField]
    private string dashboardBaseUrl =
        "http://192.168.0.116:8080";

    [SerializeField]
    [Min(0.05f)]
    private float livePollIntervalSeconds =
        0.15f;

    [Header("Display")]
    [SerializeField]
    private RawImage targetImage;

    [SerializeField]
    private Shader pointCloudShader;

    [SerializeField]
    private Color backgroundColor =
        new Color32(6, 16, 25, 255);

    [SerializeField]
    private Color liveCloudColor =
        new Color32(255, 59, 212, 255);

    [SerializeField]
    [Range(1.0f, 10.0f)]
    private float savedPointSize = 2.0f;

    [SerializeField]
    [Range(1.0f, 12.0f)]
    private float livePointSize = 4.0f;

    [Header("Render Texture")]
    [SerializeField]
    private int textureWidth = 960;

    [SerializeField]
    private int textureHeight = 576;

    [Header("Interaction")]
    [SerializeField]
    private float rotationSensitivity = 0.18f;

    [SerializeField]
    private float zoomSensitivity = 0.08f;

    [SerializeField]
    [Min(0.1f)]
    private float panSensitivity = 1.0f;

    [SerializeField]
    private bool startInPanMode = false;

    [SerializeField]
    private TMP_Text dragModeLabel;

    [SerializeField]
    private float initialYaw = -35.0f;

    [SerializeField]
    private float initialPitch = 48.0f;

    private GameObject runtimeRoot;
    private Transform cloudRoot;
    private Camera renderCamera;
    private RenderTexture renderTexture;

    private Mesh savedMesh;
    private Mesh liveMesh;

    private MeshRenderer savedRenderer;
    private MeshRenderer liveRenderer;

    private Material savedMaterial;
    private Material liveMaterial;

    private Vector3 mapCenter;
    private float mapSpan = 20.0f;

    private float yaw;
    private float pitch;
    private float zoom = 1.0f;
    private Vector3 viewTarget = Vector3.zero;
    private bool panMode;
    private bool interactionEnabled = true;

    private bool runtimeReady;
    private bool mapLoaded;
    private Coroutine sessionCoroutine;

    private sealed class ParsedMap
    {
        public Vector3[] Vertices;
        public Color32[] Colors;
        public Vector3 Center;
        public float Span;
    }

    public string DashboardBaseUrl =>
        dashboardBaseUrl;

    public Transform RuntimeContentRoot =>
        cloudRoot;

    public int RuntimeRenderLayer =>
        RenderLayer;

    public bool HasLoadedMap =>
        mapLoaded;

    public bool TryConvertMapPose(
        float mapX,
        float mapY,
        float mapYawRadians,
        float heightMeters,
        out Vector3 localPosition,
        out Quaternion localRotation)
    {
        localPosition = Vector3.zero;
        localRotation = Quaternion.identity;

        if (!mapLoaded)
            return false;

        float floorZ =
            FloorOriginZ -
            0.092484490f * mapX +
            0.025026605f * mapY;

        Vector3 floorPosition =
            LevelAndConvert(
                mapX,
                mapY,
                floorZ
            ) -
            mapCenter;

        localPosition =
            floorPosition +
            Vector3.up * heightMeters;

        float headingX =
            mapX + Mathf.Cos(mapYawRadians);

        float headingY =
            mapY + Mathf.Sin(mapYawRadians);

        float headingZ =
            FloorOriginZ -
            0.092484490f * headingX +
            0.025026605f * headingY;

        Vector3 headingPoint =
            LevelAndConvert(
                headingX,
                headingY,
                headingZ
            ) -
            mapCenter;

        Vector3 forward =
            headingPoint - floorPosition;

        forward.y = 0.0f;

        if (forward.sqrMagnitude < 0.000001f)
            return false;

        localRotation =
            Quaternion.LookRotation(
                forward.normalized,
                Vector3.up
            );

        return true;
    }

    public void SetInteractionEnabled(
        bool enabled)
    {
        interactionEnabled = enabled;
    }

    public void ZoomIn()
    {
        if (!interactionEnabled)
    return;
        zoom =
            Mathf.Clamp(
                zoom / 1.20f,
                0.35f,
                3.0f
            );

        FitCamera();
    }

    public void ZoomOut()
    {
        if (!interactionEnabled)
    return;
        zoom =
            Mathf.Clamp(
                zoom * 1.20f,
                0.35f,
                3.0f
            );

        FitCamera();
    }

    public void ResetCameraView()
    {
        ResetView();
    }

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<RawImage>();

        panMode = startInPanMode;
        ApplyDashboardControlTheme();
        UpdateDragModeLabel();
        SetupRuntime();
    }

    private void OnEnable()
    {
        SetupRuntime();

        if (renderCamera != null)
            renderCamera.enabled = true;

        if (sessionCoroutine == null)
            sessionCoroutine = StartCoroutine(RunSession());
    }

    private void OnDisable()
    {
        if (sessionCoroutine != null)
        {
            StopCoroutine(sessionCoroutine);
            sessionCoroutine = null;
        }

        if (renderCamera != null)
            renderCamera.enabled = false;
    }

    private void OnDestroy()
    {
        if (savedMesh != null)
            Destroy(savedMesh);

        if (liveMesh != null)
            Destroy(liveMesh);

        if (savedMaterial != null)
            Destroy(savedMaterial);

        if (liveMaterial != null)
            Destroy(liveMaterial);

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (runtimeRoot != null)
            Destroy(runtimeRoot);
    }

    private void SetupRuntime()
    {
        if (runtimeReady)
            return;

        if (pointCloudShader == null)
        {
            pointCloudShader =
                Shader.Find("G1/PointCloud");
        }

        if (pointCloudShader == null)
        {
            Debug.LogError(
                "[G1 SLAM] Point-cloud shader is missing.",
                this
            );
            return;
        }

        runtimeReady = true;

        runtimeRoot =
            new GameObject(
                $"{name}_SlamRuntime"
            );

        runtimeRoot.hideFlags =
            HideFlags.DontSave;

        // Keep this private rendering world away from the main XR scene.
        int runtimeSlot =
            nextRuntimeSlot++;

        float slot =
            Mathf.Abs(runtimeSlot % 20) *
            250.0f;

        runtimeRoot.transform.position =
            new Vector3(
                slot,
                -500.0f,
                500.0f
            );

        cloudRoot =
            new GameObject("CloudRoot")
                .transform;

        cloudRoot.SetParent(
            runtimeRoot.transform,
            false
        );

        GameObject cameraObject =
            new GameObject("SlamRenderCamera");

        cameraObject.transform.SetParent(
            runtimeRoot.transform,
            false
        );

        renderCamera =
            cameraObject.AddComponent<Camera>();

        renderCamera.clearFlags =
            CameraClearFlags.SolidColor;

        renderCamera.backgroundColor =
            backgroundColor;

        renderCamera.cullingMask =
            1 << RenderLayer;

        renderCamera.fieldOfView = 46.0f;
        renderCamera.nearClipPlane = 0.05f;
        renderCamera.farClipPlane = 250.0f;
        renderCamera.allowHDR = false;
        renderCamera.allowMSAA = false;

        renderTexture =
            new RenderTexture(
                Mathf.Max(256, textureWidth),
                Mathf.Max(256, textureHeight),
                24,
                RenderTextureFormat.ARGB32
            );

        renderTexture.name =
            "G1 SLAM Window";

        renderTexture.antiAliasing = 1;
        renderTexture.useMipMap = false;
        renderTexture.autoGenerateMips = false;
        renderTexture.Create();

        renderCamera.targetTexture =
            renderTexture;

        targetImage.texture =
            renderTexture;

        targetImage.color =
            Color.white;

        targetImage.raycastTarget =
            true;

        savedMaterial =
            new Material(pointCloudShader);

        savedMaterial.name =
            "G1 Saved SLAM Map";

        savedMaterial.SetColor(
            "_Tint",
            Color.white
        );

        savedMaterial.SetFloat(
            "_PointSize",
            savedPointSize
        );

        liveMaterial =
            new Material(pointCloudShader);

        liveMaterial.name =
            "G1 Live LiDAR";

        liveMaterial.SetColor(
            "_Tint",
            liveCloudColor
        );

        liveMaterial.SetFloat(
            "_PointSize",
            livePointSize
        );

        savedRenderer =
            CreatePointObject(
                "SavedMap",
                savedMaterial,
                out savedMesh
            );

        liveRenderer =
            CreatePointObject(
                "LiveCloud",
                liveMaterial,
                out liveMesh
            );

        savedRenderer.enabled = false;
        liveRenderer.enabled = false;

        ResetView();
    }

    private MeshRenderer CreatePointObject(
        string objectName,
        Material material,
        out Mesh mesh)
    {
        GameObject pointObject =
            new GameObject(objectName);

        pointObject.layer =
            RenderLayer;

        pointObject.transform.SetParent(
            cloudRoot,
            false
        );

        MeshFilter filter =
            pointObject.AddComponent<MeshFilter>();

        MeshRenderer renderer =
            pointObject.AddComponent<MeshRenderer>();

        renderer.sharedMaterial =
            material;

        renderer.shadowCastingMode =
            ShadowCastingMode.Off;

        renderer.receiveShadows =
            false;

        renderer.lightProbeUsage =
            LightProbeUsage.Off;

        renderer.reflectionProbeUsage =
            ReflectionProbeUsage.Off;

        mesh =
            new Mesh
            {
                name = objectName,
                indexFormat = IndexFormat.UInt32
            };

        filter.sharedMesh =
            mesh;

        return renderer;
    }

    private IEnumerator RunSession()
    {
        if (!mapLoaded)
            yield return DownloadSavedMap();

        if (mapLoaded)
            yield return PollLiveCloud();

        sessionCoroutine = null;
    }

    private IEnumerator DownloadSavedMap()
    {
        string url =
            dashboardBaseUrl.TrimEnd('/') +
            "/api/slam/map";

        using UnityWebRequest request =
            UnityWebRequest.Get(url);

        request.timeout = 15;

        yield return request.SendWebRequest();

        if (request.result !=
            UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                $"[G1 SLAM] Map download failed: " +
                $"{request.error} ({request.responseCode})",
                this
            );
            yield break;
        }

        string pcd =
            request.downloadHandler.text;

        Task<ParsedMap> parseTask =
            Task.Run(
                () => ParseAsciiPcd(pcd)
            );

        while (!parseTask.IsCompleted)
            yield return null;

        if (parseTask.IsFaulted)
        {
            Debug.LogError(
                "[G1 SLAM] Map parsing failed: " +
                parseTask.Exception,
                this
            );
            yield break;
        }

        ParsedMap parsed =
            parseTask.Result;

        mapCenter = parsed.Center;
        mapSpan = parsed.Span;

        for (int index = 0;
             index < parsed.Vertices.Length;
             index++)
        {
            parsed.Vertices[index] -=
                mapCenter;
        }

        int[] indices =
            SequentialIndices(
                parsed.Vertices.Length
            );

        savedMesh.Clear();
        savedMesh.vertices =
            parsed.Vertices;

        savedMesh.colors32 =
            parsed.Colors;

        savedMesh.SetIndices(
            indices,
            MeshTopology.Points,
            0,
            false
        );

        savedMesh.RecalculateBounds();

        savedRenderer.enabled =
            true;

        mapLoaded = true;

        FitCamera();

        Debug.Log(
            $"[G1 SLAM] Loaded " +
            $"{parsed.Vertices.Length:N0} saved points.",
            this
        );
    }

    private IEnumerator PollLiveCloud()
    {
        WaitForSecondsRealtime delay =
            new WaitForSecondsRealtime(
                Mathf.Max(
                    0.05f,
                    livePollIntervalSeconds
                )
            );

        string url =
            dashboardBaseUrl.TrimEnd('/') +
            "/api/slam/cloud";

        while (isActiveAndEnabled)
        {
            using UnityWebRequest request =
                UnityWebRequest.Get(url);

            request.timeout = 4;

            yield return request.SendWebRequest();

            if (request.responseCode == 200)
            {
                byte[] bytes =
                    request.downloadHandler.data;

                if (bytes != null &&
                    bytes.Length >= 12 &&
                    bytes.Length % 12 == 0)
                {
                    UpdateLiveMesh(bytes);
                }
            }
            else if (request.responseCode != 204)
            {
                Debug.LogWarning(
                    $"[G1 SLAM] Live cloud HTTP " +
                    $"{request.responseCode}: {request.error}",
                    this
                );
            }

            yield return delay;
        }
    }

    private void UpdateLiveMesh(
        byte[] bytes)
    {
        int pointCount =
            bytes.Length / 12;

        Vector3[] vertices =
            new Vector3[pointCount];

        int offset = 0;

        for (int index = 0;
             index < pointCount;
             index++)
        {
            float x =
                BitConverter.ToSingle(
                    bytes,
                    offset
                );

            float y =
                BitConverter.ToSingle(
                    bytes,
                    offset + 4
                );

            float z =
                BitConverter.ToSingle(
                    bytes,
                    offset + 8
                );

            offset += 12;

            vertices[index] =
                LevelAndConvert(
                    x,
                    y,
                    z
                ) -
                mapCenter;
        }

        liveMesh.Clear();
        liveMesh.vertices =
            vertices;

        liveMesh.SetIndices(
            SequentialIndices(pointCount),
            MeshTopology.Points,
            0,
            false
        );

        liveMesh.RecalculateBounds();
        liveRenderer.enabled = true;
    }

    private static ParsedMap ParseAsciiPcd(
        string text)
    {
        string[] lines =
            text.Split('\n');

        string[] fields = null;
        int dataIndex = -1;

        for (int index = 0;
             index < lines.Length;
             index++)
        {
            string line =
                lines[index].Trim();

            if (line.StartsWith(
                "FIELDS ",
                StringComparison.OrdinalIgnoreCase))
            {
                fields =
                    line.Split(
                        (char[])null,
                        StringSplitOptions
                            .RemoveEmptyEntries
                    );
            }

            if (string.Equals(
                line,
                "DATA ASCII",
                StringComparison.OrdinalIgnoreCase))
            {
                dataIndex = index + 1;
                break;
            }
        }

        if (fields == null ||
            dataIndex < 0)
        {
            throw new InvalidOperationException(
                "Expected an ASCII PCD file."
            );
        }

        int xIndex =
            Array.FindIndex(
                fields,
                value => value == "x"
            ) - 1;

        int yIndex =
            Array.FindIndex(
                fields,
                value => value == "y"
            ) - 1;

        int zIndex =
            Array.FindIndex(
                fields,
                value => value == "z"
            ) - 1;

        if (xIndex < 0 ||
            yIndex < 0 ||
            zIndex < 0)
        {
            throw new InvalidOperationException(
                "PCD does not contain x/y/z fields."
            );
        }

        List<Vector3> vertices =
            new List<Vector3>(
                Math.Max(
                    1024,
                    lines.Length - dataIndex
                )
            );

        List<Color32> colors =
            new List<Color32>(
                vertices.Capacity
            );

        Bounds bounds = default;
        bool hasBounds = false;

        for (int index = dataIndex;
             index < lines.Length;
             index++)
        {
            string line =
                lines[index].Trim();

            if (line.Length == 0)
                continue;

            string[] values =
                line.Split(
                    (char[])null,
                    StringSplitOptions
                        .RemoveEmptyEntries
                );

            if (values.Length <=
                Math.Max(
                    xIndex,
                    Math.Max(yIndex, zIndex)
                ))
            {
                continue;
            }

            if (!float.TryParse(
                    values[xIndex],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float x) ||
                !float.TryParse(
                    values[yIndex],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float y) ||
                !float.TryParse(
                    values[zIndex],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float z))
            {
                continue;
            }

            Vector3 point =
                LevelAndConvert(x, y, z);

            vertices.Add(point);

            float height =
                Mathf.Clamp01(
                    (point.y + 0.10f) /
                    2.60f
                );

            Color color =
                Color.HSVToRGB(
                    0.72f -
                    height * 0.72f,
                    0.88f,
                    1.0f
                );

            colors.Add(
                (Color32)color
            );

            if (!hasBounds)
            {
                bounds =
                    new Bounds(
                        point,
                        Vector3.zero
                    );

                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(point);
            }
        }

        if (vertices.Count == 0)
        {
            throw new InvalidOperationException(
                "The PCD contains no valid points."
            );
        }

        return new ParsedMap
        {
            Vertices = vertices.ToArray(),
            Colors = colors.ToArray(),
            Center = bounds.center,
            Span = Mathf.Max(
                5.0f,
                Mathf.Max(
                    bounds.size.x,
                    bounds.size.z
                )
            )
        };
    }

    private static Vector3 LevelAndConvert(
        float x,
        float y,
        float z)
    {
        Vector3 leveled =
            FloorRotation *
            new Vector3(
                x,
                y,
                z - FloorOriginZ
            );

        // ROS/map: X/Y horizontal, Z vertical.
        // Unity: X/Z horizontal, Y vertical.
        return new Vector3(
            leveled.x,
            leveled.z,
            leveled.y
        );
    }

    private static int[] SequentialIndices(
        int count)
    {
        int[] indices =
            new int[count];

        for (int index = 0;
             index < count;
             index++)
        {
            indices[index] = index;
        }

        return indices;
    }

    private void FitCamera()
    {
        if (renderCamera == null)
            return;

        float distance =
            mapSpan * 0.95f * zoom;

        float yawRadians =
            yaw * Mathf.Deg2Rad;

        float pitchRadians =
            pitch * Mathf.Deg2Rad;

        float horizontalDistance =
            distance * Mathf.Cos(pitchRadians);

        Vector3 offset =
            new Vector3(
                horizontalDistance * Mathf.Sin(yawRadians),
                distance * Mathf.Sin(pitchRadians),
                -horizontalDistance * Mathf.Cos(yawRadians)
            );

        renderCamera.transform.localPosition =
            viewTarget + offset;

        renderCamera.transform.localRotation =
            Quaternion.LookRotation(
                viewTarget -
                renderCamera.transform.localPosition,
                Vector3.up
            );

        renderCamera.farClipPlane =
            Mathf.Max(
                100.0f,
                mapSpan * 5.0f
            );
    }

    private void ApplyRotation()
    {
        if (cloudRoot != null)
            cloudRoot.localRotation =
                Quaternion.identity;

        FitCamera();
    }

    private void ResetView()
    {
        yaw = initialYaw;
        pitch = initialPitch;
        zoom = 1.0f;
        viewTarget = Vector3.zero;

        ApplyRotation();
    }

    public void OnDrag(
        PointerEventData eventData)
    {
        if (!interactionEnabled)
    return;
        if (panMode)
        {
            ApplyPan(eventData.delta);
            return;
        }

        yaw -=
            eventData.delta.x *
            rotationSensitivity;

        pitch =
            Mathf.Clamp(
                pitch -
                eventData.delta.y *
                rotationSensitivity,
                5.0f,
                85.0f
            );

        ApplyRotation();
    }

    public void ToggleDragMode()
    {
        if (!interactionEnabled)
    return;
        panMode = !panMode;
        UpdateDragModeLabel();
    }

    public void SetPanMode(
        bool enabled)
    {
        if (!interactionEnabled)
    return;
        panMode = enabled;
        UpdateDragModeLabel();
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

            if (buttonName != "SlamDragModeButton" &&
                buttonName != "ZoomInButton" &&
                buttonName != "ZoomOutButton")
            {
                continue;
            }

            HudDashboardTheme.StyleDarkGreenButton(button);
        }
    }

    private void UpdateDragModeLabel()
    {
        if (dragModeLabel != null)
        {
            dragModeLabel.text =
                panMode ? "PAN" : "ROTATE";
        }
    }

    private void ApplyPan(
        Vector2 pointerDelta)
    {
        if (renderCamera == null)
            return;

        float distance =
            mapSpan * 0.95f * zoom;

        float viewportHeight =
            targetImage != null
                ? targetImage.rectTransform.rect.height
                : textureHeight;

        viewportHeight =
            Mathf.Max(1.0f, viewportHeight);

        float worldPerPixel =
            2.0f *
            distance *
            Mathf.Tan(
                renderCamera.fieldOfView *
                0.5f *
                Mathf.Deg2Rad
            ) /
            viewportHeight;

        Vector3 cameraRight =
            Vector3.ProjectOnPlane(
                renderCamera.transform.right,
                Vector3.up
            );

        Vector3 cameraUp =
            Vector3.ProjectOnPlane(
                renderCamera.transform.up,
                Vector3.up
            );

        if (cameraRight.sqrMagnitude > 0.000001f)
            cameraRight.Normalize();

        if (cameraUp.sqrMagnitude > 0.000001f)
            cameraUp.Normalize();

        viewTarget +=
            (
                -cameraRight * pointerDelta.x -
                cameraUp * pointerDelta.y
            ) *
            worldPerPixel *
            panSensitivity;

        Vector2 planarTarget =
            Vector2.ClampMagnitude(
                new Vector2(
                    viewTarget.x,
                    viewTarget.z
                ),
                mapSpan * 1.5f
            );

        viewTarget =
            new Vector3(
                planarTarget.x,
                0.0f,
                planarTarget.y
            );

        FitCamera();
    }

    public void OnScroll(
        PointerEventData eventData)
    {
        if (!interactionEnabled)
    return;
        zoom =
            Mathf.Clamp(
                zoom *
                Mathf.Exp(
                    -eventData.scrollDelta.y *
                    zoomSensitivity
                ),
                0.35f,
                3.0f
            );

        FitCamera();
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (!interactionEnabled)
    return;
        if (eventData.clickCount >= 2)
            ResetView();
    }
}
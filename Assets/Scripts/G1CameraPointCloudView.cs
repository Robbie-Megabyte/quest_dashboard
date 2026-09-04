using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public sealed class G1CameraPointCloudView :
    MonoBehaviour,
    IDragHandler,
    IScrollHandler,
    IPointerClickHandler
{
    private const int RenderLayer = 28;
    private const int PacketHeaderSize = 32;
    private const int PacketRecordStride = 9;
    private const ushort PacketVersion = 1;

    private static int nextRuntimeSlot;

    [Header("Rendering")]
    [SerializeField]
    [Min(256)]
    private int baseTextureResolution = 768;

    [SerializeField]
    [Range(1f, 10f)]
    private float pointSize = 2.5f;

    [SerializeField]
    private Color backgroundColor =
        new Color32(6, 16, 25, 255);

    [Header("Initial View")]
    [SerializeField]
    private float initialYaw;

    [SerializeField]
    private float initialPitch;

    [Header("Interaction")]
    [SerializeField]
    private float rotationSensitivity = 0.18f;

    [SerializeField]
    private float zoomSensitivity = 0.08f;

    [SerializeField]
    private float minimumZoom = 0.45f;

    [SerializeField]
    private float maximumZoom = 3f;

    private RawImage targetImage;
    private G1CameraUdpReceiver receiver;
    private HudWindow hudWindow;

    private GameObject runtimeRoot;
    private GameObject pointObject;
    private GameObject cameraObject;

    private Camera renderCamera;
    private RenderTexture renderTexture;
    private Mesh pointMesh;
    private MeshRenderer pointRenderer;
    private Material pointMaterial;

    private readonly List<Vector3> vertices =
        new List<Vector3>(12000);

    private readonly List<Color32> colors =
        new List<Color32>(12000);

    private readonly List<int> pointIndices =
        new List<int>(12000);

    private Bounds cloudBounds;
    private Vector3 orbitTarget =
        new Vector3(0f, 0f, 2f);

    private float yaw;
    private float pitch;
    private float zoom = 1f;

    private Vector2 lastViewportSize;

    private bool runtimeReady;
    private bool subscribed;
    private bool hasCloud;
    private bool mainView;

    public Texture OutputTexture
    {
        get
        {
            return renderTexture;
        }
    }

    public void Configure(
        G1CameraUdpReceiver newReceiver,
        HudWindow newHudWindow)
    {
        if (receiver != newReceiver)
        {
            Unsubscribe();
            receiver = newReceiver;
        }

        if (newHudWindow != null)
            hudWindow = newHudWindow;

        ResolveReferences();
        EnsureRuntime();

        if (isActiveAndEnabled)
            Subscribe();

        UpdateInteractionTarget();
    }

    public void SetMainView(bool value)
    {
        if (mainView == value)
        {
            UpdateInteractionTarget();
            return;
        }

        mainView = value;
        lastViewportSize = Vector2.zero;

        if (runtimeReady)
            UpdateRenderTexture(true);

        UpdateInteractionTarget();
    }

    private void Awake()
    {
        ResolveReferences();

        yaw = initialYaw;
        pitch = initialPitch;
        zoom = 1f;
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureRuntime();
        Subscribe();
        UpdateInteractionTarget();
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (targetImage != null)
            targetImage.raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (!runtimeReady)
            return;

        UpdateRenderTexture();
        UpdateCameraPose();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (renderCamera != null)
            renderCamera.targetTexture = null;

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (pointMesh != null)
            Destroy(pointMesh);

        if (pointMaterial != null)
            Destroy(pointMaterial);

        if (runtimeRoot != null)
            Destroy(runtimeRoot);
    }

    private void ResolveReferences()
    {
        if (targetImage == null)
            targetImage = GetComponent<RawImage>();

        if (hudWindow == null)
        {
            hudWindow =
                GetComponentInParent<HudWindow>(true);
        }

        if (receiver == null)
        {
            receiver =
                G1CameraUdpReceiver.Instance;

            if (receiver == null)
            {
                receiver =
                    FindAnyObjectByType
                        <G1CameraUdpReceiver>();
            }
        }
    }

    private void Subscribe()
    {
        if (subscribed ||
            receiver == null)
        {
            return;
        }

        receiver.PointCloudPayloadUpdated +=
            HandlePointCloudPayload;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed ||
            receiver == null)
        {
            return;
        }

        receiver.PointCloudPayloadUpdated -=
            HandlePointCloudPayload;

        subscribed = false;
    }

    private void EnsureRuntime()
    {
        if (runtimeReady ||
            targetImage == null)
        {
            return;
        }

        Shader shader =
            Shader.Find("G1/PointCloud");

        if (shader == null)
        {
            Debug.LogError(
                "[G1 Camera Point Cloud] " +
                "Shader G1/PointCloud was not found.");
            return;
        }

        runtimeRoot =
            new GameObject(
                "G1_Camera_PointCloud_Runtime");

        runtimeRoot.hideFlags =
            HideFlags.DontSave;

        int slot = nextRuntimeSlot++;

        runtimeRoot.transform.position =
            new Vector3(
                slot * 100f,
                -10000f,
                10000f);

        pointObject =
            new GameObject("PointCloud");

        pointObject.layer = RenderLayer;

        pointObject.transform.SetParent(
            runtimeRoot.transform,
            false);

        MeshFilter filter =
            pointObject.AddComponent<MeshFilter>();

        pointRenderer =
            pointObject.AddComponent<MeshRenderer>();

        pointMesh =
            new Mesh
            {
                name = "G1 Camera Point Cloud",
                indexFormat = IndexFormat.UInt32
            };

        pointMesh.MarkDynamic();
        filter.sharedMesh = pointMesh;

        pointMaterial =
            new Material(shader)
            {
                name = "G1 Camera Point Cloud Material"
            };

        pointMaterial.SetColor(
            "_Tint",
            Color.white);

        pointMaterial.SetFloat(
            "_PointSize",
            pointSize);

        pointRenderer.sharedMaterial =
            pointMaterial;

        pointRenderer.shadowCastingMode =
            ShadowCastingMode.Off;

        pointRenderer.receiveShadows = false;

        pointRenderer.lightProbeUsage =
            LightProbeUsage.Off;

        pointRenderer.reflectionProbeUsage =
            ReflectionProbeUsage.Off;

        pointRenderer.enabled = false;

        cameraObject =
            new GameObject(
                "G1_Camera_PointCloud_RenderCamera");

        cameraObject.transform.SetParent(
            runtimeRoot.transform,
            false);

        renderCamera =
            cameraObject.AddComponent<Camera>();

        renderCamera.enabled = true;
        renderCamera.clearFlags =
            CameraClearFlags.SolidColor;

        renderCamera.backgroundColor =
            backgroundColor;

        renderCamera.cullingMask =
            1 << RenderLayer;

        renderCamera.fieldOfView = 54f;
        renderCamera.nearClipPlane = 0.01f;
        renderCamera.farClipPlane = 50f;
        renderCamera.allowHDR = false;
        renderCamera.allowMSAA = false;

        runtimeReady = true;

        UpdateRenderTexture(true);
        ResetView();
    }

    private void HandlePointCloudPayload(
        byte[] payload)
    {
        if (!runtimeReady)
            EnsureRuntime();

        if (!runtimeReady)
            return;

        if (!TryApplyPacket(payload))
        {
            Debug.LogWarning(
                "[G1 Camera Point Cloud] " +
                "Rejected malformed G1PC packet.");
        }
    }

    private bool TryApplyPacket(byte[] data)
    {
        if (data == null ||
            data.Length < PacketHeaderSize ||
            data[0] != (byte)'G' ||
            data[1] != (byte)'1' ||
            data[2] != (byte)'P' ||
            data[3] != (byte)'C')
        {
            return false;
        }

        ushort version =
            ReadUInt16(data, 4);

        ushort stride =
            ReadUInt16(data, 6);

        uint countValue =
            ReadUInt32(data, 8);

        uint coordinateFlags =
            ReadUInt32(data, 24);

        if (version != PacketVersion ||
            stride != PacketRecordStride ||
            countValue > 65535 ||
            coordinateFlags != 1)
        {
            return false;
        }

        int count = (int)countValue;

        long expectedLength =
            PacketHeaderSize +
            (long)count * stride;

        if (expectedLength != data.Length)
            return false;

        vertices.Clear();
        colors.Clear();

        Vector3 minimum =
            new Vector3(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity);

        Vector3 maximum =
            new Vector3(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity);

        int offset = PacketHeaderSize;

        for (int index = 0;
             index < count;
             index++)
        {
            float x =
                ReadInt16(data, offset) *
                0.001f;

            float y =
                ReadInt16(data, offset + 2) *
                0.001f;

            /*
             * G1PC uses browser coordinates with forward on -Z.
             * Convert to Unity's +Z-forward camera space.
             */
            float z =
                -ReadInt16(data, offset + 4) *
                0.001f;

            Vector3 point =
                new Vector3(x, y, z);

            vertices.Add(point);

            colors.Add(
                new Color32(
                    data[offset + 6],
                    data[offset + 7],
                    data[offset + 8],
                    255));

            minimum = Vector3.Min(minimum, point);
            maximum = Vector3.Max(maximum, point);

            offset += stride;
        }

        while (pointIndices.Count < count)
            pointIndices.Add(pointIndices.Count);

        if (pointIndices.Count > count)
        {
            pointIndices.RemoveRange(
                count,
                pointIndices.Count - count);
        }

        pointMesh.Clear(false);

        if (count == 0)
        {
            pointRenderer.enabled = false;
            return true;
        }

        pointMesh.SetVertices(vertices);
        pointMesh.SetColors(colors);

        pointMesh.SetIndices(
            pointIndices,
            MeshTopology.Points,
            0,
            false);

        Bounds nextBounds =
            new Bounds(
                (minimum + maximum) * 0.5f,
                maximum - minimum);

        Vector3 paddedSize =
            nextBounds.size +
            Vector3.one * 0.02f;

        nextBounds.size = paddedSize;

        pointMesh.bounds = nextBounds;
        cloudBounds = nextBounds;
        pointRenderer.enabled = true;

        if (!hasCloud)
        {
            hasCloud = true;
            ResetView();
        }

        return true;
    }

    private void UpdateRenderTexture(
        bool force = false)
    {
        if (targetImage == null ||
            renderCamera == null)
        {
            return;
        }

        Vector2 size =
            targetImage.rectTransform.rect.size;

        if (size.x < 1f ||
            size.y < 1f)
        {
            return;
        }

        if (!force &&
            Vector2.Distance(
                size,
                lastViewportSize) < 2f)
        {
            return;
        }

        lastViewportSize = size;

        float aspect =
            Mathf.Clamp(
                size.x / size.y,
                0.1f,
                10f);

        int requestedResolution =
            mainView
                ? baseTextureResolution
                : Mathf.Min(
                    baseTextureResolution,
                    320);

        int longestSide =
            Mathf.Clamp(
                requestedResolution,
                256,
                1536);

        int width;
        int height;

        if (aspect >= 1f)
        {
            width = longestSide;

            height =
                Mathf.Max(
                    64,
                    Mathf.RoundToInt(
                        width / aspect));
        }
        else
        {
            height = longestSide;

            width =
                Mathf.Max(
                    64,
                    Mathf.RoundToInt(
                        height * aspect));
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
                RenderTextureFormat.ARGB32)
            {
                name =
                    "G1 Camera Point Cloud RenderTexture",
                antiAliasing = 1,
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

        targetImage.color = Color.white;
    }

    private void UpdateCameraPose()
    {
        if (renderCamera == null)
            return;

        float radius =
            hasCloud
                ? Mathf.Max(
                    0.15f,
                    cloudBounds.extents.magnitude)
                : 1f;

        float verticalHalfFov =
            renderCamera.fieldOfView *
            0.5f *
            Mathf.Deg2Rad;

        float horizontalHalfFov =
            Mathf.Atan(
                Mathf.Tan(verticalHalfFov) *
                Mathf.Max(
                    0.1f,
                    renderCamera.aspect));

        float limitingHalfFov =
            Mathf.Max(
                5f * Mathf.Deg2Rad,
                Mathf.Min(
                    verticalHalfFov,
                    horizontalHalfFov));

        float distance =
            radius /
            Mathf.Sin(limitingHalfFov);

        distance *=
            1.08f *
            Mathf.Clamp(
                zoom,
                minimumZoom,
                maximumZoom);

        float yawRadians =
            yaw * Mathf.Deg2Rad;

        float pitchRadians =
            pitch * Mathf.Deg2Rad;

        float horizontalDistance =
            distance *
            Mathf.Cos(pitchRadians);

        Vector3 offset =
            new Vector3(
                horizontalDistance *
                    Mathf.Sin(yawRadians),
                distance *
                    Mathf.Sin(pitchRadians),
                -horizontalDistance *
                    Mathf.Cos(yawRadians));

        renderCamera.transform.localPosition =
            orbitTarget + offset;

        renderCamera.transform.localRotation =
            Quaternion.LookRotation(
                orbitTarget -
                    renderCamera.transform.localPosition,
                Vector3.up);

        renderCamera.nearClipPlane =
            Mathf.Max(
                0.01f,
                distance - radius * 2.5f);

        renderCamera.farClipPlane =
            distance + radius * 3f + 5f;
    }

    private void ResetView()
    {
        yaw = initialYaw;

        pitch =
            Mathf.Clamp(
                initialPitch,
                -80f,
                80f);

        zoom = 1f;

        orbitTarget =
            hasCloud
                ? cloudBounds.center
                : new Vector3(0f, 0f, 2f);

        UpdateCameraPose();
    }

    private void UpdateInteractionTarget()
    {
        if (targetImage != null)
        {
            /*
             * Only the full/main Point Cloud view intercepts input.
             * Thumbnail and split-screen point clouds remain passive.
             */
            targetImage.raycastTarget =
                mainView &&
                isActiveAndEnabled;
        }
    }

    public void OnDrag(
        PointerEventData eventData)
    {
        if (!mainView ||
            eventData == null)
        {
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
                -80f,
                80f);

        UpdateCameraPose();
    }

    public void OnScroll(
        PointerEventData eventData)
    {
        if (!mainView ||
            eventData == null)
        {
            return;
        }

        zoom =
            Mathf.Clamp(
                zoom *
                Mathf.Exp(
                    -eventData.scrollDelta.y *
                    zoomSensitivity),
                minimumZoom,
                maximumZoom);

        UpdateCameraPose();
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (mainView &&
            eventData != null &&
            eventData.clickCount >= 2)
        {
            ResetView();
        }
    }

    private static ushort ReadUInt16(
        byte[] data,
        int offset)
    {
        return
            (ushort)(
                data[offset] |
                data[offset + 1] << 8);
    }

    private static short ReadInt16(
        byte[] data,
        int offset)
    {
        return
            unchecked(
                (short)ReadUInt16(
                    data,
                    offset));
    }

    private static uint ReadUInt32(
        byte[] data,
        int offset)
    {
        return
            (uint)(
                data[offset] |
                data[offset + 1] << 8 |
                data[offset + 2] << 16 |
                data[offset + 3] << 24);
    }
}

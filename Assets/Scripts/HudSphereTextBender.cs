using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class HudSphereTextBender : MonoBehaviour
{
    [Header("Window")]
    public HudWindow hudWindow;

    [Header("Depth")]
    [Tooltip("Small offset toward the user's eyes.")]
    public float surfaceOffsetTowardUser = 0.001f;

    private TextMeshProUGUI textComponent;

    private float lastWindowWidth = -1f;
    private float lastWindowHeight = -1f;
    private Vector2 lastRectSize;

    private Vector3 lastRectOriginInWindow;
    private Vector3 lastRectRightInWindow;
    private Vector3 lastRectUpInWindow;

    private bool hasCachedTransform;
    private bool refreshNextFrame;

    private void Awake()
    {
        ResolveReferences();
        CacheGeometry();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (textComponent != null)
            textComponent.OnPreRenderText += BendText;

        if (hudWindow != null)
            hudWindow.SizeChanged += HandleWindowSizeChanged;

        CacheGeometry();
        MarkDirty();
    }

    private void OnDisable()
    {
        if (textComponent != null)
            textComponent.OnPreRenderText -= BendText;

        if (hudWindow != null)
            hudWindow.SizeChanged -= HandleWindowSizeChanged;
    }

    private void LateUpdate()
    {
        if (textComponent == null ||
            hudWindow == null)
        {
            return;
        }

        RectTransform rect =
            textComponent.rectTransform;

        Vector2 rectSize =
            rect.rect.size;

        Vector3 rectOriginInWindow =
            hudWindow.transform.InverseTransformPoint(
                rect.position);

        Vector3 rectRightInWindow =
            hudWindow.transform.InverseTransformVector(
                rect.TransformVector(Vector3.right));

        Vector3 rectUpInWindow =
            hudWindow.transform.InverseTransformVector(
                rect.TransformVector(Vector3.up));

        const float transformEpsilonSquared =
            0.0000000001f;

        bool transformChanged =
            !hasCachedTransform ||
            (
                rectOriginInWindow -
                lastRectOriginInWindow
            ).sqrMagnitude >
            transformEpsilonSquared ||
            (
                rectRightInWindow -
                lastRectRightInWindow
            ).sqrMagnitude >
            transformEpsilonSquared ||
            (
                rectUpInWindow -
                lastRectUpInWindow
            ).sqrMagnitude >
            transformEpsilonSquared;

        bool changed =
            !Mathf.Approximately(
                lastWindowWidth,
                hudWindow.WidthMeters) ||
            !Mathf.Approximately(
                lastWindowHeight,
                hudWindow.HeightMeters) ||
            rectSize != lastRectSize ||
            transformChanged;

        if (changed)
        {
            CacheGeometry();
            MarkDirty();
        }

        if (refreshNextFrame)
        {
            refreshNextFrame = false;
            MarkDirty();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled)
            return;

        ResolveReferences();
        MarkDirty();
    }

    private void ResolveReferences()
    {
        if (textComponent == null)
            textComponent = GetComponent<TextMeshProUGUI>();

        if (hudWindow == null)
            hudWindow = GetComponentInParent<HudWindow>(true);
    }

    private void HandleWindowSizeChanged(HudWindow window)
    {
        CacheGeometry();
        MarkDirty();
        refreshNextFrame = true;
    }

    private void CacheGeometry()
    {
        hasCachedTransform = false;

        if (hudWindow != null)
        {
            lastWindowWidth =
                hudWindow.WidthMeters;

            lastWindowHeight =
                hudWindow.HeightMeters;
        }

        if (textComponent == null)
            return;

        RectTransform rect =
            textComponent.rectTransform;

        lastRectSize =
            rect.rect.size;

        if (hudWindow == null)
            return;

        lastRectOriginInWindow =
            hudWindow.transform.InverseTransformPoint(
                rect.position);

        lastRectRightInWindow =
            hudWindow.transform.InverseTransformVector(
                rect.TransformVector(Vector3.right));

        lastRectUpInWindow =
            hudWindow.transform.InverseTransformVector(
                rect.TransformVector(Vector3.up));

        hasCachedTransform = true;
    }

    private void MarkDirty()
    {
        if (textComponent != null)
            textComponent.SetVerticesDirty();
    }

    private void BendText(TMP_TextInfo textInfo)
    {
        if (!isActiveAndEnabled ||
            textComponent == null ||
            hudWindow == null)
        {
            return;
        }

        RectTransform rect =
            textComponent.rectTransform;

        float width =
            Mathf.Max(0.001f, hudWindow.WidthMeters);

        float height =
            Mathf.Max(0.001f, hudWindow.HeightMeters);

        for (int i = 0;
             i < textInfo.characterCount;
             i++)
        {
            TMP_CharacterInfo character =
                textInfo.characterInfo[i];

            if (!character.isVisible)
                continue;

            int materialIndex =
                character.materialReferenceIndex;

            int vertexIndex =
                character.vertexIndex;

            Vector3[] vertices =
                textInfo.meshInfo[materialIndex].vertices;

            if (vertexIndex < 0 ||
                vertexIndex + 3 >= vertices.Length)
            {
                continue;
            }

            for (int corner = 0;
                 corner < 4;
                 corner++)
            {
                int index = vertexIndex + corner;

                Vector3 flatWorld =
                    rect.TransformPoint(vertices[index]);

                Vector3 windowLocal =
                    hudWindow.transform.InverseTransformPoint(
                        flatWorld);

                Pose surfacePose =
                    HudSphereGeometry.GetSurfacePose(
                        hudWindow,
                        windowLocal.x / width,
                        windowLocal.y / height,
                        surfaceOffsetTowardUser);

                vertices[index] =
                    rect.InverseTransformPoint(
                        surfacePose.position);
            }
        }
    }
}
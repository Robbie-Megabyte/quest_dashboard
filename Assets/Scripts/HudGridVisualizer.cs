using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HudGridVisualizer : MonoBehaviour
{
    private enum PreviewMode
    {
        Hidden,
        Move,
        Resize
    }


    [Header("References")]
    public HudGridManager gridManager;
    public Transform hudRoot;


    [Header("Grid Appearance")]
    public Color gridColor =
        new Color(
            0.20f,
            0.75f,
            1.00f,
            0.28f
        );

    public float gridLineWidth =
        0.003f;


    [Header("Drop Preview")]
    public Color previewColor =
        new Color(
            0.15f,
            1.00f,
            0.80f,
            0.95f
        );

    public float previewLineWidth =
        0.009f;


    [Header("Sphere")]
    public float surfaceOffsetTowardUser =
        0.015f;


    [Header("Curve Quality")]
    [Range(4, 64)]
    public int curveSegments =
        24;


    private readonly List<LineRenderer>
        gridLines =
            new List<LineRenderer>();


    private LineRenderer previewLine;

    private Material lineMaterial;


    private HudGridWindowController
        activeWindow;


    private PreviewMode previewMode =
        PreviewMode.Hidden;


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

        CreateMaterial();

        BuildGrid();

        CreatePreview();

        HideEverything();
    }


    void LateUpdate()
    {
        switch (previewMode)
        {
            case PreviewMode.Move:

                if (activeWindow != null)
                {
                    UpdateMoveDropPreview();
                }

                break;


            case PreviewMode.Resize:
                UpdateResizePreview();
            break;
        }
    }


    void OnDestroy()
    {
        if (lineMaterial != null)
        {
            Destroy(
                lineMaterial
            );
        }
    }


    // ========================================================
    // REFERENCES
    // ========================================================

    private void ResolveReferences()
    {
        if (gridManager == null)
        {
            gridManager =
                Object.FindAnyObjectByType
                <HudGridManager>();
        }


        if (hudRoot == null)
        {
            if (gridManager != null)
            {
                hudRoot =
                    gridManager.transform;
            }
            else
            {
                hudRoot =
                    transform.parent;
            }
        }
    }


    // ========================================================
    // MATERIAL
    // ========================================================

    private void CreateMaterial()
    {
        Shader shader =
            Shader.Find(
                "Sprites/Default"
            );


        if (shader == null)
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
                    "Unlit/Color"
                );
        }


        if (shader == null)
        {
            Debug.LogError(
                "HudGridVisualizer: no line shader found."
            );

            return;
        }


        lineMaterial =
            new Material(
                shader
            );
    }


    // ========================================================
    // PUBLIC PREVIEW API
    // ========================================================

    public void BeginWindowPreview(
        HudGridWindowController window)
    {
        activeWindow =
            window;


        previewMode =
            PreviewMode.Move;


        SetGridVisible(
            true
        );


        UpdateMoveDropPreview();
    }


    public void EndWindowPreview(
        HudGridWindowController window)
    {
        if (previewMode !=
            PreviewMode.Move)
        {
            return;
        }


        if (activeWindow !=
            window)
        {
            return;
        }


        HideEverything();
    }


    public void BeginResizePreview(
        HudGridWindowController window)
    {
        activeWindow =
            window;


        previewMode =
            PreviewMode.Resize;


        SetGridVisible(
            true
        );


        UpdateResizePreview();
    }


    public void EndResizePreview(
        HudGridWindowController window)
    {
        if (previewMode !=
            PreviewMode.Resize)
        {
            return;
        }


        if (activeWindow !=
            window)
        {
            return;
        }


        HideEverything();
    }


    public void CancelPreview()
    {
        HideEverything();
    }


    // ========================================================
    // GRID CREATION
    // ========================================================

    private void BuildGrid()
    {
        if (gridManager == null ||
            hudRoot == null)
        {
            return;
        }


        ClearGrid();


        for (
            int boundary = 0;
            boundary <= gridManager.columns;
            boundary++)
        {
            LineRenderer line =
                CreateLineRenderer(
                    $"Grid_V_{boundary}",
                    gridColor,
                    gridLineWidth
                );


            BuildVerticalLine(
                line,
                boundary
            );


            gridLines.Add(
                line
            );
        }


        for (
            int boundary = 0;
            boundary <= gridManager.rows;
            boundary++)
        {
            LineRenderer line =
                CreateLineRenderer(
                    $"Grid_H_{boundary}",
                    gridColor,
                    gridLineWidth
                );


            BuildHorizontalLine(
                line,
                boundary
            );


            gridLines.Add(
                line
            );
        }
    }


    private void ClearGrid()
    {
        foreach (
            LineRenderer line
            in gridLines)
        {
            if (line != null)
            {
                Destroy(
                    line.gameObject
                );
            }
        }


        gridLines.Clear();
    }


    private LineRenderer CreateLineRenderer(
        string objectName,
        Color color,
        float width)
    {
        GameObject lineObject =
            new GameObject(
                objectName
            );


        lineObject.transform.SetParent(
            transform,
            false
        );


        LineRenderer line =
            lineObject.AddComponent
            <LineRenderer>();


        line.useWorldSpace =
            false;

        line.loop =
            false;

        line.startWidth =
            width;

        line.endWidth =
            width;

        line.startColor =
            color;

        line.endColor =
            color;

        line.numCapVertices =
            2;

        line.numCornerVertices =
            2;


        if (lineMaterial != null)
        {
            line.material =
                lineMaterial;
        }


        return line;
    }


    // ========================================================
    // GRID LINES
    // ========================================================

    private void BuildVerticalLine(
        LineRenderer line,
        int boundary)
    {
        float yaw =
            GetColumnBoundaryYaw(
                boundary
            );


        float topPitch =
            GetRowBoundaryPitch(
                0
            );


        float bottomPitch =
            GetRowBoundaryPitch(
                gridManager.rows
            );


        line.positionCount =
            curveSegments + 1;


        for (
            int i = 0;
            i <= curveSegments;
            i++)
        {
            float t =
                i /
                (float)curveSegments;


            float pitch =
                Mathf.Lerp(
                    topPitch,
                    bottomPitch,
                    t
                );


            line.SetPosition(
                i,
                GetLocalSpherePoint(
                    yaw,
                    pitch
                )
            );
        }
    }


    private void BuildHorizontalLine(
        LineRenderer line,
        int boundary)
    {
        float pitch =
            GetRowBoundaryPitch(
                boundary
            );


        float leftYaw =
            GetColumnBoundaryYaw(
                0
            );


        float rightYaw =
            GetColumnBoundaryYaw(
                gridManager.columns
            );


        line.positionCount =
            curveSegments + 1;


        for (
            int i = 0;
            i <= curveSegments;
            i++)
        {
            float t =
                i /
                (float)curveSegments;


            float yaw =
                Mathf.Lerp(
                    leftYaw,
                    rightYaw,
                    t
                );


            line.SetPosition(
                i,
                GetLocalSpherePoint(
                    yaw,
                    pitch
                )
            );
        }
    }


    // ========================================================
    // MOVE DROP PREVIEW
    // ========================================================

    private void CreatePreview()
    {
        previewLine =
            CreateLineRenderer(
                "Grid_DropPreview",
                previewColor,
                previewLineWidth
            );


        previewLine.enabled =
            false;
    }

    private void UpdateResizePreview()
    {
        if (activeWindow == null || !activeWindow.IsGridPlaced)
        {
            HidePreview();
            return;
        }

        DrawPreviewRectangle(
            activeWindow.CurrentColumn,
            activeWindow.CurrentRow,
            activeWindow.ColumnSpan,
            activeWindow.RowSpan
        );
    }


    private void UpdateMoveDropPreview()
    {
        if (activeWindow == null ||
            gridManager == null)
        {
            HidePreview();

            return;
        }


        Vector2 currentAngles =
            gridManager.WorldPointToVisorAngles(
                activeWindow.transform.position
            );


        int columnSpan =
            activeWindow.ColumnSpan;


        int rowSpan =
            activeWindow.RowSpan;


        int lastColumn =
            gridManager.columns -
            columnSpan;


        int lastRow =
            gridManager.rows -
            rowSpan;


        if (lastColumn < 0 ||
            lastRow < 0)
        {
            HidePreview();

            return;
        }


        bool found =
            false;


        int bestColumn =
            0;

        int bestRow =
            0;


        float bestDistance =
            float.MaxValue;


        for (
            int row = 0;
            row <= lastRow;
            row++)
        {
            for (
                int column = 0;
                column <= lastColumn;
                column++)
            {
                if (!gridManager.CanOccupy(
                        activeWindow,
                        column,
                        row,
                        columnSpan,
                        rowSpan))
                {
                    continue;
                }


                Vector2 center =
                    GetPlacementCenterAngles(
                        column,
                        row,
                        columnSpan,
                        rowSpan
                    );


                float distance =
                    Vector2.Distance(
                        currentAngles,
                        center
                    );


                if (distance >=
                    bestDistance)
                {
                    continue;
                }


                bestDistance =
                    distance;


                bestColumn =
                    column;

                bestRow =
                    row;


                found =
                    true;
            }
        }


        if (!found ||
            bestDistance >
            gridManager.maximumDropDistanceDegrees)
        {
            HidePreview();

            return;
        }


        DrawPreviewRectangle(
            bestColumn,
            bestRow,
            columnSpan,
            rowSpan
        );
    }


    private void DrawPreviewRectangle(
        int column,
        int row,
        int columnSpan,
        int rowSpan)
    {
        if (previewLine == null)
            return;


        float leftYaw =
            GetColumnBoundaryYaw(
                column
            );


        float rightYaw =
            GetColumnBoundaryYaw(
                column +
                columnSpan
            );


        float topPitch =
            GetRowBoundaryPitch(
                row
            );


        float bottomPitch =
            GetRowBoundaryPitch(
                row +
                rowSpan
            );


        int segments =
            Mathf.Max(
                4,
                curveSegments / 2
            );


        int pointsPerEdge =
            segments + 1;


        previewLine.positionCount =
            pointsPerEdge * 4;


        int index =
            0;


        for (
            int i = 0;
            i <= segments;
            i++)
        {
            float t =
                i /
                (float)segments;


            previewLine.SetPosition(
                index++,
                GetLocalSpherePoint(
                    Mathf.Lerp(
                        leftYaw,
                        rightYaw,
                        t
                    ),
                    topPitch
                )
            );
        }


        for (
            int i = 0;
            i <= segments;
            i++)
        {
            float t =
                i /
                (float)segments;


            previewLine.SetPosition(
                index++,
                GetLocalSpherePoint(
                    rightYaw,
                    Mathf.Lerp(
                        topPitch,
                        bottomPitch,
                        t
                    )
                )
            );
        }


        for (
            int i = 0;
            i <= segments;
            i++)
        {
            float t =
                i /
                (float)segments;


            previewLine.SetPosition(
                index++,
                GetLocalSpherePoint(
                    Mathf.Lerp(
                        rightYaw,
                        leftYaw,
                        t
                    ),
                    bottomPitch
                )
            );
        }


        for (
            int i = 0;
            i <= segments;
            i++)
        {
            float t =
                i /
                (float)segments;


            previewLine.SetPosition(
                index++,
                GetLocalSpherePoint(
                    leftYaw,
                    Mathf.Lerp(
                        bottomPitch,
                        topPitch,
                        t
                    )
                )
            );
        }


        previewLine.enabled =
            true;
    }


    // ========================================================
    // VISIBILITY
    // ========================================================

    private void HideEverything()
{
    activeWindow = null;
    previewMode = PreviewMode.Hidden;

    HidePreview();

    // The visualizer object itself is disabled in Live mode.
    // While active in Edit mode, the grid remains visible.
    SetGridVisible(true);
}


    private void SetGridVisible(
        bool visible)
    {
        foreach (
            LineRenderer line
            in gridLines)
        {
            if (line != null)
            {
                line.enabled =
                    visible;
            }
        }


        if (!visible)
        {
            HidePreview();
        }
    }


    private void HidePreview()
    {
        if (previewLine != null)
        {
            previewLine.enabled =
                false;
        }
    }


    // ========================================================
    // GRID MATH
    // ========================================================

    private float GetColumnBoundaryYaw(
        int boundary)
    {
        float cellWidth =
            gridManager.horizontalSpanDegrees /
            gridManager.columns;


        return
            gridManager.gridCenterYawDegrees
            -
            gridManager.horizontalSpanDegrees *
            0.5f
            +
            boundary *
            cellWidth;
    }


    private float GetRowBoundaryPitch(
        int boundary)
    {
        float cellHeight =
            gridManager.verticalSpanDegrees /
            gridManager.rows;


        return
            gridManager.gridCenterPitchDegrees
            +
            gridManager.verticalSpanDegrees *
            0.5f
            -
            boundary *
            cellHeight;
    }


    private Vector2 GetPlacementCenterAngles(
        int column,
        int row,
        int columnSpan,
        int rowSpan)
    {
        float leftYaw =
            GetColumnBoundaryYaw(
                column
            );


        float rightYaw =
            GetColumnBoundaryYaw(
                column +
                columnSpan
            );


        float topPitch =
            GetRowBoundaryPitch(
                row
            );


        float bottomPitch =
            GetRowBoundaryPitch(
                row +
                rowSpan
            );


        return new Vector2(
            (
                leftYaw +
                rightYaw
            ) *
            0.5f,

            (
                topPitch +
                bottomPitch
            ) *
            0.5f
        );
    }


    // ========================================================
    // SPHERE MATH
    // ========================================================

    private Vector3 GetLocalSpherePoint(
        float yawDegrees,
        float pitchDegrees)
    {
        float yaw =
            yawDegrees *
            Mathf.Deg2Rad;


        float pitch =
            pitchDegrees *
            Mathf.Deg2Rad;


        float cosPitch =
            Mathf.Cos(
                pitch
            );


        Vector3 direction =
            new Vector3(
                Mathf.Sin(yaw) *
                cosPitch,

                Mathf.Sin(pitch),

                Mathf.Cos(yaw) *
                cosPitch
            );


        direction.Normalize();


        float drawingRadius =
            Mathf.Max(
                0.01f,
                gridManager.sphereRadius
                -
                surfaceOffsetTowardUser
            );


        Vector3 sphereCenterLocal =
            new Vector3(
                0.0f,
                0.0f,
                -gridManager.sphereRadius
            );


        Vector3 hudLocalPoint =
            sphereCenterLocal
            +
            direction *
            drawingRadius;


        Vector3 worldPoint =
            hudRoot.TransformPoint(
                hudLocalPoint
            );


        return
            transform.InverseTransformPoint(
                worldPoint
            );
    }
}
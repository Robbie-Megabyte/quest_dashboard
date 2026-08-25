using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HudSphereGraphicBender :
    BaseMeshEffect
{
    [Header("Window")]
    public HudWindow hudWindow;


    [Header("Curvature")]
    [Range(1, 8)]
    public int subdivisions = 5;


    [Tooltip(
        "Small offset toward the user's eyes."
    )]
    public float surfaceOffsetTowardUser =
        0.0f;


    private float lastWindowWidth =
        -1.0f;

    private float lastWindowHeight =
        -1.0f;

    private Vector2 lastRectSize =
        Vector2.zero;

    private bool subscribed =
        false;

    private bool forceSecondRefresh =
        false;


    // ========================================================
    // UNITY
    // ========================================================

    protected override void Awake()
    {
        base.Awake();

        ResolveReferences();
        CacheGeometryState();
    }


    protected override void OnEnable()
    {
        base.OnEnable();

        ResolveReferences();
        Subscribe();

        MarkDirty();
    }


    protected override void OnDisable()
    {
        Unsubscribe();

        base.OnDisable();
    }


    void LateUpdate()
    {
        if (hudWindow == null ||
            graphic == null)
        {
            return;
        }


        Vector2 currentRectSize =
            graphic.rectTransform.rect.size;


        bool changed =
            !Mathf.Approximately(
                lastWindowWidth,
                hudWindow.WidthMeters
            )
            ||
            !Mathf.Approximately(
                lastWindowHeight,
                hudWindow.HeightMeters
            )
            ||
            currentRectSize !=
            lastRectSize;


        if (changed)
        {
            CacheGeometryState();

            MarkDirty();
        }
        else if (forceSecondRefresh)
        {
            /*
             * Some UI geometry changes settle one frame after
             * HudWindow updates its RectTransforms.
             *
             * This second refresh prevents stale Header meshes
             * after very large resize operations.
             */
            forceSecondRefresh =
                false;

            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }
    }


    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();

        MarkDirty();
    }


    // ========================================================
    // REFERENCES / EVENTS
    // ========================================================

    private void ResolveReferences()
    {
        if (hudWindow == null)
        {
            hudWindow =
                GetComponentInParent
                <HudWindow>(true);
        }
    }


    private void Subscribe()
    {
        if (subscribed ||
            hudWindow == null)
        {
            return;
        }


        hudWindow.SizeChanged +=
            HandleWindowSizeChanged;


        subscribed =
            true;
    }


    private void Unsubscribe()
    {
        if (!subscribed ||
            hudWindow == null)
        {
            return;
        }


        hudWindow.SizeChanged -=
            HandleWindowSizeChanged;


        subscribed =
            false;
    }


    private void HandleWindowSizeChanged(
        HudWindow window)
    {
        CacheGeometryState();

        MarkDirty();

        /*
         * Force another rebuild next frame as well.
         */
        forceSecondRefresh =
            true;
    }


    private void CacheGeometryState()
    {
        if (hudWindow != null)
        {
            lastWindowWidth =
                hudWindow.WidthMeters;

            lastWindowHeight =
                hudWindow.HeightMeters;
        }


        if (graphic != null)
        {
            lastRectSize =
                graphic.rectTransform.rect.size;
        }
    }


    private void MarkDirty()
    {
        if (graphic != null)
        {
            graphic.SetVerticesDirty();
        }
    }


    // ========================================================
    // BEND MESH
    // ========================================================

    public override void ModifyMesh(
        VertexHelper vertexHelper)
    {
        if (!IsActive() ||
            hudWindow == null ||
            vertexHelper == null ||
            vertexHelper.currentVertCount == 0)
        {
            return;
        }


        List<UIVertex> source =
            new List<UIVertex>();


        vertexHelper.GetUIVertexStream(
            source
        );


        if (source.Count < 3)
            return;


        List<UIVertex> output =
            new List<UIVertex>();


        int divisionCount =
            Mathf.Max(
                1,
                subdivisions
            );


        for (
            int triangle = 0;
            triangle + 2 < source.Count;
            triangle += 3)
        {
            UIVertex a =
                source[triangle];

            UIVertex b =
                source[triangle + 1];

            UIVertex c =
                source[triangle + 2];


            SubdivideTriangle(
                a,
                b,
                c,
                divisionCount,
                output
            );
        }


        vertexHelper.Clear();


        vertexHelper.AddUIVertexTriangleStream(
            output
        );
    }


    private void SubdivideTriangle(
        UIVertex a,
        UIVertex b,
        UIVertex c,
        int divisions,
        List<UIVertex> output)
    {
        for (
            int i = 0;
            i < divisions;
            i++)
        {
            for (
                int j = 0;
                j < divisions - i;
                j++)
            {
                float u0 =
                    i /
                    (float)divisions;

                float v0 =
                    j /
                    (float)divisions;


                float u1 =
                    (i + 1) /
                    (float)divisions;

                float v1 =
                    j /
                    (float)divisions;


                float u2 =
                    i /
                    (float)divisions;

                float v2 =
                    (j + 1) /
                    (float)divisions;


                UIVertex p0 =
                    SampleTriangle(
                        a,
                        b,
                        c,
                        u0,
                        v0
                    );


                UIVertex p1 =
                    SampleTriangle(
                        a,
                        b,
                        c,
                        u1,
                        v1
                    );


                UIVertex p2 =
                    SampleTriangle(
                        a,
                        b,
                        c,
                        u2,
                        v2
                    );


                BendVertex(
                    ref p0
                );

                BendVertex(
                    ref p1
                );

                BendVertex(
                    ref p2
                );


                output.Add(
                    p0
                );

                output.Add(
                    p1
                );

                output.Add(
                    p2
                );


                if (
                    j <
                    divisions -
                    i -
                    1)
                {
                    float u3 =
                        (i + 1) /
                        (float)divisions;

                    float v3 =
                        (j + 1) /
                        (float)divisions;


                    UIVertex p3 =
                        SampleTriangle(
                            a,
                            b,
                            c,
                            u3,
                            v3
                        );


                    BendVertex(
                        ref p3
                    );


                    output.Add(
                        p1
                    );

                    output.Add(
                        p3
                    );

                    output.Add(
                        p2
                    );
                }
            }
        }
    }


    private UIVertex SampleTriangle(
        UIVertex a,
        UIVertex b,
        UIVertex c,
        float u,
        float v)
    {
        float w =
            1.0f -
            u -
            v;


        UIVertex result =
            UIVertex.simpleVert;


        result.position =
            a.position * w +
            b.position * u +
            c.position * v;


        result.normal =
            (
                a.normal * w +
                b.normal * u +
                c.normal * v
            ).normalized;


        result.tangent =
            a.tangent * w +
            b.tangent * u +
            c.tangent * v;


        result.uv0 =
            a.uv0 * w +
            b.uv0 * u +
            c.uv0 * v;


        result.uv1 =
            a.uv1 * w +
            b.uv1 * u +
            c.uv1 * v;


        result.uv2 =
            a.uv2 * w +
            b.uv2 * u +
            c.uv2 * v;


        result.uv3 =
            a.uv3 * w +
            b.uv3 * u +
            c.uv3 * v;


        Color resultColor =
            (Color)a.color * w +
            (Color)b.color * u +
            (Color)c.color * v;


        result.color =
            resultColor;


        return result;
    }


    private void BendVertex(
        ref UIVertex vertex)
    {
        if (graphic == null ||
            hudWindow == null)
        {
            return;
        }


        RectTransform rect =
            graphic.rectTransform;


        Vector3 flatWorldPosition =
            rect.TransformPoint(
                vertex.position
            );


        Vector3 windowLocal =
            hudWindow.transform
                .InverseTransformPoint(
                    flatWorldPosition
                );


        float width =
            Mathf.Max(
                0.001f,
                hudWindow.WidthMeters
            );


        float height =
            Mathf.Max(
                0.001f,
                hudWindow.HeightMeters
            );


        float normalizedX =
            windowLocal.x /
            width;


        float normalizedY =
            windowLocal.y /
            height;


        Pose surfacePose =
            HudSphereGeometry
                .GetSurfacePose(
                    hudWindow,
                    normalizedX,
                    normalizedY,
                    surfaceOffsetTowardUser
                );


        vertex.position =
            rect.InverseTransformPoint(
                surfacePose.position
            );


        Vector3 worldNormal =
            surfacePose.rotation *
            Vector3.back;


        vertex.normal =
            rect.InverseTransformDirection(
                worldNormal
            ).normalized;
    }
}
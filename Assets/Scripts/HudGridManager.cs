using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HudGridManager : MonoBehaviour
{
    public enum ResizeEdge
    {
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }


    // ========================================================
    // INSPECTOR
    // ========================================================

    [Header("Grid Size")]
    [Min(1)]
    public int columns = 6;

    [Min(1)]
    public int rows = 5;


    [Header("Visor Angular Size")]
    public float horizontalSpanDegrees = 60.0f;
    public float verticalSpanDegrees = 50.0f;


    [Header("Grid Center")]
    public float gridCenterYawDegrees = 0.0f;
    public float gridCenterPitchDegrees = -5.0f;


    [Header("HUD Geometry")]
    public float sphereRadius = 1.6f;
    public float gutterDegrees = 1.5f;


    [Header("Dragging")]
    public float maximumDropDistanceDegrees = 18.0f;


    [Header("Shared Resizing")]
    public bool enableSharedBoundaryResize = true;


    [Header("Diagnostics")]
    public bool verboseLogging = true;


    // ========================================================
    // INTERNAL TYPES
    // ========================================================

    private struct GridPlacement
    {
        public int column;
        public int row;

        public int columnSpan;
        public int rowSpan;

        public bool valid;


        public GridPlacement(
            int column,
            int row,
            int columnSpan,
            int rowSpan,
            bool valid = true)
        {
            this.column = column;
            this.row = row;

            this.columnSpan = columnSpan;
            this.rowSpan = rowSpan;

            this.valid = valid;
        }
    }


    private struct PlannedPlacement
    {
        public HudGridWindowController window;

        public int column;
        public int row;

        public int columnSpan;
        public int rowSpan;


        public PlannedPlacement(
            HudGridWindowController window,
            int column,
            int row,
            int columnSpan,
            int rowSpan)
        {
            this.window = window;

            this.column = column;
            this.row = row;

            this.columnSpan = columnSpan;
            this.rowSpan = rowSpan;
        }


        public int Left
        {
            get { return column; }
        }

        public int Right
        {
            get { return column + columnSpan; }
        }

        public int Top
        {
            get { return row; }
        }

        public int Bottom
        {
            get { return row + rowSpan; }
        }
    }


    // ========================================================
    // RUNTIME STATE
    // ========================================================

    private HudGridWindowController[,] occupancy;


    private readonly HashSet<HudGridWindowController>
        registeredWindows =
            new HashSet<HudGridWindowController>();


    private readonly Dictionary
        <HudGridWindowController, GridPlacement>
        dragOrigins =
            new Dictionary
            <HudGridWindowController, GridPlacement>();


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        RebuildOccupancyGrid();
    }


    private void RebuildOccupancyGrid()
    {
        columns =
            Mathf.Max(
                1,
                columns
            );


        rows =
            Mathf.Max(
                1,
                rows
            );


        occupancy =
            new HudGridWindowController[
                columns,
                rows
            ];
    }


    // ========================================================
    // REGISTRATION
    // ========================================================

    public void RegisterWindow(
        HudGridWindowController window)
    {
        if (window == null)
            return;


        if (registeredWindows.Contains(window))
            return;


        registeredWindows.Add(
            window
        );


        int width =
            Mathf.Clamp(
                window.ColumnSpan,
                1,
                columns
            );


        int height =
            Mathf.Clamp(
                window.RowSpan,
                1,
                rows
            );


        int requestedColumn =
            Mathf.Clamp(
                window.StartColumn,
                0,
                Mathf.Max(
                    0,
                    columns - width
                )
            );


        int requestedRow =
            Mathf.Clamp(
                window.StartRow,
                0,
                Mathf.Max(
                    0,
                    rows - height
                )
            );


        if (IsAreaFree(
                requestedColumn,
                requestedRow,
                width,
                height,
                window))
        {
            PlaceWindow(
                window,
                requestedColumn,
                requestedRow,
                width,
                height
            );

            return;
        }


        Vector2 requestedAngles =
            GetPlacementCenterAngles(
                requestedColumn,
                requestedRow,
                width,
                height
            );


        if (FindNearestFreePlacement(
                window,
                requestedAngles,
                width,
                height,
                out GridPlacement fallback,
                false))
        {
            PlaceWindow(
                window,
                fallback.column,
                fallback.row,
                fallback.columnSpan,
                fallback.rowSpan
            );

            return;
        }


        Debug.LogError(
            $"HUD GRID: no free startup placement exists " +
            $"for {window.name}."
        );
    }


    public void UnregisterWindow(
        HudGridWindowController window)
    {
        if (window == null)
            return;


        ReleaseCells(
            window
        );


        registeredWindows.Remove(
            window
        );


        dragOrigins.Remove(
            window
        );
    }


    // ========================================================
    // WINDOW MOVEMENT
    // ========================================================

    public void BeginWindowMove(
        HudGridWindowController window)
    {
        if (window == null)
            return;


        if (window.IsGridPlaced)
        {
            dragOrigins[window] =
                new GridPlacement(
                    window.CurrentColumn,
                    window.CurrentRow,
                    window.ColumnSpan,
                    window.RowSpan
                );
        }


        ReleaseCells(
            window
        );


        window.SetGridPlaced(
            false
        );


        if (window.HudWindow != null &&
            window.HudWindow.sphereConstraint != null)
        {
            window.HudWindow
                .sphereConstraint
                .ReleaseLayoutControl();
        }


        if (verboseLogging)
        {
            Debug.Log(
                $"HUD GRID: moving {window.name}"
            );
        }
    }


    public void EndWindowMove(
        HudGridWindowController window)
    {
        if (window == null)
            return;


        Vector2 releasedAngles =
            GetCurrentWindowAngles(
                window
            );


        int width =
            Mathf.Clamp(
                window.ColumnSpan,
                1,
                columns
            );


        int height =
            Mathf.Clamp(
                window.RowSpan,
                1,
                rows
            );


        if (FindNearestFreePlacement(
                window,
                releasedAngles,
                width,
                height,
                out GridPlacement placement,
                true))
        {
            PlaceWindow(
                window,
                placement.column,
                placement.row,
                placement.columnSpan,
                placement.rowSpan
            );


            dragOrigins.Remove(
                window
            );

            return;
        }


        if (dragOrigins.TryGetValue(
                window,
                out GridPlacement origin)
            &&
            origin.valid
            &&
            IsAreaFree(
                origin.column,
                origin.row,
                origin.columnSpan,
                origin.rowSpan,
                window))
        {
            PlaceWindow(
                window,
                origin.column,
                origin.row,
                origin.columnSpan,
                origin.rowSpan
            );
        }
        else
        {
            if (FindNearestFreePlacement(
                    window,
                    releasedAngles,
                    width,
                    height,
                    out GridPlacement emergency,
                    false))
            {
                PlaceWindow(
                    window,
                    emergency.column,
                    emergency.row,
                    emergency.columnSpan,
                    emergency.rowSpan
                );
            }
        }


        dragOrigins.Remove(
            window
        );
    }


    // ========================================================
    // RESIZE ENTRY
    // ========================================================

    public bool ResizeWindowFromWorldPoint(
        HudGridWindowController window,
        ResizeEdge edge,
        Vector3 releasedWorldPoint)
    {
        if (window == null ||
            !window.IsGridPlaced)
        {
            return false;
        }


        if (IsCornerEdge(edge))
        {
            return ResizeCornerSmart(
                window,
                edge,
                releasedWorldPoint
            );
        }


        return ResizeCardinalEdge(
            window,
            edge,
            releasedWorldPoint
        );
    }


    private bool IsCornerEdge(
        ResizeEdge edge)
    {
        return
            edge == ResizeEdge.TopLeft ||
            edge == ResizeEdge.TopRight ||
            edge == ResizeEdge.BottomLeft ||
            edge == ResizeEdge.BottomRight;
    }


    // ========================================================
    // SMART DIAGONAL RESIZE
    // ========================================================

    private bool ResizeCornerSmart(
        HudGridWindowController source,
        ResizeEdge edge,
        Vector3 releasedWorldPoint)
    {
        Vector2 releasedAngles =
            WorldPointToVisorAngles(
                releasedWorldPoint
            );


        int oldLeft =
            source.CurrentColumn;

        int oldTop =
            source.CurrentRow;

        int oldRight =
            oldLeft +
            source.ColumnSpan;

        int oldBottom =
            oldTop +
            source.RowSpan;


        int newLeft =
            oldLeft;

        int newTop =
            oldTop;

        int newRight =
            oldRight;

        int newBottom =
            oldBottom;


        // ----------------------------------------------------
        // MOVE THE TWO BOUNDARIES OWNED BY THIS CORNER
        // ----------------------------------------------------

        switch (edge)
        {
            case ResizeEdge.TopLeft:

                newLeft =
                    Mathf.Clamp(
                        GetNearestColumnBoundary(
                            releasedAngles.x
                        ),
                        0,
                        oldRight - 1
                    );


                newTop =
                    Mathf.Clamp(
                        GetNearestRowBoundary(
                            releasedAngles.y
                        ),
                        0,
                        oldBottom - 1
                    );

                break;


            case ResizeEdge.TopRight:

                newRight =
                    Mathf.Clamp(
                        GetNearestColumnBoundary(
                            releasedAngles.x
                        ),
                        oldLeft + 1,
                        columns
                    );


                newTop =
                    Mathf.Clamp(
                        GetNearestRowBoundary(
                            releasedAngles.y
                        ),
                        0,
                        oldBottom - 1
                    );

                break;


            case ResizeEdge.BottomLeft:

                newLeft =
                    Mathf.Clamp(
                        GetNearestColumnBoundary(
                            releasedAngles.x
                        ),
                        0,
                        oldRight - 1
                    );


                newBottom =
                    Mathf.Clamp(
                        GetNearestRowBoundary(
                            releasedAngles.y
                        ),
                        oldTop + 1,
                        rows
                    );

                break;


            case ResizeEdge.BottomRight:

                newRight =
                    Mathf.Clamp(
                        GetNearestColumnBoundary(
                            releasedAngles.x
                        ),
                        oldLeft + 1,
                        columns
                    );


                newBottom =
                    Mathf.Clamp(
                        GetNearestRowBoundary(
                            releasedAngles.y
                        ),
                        oldTop + 1,
                        rows
                    );

                break;
        }


        if (newLeft == oldLeft &&
            newRight == oldRight &&
            newTop == oldTop &&
            newBottom == oldBottom)
        {
            return false;
        }


        PlannedPlacement sourcePlan =
            new PlannedPlacement(
                source,
                newLeft,
                newTop,
                newRight - newLeft,
                newBottom - newTop
            );


        List<PlannedPlacement> plans =
            new List<PlannedPlacement>();


        plans.Add(
            sourcePlan
        );


        // ----------------------------------------------------
        // DETERMINE WHICH DIRECTIONS ACTUALLY EXPANDED
        //
        // Shrinking never enlarges a neighbor.
        // ----------------------------------------------------

        bool expandsLeft =
            newLeft <
            oldLeft;


        bool expandsRight =
            newRight >
            oldRight;


        bool expandsTop =
            newTop <
            oldTop;


        bool expandsBottom =
            newBottom >
            oldBottom;


        // ----------------------------------------------------
        // CLAIM THE NEW RECTANGLE.
        //
        // Any existing window intersected by an EXPANDING
        // edge is trimmed away from that edge.
        //
        // A diagonal neighbor can therefore be trimmed in
        // two dimensions in one atomic operation.
        // ----------------------------------------------------

        foreach (
            HudGridWindowController candidate
            in registeredWindows)
        {
            if (candidate == null ||
                candidate == source ||
                !candidate.IsGridPlaced)
            {
                continue;
            }


            int candidateLeft =
                candidate.CurrentColumn;

            int candidateTop =
                candidate.CurrentRow;

            int candidateRight =
                candidateLeft +
                candidate.ColumnSpan;

            int candidateBottom =
                candidateTop +
                candidate.RowSpan;


            if (!RectanglesOverlap(
                    sourcePlan.Left,
                    sourcePlan.Top,
                    sourcePlan.Right,
                    sourcePlan.Bottom,
                    candidateLeft,
                    candidateTop,
                    candidateRight,
                    candidateBottom))
            {
                continue;
            }


            int trimmedLeft =
                candidateLeft;

            int trimmedTop =
                candidateTop;

            int trimmedRight =
                candidateRight;

            int trimmedBottom =
                candidateBottom;


            /*
             * Candidate was originally entirely to the LEFT
             * of the source.
             */
            if (expandsLeft &&
                candidateRight <=
                oldLeft)
            {
                trimmedRight =
                    Mathf.Min(
                        trimmedRight,
                        newLeft
                    );
            }


            /*
             * Candidate was originally entirely to the RIGHT
             * of the source.
             */
            if (expandsRight &&
                candidateLeft >=
                oldRight)
            {
                trimmedLeft =
                    Mathf.Max(
                        trimmedLeft,
                        newRight
                    );
            }


            /*
             * Candidate was originally entirely ABOVE
             * the source.
             */
            if (expandsTop &&
                candidateBottom <=
                oldTop)
            {
                trimmedBottom =
                    Mathf.Min(
                        trimmedBottom,
                        newTop
                    );
            }


            /*
             * Candidate was originally entirely BELOW
             * the source.
             */
            if (expandsBottom &&
                candidateTop >=
                oldBottom)
            {
                trimmedTop =
                    Mathf.Max(
                        trimmedTop,
                        newBottom
                    );
            }


            int trimmedWidth =
                trimmedRight -
                trimmedLeft;


            int trimmedHeight =
                trimmedBottom -
                trimmedTop;


            /*
             * Every window must remain at least 1 × 1.
             */
            if (trimmedWidth < 1 ||
                trimmedHeight < 1)
            {
                if (verboseLogging)
                {
                    Debug.Log(
                        $"HUD CORNER RESIZE rejected: " +
                        $"{candidate.name} would become " +
                        $"smaller than 1x1."
                    );
                }


                return false;
            }


            PlannedPlacement candidatePlan =
                new PlannedPlacement(
                    candidate,
                    trimmedLeft,
                    trimmedTop,
                    trimmedWidth,
                    trimmedHeight
                );


            /*
             * If the allowed trims still do not remove the
             * overlap, then this arrangement cannot be
             * resolved by this corner drag.
             */
            if (PlacementsOverlap(
                    sourcePlan,
                    candidatePlan))
            {
                if (verboseLogging)
                {
                    Debug.Log(
                        $"HUD CORNER RESIZE rejected: " +
                        $"{candidate.name} cannot be trimmed " +
                        $"cleanly away from {source.name}."
                    );
                }


                return false;
            }


            plans.Add(
                candidatePlan
            );
        }


        bool applied =
            ApplyPlacementTransaction(
                plans
            );


        if (applied &&
            verboseLogging)
        {
            Debug.Log(
                $"HUD CORNER RESIZE: {source.name} " +
                $"{edge} -> " +
                $"({newLeft},{newTop}) " +
                $"to ({newRight},{newBottom}), " +
                $"{plans.Count - 1} neighbor(s) affected."
            );
        }


        return applied;
    }


    // ========================================================
    // CARDINAL RESIZE
    //
    // Kept for compatibility even though we're going to hide
    // the four cardinal handles.
    // ========================================================

    private bool ResizeCardinalEdge(
        HudGridWindowController source,
        ResizeEdge edge,
        Vector3 releasedWorldPoint)
    {
        Vector2 releasedAngles =
            WorldPointToVisorAngles(
                releasedWorldPoint
            );


        int oldLeft =
            source.CurrentColumn;

        int oldTop =
            source.CurrentRow;

        int oldRight =
            oldLeft +
            source.ColumnSpan;

        int oldBottom =
            oldTop +
            source.RowSpan;


        int newBoundary;


        switch (edge)
        {
            case ResizeEdge.Left:

                newBoundary =
                    Mathf.Clamp(
                        GetNearestColumnBoundary(
                            releasedAngles.x
                        ),
                        0,
                        oldRight - 1
                    );

                if (newBoundary ==
                    oldLeft)
                {
                    return false;
                }

                break;


            case ResizeEdge.Right:

                newBoundary =
                    Mathf.Clamp(
                        GetNearestColumnBoundary(
                            releasedAngles.x
                        ),
                        oldLeft + 1,
                        columns
                    );

                if (newBoundary ==
                    oldRight)
                {
                    return false;
                }

                break;


            case ResizeEdge.Top:

                newBoundary =
                    Mathf.Clamp(
                        GetNearestRowBoundary(
                            releasedAngles.y
                        ),
                        0,
                        oldBottom - 1
                    );

                if (newBoundary ==
                    oldTop)
                {
                    return false;
                }

                break;


            case ResizeEdge.Bottom:

                newBoundary =
                    Mathf.Clamp(
                        GetNearestRowBoundary(
                            releasedAngles.y
                        ),
                        oldTop + 1,
                        rows
                    );

                if (newBoundary ==
                    oldBottom)
                {
                    return false;
                }

                break;


            default:

                return false;
        }


        bool expandingTowardNeighbor =
            IsExpandingAcrossEdge(
                edge,
                newBoundary,
                oldLeft,
                oldTop,
                oldRight,
                oldBottom
            );


        List<PlannedPlacement> plans =
            new List<PlannedPlacement>();


        switch (edge)
        {
            case ResizeEdge.Left:

                plans.Add(
                    new PlannedPlacement(
                        source,
                        newBoundary,
                        oldTop,
                        oldRight -
                        newBoundary,
                        oldBottom -
                        oldTop
                    )
                );

                break;


            case ResizeEdge.Right:

                plans.Add(
                    new PlannedPlacement(
                        source,
                        oldLeft,
                        oldTop,
                        newBoundary -
                        oldLeft,
                        oldBottom -
                        oldTop
                    )
                );

                break;


            case ResizeEdge.Top:

                plans.Add(
                    new PlannedPlacement(
                        source,
                        oldLeft,
                        newBoundary,
                        oldRight -
                        oldLeft,
                        oldBottom -
                        newBoundary
                    )
                );

                break;


            case ResizeEdge.Bottom:

                plans.Add(
                    new PlannedPlacement(
                        source,
                        oldLeft,
                        oldTop,
                        oldRight -
                        oldLeft,
                        newBoundary -
                        oldTop
                    )
                );

                break;
        }


        if (enableSharedBoundaryResize &&
            expandingTowardNeighbor)
        {
            List<HudGridWindowController>
                neighbors =
                    FindAdjacentWindows(
                        source,
                        edge
                    );


            foreach (
                HudGridWindowController neighbor
                in neighbors)
            {
                int nLeft =
                    neighbor.CurrentColumn;

                int nTop =
                    neighbor.CurrentRow;

                int nRight =
                    nLeft +
                    neighbor.ColumnSpan;

                int nBottom =
                    nTop +
                    neighbor.RowSpan;


                switch (edge)
                {
                    case ResizeEdge.Left:

                        plans.Add(
                            new PlannedPlacement(
                                neighbor,
                                nLeft,
                                nTop,
                                newBoundary -
                                nLeft,
                                nBottom -
                                nTop
                            )
                        );

                        break;


                    case ResizeEdge.Right:

                        plans.Add(
                            new PlannedPlacement(
                                neighbor,
                                newBoundary,
                                nTop,
                                nRight -
                                newBoundary,
                                nBottom -
                                nTop
                            )
                        );

                        break;


                    case ResizeEdge.Top:

                        plans.Add(
                            new PlannedPlacement(
                                neighbor,
                                nLeft,
                                nTop,
                                nRight -
                                nLeft,
                                newBoundary -
                                nTop
                            )
                        );

                        break;


                    case ResizeEdge.Bottom:

                        plans.Add(
                            new PlannedPlacement(
                                neighbor,
                                nLeft,
                                newBoundary,
                                nRight -
                                nLeft,
                                nBottom -
                                newBoundary
                            )
                        );

                        break;
                }
            }
        }


        return ApplyPlacementTransaction(
            plans
        );
    }


    private bool IsExpandingAcrossEdge(
        ResizeEdge edge,
        int newBoundary,
        int oldLeft,
        int oldTop,
        int oldRight,
        int oldBottom)
    {
        switch (edge)
        {
            case ResizeEdge.Left:

                return
                    newBoundary <
                    oldLeft;


            case ResizeEdge.Right:

                return
                    newBoundary >
                    oldRight;


            case ResizeEdge.Top:

                return
                    newBoundary <
                    oldTop;


            case ResizeEdge.Bottom:

                return
                    newBoundary >
                    oldBottom;
        }


        return false;
    }


    // ========================================================
    // CARDINAL ADJACENCY
    // ========================================================

    private List<HudGridWindowController>
        FindAdjacentWindows(
            HudGridWindowController source,
            ResizeEdge edge)
    {
        List<HudGridWindowController> result =
            new List<HudGridWindowController>();


        int sourceLeft =
            source.CurrentColumn;

        int sourceTop =
            source.CurrentRow;

        int sourceRight =
            sourceLeft +
            source.ColumnSpan;

        int sourceBottom =
            sourceTop +
            source.RowSpan;


        foreach (
            HudGridWindowController candidate
            in registeredWindows)
        {
            if (candidate == null ||
                candidate == source ||
                !candidate.IsGridPlaced)
            {
                continue;
            }


            int left =
                candidate.CurrentColumn;

            int top =
                candidate.CurrentRow;

            int right =
                left +
                candidate.ColumnSpan;

            int bottom =
                top +
                candidate.RowSpan;


            bool adjacent =
                false;


            switch (edge)
            {
                case ResizeEdge.Left:

                    adjacent =
                        right ==
                        sourceLeft
                        &&
                        RangesOverlap(
                            top,
                            bottom,
                            sourceTop,
                            sourceBottom
                        );

                    break;


                case ResizeEdge.Right:

                    adjacent =
                        left ==
                        sourceRight
                        &&
                        RangesOverlap(
                            top,
                            bottom,
                            sourceTop,
                            sourceBottom
                        );

                    break;


                case ResizeEdge.Top:

                    adjacent =
                        bottom ==
                        sourceTop
                        &&
                        RangesOverlap(
                            left,
                            right,
                            sourceLeft,
                            sourceRight
                        );

                    break;


                case ResizeEdge.Bottom:

                    adjacent =
                        top ==
                        sourceBottom
                        &&
                        RangesOverlap(
                            left,
                            right,
                            sourceLeft,
                            sourceRight
                        );

                    break;
            }


            if (adjacent)
            {
                result.Add(
                    candidate
                );
            }
        }


        return result;
    }


    private bool RangesOverlap(
        int aMin,
        int aMax,
        int bMin,
        int bMax)
    {
        return
            Mathf.Max(
                aMin,
                bMin
            )
            <
            Mathf.Min(
                aMax,
                bMax
            );
    }


    // ========================================================
    // ATOMIC MULTI-WINDOW TRANSACTION
    // ========================================================

    private bool ApplyPlacementTransaction(
        List<PlannedPlacement> plans)
    {
        if (plans == null ||
            plans.Count == 0)
        {
            return false;
        }


        HashSet<HudGridWindowController>
            movingWindows =
                new HashSet<HudGridWindowController>();


        foreach (
            PlannedPlacement plan
            in plans)
        {
            if (plan.window == null ||
                !IsPlacementInsideGrid(
                    plan))
            {
                return false;
            }


            movingWindows.Add(
                plan.window
            );
        }


        /*
         * Planned windows must not overlap each other.
         */
        for (
            int i = 0;
            i < plans.Count;
            i++)
        {
            for (
                int j = i + 1;
                j < plans.Count;
                j++)
            {
                if (PlacementsOverlap(
                        plans[i],
                        plans[j]))
                {
                    return false;
                }
            }
        }


        /*
         * Planned cells may not collide with a window that is
         * NOT participating in this transaction.
         */
        foreach (
            PlannedPlacement plan
            in plans)
        {
            for (
                int y = plan.row;
                y <
                plan.row +
                plan.rowSpan;
                y++)
            {
                for (
                    int x = plan.column;
                    x <
                    plan.column +
                    plan.columnSpan;
                    x++)
                {
                    HudGridWindowController occupant =
                        occupancy[x, y];


                    if (occupant != null &&
                        !movingWindows.Contains(
                            occupant))
                    {
                        return false;
                    }
                }
            }
        }


        /*
         * Validation passed. Only now mutate occupancy.
         */
        foreach (
            HudGridWindowController window
            in movingWindows)
        {
            ReleaseCells(
                window
            );
        }


        foreach (
            PlannedPlacement plan
            in plans)
        {
            FillCells(
                plan.window,
                plan.column,
                plan.row,
                plan.columnSpan,
                plan.rowSpan
            );


            plan.window.ApplyGridPlacement(
                plan.column,
                plan.row,
                plan.columnSpan,
                plan.rowSpan
            );


            ApplyWindowGeometry(
                plan.window,
                plan.column,
                plan.row,
                plan.columnSpan,
                plan.rowSpan
            );
        }


        return true;
    }


    private bool IsPlacementInsideGrid(
        PlannedPlacement placement)
    {
        if (placement.columnSpan < 1 ||
            placement.rowSpan < 1)
        {
            return false;
        }


        if (placement.column < 0 ||
            placement.row < 0)
        {
            return false;
        }


        if (
            placement.column +
            placement.columnSpan >
            columns)
        {
            return false;
        }


        if (
            placement.row +
            placement.rowSpan >
            rows)
        {
            return false;
        }


        return true;
    }


    private bool PlacementsOverlap(
        PlannedPlacement a,
        PlannedPlacement b)
    {
        return
            RectanglesOverlap(
                a.Left,
                a.Top,
                a.Right,
                a.Bottom,

                b.Left,
                b.Top,
                b.Right,
                b.Bottom
            );
    }


    private bool RectanglesOverlap(
        int aLeft,
        int aTop,
        int aRight,
        int aBottom,
        int bLeft,
        int bTop,
        int bRight,
        int bBottom)
    {
        return
            aLeft < bRight &&
            aRight > bLeft &&
            aTop < bBottom &&
            aBottom > bTop;
    }


    // ========================================================
    // FREE PLACEMENT SEARCH
    // ========================================================

    private bool FindNearestFreePlacement(
        HudGridWindowController window,
        Vector2 targetAngles,
        int columnSpan,
        int rowSpan,
        out GridPlacement bestPlacement,
        bool enforceMaximumDistance)
    {
        bestPlacement =
            new GridPlacement(
                0,
                0,
                columnSpan,
                rowSpan,
                false
            );


        float bestDistance =
            float.MaxValue;


        int lastColumn =
            columns -
            columnSpan;

        int lastRow =
            rows -
            rowSpan;


        if (lastColumn < 0 ||
            lastRow < 0)
        {
            return false;
        }


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
                if (!IsAreaFree(
                        column,
                        row,
                        columnSpan,
                        rowSpan,
                        window))
                {
                    continue;
                }


                Vector2 candidateAngles =
                    GetPlacementCenterAngles(
                        column,
                        row,
                        columnSpan,
                        rowSpan
                    );


                float distance =
                    Vector2.Distance(
                        targetAngles,
                        candidateAngles
                    );


                if (distance >=
                    bestDistance)
                {
                    continue;
                }


                bestDistance =
                    distance;


                bestPlacement =
                    new GridPlacement(
                        column,
                        row,
                        columnSpan,
                        rowSpan,
                        true
                    );
            }
        }


        if (!bestPlacement.valid)
            return false;


        if (enforceMaximumDistance &&
            bestDistance >
            maximumDropDistanceDegrees)
        {
            return false;
        }


        return true;
    }


    // ========================================================
    // SINGLE WINDOW PLACEMENT
    // ========================================================

    public bool TryPlaceWindowExact(
        HudGridWindowController window,
        int column,
        int row,
        int columnSpan,
        int rowSpan)
    {
        if (window == null)
            return false;

        /*
        * Exact placement must reject invalid values instead of
        * silently clamping them. Preset validation depends on
        * exact coordinates being preserved.
        */
        if (
            column < 0 ||
            row < 0 ||
            columnSpan < 1 ||
            rowSpan < 1 ||
            column + columnSpan > columns ||
            row + rowSpan > rows
        )
        {
            Debug.LogError(
                $"HUD GRID: invalid exact placement for " +
                $"{window.name}: column={column}, row={row}, " +
                $"span={columnSpan}x{rowSpan}"
            );

            return false;
        }

        bool wasAlreadyRegistered =
            registeredWindows.Contains(window);

        if (!wasAlreadyRegistered)
        {
            registeredWindows.Add(window);
        }

        bool placed =
            PlaceWindow(
                window,
                column,
                row,
                columnSpan,
                rowSpan
            );

        /*
        * Do not leave a newly introduced window registered when
        * its requested preset position could not be occupied.
        */
        if (!placed &&
            !wasAlreadyRegistered)
        {
            registeredWindows.Remove(window);
        }

        return placed;
    }

    private bool PlaceWindow(
        HudGridWindowController window,
        int column,
        int row,
        int columnSpan,
        int rowSpan)
    {
        if (window == null)
            return false;


        columnSpan =
            Mathf.Clamp(
                columnSpan,
                1,
                columns
            );


        rowSpan =
            Mathf.Clamp(
                rowSpan,
                1,
                rows
            );


        if (!IsAreaFree(
                column,
                row,
                columnSpan,
                rowSpan,
                window))
        {
            return false;
        }


        ReleaseCells(
            window
        );


        FillCells(
            window,
            column,
            row,
            columnSpan,
            rowSpan
        );


        window.ApplyGridPlacement(
            column,
            row,
            columnSpan,
            rowSpan
        );


        ApplyWindowGeometry(
            window,
            column,
            row,
            columnSpan,
            rowSpan
        );


        if (verboseLogging)
        {
            Debug.Log(
                $"HUD GRID: {window.name} -> " +
                $"column {column}, row {row}, " +
                $"span {columnSpan}x{rowSpan}"
            );
        }


        return true;
    }


    private void FillCells(
        HudGridWindowController window,
        int column,
        int row,
        int columnSpan,
        int rowSpan)
    {
        for (
            int y = row;
            y < row + rowSpan;
            y++)
        {
            for (
                int x = column;
                x < column + columnSpan;
                x++)
            {
                occupancy[x, y] =
                    window;
            }
        }
    }


    // ========================================================
    // WINDOW GEOMETRY
    // ========================================================

    private void ApplyWindowGeometry(
        HudGridWindowController controller,
        int column,
        int row,
        int columnSpan,
        int rowSpan)
    {
        HudWindow window =
            controller.HudWindow;


        if (window == null)
            return;


        float cellWidthDegrees =
            horizontalSpanDegrees /
            columns;


        float cellHeightDegrees =
            verticalSpanDegrees /
            rows;


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


        float centerYaw =
            (
                leftYaw +
                rightYaw
            ) *
            0.5f;


        float centerPitch =
            (
                topPitch +
                bottomPitch
            ) *
            0.5f;


        float panelWidthDegrees =
            Mathf.Max(
                1.0f,
                columnSpan *
                cellWidthDegrees
                -
                gutterDegrees
            );


        float panelHeightDegrees =
            Mathf.Max(
                1.0f,
                rowSpan *
                cellHeightDegrees
                -
                gutterDegrees
            );


        float widthMeters =
            AngleDegreesToMeters(
                panelWidthDegrees
            );


        float heightMeters =
            AngleDegreesToMeters(
                panelHeightDegrees
            );


        window.SetSizeMeters(
            widthMeters,
            heightMeters
        );


        if (window.sphereConstraint != null)
        {
            window.sphereConstraint
                .SetVisorAngles(
                    centerYaw,
                    centerPitch
                );
        }
    }


    // ========================================================
    // OCCUPANCY
    // ========================================================

    private bool IsAreaFree(
        int column,
        int row,
        int columnSpan,
        int rowSpan,
        HudGridWindowController requestingWindow)
    {
        if (column < 0 ||
            row < 0 ||
            column + columnSpan > columns ||
            row + rowSpan > rows)
        {
            return false;
        }


        for (
            int y = row;
            y < row + rowSpan;
            y++)
        {
            for (
                int x = column;
                x < column + columnSpan;
                x++)
            {
                HudGridWindowController occupant =
                    occupancy[x, y];


                if (occupant != null &&
                    occupant != requestingWindow)
                {
                    return false;
                }
            }
        }


        return true;
    }


    private void ReleaseCells(
        HudGridWindowController window)
    {
        if (occupancy == null ||
            window == null)
        {
            return;
        }


        for (
            int y = 0;
            y < rows;
            y++)
        {
            for (
                int x = 0;
                x < columns;
                x++)
            {
                if (occupancy[x, y] ==
                    window)
                {
                    occupancy[x, y] =
                        null;
                }
            }
        }
    }


    // ========================================================
    // GRID ANGLE MATH
    // ========================================================

    private float GetColumnBoundaryYaw(
        int boundary)
    {
        float cellWidth =
            horizontalSpanDegrees /
            columns;


        return
            gridCenterYawDegrees
            -
            horizontalSpanDegrees *
            0.5f
            +
            boundary *
            cellWidth;
    }


    private float GetRowBoundaryPitch(
        int boundary)
    {
        float cellHeight =
            verticalSpanDegrees /
            rows;


        return
            gridCenterPitchDegrees
            +
            verticalSpanDegrees *
            0.5f
            -
            boundary *
            cellHeight;
    }


    private int GetNearestColumnBoundary(
        float yawDegrees)
    {
        float cellWidth =
            horizontalSpanDegrees /
            columns;


        float gridLeft =
            gridCenterYawDegrees
            -
            horizontalSpanDegrees *
            0.5f;


        float coordinate =
            (
                yawDegrees -
                gridLeft
            )
            /
            cellWidth;


        return Mathf.Clamp(
            Mathf.RoundToInt(
                coordinate
            ),
            0,
            columns
        );
    }


    private int GetNearestRowBoundary(
        float pitchDegrees)
    {
        float cellHeight =
            verticalSpanDegrees /
            rows;


        float gridTop =
            gridCenterPitchDegrees
            +
            verticalSpanDegrees *
            0.5f;


        float coordinate =
            (
                gridTop -
                pitchDegrees
            )
            /
            cellHeight;


        return Mathf.Clamp(
            Mathf.RoundToInt(
                coordinate
            ),
            0,
            rows
        );
    }


    private Vector2 GetPlacementCenterAngles(
        int column,
        int row,
        int columnSpan,
        int rowSpan)
    {
        float left =
            GetColumnBoundaryYaw(
                column
            );


        float right =
            GetColumnBoundaryYaw(
                column +
                columnSpan
            );


        float top =
            GetRowBoundaryPitch(
                row
            );


        float bottom =
            GetRowBoundaryPitch(
                row +
                rowSpan
            );


        return new Vector2(
            (
                left +
                right
            ) *
            0.5f,

            (
                top +
                bottom
            ) *
            0.5f
        );
    }


    private Vector2 GetCurrentWindowAngles(
        HudGridWindowController controller)
    {
        if (controller == null ||
            controller.HudWindow == null ||
            controller.HudWindow
                .sphereConstraint == null)
        {
            return Vector2.zero;
        }


        return controller
            .HudWindow
            .sphereConstraint
            .CaptureVisorAnglesNow();
    }


    public Vector2 WorldPointToVisorAngles(
        Vector3 worldPoint)
    {
        Vector3 localPoint =
            transform.InverseTransformPoint(
                worldPoint
            );


        Vector3 sphereCenter =
            new Vector3(
                0.0f,
                0.0f,
                -sphereRadius
            );


        Vector3 direction =
            localPoint -
            sphereCenter;


        if (direction.sqrMagnitude <
            0.000001f)
        {
            direction =
                Vector3.forward;
        }
        else
        {
            direction.Normalize();
        }


        float yaw =
            Mathf.Atan2(
                direction.x,
                direction.z
            )
            *
            Mathf.Rad2Deg;


        float pitch =
            Mathf.Asin(
                Mathf.Clamp(
                    direction.y,
                    -1.0f,
                    1.0f
                )
            )
            *
            Mathf.Rad2Deg;


        return new Vector2(
            yaw,
            pitch
        );
    }


    private float AngleDegreesToMeters(
        float degrees)
    {
        float halfAngleRadians =
            degrees *
            Mathf.Deg2Rad *
            0.5f;


        return
            2.0f *
            sphereRadius *
            Mathf.Tan(
                halfAngleRadians
            );
    }


    // ========================================================
    // PUBLIC QUERY
    // ========================================================

    public bool CanOccupy(
        HudGridWindowController window,
        int column,
        int row,
        int columnSpan,
        int rowSpan)
    {
        return IsAreaFree(
            column,
            row,
            columnSpan,
            rowSpan,
            window
        );
    }
}
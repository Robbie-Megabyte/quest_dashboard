using UnityEngine;

public static class HudSphereGeometry
{
    public static float GetRadius(
        HudWindow window)
    {
        if (window != null &&
            window.sphereConstraint != null)
        {
            return Mathf.Max(
                0.01f,
                window.sphereConstraint.radius
            );
        }

        return 1.6f;
    }


    public static float MetersToDegrees(
        float meters,
        float radius)
    {
        radius =
            Mathf.Max(
                0.01f,
                radius
            );

        return
            2.0f *
            Mathf.Atan(
                meters /
                (2.0f * radius)
            ) *
            Mathf.Rad2Deg;
    }


    /*
     * normalizedX / normalizedY:
     *
     * center       =  0,    0
     * left edge    = -0.5
     * right edge   = +0.5
     * bottom edge  = -0.5
     * top edge     = +0.5
     *
     * The returned pose is tangent to the same imaginary
     * sphere that the window is sitting on.
     */
    public static Pose GetSurfacePose(
        HudWindow window,
        float normalizedX,
        float normalizedY,
        float towardUserMeters = 0.0f)
    {
        if (window == null)
        {
            return new Pose(
                Vector3.zero,
                Quaternion.identity
            );
        }


        Transform root =
            window.transform;


        float radius =
            GetRadius(
                window
            );


        float widthDegrees =
            MetersToDegrees(
                window.WidthMeters,
                radius
            );


        float heightDegrees =
            MetersToDegrees(
                window.HeightMeters,
                radius
            );


        float yawOffset =
            normalizedX *
            widthDegrees;


        float pitchOffset =
            normalizedY *
            heightDegrees;


        /*
         * First walk horizontally across the sphere.
         */
        Quaternion yawRotation =
            Quaternion.AngleAxis(
                yawOffset,
                root.up
            );


        Vector3 yawRight =
            yawRotation *
            root.right;


        /*
         * Positive normalized Y means UP.
         *
         * Unity's positive rotation around +X moves +Z
         * downward, hence the minus sign.
         */
        Quaternion pitchRotation =
            Quaternion.AngleAxis(
                -pitchOffset,
                yawRight
            );


        Quaternion surfaceRotation =
            pitchRotation *
            yawRotation *
            root.rotation;


        Vector3 surfaceNormal =
            surfaceRotation *
            Vector3.forward;


        /*
         * Window root is on the sphere surface.
         *
         * Therefore the sphere center is exactly one radius
         * behind the root along its outward normal.
         */
        Vector3 sphereCenter =
            root.position -
            root.forward *
            radius;


        float drawingRadius =
            Mathf.Max(
                0.01f,
                radius -
                towardUserMeters
            );


        Vector3 surfacePosition =
            sphereCenter +
            surfaceNormal *
            drawingRadius;


        return new Pose(
            surfacePosition,
            surfaceRotation
        );
    }
}
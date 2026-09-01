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

        HudSphereWindowConstraint constraint =
            window.sphereConstraint;

        if (constraint == null ||
            constraint.hudRoot == null)
        {
            return new Pose(
                window.transform.position,
                window.transform.rotation
            );
        }

        Transform hudRoot =
            constraint.hudRoot;

        float radius =
            GetRadius(window);

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

        Vector3 sphereCenterLocal =
            new Vector3(
                0.0f,
                0.0f,
                -radius
            );

        /*
        * Recover the window centre using the same global
        * yaw/pitch coordinate system as HudGridManager and
        * HudGridVisualizer.
        */
        Vector3 rootLocal =
            hudRoot.InverseTransformPoint(
                window.transform.position
            );

        Vector3 centerDirection =
            rootLocal -
            sphereCenterLocal;

        if (centerDirection.sqrMagnitude < 0.000001f)
        {
            centerDirection =
                Vector3.forward;
        }
        else
        {
            centerDirection.Normalize();
        }

        float centerYawDegrees =
            Mathf.Atan2(
                centerDirection.x,
                centerDirection.z
            ) *
            Mathf.Rad2Deg;

        float centerPitchDegrees =
            Mathf.Asin(
                Mathf.Clamp(
                    centerDirection.y,
                    -1.0f,
                    1.0f
                )
            ) *
            Mathf.Rad2Deg;

        float yawDegrees =
            centerYawDegrees +
            normalizedX * widthDegrees;

        float pitchDegrees =
            centerPitchDegrees +
            normalizedY * heightDegrees;

        float yawRadians =
            yawDegrees *
            Mathf.Deg2Rad;

        float pitchRadians =
            pitchDegrees *
            Mathf.Deg2Rad;

        float cosPitch =
            Mathf.Cos(
                pitchRadians
            );

        Vector3 direction =
            new Vector3(
                Mathf.Sin(yawRadians) * cosPitch,
                Mathf.Sin(pitchRadians),
                Mathf.Cos(yawRadians) * cosPitch
            ).normalized;

        float drawingRadius =
            Mathf.Max(
                0.01f,
                radius - towardUserMeters
            );

        Vector3 surfaceLocalPosition =
            sphereCenterLocal +
            direction * drawingRadius;

        Vector3 surfaceWorldPosition =
            hudRoot.TransformPoint(
                surfaceLocalPosition
            );

        Quaternion surfaceLocalRotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up
            );

        Quaternion surfaceWorldRotation =
            hudRoot.rotation *
            surfaceLocalRotation;

        return new Pose(
            surfaceWorldPosition,
            surfaceWorldRotation
        );
    }
}
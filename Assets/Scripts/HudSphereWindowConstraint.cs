using UnityEngine;

[DisallowMultipleComponent]
public class HudSphereWindowConstraint : MonoBehaviour
{
    [Header("HUD")]
    public Transform hudRoot;

    [Header("Virtual HUD Sphere")]
    public float radius = 0.95f;

    [Range(10.0f, 85.0f)]
    public float maxAngleFromCenter = 55.0f;

    [Header("Runtime")]
    public bool hudConstraintEnabled = true;

    [SerializeField]
    private bool layoutDriven = false;

    [SerializeField]
    private float visorYawDegrees = 0.0f;

    [SerializeField]
    private float visorPitchDegrees = 0.0f;


    public bool LayoutDriven
    {
        get
        {
            return layoutDriven;
        }
    }


    public float VisorYawDegrees
    {
        get
        {
            return visorYawDegrees;
        }
    }


    public float VisorPitchDegrees
    {
        get
        {
            return visorPitchDegrees;
        }
    }


    public Vector2 VisorAngles
    {
        get
        {
            return new Vector2(
                visorYawDegrees,
                visorPitchDegrees
            );
        }
    }


    void LateUpdate()
    {
        if (!hudConstraintEnabled ||
            hudRoot == null)
        {
            return;
        }

        if (layoutDriven)
        {
            ApplyFromStoredAngles();
        }
        else
        {
            CaptureFromCurrentPosition();
        }
    }


    /*
     * FREE WINDOW MODE
     *
     * XR Interaction Toolkit moves the Transform.
     * We read that attempted position, project it back onto
     * our virtual sphere, and calculate its visor angles.
     */
    private void CaptureFromCurrentPosition()
    {
        Vector3 sphereCenter =
            GetSphereCenterLocal();

        Vector3 currentLocal =
            hudRoot.InverseTransformPoint(
                transform.position
            );

        Vector3 direction =
            currentLocal -
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

        direction =
            ClampDirection(
                direction
            );

        DirectionToAngles(
            direction,
            out visorYawDegrees,
            out visorPitchDegrees
        );

        ApplyDirection(
            direction
        );
    }


    /*
     * LAYOUT / TILING MODE
     *
     * The tiling system owns yaw/pitch.
     * The 3D pose is derived from those coordinates.
     */
    private void ApplyFromStoredAngles()
    {
        Vector3 direction =
            AnglesToDirection(
                visorYawDegrees,
                visorPitchDegrees
            );

        direction =
            ClampDirection(
                direction
            );

        DirectionToAngles(
            direction,
            out visorYawDegrees,
            out visorPitchDegrees
        );

        ApplyDirection(
            direction
        );
    }


    private Vector3 GetSphereCenterLocal()
    {
        return new Vector3(
            0.0f,
            0.0f,
            -radius
        );
    }


    private void ApplyDirection(
        Vector3 direction)
    {
        Vector3 sphereCenter =
            GetSphereCenterLocal();

        Vector3 targetLocal =
            sphereCenter +
            direction * radius;

        transform.position =
            hudRoot.TransformPoint(
                targetLocal
            );


        /*
         * Only the spherical HUD decides window rotation.
         *
         * User grabbing never rotates the panel.
         */
        Quaternion tangentRotationLocal =
            Quaternion.LookRotation(
                direction,
                Vector3.up
            );

        transform.rotation =
            hudRoot.rotation *
            tangentRotationLocal;
    }


    private Vector3 ClampDirection(
        Vector3 direction)
    {
        direction.Normalize();

        float angle =
            Vector3.Angle(
                Vector3.forward,
                direction
            );

        if (angle <=
            maxAngleFromCenter)
        {
            return direction;
        }

        Vector3 axis =
            Vector3.Cross(
                Vector3.forward,
                direction
            );

        if (axis.sqrMagnitude <
            0.000001f)
        {
            axis =
                Vector3.up;
        }

        axis.Normalize();

        direction =
            Quaternion.AngleAxis(
                maxAngleFromCenter,
                axis
            ) *
            Vector3.forward;

        return direction.normalized;
    }


    private Vector3 AnglesToDirection(
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

        return direction.normalized;
    }


    private void DirectionToAngles(
        Vector3 direction,
        out float yawDegrees,
        out float pitchDegrees)
    {
        direction.Normalize();

        yawDegrees =
            Mathf.Atan2(
                direction.x,
                direction.z
            ) *
            Mathf.Rad2Deg;

        pitchDegrees =
            Mathf.Asin(
                Mathf.Clamp(
                    direction.y,
                    -1.0f,
                    1.0f
                )
            ) *
            Mathf.Rad2Deg;
    }


    /*
     * Used by the future tile layout manager.
     */
    public void SetVisorAngles(
        float yawDegrees,
        float pitchDegrees)
    {
        visorYawDegrees =
            yawDegrees;

        visorPitchDegrees =
            pitchDegrees;

        layoutDriven =
            true;

        if (hudConstraintEnabled)
        {
            ApplyFromStoredAngles();
        }
    }


    public void SetVisorAngles(
        Vector2 angles)
    {
        SetVisorAngles(
            angles.x,
            angles.y
        );
    }


    /*
     * Called when a tiled/snapped window is pulled out of
     * its group and becomes freely movable again.
     */
    public void ReleaseLayoutControl()
    {
        layoutDriven =
            false;
    }


    /*
     * Capture the current free position before a window
     * becomes part of a snapped layout.
     */
    public Vector2 CaptureVisorAnglesNow()
    {
        if (hudRoot == null)
        {
            return VisorAngles;
        }

        CaptureFromCurrentPosition();

        return VisorAngles;
    }


    public void EnableHudConstraint()
    {
        hudConstraintEnabled =
            true;

        if (layoutDriven)
        {
            ApplyFromStoredAngles();
        }
        else
        {
            CaptureFromCurrentPosition();
        }
    }


    public void DisableHudConstraint()
    {
        hudConstraintEnabled =
            false;
    }


    public void SetHudConstraint(
        bool enabled)
    {
        if (enabled)
        {
            EnableHudConstraint();
        }
        else
        {
            DisableHudConstraint();
        }
    }
}
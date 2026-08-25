using UnityEngine;

public class SoftHeadLockedHud : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip(
        "Quest Main Camera. If empty, Camera.main is used."
    )]
    public Transform xrCamera;

    [Header("HUD Placement")]
    [Tooltip("Distance in front of the headset.")]
    public float distanceFromHead = 1.2f;

    [Tooltip(
        "Offset of the whole HUD. Normally leave this at 0."
    )]
    public float horizontalOffset = 0.0f;

    [Tooltip(
        "Offset of the whole HUD. Normally leave this at 0."
    )]
    public float verticalOffset = 0.0f;

    [Header("Soft Follow")]
    [Tooltip(
        "Higher values make position follow the head more tightly."
    )]
    public float positionFollowSpeed = 12.0f;

    [Tooltip(
        "Higher values make rotation follow the head more tightly."
    )]
    public float rotationFollowSpeed = 10.0f;

    [Header("Recovery")]
    [Tooltip(
        "Snap back if the HUD gets too far from its target."
    )]
    public float snapDistance = 0.75f;

    [Tooltip(
        "Snap back if the HUD gets too far outside the view direction."
    )]
    public float snapAngle = 60.0f;

    [Header("Quest Startup")]
    [Tooltip(
        "For the first few frames, keep snapping the HUD " +
        "to the headset while XR tracking initializes."
    )]
    public int startupSnapFrames = 45;

    private int startupFramesRemaining = 0;


    void Awake()
    {
        FindCameraIfNeeded();

        startupFramesRemaining =
            Mathf.Max(1, startupSnapFrames);
    }


    void OnEnable()
    {
        FindCameraIfNeeded();

        startupFramesRemaining =
            Mathf.Max(1, startupSnapFrames);
    }


    void LateUpdate()
    {
        if (!FindCameraIfNeeded())
            return;

        Vector3 targetPosition =
            CalculateTargetPosition();

        Quaternion targetRotation =
            xrCamera.rotation;


        // ----------------------------------------------------
        // QUEST STARTUP
        //
        // OpenXR may not have a final tracked camera pose
        // during Unity's first frame.
        //
        // For a short period we therefore force the HUD to
        // the correct camera-relative pose every frame.
        // ----------------------------------------------------

        if (startupFramesRemaining > 0)
        {
            transform.SetPositionAndRotation(
                targetPosition,
                targetRotation
            );

            startupFramesRemaining--;

            return;
        }


        // ----------------------------------------------------
        // LARGE-MOVEMENT RECOVERY
        // ----------------------------------------------------

        float positionError =
            Vector3.Distance(
                transform.position,
                targetPosition
            );

        float rotationError =
            Quaternion.Angle(
                transform.rotation,
                targetRotation
            );

        if (positionError > snapDistance ||
            rotationError > snapAngle)
        {
            transform.SetPositionAndRotation(
                targetPosition,
                targetRotation
            );

            return;
        }


        // ----------------------------------------------------
        // SOFT FOLLOW
        // ----------------------------------------------------

        float positionAlpha =
            1.0f -
            Mathf.Exp(
                -positionFollowSpeed *
                Time.unscaledDeltaTime
            );

        float rotationAlpha =
            1.0f -
            Mathf.Exp(
                -rotationFollowSpeed *
                Time.unscaledDeltaTime
            );


        transform.position =
            Vector3.Lerp(
                transform.position,
                targetPosition,
                positionAlpha
            );


        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationAlpha
            );
    }


    private Vector3 CalculateTargetPosition()
    {
        return
            xrCamera.position
            + xrCamera.forward * distanceFromHead
            + xrCamera.right * horizontalOffset
            + xrCamera.up * verticalOffset;
    }


    private bool FindCameraIfNeeded()
    {
        if (xrCamera != null)
            return true;

        Camera mainCamera =
            Camera.main;

        if (mainCamera == null)
            return false;

        xrCamera =
            mainCamera.transform;

        return true;
    }


    public void SnapNow()
    {
        if (!FindCameraIfNeeded())
            return;

        transform.SetPositionAndRotation(
            CalculateTargetPosition(),
            xrCamera.rotation
        );
    }


    public void RestartStartupSnap()
    {
        startupFramesRemaining =
            Mathf.Max(
                1,
                startupSnapFrames
            );
    }
}
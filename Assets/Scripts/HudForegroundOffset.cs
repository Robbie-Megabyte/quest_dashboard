using UnityEngine;

[DisallowMultipleComponent]
public class HudForegroundOffset : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField]
    private Camera xrCamera;


    [Header("Foreground Offset")]
    [Tooltip(
        "Moves this object toward the camera while preserving " +
        "its original position inside its parent."
    )]
    public float distanceTowardCamera = 0.010f;


    [Header("Runtime")]
    [SerializeField]
    private Vector3 originalLocalPosition;

    [SerializeField]
    private Quaternion originalLocalRotation;

    [SerializeField]
    private bool originalPoseCaptured = false;


    void Awake()
    {
        CaptureOriginalPose();
        RebindCamera();
    }


    void Start()
    {
        RebindCamera();
        ApplyOffset();
    }


    void LateUpdate()
    {
        ApplyOffset();
    }


    private void RebindCamera()
    {
        if (xrCamera == null)
        {
            xrCamera =
                Camera.main;
        }
    }


    private void CaptureOriginalPose()
    {
        if (originalPoseCaptured)
            return;


        originalLocalPosition =
            transform.localPosition;


        originalLocalRotation =
            transform.localRotation;


        originalPoseCaptured =
            true;
    }


    [ContextMenu("Recapture Original Pose")]
    public void RecaptureOriginalPose()
    {
        originalLocalPosition =
            transform.localPosition;


        originalLocalRotation =
            transform.localRotation;


        originalPoseCaptured =
            true;
    }


    public void ApplyOffset()
    {
        if (!originalPoseCaptured)
        {
            CaptureOriginalPose();
        }


        RebindCamera();


        if (xrCamera == null)
            return;


        Transform parent =
            transform.parent;


        Vector3 baseWorldPosition;


        Quaternion baseWorldRotation;


        if (parent != null)
        {
            baseWorldPosition =
                parent.TransformPoint(
                    originalLocalPosition
                );


            baseWorldRotation =
                parent.rotation *
                originalLocalRotation;
        }
        else
        {
            baseWorldPosition =
                originalLocalPosition;


            baseWorldRotation =
                originalLocalRotation;
        }


        Vector3 towardCamera =
            (
                xrCamera.transform.position
                -
                baseWorldPosition
            ).normalized;


        transform.position =
            baseWorldPosition
            +
            towardCamera *
            distanceTowardCamera;


        transform.rotation =
            baseWorldRotation;
    }
}
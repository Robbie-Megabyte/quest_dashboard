using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class Hud3DButtonLabel : MonoBehaviour
{
    [Header("Automatically Resolved")]
    [SerializeField]
    private TMP_Text label;

    [SerializeField]
    private Camera xrCamera;


    [Header("Placement")]
    [Tooltip(
        "Distance from the button center toward the camera."
    )]
    public float distanceTowardCamera = 0.012f;


    [Header("Text")]
    [Tooltip(
        "Local scale of the 3D TextMeshPro object."
    )]
    public float textScale = 0.006f;

    public bool forceBlackText = true;

    public int sortingOrder = 100;


    void Awake()
    {
        RebindReferences();
    }


    void OnEnable()
    {
        RebindReferences();
        ApplyLabelPose();
    }


    void Start()
    {
        RebindReferences();
        ApplyLabelPose();
    }


    void LateUpdate()
    {
        ApplyLabelPose();
    }


    private void RebindReferences()
    {
        if (label == null)
        {
            Transform found =
                transform.Find("Label");

            if (found != null)
            {
                label =
                    found.GetComponent<TMP_Text>();
            }
        }


        if (xrCamera == null)
        {
            xrCamera =
                Camera.main;
        }
    }


    public void ApplyLabelPose()
    {
        RebindReferences();


        if (label == null ||
            xrCamera == null)
        {
            return;
        }


        Vector3 towardCamera =
            (
                xrCamera.transform.position
                -
                transform.position
            ).normalized;


        // ----------------------------------------------------
        // POSITION
        //
        // Put the text physically on the camera-facing side
        // of the button.
        // ----------------------------------------------------

        label.transform.position =
            transform.position
            +
            towardCamera *
            distanceTowardCamera;


        // ----------------------------------------------------
        // ROTATION
        //
        // Important:
        //
        // TextMeshPro 3D's visible face in our setup points
        // opposite its local +Z.
        //
        // Therefore the object's +Z must point AWAY from
        // the camera, not toward it.
        // ----------------------------------------------------

        Vector3 textForward =
            -towardCamera;


        Vector3 desiredUp =
            transform.up;


        desiredUp =
            Vector3.ProjectOnPlane(
                desiredUp,
                textForward
            );


        if (desiredUp.sqrMagnitude <
            0.0001f)
        {
            desiredUp =
                xrCamera.transform.up;
        }


        desiredUp.Normalize();


        label.transform.rotation =
            Quaternion.LookRotation(
                textForward,
                desiredUp
            );


        // ----------------------------------------------------
        // SCALE / APPEARANCE
        // ----------------------------------------------------

        label.transform.localScale =
            Vector3.one *
            textScale;


        if (forceBlackText)
        {
            label.color =
                HudDashboardTheme.TextPrimary;
        }


        Renderer labelRenderer =
            label.GetComponent<Renderer>();


        if (labelRenderer != null)
        {
            labelRenderer.sortingOrder =
                sortingOrder;
        }
    }
}
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class LinearRailSlider : MonoBehaviour
{
    public enum MoveAxis { LeftRight_X, FrontBack_Z }

    [Header("Axis Settings")]
    public MoveAxis allowedAxis;

    [Header("Movement Limits (Local Space)")]
    public float minLimit = -0.5f;
    public float maxLimit = 0.5f;

    private XRGrabInteractable grabInteractable;
    private Rigidbody body;
    private Transform interactorTransform;
    private Transform railFrame;
    private Vector3 initialLocalGrabOffset;
    private bool isGrabbed = false;

    void Start()
    {
        body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        // This object is a constrained machine carriage, not a loose physics prop.
        body.useGravity = false;
        body.isKinematic = true;

        railFrame = transform.parent != null ? transform.parent : transform;
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
        {
            grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
        }

        // Let this script constrain the carriage to its rail axis while selected.
        grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
        grabInteractable.trackPosition = false;
        grabInteractable.trackRotation = false;

        // Subscribe to grab events
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        interactorTransform = args.interactorObject.transform;

        // Calculate where the hand is relative to the slider's parent object
        Vector3 handLocalPos = railFrame.InverseTransformPoint(interactorTransform.position);
        initialLocalGrabOffset = transform.localPosition - handLocalPos;

        isGrabbed = true;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        isGrabbed = false;
        interactorTransform = null;
    }

    void LateUpdate()
    {
        if (!isGrabbed || interactorTransform == null) return;

        // Find hand position in the parent rail space
        Vector3 handLocalPos = railFrame.InverseTransformPoint(interactorTransform.position);
        Vector3 targetLocalPosition = transform.localPosition;

        if (allowedAxis == MoveAxis.LeftRight_X)
        {
            float targetX = handLocalPos.x + initialLocalGrabOffset.x;
            targetLocalPosition.x = Mathf.Clamp(targetX, minLimit, maxLimit);
        }
        else if (allowedAxis == MoveAxis.FrontBack_Z)
        {
            float targetZ = handLocalPos.z + initialLocalGrabOffset.z;
            targetLocalPosition.z = Mathf.Clamp(targetZ, minLimit, maxLimit);
        }

        // Apply clamped position smoothly
        transform.localPosition = targetLocalPosition;
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
}

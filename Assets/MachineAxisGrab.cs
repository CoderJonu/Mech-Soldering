using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Constrained two-axis machine interaction. Assign the green handle collider to
/// horizontalHandle, red handle colliders to verticalHandles, and the moving
/// carriage transforms in the Inspector. Holding the controller grip engages a handle.
/// </summary>
public class MachineAxisGrab : MonoBehaviour
{
    [Header("Moving parts")]
    [SerializeField] private Transform horizontalCarriage;
    [SerializeField] private Transform verticalCarriage;

    [Header("Grab surfaces")]
    [SerializeField] private Collider horizontalHandle;
    [SerializeField] private Collider[] verticalHandles;

    [Header("Controller transforms")]
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;

    [Header("Travel")]
    [SerializeField] private Vector3 horizontalAxis = Vector3.right;
    [SerializeField] private Vector3 verticalAxis = Vector3.up;
    [SerializeField] private float maxGrabDistance = 0.12f;
    [SerializeField] private bool useTravelLimits;
    [SerializeField] private float horizontalMin;
    [SerializeField] private float horizontalMax = 1f;
    [SerializeField] private float verticalMin;
    [SerializeField] private float verticalMax = 1f;

    private InputAction leftGrip;
    private InputAction rightGrip;
    private GrabState leftState;
    private GrabState rightState;
    private Vector3 horizontalOrigin;
    private Vector3 verticalOrigin;

    private enum Axis { None, Horizontal, Vertical }
    private struct GrabState
    {
        public Axis axis;
        public Vector3 lastControllerPosition;
    }

    private void Awake()
    {
        leftGrip = new InputAction("LeftGrip", InputActionType.Button, "<XRController>{LeftHand}/gripPressed");
        rightGrip = new InputAction("RightGrip", InputActionType.Button, "<XRController>{RightHand}/gripPressed");
    }

    private void OnEnable()
    {
        leftGrip.Enable();
        rightGrip.Enable();
    }

    private void Start()
    {
        if (horizontalCarriage != null) horizontalOrigin = horizontalCarriage.position;
        if (verticalCarriage != null) verticalOrigin = verticalCarriage.position;
    }

    private void OnDisable()
    {
        leftGrip?.Disable();
        rightGrip?.Disable();
    }

    private void OnDestroy()
    {
        leftGrip?.Dispose();
        rightGrip?.Dispose();
    }

    private void Update()
    {
        UpdateHand(leftController, leftGrip, ref leftState);
        UpdateHand(rightController, rightGrip, ref rightState);
    }

    private void UpdateHand(Transform hand, InputAction grip, ref GrabState state)
    {
        if (hand == null || grip == null) return;
        if (!grip.IsPressed())
        {
            state.axis = Axis.None;
            return;
        }

        if (state.axis == Axis.None)
        {
            state.axis = IsNear(horizontalHandle, hand.position) ? Axis.Horizontal : Axis.None;
            if (state.axis == Axis.None && verticalHandles != null)
            {
                foreach (Collider handle in verticalHandles)
                {
                    if (IsNear(handle, hand.position)) { state.axis = Axis.Vertical; break; }
                }
            }
            state.lastControllerPosition = hand.position;
            return;
        }

        Vector3 delta = hand.position - state.lastControllerPosition;
        state.lastControllerPosition = hand.position;
        if (state.axis == Axis.Horizontal)
            MoveOnAxis(horizontalCarriage, horizontalAxis, horizontalOrigin, delta, horizontalMin, horizontalMax);
        else
            MoveOnAxis(verticalCarriage, verticalAxis, verticalOrigin, delta, verticalMin, verticalMax);
    }

    private bool IsNear(Collider handle, Vector3 position)
    {
        return handle != null && Vector3.Distance(handle.ClosestPoint(position), position) <= maxGrabDistance;
    }

    private void MoveOnAxis(Transform part, Vector3 axis, Vector3 origin, Vector3 delta, float min, float max)
    {
        if (part == null || axis.sqrMagnitude < 0.0001f) return;
        axis.Normalize();
        float amount = Vector3.Dot(delta, axis);
        Vector3 next = part.position + axis * amount;
        if (useTravelLimits)
        {
            float offset = Vector3.Dot(next - origin, axis);
            next += axis * (Mathf.Clamp(offset, min, max) - offset);
        }
        part.position = next;
    }
}

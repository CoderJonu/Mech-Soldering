using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Simple room-scale XR rig: headset tracking moves the camera naturally, while the
/// left stick moves through the room and the right stick snap-turns. It also creates
/// visible controller models which follow the real left and right controllers.
/// </summary>
public sealed class VRPlayerRigController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float moveSpeed = 1.5f;
    [SerializeField, Range(15f, 90f)] private float snapTurnDegrees = 45f;
    [SerializeField] private Transform head;

    private float lastTurnTime;

    public void SetHead(Transform headTransform) => head = headTransform;

    private void Awake()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;

        CreateHand("Left XR Controller", XRNode.LeftHand, new Color(0.15f, 0.55f, 1f), new Vector3(-0.24f, -0.22f, 0.42f));
        CreateHand("Right XR Controller", XRNode.RightHand, new Color(1f, 0.36f, 0.12f), new Vector3(0.24f, -0.22f, 0.42f));
    }

    private void Update()
    {
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        leftHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 moveInput);

        // Keyboard controls keep the project easy to test in the Unity Editor.
        moveInput += new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        Vector3 forward = head == null ? transform.forward : head.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);
        transform.position += (forward * moveInput.y + right * moveInput.x) * moveSpeed * Time.deltaTime;

        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        rightHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 turnInput);
        if (Mathf.Abs(turnInput.x) > 0.7f && Time.time - lastTurnTime > 0.3f)
        {
            transform.RotateAround(head == null ? transform.position : head.position, Vector3.up, Mathf.Sign(turnInput.x) * snapTurnDegrees);
            lastTurnTime = Time.time;
        }
    }

    private void CreateHand(string name, XRNode node, Color colour, Vector3 desktopOffset)
    {
        GameObject hand = new GameObject(name);
        hand.transform.SetParent(transform, false);
        XRControllerVisual visual = hand.AddComponent<XRControllerVisual>();
        visual.Configure(node, head, desktopOffset);

        // A compact controller silhouette rather than the old rod placeholder.
        CreatePart(hand.transform, "Controller Body", PrimitiveType.Sphere, new Vector3(0f, 0.025f, 0f),
            Vector3.zero, new Vector3(0.085f, 0.06f, 0.11f), colour);
        CreatePart(hand.transform, "Controller Handle", PrimitiveType.Cube, new Vector3(0f, -0.07f, -0.02f),
            new Vector3(15f, 0f, 0f), new Vector3(0.055f, 0.14f, 0.065f), colour * 0.7f);
        CreatePart(hand.transform, "Thumbstick", PrimitiveType.Cylinder, new Vector3(0f, 0.068f, 0.004f),
            Vector3.zero, new Vector3(0.028f, 0.012f, 0.028f), Color.black);
        CreatePart(hand.transform, "Trigger", PrimitiveType.Cube, new Vector3(0f, 0.014f, 0.062f),
            new Vector3(20f, 0f, 0f), new Vector3(0.042f, 0.026f, 0.046f), Color.white);
        CreateTrackingRing(hand.transform, colour);
    }

    private static void CreatePart(Transform parent, string partName, PrimitiveType shape, Vector3 position,
        Vector3 rotation, Vector3 scale, Color colour)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localEulerAngles = rotation;
        part.transform.localScale = scale;
        Destroy(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().material.color = colour;
    }

    private static void CreateTrackingRing(Transform parent, Color colour)
    {
        GameObject ring = new GameObject("Tracking Ring");
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = new Vector3(0f, 0.075f, 0f);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        LineRenderer line = ring.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 20;
        line.startWidth = 0.009f;
        line.endWidth = 0.009f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = colour;
        line.endColor = colour;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.095f, Mathf.Sin(angle) * 0.065f, 0f));
        }
    }
}

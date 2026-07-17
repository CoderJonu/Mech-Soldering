using UnityEngine;
using UnityEngine.XR;

/// <summary>Tracks an XR controller pose and provides a desktop fallback pose.</summary>
public sealed class XRControllerVisual : MonoBehaviour
{
    private static readonly InputFeatureUsage<Vector3> GripPosition = new InputFeatureUsage<Vector3>("gripPosition");
    private static readonly InputFeatureUsage<Quaternion> GripRotation = new InputFeatureUsage<Quaternion>("gripRotation");

    private XRNode node;
    private Transform head;
    private Vector3 desktopOffset;
    private bool configured;

    public void Configure(XRNode controllerNode, Transform headTransform, Vector3 fallbackOffset)
    {
        node = controllerNode;
        head = headTransform;
        desktopOffset = fallbackOffset;
        configured = true;
    }

    private void LateUpdate()
    {
        if (!configured)
            return;

        if (TryGetControllerPose(out Vector3 position, out Quaternion rotation))
        {
            transform.localPosition = position;
            transform.localRotation = rotation;
            return;
        }

        // The visible models remain useful in editor/desktop demonstrations.
        transform.position = head == null ? transform.TransformPoint(desktopOffset) : head.TransformPoint(desktopOffset);
        transform.rotation = head == null ? Quaternion.identity : head.rotation;
    }

    private bool TryGetControllerPose(out Vector3 position, out Quaternion rotation)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        position = Vector3.zero;
        rotation = Quaternion.identity;

        // Different OpenXR runtimes expose the hand pose as either grip or device pose.
        bool hasPosition = device.isValid &&
            (device.TryGetFeatureValue(GripPosition, out position) ||
             device.TryGetFeatureValue(CommonUsages.devicePosition, out position));
        bool hasRotation = device.isValid &&
            (device.TryGetFeatureValue(GripRotation, out rotation) ||
             device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation));
        if (hasPosition && hasRotation)
            return true;

        // Fallback for runtimes that only publish pose data through InputTracking.
        position = InputTracking.GetLocalPosition(node);
        rotation = InputTracking.GetLocalRotation(node);
        return position != Vector3.zero || rotation != Quaternion.identity;
    }
}

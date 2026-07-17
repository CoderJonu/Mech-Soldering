using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Creates a virtual PCB, records the solder pads selected by the user, and exports
/// a simple millimetre G-code program.  It is intentionally independent of a
/// specific soldering machine: configure the heights, feed rates and tool commands
/// for the real machine before using an exported file on hardware.
/// </summary>
public sealed class VRPcbSolderingManager : MonoBehaviour
{
    private static readonly InputFeatureUsage<Vector3> GripPosition = new InputFeatureUsage<Vector3>("gripPosition");
    private static readonly InputFeatureUsage<Quaternion> GripRotation = new InputFeatureUsage<Quaternion>("gripRotation");

    [Header("PCB")]
    [SerializeField] private Vector2 boardSizeMm = new Vector2(180f, 120f);
    [SerializeField] private Vector3 boardWorldPosition = new Vector3(0f, 0.98f, 1.25f);
    [SerializeField, Min(0.01f)] private float worldMetresPerBoardMetre = 1f;
    [SerializeField] private Material boardMaterial;
    [SerializeField] private Material markerMaterial;

    [Header("G-code")]
    [SerializeField, Min(0f)] private float travelHeightMm = 5f;
    [SerializeField, Min(0f)] private float solderHeightMm = 0.2f;
    [SerializeField, Min(1f)] private float travelFeedMmPerMin = 2400f;
    [SerializeField, Min(1f)] private float solderFeedMmPerMin = 300f;
    [SerializeField, Min(0)] private int dwellMilliseconds = 750;
    [Tooltip("Optional machine-specific command placed before each solder dwell, e.g. M3 S255.")]
    [SerializeField] private string toolOnCommand = "";
    [Tooltip("Optional machine-specific command placed after each solder dwell, e.g. M5.")]
    [SerializeField] private string toolOffCommand = "";
    [SerializeField] private string exportFileName = "vr_pcb_solder_points.gcode.txt";

    private readonly List<Vector2> markedPadsMm = new List<Vector2>();
    private Transform board;
    private Camera mainCamera;
    private bool wasTriggerPressed;
    private string lastExportPath;

    public IReadOnlyList<Vector2> MarkedPadsMm => markedPadsMm;
    public string LastExportPath => lastExportPath;

    private void Awake()
    {
        CreateBoard();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        bool triggerPressed = GetRightTriggerPressed();
        if (triggerPressed && !wasTriggerPressed)
            TryMarkFromRightController();
        wasTriggerPressed = triggerPressed;

        // Editor/desktop fallback: click on the PCB, Backspace removes the last pad,
        // and E exports. This makes it possible to demonstrate without a headset.
        if (Input.GetMouseButtonDown(0))
            TryMarkFromMouse();
        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete))
            RemoveLastPad();
        if (Input.GetKeyDown(KeyCode.E))
            ExportGCode();
    }

    public void TryMarkFromRightController()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;
        bool hasPosition = device.isValid &&
            (device.TryGetFeatureValue(GripPosition, out position) ||
             device.TryGetFeatureValue(CommonUsages.devicePosition, out position));
        bool hasRotation = device.isValid &&
            (device.TryGetFeatureValue(GripRotation, out rotation) ||
             device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation));
        if (!hasPosition || !hasRotation)
        {
            position = InputTracking.GetLocalPosition(XRNode.RightHand);
            rotation = InputTracking.GetLocalRotation(XRNode.RightHand);
            if (position == Vector3.zero && rotation == Quaternion.identity)
                return;
        }

        // OpenXR poses are relative to the tracking origin. Convert them through the
        // movable player rig so marking still works after walking or snap-turning.
        VRPlayerRigController rig = FindObjectOfType<VRPlayerRigController>();
        if (rig != null)
        {
            position = rig.transform.TransformPoint(position);
            rotation = rig.transform.rotation * rotation;
        }
        TryMark(new Ray(position, rotation * Vector3.forward));
    }

    public void TryMarkFromMouse()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera != null)
            TryMark(mainCamera.ScreenPointToRay(Input.mousePosition));
    }

    public void TryMark(Ray ray)
    {
        if (board == null || !Physics.Raycast(ray, out RaycastHit hit, 5f) || hit.transform != board)
            return;

        Vector3 local = board.InverseTransformPoint(hit.point);
        Vector3 scale = board.localScale;
        float xMm = Mathf.Clamp((local.x / scale.x + 0.5f) * boardSizeMm.x, 0f, boardSizeMm.x);
        float yMm = Mathf.Clamp((local.z / scale.z + 0.5f) * boardSizeMm.y, 0f, boardSizeMm.y);
        AddPad(new Vector2(xMm, yMm));
    }

    public void AddPad(Vector2 padMm)
    {
        markedPadsMm.Add(padMm);
        CreateMarker(padMm, markedPadsMm.Count);
        Debug.Log($"VR PCB: marked pad {markedPadsMm.Count} at X{padMm.x:F2} Y{padMm.y:F2} mm.");
    }

    public void RemoveLastPad()
    {
        if (markedPadsMm.Count == 0)
            return;
        markedPadsMm.RemoveAt(markedPadsMm.Count - 1);
        Transform marker = board.Find("PadMarker_" + (markedPadsMm.Count + 1));
        if (marker != null)
            Destroy(marker.gameObject);
    }

    public void ClearPads()
    {
        markedPadsMm.Clear();
        for (int i = board.childCount - 1; i >= 0; i--)
            Destroy(board.GetChild(i).gameObject);
    }

    public void ExportGCode()
    {
        if (markedPadsMm.Count == 0)
        {
            Debug.LogWarning("VR PCB: no solder points have been marked, so no G-code was exported.");
            return;
        }

        string path = Path.Combine(Application.persistentDataPath, exportFileName);
        File.WriteAllText(path, BuildGCode(), Encoding.UTF8);
        lastExportPath = path;
        Debug.Log("VR PCB: G-code exported to " + path);
    }

    public string BuildGCode()
    {
        StringBuilder gcode = new StringBuilder();
        gcode.AppendLine("; Generated by VR PCB Soldering");
        gcode.AppendLine("; Coordinates are millimetres, board origin is lower-left corner.");
        gcode.AppendLine("G21 ; millimetres");
        gcode.AppendLine("G90 ; absolute positioning");
        gcode.AppendLine("G0 Z" + F(travelHeightMm));

        for (int i = 0; i < markedPadsMm.Count; i++)
        {
            Vector2 pad = markedPadsMm[i];
            gcode.AppendLine($"; Pad {i + 1}");
            gcode.AppendLine($"G0 X{F(pad.x)} Y{F(pad.y)} F{F(travelFeedMmPerMin)}");
            gcode.AppendLine($"G1 Z{F(solderHeightMm)} F{F(solderFeedMmPerMin)}");
            if (!string.IsNullOrWhiteSpace(toolOnCommand)) gcode.AppendLine(toolOnCommand.Trim());
            if (dwellMilliseconds > 0) gcode.AppendLine("G4 P" + dwellMilliseconds);
            if (!string.IsNullOrWhiteSpace(toolOffCommand)) gcode.AppendLine(toolOffCommand.Trim());
            gcode.AppendLine("G0 Z" + F(travelHeightMm));
        }

        gcode.AppendLine("M400 ; finish queued moves");
        gcode.AppendLine("M2 ; end program");
        return gcode.ToString();
    }

    private bool GetRightTriggerPressed()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        return device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed;
    }

    private void CreateBoard()
    {
        GameObject boardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boardObject.name = "Virtual PCB - Click / Trigger to Mark";
        boardObject.transform.SetParent(transform, false);
        boardObject.transform.position = boardWorldPosition;
        boardObject.transform.localScale = new Vector3(boardSizeMm.x / 1000f * worldMetresPerBoardMetre, 0.006f, boardSizeMm.y / 1000f * worldMetresPerBoardMetre);
        board = boardObject.transform;
        if (boardMaterial != null)
            boardObject.GetComponent<Renderer>().material = boardMaterial;
        else
            boardObject.GetComponent<Renderer>().material.color = new Color(0.02f, 0.32f, 0.15f);
    }

    private void CreateMarker(Vector2 padMm, int index)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "PadMarker_" + index;
        marker.transform.SetParent(board, false);
        marker.transform.localPosition = new Vector3((padMm.x / boardSizeMm.x - 0.5f) * board.localScale.x, 0.8f, (padMm.y / boardSizeMm.y - 0.5f) * board.localScale.z);
        marker.transform.localScale = Vector3.one * 0.035f;
        Destroy(marker.GetComponent<Collider>());
        if (markerMaterial != null)
            marker.GetComponent<Renderer>().material = markerMaterial;
        else
            marker.GetComponent<Renderer>().material.color = Color.yellow;
    }

    private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

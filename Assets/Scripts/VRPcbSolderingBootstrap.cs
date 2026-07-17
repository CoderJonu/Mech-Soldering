using UnityEngine;

/// <summary>Ensures the marking workflow is present when the sample scene runs.</summary>
public static class VRPcbSolderingBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateManager()
    {
        Camera camera = FindOrCreateCamera();
        CreatePlayerRig(camera);
        if (Object.FindObjectOfType<VRPcbSolderingManager>() != null)
            return;
        new GameObject("VR PCB Soldering Manager").AddComponent<VRPcbSolderingManager>();
    }

    private static Camera FindOrCreateCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
            camera = Object.FindObjectOfType<Camera>();
        if (camera != null)
        {
            camera.gameObject.tag = "MainCamera";
            return camera;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 1.55f, -1f);
        cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
        camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        return camera;
    }

    private static void CreatePlayerRig(Camera camera)
    {
        if (Object.FindObjectOfType<VRPlayerRigController>() != null)
            return;

        GameObject rig = new GameObject("XR Player Rig - Move with Left Stick");
        // The headset already supplies its real-world eye height. Keeping the rig at
        // floor level avoids adding the old scene-camera Y position a second time.
        // The small negative offset gives a more comfortable seated/standing view.
        rig.transform.position = new Vector3(camera.transform.position.x, 0.50f, camera.transform.position.z);
        rig.transform.rotation = Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f);
        camera.transform.SetParent(rig.transform, true);
        VRPlayerRigController controller = rig.AddComponent<VRPlayerRigController>();
        controller.SetHead(camera.transform);
    }
}

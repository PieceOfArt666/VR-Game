using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;
using TMPro;
using System.IO;
using System.Collections.Generic;

[InitializeOnLoad]
public static class VRSceneCreator
{
    public const string ScenePath = "Assets/Scenes/vr.unity";
    private const string AutoRunPrefKey = "VR_VRScene_Created_Timestamp_v1";

    static VRSceneCreator()
    {
        EditorApplication.delayCall += AutoBuildIfRequested;
    }

    private static void AutoBuildIfRequested()
    {
        if (!EditorPrefs.GetBool(AutoRunPrefKey, false))
        {
            EditorPrefs.SetBool(AutoRunPrefKey, true);
            BuildVRScene();
        }
    }

    [MenuItem("VR Assignment/Create 'vr' Scene from Guide")]
    public static void BuildVRScene()
    {
        Debug.Log("<color=cyan>[VR Setup] Creating new scene 'vr.unity' strictly matching the ChatGPT Guide...</color>");

        if (!Directory.Exists("Assets/Scenes"))
            Directory.CreateDirectory("Assets/Scenes");
        if (!Directory.Exists("Assets/Materials"))
            Directory.CreateDirectory("Assets/Materials");

        // 1. Create a fresh new scene
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Directional Light
        GameObject lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.98f, 0.92f);
        light.intensity = 1.1f;
        lightGo.transform.position = new Vector3(0f, 3f, 0f);
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 3. Ground Plane
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Plane";
        plane.transform.position = Vector3.zero;
        plane.transform.localScale = new Vector3(2f, 1f, 2f);
        Material floorMat = CreateOrGetMaterial("FloorMat", new Color(0.28f, 0.30f, 0.35f));
        plane.GetComponent<Renderer>().sharedMaterial = floorMat;

        // 4. XR Interaction Manager
        GameObject xriGo = new GameObject("XR Interaction Manager");
        XRInteractionManager interactionManager = xriGo.AddComponent<XRInteractionManager>();

        // 5. Input Action Manager
        GameObject iamGo = new GameObject("Input Action Manager");
        InputActionManager iam = iamGo.AddComponent<InputActionManager>();
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/XRI Default Input Actions.inputactions");
        if (inputActions != null)
        {
            iam.actionAssets = new List<InputActionAsset> { inputActions };
        }

        // 6. XR Origin (XR Rig) Prefab
        string rigPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rigPrefabPath);
        GameObject xrOriginGo = null;
        if (rigPrefab != null)
        {
            xrOriginGo = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            xrOriginGo.name = "XR Origin";
            xrOriginGo.transform.position = Vector3.zero;
            xrOriginGo.transform.rotation = Quaternion.identity;
        }

        // 7. XR Device Simulator (for desktop/mouse testing as in Guide Step 10)
        string simPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/XR Device Simulator/XR Device Simulator.prefab";
        if (!File.Exists(simPrefabPath))
            simPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Device Simulator.prefab";

        GameObject simPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(simPrefabPath);
        if (simPrefab != null)
        {
            GameObject simInstance = (GameObject)PrefabUtility.InstantiatePrefab(simPrefab);
            simInstance.name = "XR Device Simulator";
        }

        // 8. Materials for Environment & Containers
        Material tableMat = CreateOrGetMaterial("TableMat", new Color(0.2f, 0.22f, 0.25f));
        Material matA = CreateOrGetMaterial("ContainerA_Mat", new Color(0.45f, 0.50f, 0.55f));
        Material matB = CreateOrGetMaterial("ContainerB_Mat", new Color(0.15f, 0.65f, 0.45f));
        Material matC = CreateOrGetMaterial("ContainerC_Mat", new Color(0.85f, 0.35f, 0.35f));
        Material cubeMat = CreateOrGetMaterial("InteractiveCube_Mat", new Color(0.13f, 0.77f, 0.36f));

        // 9. Table (Step 4.1: Position (0, 0.75, 1.6), Scale (1.8, 0.1, 0.65))
        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Table";
        table.transform.position = new Vector3(0f, 0.75f, 1.6f);
        table.transform.localScale = new Vector3(1.8f, 0.1f, 0.65f);
        table.GetComponent<Renderer>().sharedMaterial = tableMat;

        // 10. Containers (Step 4.2 - 4.5)
        GameObject containerA = CreateContainer("Container_A", new Vector3(-0.55f, 0.82f, 1.6f), matA, false, out _);
        GameObject containerB = CreateContainer("Container_B", new Vector3(0f, 0.82f, 1.6f), matB, true, out Transform snapPoint);
        GameObject containerC = CreateContainer("Container_C", new Vector3(0.55f, 0.82f, 1.6f), matC, false, out _);

        // 11. World Space VR UI Canvas (Step 6)
        GameObject canvasGo = new GameObject("Canvas");
        canvasGo.transform.position = new Vector3(0f, 1.5f, 2.05f);
        canvasGo.transform.rotation = Quaternion.identity; // Facing player camera directly
        canvasGo.transform.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 350f);

        // Subtle dark translucent background card
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.09f, 0.14f, 0.88f);
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // InstructionsText (Step 6.2)
        GameObject instrGo = new GameObject("InstructionsText");
        instrGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI instrTmp = instrGo.AddComponent<TextMeshProUGUI>();
        instrTmp.text = "<b>VR CUBE PLACEMENT</b>\n\n• Grab the cube from Container A.\n• Move it using your VR controller.\n• Drop it in the correct container.";
        instrTmp.fontSize = 26;
        instrTmp.alignment = TextAlignmentOptions.TopLeft;
        instrTmp.color = Color.white;
        RectTransform instrRect = instrGo.GetComponent<RectTransform>();
        instrRect.anchorMin = new Vector2(0.06f, 0.40f);
        instrRect.anchorMax = new Vector2(0.94f, 0.94f);
        instrRect.offsetMin = Vector2.zero;
        instrRect.offsetMax = Vector2.zero;

        // FeedbackText (Step 6.2)
        GameObject feedbackGo = new GameObject("FeedbackText");
        feedbackGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI feedbackTmp = feedbackGo.AddComponent<TextMeshProUGUI>();
        feedbackTmp.text = "Waiting for interaction...";
        feedbackTmp.fontSize = 30;
        feedbackTmp.fontStyle = FontStyles.Bold;
        feedbackTmp.alignment = TextAlignmentOptions.Center;
        feedbackTmp.color = new Color(0.2f, 0.85f, 1f);
        RectTransform fbRect = feedbackGo.GetComponent<RectTransform>();
        fbRect.anchorMin = new Vector2(0.06f, 0.08f);
        fbRect.anchorMax = new Vector2(0.94f, 0.38f);
        fbRect.offsetMin = Vector2.zero;
        fbRect.offsetMax = Vector2.zero;

        // 12. Interactive Cube (Step 5)
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "InteractiveCube";
        cube.transform.position = new Vector3(-0.55f, 0.91f, 1.6f);
        cube.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        cube.GetComponent<Renderer>().sharedMaterial = cubeMat;

        Rigidbody rb = cube.GetComponent<Rigidbody>();
        if (rb == null) rb = cube.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;

        XRGrabInteractable grab = cube.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = false;
        grab.interactionManager = interactionManager;

        BoxCollider boxCol = cube.GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            grab.colliders.Clear();
            grab.colliders.Add(boxCol);
        }

        AudioSource audioSource = cube.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;

        AudioClip soundClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/grab_sound.wav");
        if (soundClip != null)
            audioSource.clip = soundClip;

        CubeInteraction interaction = cube.AddComponent<CubeInteraction>();
        interaction.cubeRenderer = cube.GetComponent<Renderer>();
        interaction.audioSource = audioSource;
        interaction.grabSound = soundClip;
        interaction.feedbackText = feedbackTmp;
        interaction.defaultColor = new Color(0.13f, 0.77f, 0.36f, 1f); // Green unhovered
        interaction.hoverColor = new Color(0f, 0.65f, 1f, 1f);          // Blue hovered
        interaction.hoverScale = 1.15f;
        interaction.hoverSpeed = 8f;
        interaction.successColor = new Color(0.13f, 0.77f, 0.36f, 1f); // Green success
        interaction.failureColor = new Color(0.97f, 0.30f, 0.30f, 1f); // Red failure

        // 13. Save Scene to Assets/Scenes/vr.unity
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"<color=green>[VR Setup] Successfully created and saved scene at: {ScenePath}</color>");

        // 14. Ensure vr.unity is added to Build Settings
        AddSceneToBuildSettings(ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static GameObject CreateContainer(string name, Vector3 position, Material mat, bool isCorrect, out Transform snapPointTransform)
    {
        snapPointTransform = null;
        GameObject container = new GameObject(name);
        container.transform.position = position;

        // Shallow open-top box with 5 cube children as specified in Step 4.3:
        // Bottom: Local Pos (0, 0, 0), Local Scale (0.34, 0.04, 0.34)
        CreateWall(container, "Bottom", new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.04f, 0.34f), mat);

        // LeftWall: Local Pos (-0.17, 0.125, 0), Local Scale (0.035, 0.25, 0.34)
        CreateWall(container, "LeftWall", new Vector3(-0.17f, 0.125f, 0f), new Vector3(0.035f, 0.25f, 0.34f), mat);

        // RightWall: Local Pos (0.17, 0.125, 0), Local Scale (0.035, 0.25, 0.34)
        CreateWall(container, "RightWall", new Vector3(0.17f, 0.125f, 0f), new Vector3(0.035f, 0.25f, 0.34f), mat);

        // FrontWall: Local Pos (0, 0.125, -0.17), Local Scale (0.34, 0.25, 0.035)
        CreateWall(container, "FrontWall", new Vector3(0f, 0.125f, -0.17f), new Vector3(0.34f, 0.25f, 0.035f), mat);

        // BackWall: Local Pos (0, 0.125, 0.17), Local Scale (0.34, 0.25, 0.035)
        CreateWall(container, "BackWall", new Vector3(0f, 0.125f, 0.17f), new Vector3(0.34f, 0.25f, 0.035f), mat);

        // Step 4.4: TriggerZone child
        // Local Pos (0, 0.15, 0), Box Collider isTrigger = true, size (0.30, 0.24, 0.30)
        GameObject triggerZone = new GameObject("TriggerZone");
        triggerZone.transform.SetParent(container.transform, false);
        triggerZone.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        BoxCollider box = triggerZone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.30f, 0.24f, 0.30f);

        ContainerZone zone = triggerZone.AddComponent<ContainerZone>();
        zone.isCorrect = isCorrect;

        // Step 4.5: SnapPoint in Container_B at local position (0, 0.09, 0)
        if (isCorrect)
        {
            GameObject snap = new GameObject("SnapPoint");
            snap.transform.SetParent(container.transform, false);
            snap.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            snapPointTransform = snap.transform;
            zone.snapPoint = snapPointTransform;
        }

        return container;
    }

    private static void CreateWall(GameObject parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent.transform, false);
        wall.transform.localPosition = localPos;
        wall.transform.localScale = localScale;
        wall.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static Material CreateOrGetMaterial(string name, Color color)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
        }
        return mat;
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool found = false;
        foreach (var s in scenes)
        {
            if (s.path == scenePath)
            {
                s.enabled = true;
                found = true;
                break;
            }
        }
        if (!found)
        {
            // Insert vr scene as first (index 0)
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

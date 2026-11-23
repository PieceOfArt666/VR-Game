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
public static class TestSceneCreator
{
    public const string ScenePath = "Assets/Scenes/test scene.unity";
    private const string AutoRunPrefKey = "VR_TestScene_Created_Timestamp_v5";

    static TestSceneCreator()
    {
        EditorApplication.delayCall += AutoBuildIfRequested;
    }

    private static void AutoBuildIfRequested()
    {
        if (!EditorPrefs.GetBool(AutoRunPrefKey, false))
        {
            EditorPrefs.SetBool(AutoRunPrefKey, true);
            BuildCompleteTestScene();
        }
    }

    [MenuItem("VR Assignment/Redo Everything in 'test scene'")]
    public static void BuildCompleteTestScene()
    {
        Debug.Log("<color=cyan>[VR Setup] Building complete 'test scene' with Interactive Tutorial & UI Materials...</color>");

        if (!Directory.Exists("Assets/Scenes"))
            Directory.CreateDirectory("Assets/Scenes");
        if (!Directory.Exists("Assets/Materials"))
            Directory.CreateDirectory("Assets/Materials");
        if (!Directory.Exists("Assets/Materials/UI"))
            Directory.CreateDirectory("Assets/Materials/UI");

        // 1. Copy BasicScene if test scene does not exist yet
        if (!File.Exists(ScenePath))
        {
            if (File.Exists("Assets/Scenes/BasicScene.unity"))
            {
                AssetDatabase.CopyAsset("Assets/Scenes/BasicScene.unity", ScenePath);
                AssetDatabase.Refresh();
            }
        }

        // 2. Open or create the scene
        Scene scene;
        if (File.Exists(ScenePath))
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // 3. Ensure Environment / Ground exists
        GameObject plane = GameObject.Find("Plane");
        if (plane == null)
        {
            plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Plane";
            plane.transform.position = Vector3.zero;
            plane.transform.localScale = new Vector3(2f, 1f, 2f);
        }

        // 4. Ensure XR Interaction Manager exists
        XRInteractionManager interactionManager = Object.FindAnyObjectByType<XRInteractionManager>();
        if (interactionManager == null)
        {
            GameObject xriGo = new GameObject("XR Interaction Manager");
            interactionManager = xriGo.AddComponent<XRInteractionManager>();
        }

        // 5. Ensure Input Action Manager exists with XRI Default Input Actions enabled!
        GameObject iamGo = GameObject.Find("Input Action Manager");
        if (iamGo == null)
        {
            iamGo = new GameObject("Input Action Manager");
        }
        InputActionManager iam = iamGo.GetComponent<InputActionManager>();
        if (iam == null)
        {
            iam = iamGo.AddComponent<InputActionManager>();
        }
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/XRI Default Input Actions.inputactions");
        if (inputActions != null)
        {
            iam.actionAssets = new List<InputActionAsset> { inputActions };
        }

        // 6. Ensure XR Origin exists
        Transform playerCam = null;
        GameObject xrOriginGo = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin");
        if (xrOriginGo == null)
        {
            string rigPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rigPrefabPath);
            if (rigPrefab != null)
            {
                xrOriginGo = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
                xrOriginGo.name = "XR Origin (XR Rig)";
                xrOriginGo.transform.position = Vector3.zero;
                xrOriginGo.transform.rotation = Quaternion.identity;

                GameObject defaultCam = GameObject.Find("Main Camera");
                if (defaultCam != null && defaultCam.transform.parent == null)
                {
                    Object.DestroyImmediate(defaultCam);
                }
            }
        }

        if (Camera.main != null)
            playerCam = Camera.main.transform;

        // 7. Ensure XR Device Simulator is in the scene
        GameObject existingSim = GameObject.Find("XR Device Simulator") ?? GameObject.Find("XR Device Simulator(Clone)");
        if (existingSim == null)
        {
            string simPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/XR Device Simulator/XR Device Simulator.prefab";
            if (!File.Exists(simPrefabPath))
                simPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Device Simulator.prefab";

            GameObject simPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(simPrefabPath);
            if (simPrefab != null)
            {
                GameObject simInstance = (GameObject)PrefabUtility.InstantiatePrefab(simPrefab);
                simInstance.name = "XR Device Simulator";
            }
        }

        // 8. Materials for Environment & Containers
        Material tableMat = CreateOrGetMaterial("TableMat", new Color(0.2f, 0.22f, 0.25f));
        Material matA = CreateOrGetMaterial("ContainerA_Mat", new Color(0.45f, 0.5f, 0.55f));
        Material matB = CreateOrGetMaterial("ContainerB_Mat", new Color(0.15f, 0.65f, 0.45f));
        Material matC = CreateOrGetMaterial("ContainerC_Mat", new Color(0.85f, 0.35f, 0.35f));
        Material cubeMat = CreateOrGetMaterial("InteractiveCube_Mat", new Color(0.95f, 0.65f, 0.15f));

        // 9. Table
        GameObject oldTable = GameObject.Find("Table");
        if (oldTable != null) Object.DestroyImmediate(oldTable);
        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Table";
        table.transform.position = new Vector3(0f, 0.75f, 1.6f);
        table.transform.localScale = new Vector3(1.8f, 0.1f, 0.65f);
        table.GetComponent<Renderer>().sharedMaterial = tableMat;

        // 10. Containers (shallow rim walls on Ignore Raycast layer)
        GameObject containerA = CreateContainer("Container_A", new Vector3(-0.55f, 0.82f, 1.6f), matA, false, out _);
        GameObject containerB = CreateContainer("Container_B", new Vector3(0f, 0.82f, 1.6f), matB, true, out Transform snapPoint);
        GameObject containerC = CreateContainer("Container_C", new Vector3(0.55f, 0.82f, 1.6f), matC, false, out _);

        // 11. Interactive Cube
        GameObject oldCube = GameObject.Find("InteractiveCube");
        if (oldCube != null) Object.DestroyImmediate(oldCube);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "InteractiveCube";
        cube.transform.position = new Vector3(-0.55f, 0.92f, 1.6f);
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
        interaction.defaultColor = new Color(0.13f, 0.77f, 0.36f, 1f);
        interaction.hoverColor = new Color(0f, 0.65f, 1f, 1f);
        interaction.hoverScale = 1.15f;
        interaction.hoverSpeed = 8f;
        interaction.successColor = new Color(0.13f, 0.77f, 0.36f, 1f);
        interaction.failureColor = new Color(0.97f, 0.30f, 0.30f, 1f);

        // 12. World Space UI Canvas with Tutorial UI & UI Materials
        GameObject oldCanvas = GameObject.Find("Canvas_VR_UI");
        if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);

        GameObject canvasGo = new GameObject("Canvas_VR_UI");
        canvasGo.transform.position = new Vector3(0f, 1.55f, 2.05f);
        canvasGo.transform.rotation = Quaternion.identity; // Facing camera directly!
        canvasGo.transform.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(960f, 500f);

        // UI Assets (Sprites & Materials)
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Panel_Background.png");
        Sprite badgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Step_Badge.png");
        Sprite progressSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/ProgressBar_Fill.png");
        Material uiPanelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI/UI_Panel_Mat.mat");
        Material uiGlowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI/UI_Glow_Mat.mat");

        // Background Panel Card
        GameObject bgGo = new GameObject("BackgroundPanel");
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bgImg = bgGo.AddComponent<Image>();
        if (panelSprite != null)
        {
            bgImg.sprite = panelSprite;
            bgImg.type = Image.Type.Sliced;
        }
        if (uiPanelMat != null) bgImg.material = uiPanelMat;
        bgImg.color = new Color(1f, 1f, 1f, 0.95f);
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Step Badge
        GameObject badgeGo = new GameObject("StepBadge");
        badgeGo.transform.SetParent(canvasGo.transform, false);
        Image badgeImg = badgeGo.AddComponent<Image>();
        if (badgeSprite != null)
        {
            badgeImg.sprite = badgeSprite;
            badgeImg.type = Image.Type.Sliced;
        }
        if (uiGlowMat != null) badgeImg.material = uiGlowMat;
        badgeImg.color = new Color(0f, 0.85f, 1f, 0.85f);
        RectTransform badgeRect = badgeGo.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.04f, 0.84f);
        badgeRect.anchorMax = new Vector2(0.32f, 0.94f);
        badgeRect.sizeDelta = Vector2.zero;

        GameObject badgeTextGo = new GameObject("BadgeText");
        badgeTextGo.transform.SetParent(badgeGo.transform, false);
        TextMeshProUGUI badgeTmp = badgeTextGo.AddComponent<TextMeshProUGUI>();
        badgeTmp.text = "STEP 1 / 6";
        badgeTmp.fontSize = 20;
        badgeTmp.fontStyle = FontStyles.Bold;
        badgeTmp.alignment = TextAlignmentOptions.Center;
        badgeTmp.color = Color.white;
        RectTransform badgeTextRect = badgeTextGo.GetComponent<RectTransform>();
        badgeTextRect.anchorMin = Vector2.zero;
        badgeTextRect.anchorMax = Vector2.one;
        badgeTextRect.sizeDelta = Vector2.zero;

        // Progress Bar Track
        GameObject trackGo = new GameObject("ProgressTrack");
        trackGo.transform.SetParent(canvasGo.transform, false);
        Image trackImg = trackGo.AddComponent<Image>();
        trackImg.color = new Color(0.08f, 0.12f, 0.18f, 0.9f);
        RectTransform trackRect = trackGo.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.35f, 0.865f);
        trackRect.anchorMax = new Vector2(0.96f, 0.915f);
        trackRect.sizeDelta = Vector2.zero;

        // Progress Bar Fill
        GameObject fillGo = new GameObject("ProgressFill");
        fillGo.transform.SetParent(trackGo.transform, false);
        Image fillImg = fillGo.AddComponent<Image>();
        if (progressSprite != null)
        {
            fillImg.sprite = progressSprite;
            fillImg.type = Image.Type.Sliced;
        }
        fillImg.color = new Color(0f, 0.9f, 1f, 1f);
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0.16f, 1f);
        fillRect.sizeDelta = Vector2.zero;

        // Instruction Title
        GameObject titleGo = new GameObject("InstructionTitle");
        titleGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "Look Around (HMD)";
        titleTmp.fontSize = 32;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.color = new Color(0.2f, 0.95f, 1f);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.04f, 0.68f);
        titleRect.anchorMax = new Vector2(0.96f, 0.82f);
        titleRect.sizeDelta = Vector2.zero;

        // Instruction Body
        GameObject bodyGo = new GameObject("InstructionBody");
        bodyGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI bodyTmp = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyTmp.text = "Hold <b>Right Mouse Button</b> and move your mouse to explore your VR surroundings.";
        bodyTmp.fontSize = 24;
        bodyTmp.lineSpacing = 12;
        bodyTmp.alignment = TextAlignmentOptions.TopLeft;
        bodyTmp.color = new Color(0.9f, 0.95f, 1f);
        RectTransform bodyRect = bodyGo.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.04f, 0.32f);
        bodyRect.anchorMax = new Vector2(0.96f, 0.66f);
        bodyRect.sizeDelta = Vector2.zero;

        // Divider
        GameObject divGo = new GameObject("Divider");
        divGo.transform.SetParent(canvasGo.transform, false);
        Image divImg = divGo.AddComponent<Image>();
        divImg.color = new Color(0f, 0.85f, 1f, 0.35f);
        RectTransform divRect = divGo.GetComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0.04f, 0.28f);
        divRect.anchorMax = new Vector2(0.96f, 0.29f);
        divRect.sizeDelta = Vector2.zero;

        // Feedback Card Text
        GameObject feedbackGo = new GameObject("FeedbackText");
        feedbackGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI feedbackTmp = feedbackGo.AddComponent<TextMeshProUGUI>();
        feedbackTmp.text = "Looking around...";
        feedbackTmp.fontSize = 26;
        feedbackTmp.fontStyle = FontStyles.Bold;
        feedbackTmp.alignment = TextAlignmentOptions.Center;
        feedbackTmp.color = Color.cyan;
        RectTransform fbRect = feedbackGo.GetComponent<RectTransform>();
        fbRect.anchorMin = new Vector2(0.04f, 0.05f);
        fbRect.anchorMax = new Vector2(0.96f, 0.25f);
        fbRect.offsetMin = Vector2.zero;
        fbRect.offsetMax = Vector2.zero;

        interaction.feedbackText = feedbackTmp;

        // Tutorial Manager Component
        TutorialManager tutorial = canvasGo.AddComponent<TutorialManager>();
        tutorial.stepBadgeText = badgeTmp;
        tutorial.instructionTitleText = titleTmp;
        tutorial.instructionBodyText = bodyTmp;
        tutorial.progressBarFill = fillImg;
        tutorial.feedbackText = feedbackTmp;
        tutorial.cubeInteraction = interaction;
        tutorial.playerCamera = playerCam;

        // 13. Save Scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        // 14. Register in Build Settings
        AddSceneToBuildSettings(ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[VR Setup] Successfully created 'test scene.unity' with Tutorial and UI Materials!</color>");
    }

    private static GameObject CreateContainer(string name, Vector3 position, Material mat, bool isCorrect, out Transform snapPointTransform)
    {
        snapPointTransform = null;
        GameObject old = GameObject.Find(name);
        if (old != null) Object.DestroyImmediate(old);

        GameObject container = new GameObject(name);
        container.transform.position = position;

        CreateWall(container, "Bottom", new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.04f, 0.34f), mat);
        CreateWall(container, "LeftWall", new Vector3(-0.17f, 0.04f, 0f), new Vector3(0.035f, 0.06f, 0.34f), mat);
        CreateWall(container, "RightWall", new Vector3(0.17f, 0.04f, 0f), new Vector3(0.035f, 0.06f, 0.34f), mat);
        CreateWall(container, "FrontWall", new Vector3(0f, 0.04f, -0.17f), new Vector3(0.34f, 0.06f, 0.035f), mat);
        CreateWall(container, "BackWall", new Vector3(0f, 0.04f, 0.17f), new Vector3(0.34f, 0.06f, 0.035f), mat);

        GameObject triggerZone = new GameObject("TriggerZone");
        triggerZone.layer = 2; // Ignore Raycast
        triggerZone.transform.SetParent(container.transform, false);
        triggerZone.transform.localPosition = new Vector3(0f, 0.10f, 0f);
        BoxCollider box = triggerZone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.32f, 0.20f, 0.32f);

        ContainerZone zone = triggerZone.AddComponent<ContainerZone>();
        zone.isCorrect = isCorrect;

        if (isCorrect)
        {
            GameObject snap = new GameObject("SnapPoint");
            snap.transform.SetParent(container.transform, false);
            snap.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            snapPointTransform = snap.transform;
            zone.snapPoint = snapPointTransform;
        }

        return container;
    }

    private static void CreateWall(GameObject parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.layer = 2; // Ignore Raycast
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
        foreach (var s in scenes)
        {
            if (s.path == scenePath)
                return;
        }
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

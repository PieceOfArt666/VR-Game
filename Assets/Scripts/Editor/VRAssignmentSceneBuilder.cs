using UnityEngine;
using UnityEditor;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

public static class VRAssignmentSceneBuilder
{
    [MenuItem("VR Assignment/Generate Assignment Scene Objects")]
    public static void GenerateSceneObjects()
    {
        // 1. Materials
        Material tableMat = CreateMaterial("TableMat", new Color(0.2f, 0.22f, 0.25f));
        Material matA = CreateMaterial("ContainerA_Mat", new Color(0.45f, 0.5f, 0.55f));
        Material matB = CreateMaterial("ContainerB_Mat", new Color(0.15f, 0.65f, 0.45f));
        Material matC = CreateMaterial("ContainerC_Mat", new Color(0.85f, 0.35f, 0.35f));
        Material cubeMat = CreateMaterial("InteractiveCube_Mat", new Color(0.95f, 0.65f, 0.15f));

        // 2. Table
        GameObject oldTable = GameObject.Find("Table");
        if (oldTable != null) Object.DestroyImmediate(oldTable);
        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Table";
        table.transform.position = new Vector3(0f, 0.75f, 1.6f);
        table.transform.localScale = new Vector3(1.8f, 0.1f, 0.65f);
        table.GetComponent<Renderer>().sharedMaterial = tableMat;

        // 3. Containers
        GameObject containerA = CreateContainer("Container_A", new Vector3(-0.55f, 0.82f, 1.6f), matA, false, out _);
        GameObject containerB = CreateContainer("Container_B", new Vector3(0f, 0.82f, 1.6f), matB, true, out Transform snapPoint);
        GameObject containerC = CreateContainer("Container_C", new Vector3(0.55f, 0.82f, 1.6f), matC, false, out _);

        // 4. World Space UI Canvas
        GameObject oldCanvas = GameObject.Find("Canvas_VR_UI");
        if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);

        GameObject canvasGo = new GameObject("Canvas_VR_UI");
        canvasGo.transform.position = new Vector3(0f, 1.5f, 2.05f);
        canvasGo.transform.rotation = Quaternion.identity;
        canvasGo.transform.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 400f);

        // Background image
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        UnityEngine.UI.Image bgImage = bgGo.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.08f, 0.12f, 0.18f, 0.92f);
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Instructions Text
        GameObject instrGo = new GameObject("InstructionsText");
        instrGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI instrTmp = instrGo.AddComponent<TextMeshProUGUI>();
        instrTmp.text = "<b>VR CUBE PLACEMENT</b>\n\n• Grab the cube from Container A.\n• Move it using your VR controller.\n• Drop it in the correct container (B).";
        instrTmp.fontSize = 28;
        instrTmp.alignment = TextAlignmentOptions.TopLeft;
        instrTmp.color = Color.white;
        RectTransform instrRect = instrGo.GetComponent<RectTransform>();
        instrRect.anchorMin = new Vector2(0.05f, 0.45f);
        instrRect.anchorMax = new Vector2(0.95f, 0.95f);
        instrRect.offsetMin = Vector2.zero;
        instrRect.offsetMax = Vector2.zero;

        // Feedback Text
        GameObject feedbackGo = new GameObject("FeedbackText");
        feedbackGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI feedbackTmp = feedbackGo.AddComponent<TextMeshProUGUI>();
        feedbackTmp.text = "Waiting for interaction...";
        feedbackTmp.fontSize = 32;
        feedbackTmp.fontStyle = FontStyles.Bold;
        feedbackTmp.alignment = TextAlignmentOptions.Center;
        feedbackTmp.color = Color.cyan;
        RectTransform fbRect = feedbackGo.GetComponent<RectTransform>();
        fbRect.anchorMin = new Vector2(0.05f, 0.08f);
        fbRect.anchorMax = new Vector2(0.95f, 0.40f);
        fbRect.offsetMin = Vector2.zero;
        fbRect.offsetMax = Vector2.zero;

        // 5. Interactive Cube
        GameObject oldCube = GameObject.Find("InteractiveCube");
        if (oldCube != null) Object.DestroyImmediate(oldCube);

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
        interaction.defaultColor = new Color(0.13f, 0.77f, 0.36f, 1f);
        interaction.hoverColor = new Color(0f, 0.65f, 1f, 1f);
        interaction.hoverScale = 1.15f;
        interaction.hoverSpeed = 8f;
        interaction.successColor = new Color(0.13f, 0.77f, 0.36f, 1f);
        interaction.failureColor = new Color(0.97f, 0.30f, 0.30f, 1f);

        // Mark scene dirty so changes can be saved
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("<color=green>VR Assignment Scene setup completed successfully!</color>");
        EditorUtility.DisplayDialog("VR Assignment Setup", "Scene objects and interactions generated successfully!", "OK");
    }

    private static GameObject CreateContainer(string name, Vector3 position, Material mat, bool isCorrect, out Transform snapPointTransform)
    {
        snapPointTransform = null;
        GameObject old = GameObject.Find(name);
        if (old != null) Object.DestroyImmediate(old);

        GameObject container = new GameObject(name);
        container.transform.position = position;

        // Bottom and 4 walls
        CreateWall(container, "Bottom", new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.04f, 0.34f), mat);
        CreateWall(container, "LeftWall", new Vector3(-0.17f, 0.125f, 0f), new Vector3(0.035f, 0.25f, 0.34f), mat);
        CreateWall(container, "RightWall", new Vector3(0.17f, 0.125f, 0f), new Vector3(0.035f, 0.25f, 0.34f), mat);
        CreateWall(container, "FrontWall", new Vector3(0f, 0.125f, -0.17f), new Vector3(0.34f, 0.25f, 0.035f), mat);
        CreateWall(container, "BackWall", new Vector3(0f, 0.125f, 0.17f), new Vector3(0.34f, 0.25f, 0.035f), mat);

        // TriggerZone
        GameObject triggerZone = new GameObject("TriggerZone");
        triggerZone.transform.SetParent(container.transform, false);
        triggerZone.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        BoxCollider box = triggerZone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.30f, 0.24f, 0.30f);

        ContainerZone zone = triggerZone.AddComponent<ContainerZone>();
        zone.isCorrect = isCorrect;

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

    private static Material CreateMaterial(string name, Color color)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

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
}

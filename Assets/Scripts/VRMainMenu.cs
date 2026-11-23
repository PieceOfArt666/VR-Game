using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

public class VRMainMenu : MonoBehaviour
{
    [Header("Scene References")]
    public TutorialManager tutorialManager;
    public CubeInteraction cubeInteraction;

    [Header("Menu State")]
    public bool isMenuOpen = true;
    public bool isFreePlayMode = false;

    [Header("Panels")]
    public GameObject menuPanel;
    public GameObject controlsPanel;
    public GameObject tutorialContainer;

    [Header("Top Bar")]
    public Button menuToggleButton;
    public TMP_Text menuToggleText;

    [Header("Main Menu Buttons")]
    public Button startTutorialButton;
    public Button freePlayButton;
    public Button resetCubeButton;
    public Button controlsButton;
    public Button quitButton;

    [Header("Controls Guide Buttons")]
    public Button backFromControlsButton;

    private Canvas canvas;
    private AudioSource audioSource;
    private bool initialized = false;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>();
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        if (cubeInteraction == null)
            cubeInteraction = UnityEngine.Object.FindAnyObjectByType<CubeInteraction>();

        if (tutorialManager == null)
            tutorialManager = UnityEngine.Object.FindAnyObjectByType<TutorialManager>();

        EnsureVRRaycasting();
    }

    void Start()
    {
        BuildOrSetupMenuUI();
        SetMenuState(isMenuOpen);
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.mKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            {
                ToggleMenu();
            }
        }
    }

    private void EnsureVRRaycasting()
    {
        // 1. Ensure TrackedDeviceGraphicRaycaster is present for VR Ray interaction
        if (canvas != null)
        {
            var tdgr = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
            if (tdgr == null)
            {
                tdgr = canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
        }

        // 2. Ensure EventSystem with XRUIInputModule exists for VR controller raycasting
        EventSystem es = EventSystem.current;
        if (es == null)
            es = UnityEngine.Object.FindAnyObjectByType<EventSystem>();

        if (es == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            es = esGo.AddComponent<EventSystem>();
        }

        // Remove blocking standalone / mouse modules if present
        var standalone = es.GetComponent<StandaloneInputModule>();
        if (standalone != null) Destroy(standalone);

        var inputSysModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (inputSysModule != null) Destroy(inputSysModule);

        // Add XRUIInputModule to process TrackedDeviceGraphicRaycaster
        var xrui = es.GetComponent<XRUIInputModule>();
        if (xrui == null)
        {
            xrui = es.gameObject.AddComponent<XRUIInputModule>();
        }
    }

    private void BuildOrSetupMenuUI()
    {
        if (initialized) return;

        // Locate existing tutorial elements if tutorialContainer not assigned
        if (tutorialContainer == null)
        {
            // Find existing tutorial UI pieces (StepBadge, Instructions, ProgressTrack, etc.)
            Transform stepBadge = transform.Find("StepBadge");
            if (stepBadge != null)
                tutorialContainer = stepBadge.parent != null ? stepBadge.parent.gameObject : gameObject;
        }

        // Check if MenuPanel already exists
        Transform existingMenu = transform.Find("MainMenuPanel");
        if (existingMenu != null)
        {
            menuPanel = existingMenu.gameObject;
        }
        else
        {
            CreateMenuPanel();
        }

        // Check if ControlsPanel already exists
        Transform existingControls = transform.Find("ControlsPanel");
        if (existingControls != null)
        {
            controlsPanel = existingControls.gameObject;
        }
        else
        {
            CreateControlsPanel();
        }

        // Check if ToggleButton exists
        Transform existingToggle = transform.Find("MenuToggleButton");
        if (existingToggle != null)
        {
            menuToggleButton = existingToggle.GetComponent<Button>();
            menuToggleText = existingToggle.GetComponentInChildren<TMP_Text>();
        }
        else
        {
            CreateMenuToggleButton();
        }

        initialized = true;
    }

    private void CreateMenuToggleButton()
    {
        GameObject toggleGo = new GameObject("MenuToggleButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(VRButtonHover));
        toggleGo.transform.SetParent(transform, false);

        RectTransform rt = toggleGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-15f, -15f);
        rt.sizeDelta = new Vector2(150f, 44f);

        Image img = toggleGo.GetComponent<Image>();
        img.color = new Color(0.06f, 0.10f, 0.18f, 0.90f);

        Outline outline = toggleGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.22f, 0.74f, 0.97f, 0.7f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        menuToggleButton = toggleGo.GetComponent<Button>();
        menuToggleButton.onClick.AddListener(ToggleMenu);

        // Text
        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(toggleGo.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        menuToggleText = textGo.GetComponent<TextMeshProUGUI>();
        menuToggleText.text = "<b>[ ☰ MENU ]</b>";
        menuToggleText.fontSize = 20;
        menuToggleText.alignment = TextAlignmentOptions.Center;
        menuToggleText.color = new Color(0.9f, 0.95f, 1f, 1f);
    }

    private void CreateMenuPanel()
    {
        GameObject panelGo = new GameObject("MainMenuPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        panelGo.transform.SetParent(transform, false);

        RectTransform rt = panelGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(620f, 440f);

        Image img = panelGo.GetComponent<Image>();
        img.color = new Color(0.04f, 0.07f, 0.14f, 0.94f);

        Outline outline = panelGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.22f, 0.74f, 0.97f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        menuPanel = panelGo;

        // Header Title
        GameObject titleGo = new GameObject("MenuTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(panelGo.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -25f);
        titleRt.sizeDelta = new Vector2(580f, 50f);

        TextMeshProUGUI titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.text = "<color=#38bdf8><b>VR CUBE PLACEMENT</b></color>";
        titleText.fontSize = 32;
        titleText.alignment = TextAlignmentOptions.Center;

        // Subtitle
        GameObject subGo = new GameObject("MenuSubtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        subGo.transform.SetParent(panelGo.transform, false);
        RectTransform subRt = subGo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 1f);
        subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -75f);
        subRt.sizeDelta = new Vector2(580f, 30f);

        TextMeshProUGUI subText = subGo.GetComponent<TextMeshProUGUI>();
        subText.text = "<color=#94a3b8>Aim your VR Ray at an option to interact</color>";
        subText.fontSize = 18;
        subText.alignment = TextAlignmentOptions.Center;

        // Button Container
        float startY = -125f;
        float spacing = 55f;

        startTutorialButton = CreateStyledButton(panelGo.transform, "Btn_Tutorial", "▶  START TUTORIAL", new Vector2(0f, startY), new Vector2(480f, 46f), OnStartTutorialClicked);
        freePlayButton = CreateStyledButton(panelGo.transform, "Btn_FreePlay", "★  FREE PLAY MODE", new Vector2(0f, startY - spacing), new Vector2(480f, 46f), OnFreePlayClicked);
        resetCubeButton = CreateStyledButton(panelGo.transform, "Btn_Reset", "↺  RESET CUBE TO CONTAINER A", new Vector2(0f, startY - spacing * 2), new Vector2(480f, 46f), OnResetCubeClicked);
        controlsButton = CreateStyledButton(panelGo.transform, "Btn_Controls", "⌨  CONTROLS & GUIDE", new Vector2(0f, startY - spacing * 3), new Vector2(480f, 46f), OnControlsClicked);
        quitButton = CreateStyledButton(panelGo.transform, "Btn_Quit", "✕  QUIT", new Vector2(0f, startY - spacing * 4), new Vector2(480f, 46f), OnQuitClicked);
    }

    private void CreateControlsPanel()
    {
        GameObject panelGo = new GameObject("ControlsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        panelGo.transform.SetParent(transform, false);

        RectTransform rt = panelGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(680f, 460f);

        Image img = panelGo.GetComponent<Image>();
        img.color = new Color(0.04f, 0.07f, 0.14f, 0.96f);

        Outline outline = panelGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.22f, 0.74f, 0.97f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        controlsPanel = panelGo;

        // Title
        GameObject titleGo = new GameObject("ControlsTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(panelGo.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -25f);
        titleRt.sizeDelta = new Vector2(620f, 45f);

        TextMeshProUGUI titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.text = "<color=#38bdf8><b>VR & SIMULATOR CONTROLS</b></color>";
        titleText.fontSize = 28;
        titleText.alignment = TextAlignmentOptions.Center;

        // Content
        GameObject bodyGo = new GameObject("ControlsBody", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        bodyGo.transform.SetParent(panelGo.transform, false);
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.anchoredPosition = new Vector2(0f, -10f);
        bodyRt.sizeDelta = new Vector2(-60f, -150f);

        TextMeshProUGUI bodyText = bodyGo.GetComponent<TextMeshProUGUI>();
        bodyText.fontSize = 17;
        bodyText.lineSpacing = 12f;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.text =
            "<color=#fbbf24><b>VR CONTROLLER:</b></color>\n" +
            "  • <b>Aim Ray:</b> Point right controller beam at cube or UI buttons\n" +
            "  • <b>Grip / Grab:</b> Squeeze <b>Grip</b> (or <b>Trigger</b>) to pick up\n" +
            "  • <b>Move:</b> Move your hand to carry the cube with the ray in 3D\n" +
            "  • <b>Drop:</b> Release Grip over Container B (Green) or C (Red)\n\n" +
            "<color=#38bdf8><b>XR DEVICE SIMULATOR (Editor / Keyboard):</b></color>\n" +
            "  • <b>Right Hand Control:</b> Hold <color=#fbbf24><b>[SPACE]</b></color> or press <color=#fbbf24><b>[T]</b></color> to toggle\n" +
            "  • <b>Aim VR Ray:</b> Move mouse while holding Space / toggled\n" +
            "  • <b>Grip / Grab:</b> Press <color=#fbbf24><b>[G]</b></color>, <color=#fbbf24><b>[E]</b></color>, or Left Click\n" +
            "  • <b>Move Object:</b> Move mouse to sweep the ray and carry cube\n" +
            "  • <b>Drop Object:</b> Press <color=#fbbf24><b>[G]</b></color> or Left Click again to drop";

        // Back Button
        backFromControlsButton = CreateStyledButton(panelGo.transform, "Btn_Back", "←  BACK TO MENU", new Vector2(0f, -400f), new Vector2(300f, 44f), OnBackFromControlsClicked);

        controlsPanel.SetActive(false);
    }

    private Button CreateStyledButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(VRButtonHover));
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnGo.GetComponent<Image>();
        img.color = new Color(0.06f, 0.10f, 0.18f, 0.88f);

        Outline outline = btnGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.35f, 0.55f, 0.5f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnGo.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        // Text
        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = $"<b>{label}</b>";
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.92f, 0.96f, 1f, 1f);

        return btn;
    }

    public void ToggleMenu()
    {
        SetMenuState(!isMenuOpen);
    }

    public void SetMenuState(bool open)
    {
        isMenuOpen = open;

        if (menuPanel != null)
            menuPanel.SetActive(isMenuOpen);

        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        if (menuToggleText != null)
        {
            menuToggleText.text = isMenuOpen ? "<b>[ ✕ CLOSE ]</b>" : "<b>[ ☰ MENU ]</b>";
        }

        // When menu is open, pause/hide tutorial steps to prevent clutter
        if (tutorialManager != null)
        {
            if (isMenuOpen)
            {
                // Temporarily hide tutorial texts while in main menu
                SetTutorialElementsVisible(false);
            }
            else
            {
                // Restore tutorial visibility based on mode
                SetTutorialElementsVisible(!isFreePlayMode);
            }
        }
    }

    private void SetTutorialElementsVisible(bool visible)
    {
        if (tutorialManager == null) return;

        if (tutorialManager.stepBadgeText != null)
            tutorialManager.stepBadgeText.transform.parent.gameObject.SetActive(visible);

        if (tutorialManager.instructionTitleText != null)
            tutorialManager.instructionTitleText.gameObject.SetActive(visible);

        if (tutorialManager.instructionBodyText != null)
            tutorialManager.instructionBodyText.gameObject.SetActive(visible);

        if (tutorialManager.progressBarFill != null && tutorialManager.progressBarFill.transform.parent != null)
            tutorialManager.progressBarFill.transform.parent.gameObject.SetActive(visible);
    }

    public void OnStartTutorialClicked()
    {
        isFreePlayMode = false;
        SetMenuState(false);

        if (tutorialManager != null)
        {
            tutorialManager.RestartTutorial();
            SetTutorialElementsVisible(true);
        }
    }

    public void OnFreePlayClicked()
    {
        isFreePlayMode = true;
        SetMenuState(false);

        if (tutorialManager != null)
        {
            SetTutorialElementsVisible(false);
            if (tutorialManager.feedbackText != null)
            {
                tutorialManager.feedbackText.text =
                    "<size=85%><color=#38bdf8>FREE PLAY MODE</color></size>\n" +
                    "<size=105%><color=#34d399><b>Use your VR Ray to pick up and place the cube!</b></color></size>";
            }
        }
    }

    public void OnResetCubeClicked()
    {
        if (cubeInteraction != null)
        {
            cubeInteraction.ResetCubeToStart();
        }

        if (tutorialManager != null && tutorialManager.feedbackText != null)
        {
            tutorialManager.feedbackText.text =
                "<size=85%><color=#38bdf8>CUBE RESET</color></size>\n" +
                "<size=105%><color=#34d399><b>Green Cube respawned in Container A!</b></color></size>";
        }
    }

    public void OnControlsClicked()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);

        if (controlsPanel != null)
            controlsPanel.SetActive(true);
    }

    public void OnBackFromControlsClicked()
    {
        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        if (menuPanel != null)
            menuPanel.SetActive(true);
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

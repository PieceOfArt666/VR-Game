using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public enum Step
    {
        LookAround = 0,
        MoveToTable = 1,
        ActivateController = 2,
        HoverCube = 3,
        GrabCube = 4,
        DropInContainerC = 5,
        DropInContainerB = 6,
        Completed = 7
    }

    [Header("UI References")]
    public TMP_Text stepBadgeText;
    public TMP_Text instructionTitleText;
    public TMP_Text instructionBodyText;
    public Image progressBarFill;
    public TMP_Text feedbackText;

    [Header("Scene References")]
    public CubeInteraction cubeInteraction;
    public Transform playerCamera;
    public VRMainMenu mainMenu;

    [Header("Current State")]
    public Step currentStep = Step.LookAround;

    private float accumulatedCameraRotation = 0f;
    private Quaternion lastCameraRotation;
    private Vector3 lastPlayerPosition;
    private float accumulatedMovement = 0f;
    private float stepTimer = 0f;
    private bool stepTransitioning = false;
    private float currentProgress = 0.16f;
    private float targetProgress = 0.16f;

    void Awake()
    {
        // Auto-ensure VRMainMenu component on this canvas
        mainMenu = GetComponent<VRMainMenu>();
        if (mainMenu == null)
            mainMenu = gameObject.AddComponent<VRMainMenu>();
    }

    void Start()
    {
        FindPlayerCamera();

        // 1. Setup Sci-Fi UI Art & Backgrounds
        SetupUIArt();

        // 2. Proactive auto-hookup if references were not set in inspector
        if (stepBadgeText == null)
        {
            Transform t = transform.Find("StepBadge/BadgeText") ?? transform.Find("StepBadge");
            if (t != null) stepBadgeText = t.GetComponent<TMP_Text>();
        }
        if (instructionTitleText == null)
        {
            Transform t = transform.Find("StepTitle");
            if (t != null) instructionTitleText = t.GetComponent<TMP_Text>();
        }
        if (instructionBodyText == null)
        {
            Transform t = transform.Find("InstructionsText");
            if (t != null) instructionBodyText = t.GetComponent<TMP_Text>();
        }
        if (feedbackText == null)
        {
            Transform t = transform.Find("FeedbackText");
            if (t != null) feedbackText = t.GetComponent<TMP_Text>();
        }
        if (progressBarFill == null)
        {
            Transform t = transform.Find("ProgressTrack/ProgressFill") ?? transform.Find("ProgressBar/ProgressFill") ?? transform.Find("ProgressFill");
            if (t != null) progressBarFill = t.GetComponent<Image>();
        }

        // 3. Configure Progress Bar for smooth horizontal fill
        if (progressBarFill != null)
        {
            progressBarFill.type = Image.Type.Filled;
            progressBarFill.fillMethod = Image.FillMethod.Horizontal;
            progressBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressBarFill.fillClockwise = true;
            progressBarFill.raycastTarget = false;

            RectTransform rt = progressBarFill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            currentProgress = GetStepProgress(currentStep);
            targetProgress = currentProgress;
            progressBarFill.fillAmount = currentProgress;
        }

        // Auto-hook cube interaction
        if (cubeInteraction == null)
            cubeInteraction = UnityEngine.Object.FindAnyObjectByType<CubeInteraction>();

        if (cubeInteraction != null)
        {
            cubeInteraction.OnCubeGrabbed += HandleCubeGrabbed;
            cubeInteraction.OnCubeFailure += HandleCubeFailure;
            cubeInteraction.OnCubeSuccess += HandleCubeSuccess;
        }

        UpdateUI();
    }

    void OnDestroy()
    {
        if (cubeInteraction != null)
        {
            cubeInteraction.OnCubeGrabbed -= HandleCubeGrabbed;
            cubeInteraction.OnCubeFailure -= HandleCubeFailure;
            cubeInteraction.OnCubeSuccess -= HandleCubeSuccess;
        }
    }

    private Sprite LoadSpriteFromFile(string relativePath)
    {
        string fullPath = Path.Combine(Application.dataPath, relativePath);
        if (File.Exists(fullPath))
        {
            byte[] data = File.ReadAllBytes(fullPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(data))
            {
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        return null;
    }

    private void SetupUIArt()
    {
        // 1. Activate and style BackgroundPanel with high-tech sci-fi frame
        Transform bg = transform.Find("BackgroundPanel");
        if (bg == null)
        {
            GameObject bgGo = new GameObject("BackgroundPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.SetAsFirstSibling();
            bg = bgGo.transform;
            RectTransform rt = bgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }

        if (bg != null)
        {
            bg.gameObject.SetActive(true);
            bg.SetAsFirstSibling(); // Ensure it's behind text

            Image bgImg = bg.GetComponent<Image>();
            if (bgImg == null) bgImg = bg.gameObject.AddComponent<Image>();

            Sprite frameSprite = LoadSpriteFromFile("Textures/UI/Menu_Panel_Frame.jpg");
            if (frameSprite != null)
            {
                bgImg.sprite = frameSprite;
                bgImg.type = Image.Type.Simple;
                bgImg.color = new Color(1f, 1f, 1f, 0.96f);
            }
            else
            {
                bgImg.color = new Color(0.04f, 0.08f, 0.16f, 0.94f);
            }

            // Glowing cyan outline
            Outline outline = bg.GetComponent<Outline>();
            if (outline == null) outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.74f, 0.97f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        // 2. Add Top Header Banner image
        Transform existingBanner = transform.Find("HeaderBanner");
        if (existingBanner == null)
        {
            Sprite bannerSprite = LoadSpriteFromFile("Textures/UI/Menu_Header_Banner.jpg");
            if (bannerSprite != null)
            {
                GameObject bannerGo = new GameObject("HeaderBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bannerGo.transform.SetParent(transform, false);
                if (bg != null) bannerGo.transform.SetSiblingIndex(1);

                RectTransform bannerRt = bannerGo.GetComponent<RectTransform>();
                bannerRt.anchorMin = new Vector2(0f, 1f);
                bannerRt.anchorMax = new Vector2(1f, 1f);
                bannerRt.pivot = new Vector2(0.5f, 1f);
                bannerRt.anchoredPosition = new Vector2(0f, -6f);
                bannerRt.sizeDelta = new Vector2(-20f, 65f);

                Image bannerImg = bannerGo.GetComponent<Image>();
                bannerImg.sprite = bannerSprite;
                bannerImg.preserveAspect = false;
                bannerImg.color = new Color(1f, 1f, 1f, 0.92f);
                bannerImg.raycastTarget = false;
            }
        }

        // 3. Step Badge Graphic
        Transform badgeTrans = transform.Find("StepBadge");
        if (badgeTrans != null)
        {
            Image badgeImg = badgeTrans.GetComponent<Image>();
            if (badgeImg == null) badgeImg = badgeTrans.gameObject.AddComponent<Image>();
            Sprite badgeSprite = LoadSpriteFromFile("Textures/UI/Step_Badge.png");
            if (badgeSprite != null)
            {
                badgeImg.sprite = badgeSprite;
                badgeImg.type = Image.Type.Sliced;
                badgeImg.color = Color.white;
            }
        }

        // 4. Progress Track & Fill
        Transform trackTrans = transform.Find("ProgressTrack");
        if (trackTrans != null)
        {
            Image trackImg = trackTrans.GetComponent<Image>();
            if (trackImg == null) trackImg = trackTrans.gameObject.AddComponent<Image>();
            Sprite trackSprite = LoadSpriteFromFile("Textures/UI/ProgressBar_Track.png");
            if (trackSprite != null)
            {
                trackImg.sprite = trackSprite;
                trackImg.type = Image.Type.Sliced;
                trackImg.color = Color.white;
            }
        }

        if (progressBarFill != null)
        {
            Sprite fillSprite = LoadSpriteFromFile("Textures/UI/ProgressBar_Fill.png");
            if (fillSprite != null)
            {
                progressBarFill.sprite = fillSprite;
                progressBarFill.type = Image.Type.Filled;
                progressBarFill.color = Color.white;
            }
        }

        // 5. Feedback Box Graphic (Status_Box.png behind FeedbackText)
        if (feedbackText != null)
        {
            Transform fbBox = transform.Find("FeedbackStatusBox");
            if (fbBox == null)
            {
                GameObject boxGo = new GameObject("FeedbackStatusBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                boxGo.transform.SetParent(transform, false);
                boxGo.transform.SetSiblingIndex(feedbackText.transform.GetSiblingIndex());

                RectTransform boxRt = boxGo.GetComponent<RectTransform>();
                RectTransform textRt = feedbackText.rectTransform;
                boxRt.anchorMin = textRt.anchorMin;
                boxRt.anchorMax = textRt.anchorMax;
                boxRt.pivot = textRt.pivot;
                boxRt.anchoredPosition = textRt.anchoredPosition;
                boxRt.sizeDelta = new Vector2(textRt.sizeDelta.x + 20f, textRt.sizeDelta.y + 12f);

                Image boxImg = boxGo.GetComponent<Image>();
                Sprite boxSprite = LoadSpriteFromFile("Textures/UI/Status_Box.png");
                if (boxSprite != null)
                {
                    boxImg.sprite = boxSprite;
                    boxImg.type = Image.Type.Sliced;
                    boxImg.color = Color.white;
                }
                else
                {
                    boxImg.color = new Color(0.05f, 0.12f, 0.22f, 0.85f);
                }
                boxImg.raycastTarget = false;
            }
        }
    }

    private void FindPlayerCamera()
    {
        if (playerCamera == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                playerCamera = cam.transform;
            }
            else
            {
                GameObject camGo = GameObject.FindWithTag("MainCamera");
                if (camGo != null)
                    playerCamera = camGo.transform;
            }
        }

        if (playerCamera != null)
        {
            lastCameraRotation = playerCamera.rotation;
            lastPlayerPosition = playerCamera.position;
        }
    }

    void Update()
    {
        // Smooth Progress Bar fill animation
        if (progressBarFill != null)
        {
            currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, Time.deltaTime * 0.75f);
            progressBarFill.fillAmount = currentProgress;
        }

        // Keep UI facing the player camera smoothly
        BillboardTowardsCamera();

        // If main menu is open, pause tutorial advancement
        if (mainMenu != null && mainMenu.isMenuOpen)
            return;

        // Skip / restart hotkeys
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (kb != null)
        {
            if (kb.nKey.wasPressedThisFrame)
            {
                AdvanceStep();
                return;
            }

            if (kb.rKey.wasPressedThisFrame)
            {
                RestartTutorial();
                return;
            }
        }

        stepTimer += Time.deltaTime;

        if (stepTransitioning)
            return;

        // Process step completion checks
        switch (currentStep)
        {
            case Step.LookAround:
                CheckLookAround(mouse);
                break;

            case Step.MoveToTable:
                CheckMovement(kb);
                break;

            case Step.ActivateController:
                CheckActivateController(kb, mouse);
                break;

            case Step.HoverCube:
                CheckHoverCube();
                break;

            case Step.GrabCube:
                if (cubeInteraction != null && cubeInteraction.IsGrabbed)
                {
                    AdvanceStep();
                }
                break;

            case Step.DropInContainerC:
                break;

            case Step.DropInContainerB:
                if (cubeInteraction != null && cubeInteraction.IsCompleted)
                {
                    AdvanceStep();
                }
                break;
        }
    }

    private void CheckLookAround(Mouse mouse)
    {
        bool mouseLook = false;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude > 4f)
                mouseLook = true;
        }

        if (playerCamera != null)
        {
            float angleDelta = Quaternion.Angle(playerCamera.rotation, lastCameraRotation);
            accumulatedCameraRotation += angleDelta;
            lastCameraRotation = playerCamera.rotation;

            if (accumulatedCameraRotation >= 15f || mouseLook)
            {
                AdvanceStep();
                return;
            }
        }
        else if (mouseLook)
        {
            AdvanceStep();
            return;
        }

        if (stepTimer >= 6f)
        {
            AdvanceStep();
        }
    }

    private void CheckMovement(Keyboard kb)
    {
        bool keyPressed = false;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed)
            {
                accumulatedMovement += Time.deltaTime * 1.5f;
                keyPressed = true;
            }
        }

        if (playerCamera != null)
        {
            float dist = Vector3.Distance(playerCamera.position, lastPlayerPosition);
            accumulatedMovement += dist;
            lastPlayerPosition = playerCamera.position;

            if (accumulatedMovement >= 0.35f)
            {
                AdvanceStep();
                return;
            }
        }
        else if (keyPressed && accumulatedMovement >= 0.35f)
        {
            AdvanceStep();
            return;
        }

        if (stepTimer >= 6f)
        {
            AdvanceStep();
        }
    }

    private void CheckActivateController(Keyboard kb, Mouse mouse)
    {
        bool rayActive = false;
        var interactors = UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(FindObjectsInactive.Exclude);
        foreach (var i in interactors)
        {
            if (i != null && i.isActiveAndEnabled && i is UnityEngine.XR.Interaction.Toolkit.Interactors.IXRRayProvider)
            {
                rayActive = true;
                break;
            }
        }

        if (kb != null)
        {
            if (kb.spaceKey.isPressed || kb.leftShiftKey.isPressed || kb.gKey.isPressed || kb.tKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
                rayActive = true;
        }

        if (cubeInteraction != null && (cubeInteraction.IsHovered || cubeInteraction.IsGrabbed))
            rayActive = true;

        if (rayActive)
        {
            AdvanceStep();
        }
    }

    private void CheckHoverCube()
    {
        if (cubeInteraction != null)
        {
            if (cubeInteraction.IsGrabbed)
            {
                AdvanceStep();
                return;
            }

            if (cubeInteraction.IsHovered)
            {
                StartCoroutine(HoverConfirmation());
            }
        }
    }

    private IEnumerator HoverConfirmation()
    {
        stepTransitioning = true;
        yield return new WaitForSeconds(0.35f);
        if (cubeInteraction != null && (cubeInteraction.IsHovered || cubeInteraction.IsGrabbed))
        {
            stepTransitioning = false;
            AdvanceStep();
        }
        else
        {
            stepTransitioning = false;
        }
    }

    private void HandleCubeGrabbed()
    {
        if (currentStep <= Step.GrabCube)
        {
            SetStep(Step.DropInContainerC);
        }
    }

    private void HandleCubeFailure()
    {
        if (currentStep == Step.DropInContainerC)
        {
            AdvanceStep();
        }
    }

    private void HandleCubeSuccess()
    {
        if (currentStep <= Step.DropInContainerB)
        {
            SetStep(Step.Completed);
        }
    }

    public void AdvanceStep()
    {
        if (currentStep < Step.Completed)
        {
            SetStep(currentStep + 1);
        }
    }

    public void SetStep(Step step)
    {
        currentStep = step;
        stepTimer = 0f;
        UpdateUI();
    }

    public void RestartTutorial()
    {
        accumulatedCameraRotation = 0f;
        accumulatedMovement = 0f;
        stepTimer = 0f;

        if (cubeInteraction != null)
            cubeInteraction.ResetCubeToStart();

        SetStep(Step.LookAround);
    }

    public float GetStepProgress(Step step)
    {
        switch (step)
        {
            case Step.LookAround: return 0.16f;
            case Step.MoveToTable: return 0.33f;
            case Step.ActivateController: return 0.50f;
            case Step.HoverCube: return 0.67f;
            case Step.GrabCube: return 0.83f;
            case Step.DropInContainerC: return 0.92f;
            case Step.DropInContainerB: return 0.96f;
            case Step.Completed: return 1.0f;
            default: return 0.16f;
        }
    }

    private void UpdateUI()
    {
        targetProgress = GetStepProgress(currentStep);

        string badge = "";
        string title = "";
        string body = "";
        string feedback = "";
        Color badgeColor = new Color(0.22f, 0.74f, 0.97f);

        switch (currentStep)
        {
            case Step.LookAround:
                badge = "STEP 1 / 6";
                badgeColor = new Color(0.22f, 0.74f, 0.97f);
                title = "Look Around (HMD)";
                body = "• Hold <color=#facc15><b>Right Mouse Button</b></color> and move mouse to look around.\n• Observe the table, colored containers, and golden cube.";
                feedback = "Looking around the VR space...";
                break;

            case Step.MoveToTable:
                badge = "STEP 2 / 6";
                badgeColor = new Color(0.22f, 0.74f, 0.97f);
                title = "Locomotion (WASD)";
                body = "• Press <color=#facc15><b>W / A / S / D</b></color> (while holding RMB) to walk forward.\n• Step up close to the table with Containers A, B, and C.";
                feedback = "Moving closer to the table...";
                break;

            case Step.ActivateController:
                badge = "STEP 3 / 6";
                badgeColor = new Color(0.98f, 0.75f, 0.14f);
                title = "Aim Controller Raycast";
                body = "• Hold <color=#fbbf24><b>[SPACE]</b></color> (or press <color=#fbbf24><b>[T]</b></color> to toggle) to control your <b>Right Hand</b>.\n• Move mouse to aim the white <b>Controller Raycast beam</b> forward at the table.";
                feedback = "Aim your Controller Raycast forward at the workstation.";
                break;

            case Step.HoverCube:
                badge = "STEP 4 / 6";
                badgeColor = new Color(0.2f, 0.83f, 0.6f);
                title = "Hover Affordance (Container A)";
                body = "• Aim your <b>Controller Raycast</b> at the <color=#22c55e><b>Green Cube</b></color> in Container A.\n• Observe the smooth <color=#00a6ff><b>Blue highlight</b></color>, <b>1.15x breathing scale</b>, and gentle floating bob animation!";
                feedback = "Aim Controller Raycast at Green Cube to see Blue hover & bob animation.";
                break;

            case Step.GrabCube:
                badge = "STEP 5 / 6";
                badgeColor = new Color(0.98f, 0.45f, 0.09f);
                title = "Grip Cube & Raycast Push/Pull";
                body = "• Aim Controller Raycast at cube and hold <color=#f97316><b>[G]</b></color> (or Left Click / VR Grip) to carry!\n• While holding, press <color=#38bdf8><b>[E]</b></color> to push forward or <color=#38bdf8><b>[Q]</b></color> to pull backward (or use Controller Thumbstick)!\n• Release [G] at any time to drop.";
                feedback = "Hold [G] to carry. Press [Q]/[E] or Controller Thumbstick to push/pull along ray.";
                break;

            case Step.DropInContainerC:
                badge = "STEP 6 / 6";
                badgeColor = new Color(0.97f, 0.44f, 0.44f);
                title = "Move with Controller Raycast (Error Test)";
                body = "• Carry cube with [G] and adjust distance with <color=#38bdf8><b>[Q] / [E]</b></color>.\n• Drop it outside Container B (e.g. over Container C or table).\n• <color=#f87171><b>Release [G]</b></color> to drop! Since it is not Container B, it turns RED, shakes, and returns to spawn.";
                feedback = "Release [G] outside Container B to observe Red error return to spawn.";
                break;

            case Step.DropInContainerB:
                badge = "FINAL TASK";
                badgeColor = new Color(0.29f, 0.87f, 0.5f);
                title = "Place with Controller Raycast in Container B";
                body = "• Aim Controller Raycast at the cube, hold <color=#34d399><b>[G]</b></color> to carry it.\n• Adjust distance with <color=#38bdf8><b>[Q] / [E]</b></color> or Thumbstick over <color=#4ade80><b>Green Container (B)</b></color>!\n• Release [G] inside Container B for magnetic snap & SUCCESS!";
                feedback = "Release [G] inside Container B (Green) for SUCCESS!";
                break;

            case Step.Completed:
                badge = "TUTORIAL COMPLETE";
                badgeColor = new Color(0.18f, 0.83f, 0.75f);
                title = "All Mechanics Verified!";
                body = "<b>Congratulations!</b> You have verified:\n" +
                       "<color=#34d399>[OK]</color> 6-DOF Tracking & Locomotion (WASD)\n" +
                       "<color=#34d399>[OK]</color> Controller Raycast Object Manipulation & Movement\n" +
                       "<color=#34d399>[OK]</color> Controller Raycast UI Interaction & Menu Navigation\n" +
                       "<color=#34d399>[OK]</color> Hover Affordance (Blue Lerp, 1.15x Scale & Floating Bob)\n" +
                       "<color=#34d399>[OK]</color> Container C Error Shake & Arc Return\n" +
                       "<color=#34d399>[OK]</color> Container B Magnetic Snap & Scale Pop";
                feedback = "SUCCESS! All mechanics passed. Press R to restart, or aim ray at [MENU].";
                break;
        }

        if (stepBadgeText != null)
        {
            stepBadgeText.text = badge;
            stepBadgeText.color = badgeColor;
        }

        if (instructionTitleText != null)
        {
            instructionTitleText.text = title;
        }

        if (instructionBodyText != null)
        {
            instructionBodyText.text = body;
        }

        if (feedbackText != null)
        {
            string hex = ColorUtility.ToHtmlStringRGB(badgeColor);
            feedbackText.text = $"<size=80%><color=#94a3b8>STATUS</color></size>\n<size=105%><color=#{hex}><b>{feedback}</b></color></size>";
        }
    }

    private void BillboardTowardsCamera()
    {
        if (playerCamera == null)
            FindPlayerCamera();

        if (playerCamera != null)
        {
            Vector3 targetDir = transform.position - playerCamera.position;
            targetDir.y = 0f;

            if (targetDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using TMPro;

[RequireComponent(typeof(Button))]
public class VRButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Hover Animation Settings")]
    public float hoverScale = 1.08f;
    public float animationSpeed = 14f;
    public Color normalColor = new Color(0.06f, 0.10f, 0.18f, 0.88f);
    public Color hoverColor = new Color(0.03f, 0.45f, 0.72f, 0.95f);
    public Color normalTextColor = new Color(0.90f, 0.94f, 1f, 1f);
    public Color hoverTextColor = Color.white;
    public Color normalBorderColor = new Color(0.20f, 0.35f, 0.55f, 0.5f);
    public Color hoverBorderColor = new Color(0.22f, 0.74f, 0.97f, 1f);

    private Button button;
    private Image buttonImage;
    private TMP_Text buttonText;
    private Outline outline;
    private RectTransform rectTransform;
    private Vector3 originalScale;

    private bool isPointerHovered;
    private bool isRayHovered;
    private bool isPressed;
    private float pressTimer = 0f;
    private int lastClickFrame = -1;

    private static AudioSource s_AudioSource;
    private static AudioClip s_ClickSound;
    private static AudioClip s_HoverSound;

    private readonly Vector3[] corners = new Vector3[4];

    void Awake()
    {
        button = GetComponent<Button>();
        buttonImage = GetComponent<Image>();
        buttonText = GetComponentInChildren<TMP_Text>();
        outline = GetComponent<Outline>();
        rectTransform = GetComponent<RectTransform>();
        originalScale = transform.localScale;

        if (button != null)
        {
            button.transition = Selectable.Transition.None;
        }

        if (buttonImage != null)
            buttonImage.color = normalColor;
        if (buttonText != null)
        {
            buttonText.color = normalTextColor;
            buttonText.raycastTarget = false;
        }

        EnsureAudio();
    }

    void OnDisable()
    {
        isPointerHovered = false;
        isRayHovered = false;
        isPressed = false;
        transform.localScale = originalScale;
    }

    void Update()
    {
        // 1. DIRECT CONTROLLER RAYCAST CHECK
        // Allows the VR Controller Ray to target and trigger this button in 3D world space
        bool hitByRay = CheckControllerRaycast(out bool triggerPressed);
        
        if (hitByRay && !isRayHovered)
        {
            PlayHoverSound();
        }
        isRayHovered = hitByRay;

        if (hitByRay && triggerPressed)
        {
            TriggerButtonClick();
        }

        // Manage press visual timer
        if (isPressed)
        {
            pressTimer -= Time.deltaTime;
            if (pressTimer <= 0f)
                isPressed = false;
        }

        // 2. SMOOTH HOVER & CLICK ANIMATIONS
        bool hovered = isPointerHovered || isRayHovered;
        float targetMultiplier = isPressed ? 0.95f : (hovered ? hoverScale : 1f);
        Vector3 targetScale = originalScale * targetMultiplier;

        // Smooth scale lerp
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animationSpeed);

        // Smooth background color lerp
        if (buttonImage != null)
        {
            Color targetBg = isPressed ? new Color(0.02f, 0.55f, 0.88f, 1f) : (hovered ? hoverColor : normalColor);
            buttonImage.color = Color.Lerp(buttonImage.color, targetBg, Time.deltaTime * animationSpeed);
        }

        // Smooth text color lerp
        if (buttonText != null)
        {
            Color targetTxt = hovered ? hoverTextColor : normalTextColor;
            buttonText.color = Color.Lerp(buttonText.color, targetTxt, Time.deltaTime * animationSpeed);
        }

        // Smooth outline border glow lerp
        if (outline != null)
        {
            Color targetBorder = isPressed ? Color.white : (hovered ? hoverBorderColor : normalBorderColor);
            outline.effectColor = Color.Lerp(outline.effectColor, targetBorder, Time.deltaTime * animationSpeed);
        }
    }

    private bool CheckControllerRaycast(out bool triggerPressed)
    {
        triggerPressed = false;
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        // Get the 4 world space corners of this button:
        // [0] = Bottom-Left, [1] = Top-Left, [2] = Top-Right, [3] = Bottom-Right
        rectTransform.GetWorldCorners(corners);
        Vector3 u = corners[3] - corners[0]; // local X axis in world
        Vector3 v = corners[1] - corners[0]; // local Y axis in world
        float uMagSq = u.sqrMagnitude;
        float vMagSq = v.sqrMagnitude;
        if (uMagSq < 0.00001f || vMagSq < 0.00001f)
            return false;

        Plane buttonPlane = new Plane(corners[0], corners[1], corners[2]);

        // Check active VR interactors with raycast capabilities
        var interactors = UnityEngine.Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Exclude);
        foreach (var interactor in interactors)
        {
            if (interactor == null || !interactor.isActiveAndEnabled)
                continue;

            Ray ray;
            if (interactor is IXRRayProvider rayProvider)
            {
                Transform origin = rayProvider.GetOrCreateRayOrigin() ?? interactor.transform;
                ray = new Ray(origin.position, origin.forward);
            }
            else
            {
                ray = new Ray(interactor.transform.position, interactor.transform.forward);
            }

            // Test ray-plane intersection
            if (buttonPlane.Raycast(ray, out float enter) && enter > 0f && enter < 10f)
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 w = hitPoint - corners[0];

                float dotU = Vector3.Dot(w, u);
                float dotV = Vector3.Dot(w, v);

                // Is hitPoint inside the 2D bounding rect on this plane?
                if (dotU >= 0f && dotU <= uMagSq && dotV >= 0f && dotV <= vMagSq)
                {
                    // Check if controller Trigger / Grip / UI Press was clicked
                    if (interactor is NearFarInteractor nfi && nfi.uiPressInput != null && nfi.uiPressInput.ReadWasPerformedThisFrame())
                    {
                        triggerPressed = true;
                    }

                    if (interactor is XRBaseInputInteractor inputInt && inputInt.selectInput != null && inputInt.selectInput.ReadWasPerformedThisFrame())
                    {
                        triggerPressed = true;
                    }

                    // Simulator & desktop fallbacks when aiming with the raycast
                    var kb = Keyboard.current;
                    var mouse = Mouse.current;
                    if (kb != null && kb.enterKey.wasPressedThisFrame)
                        triggerPressed = true;
                    if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                        triggerPressed = true;

                    return true;
                }
            }
        }

        return false;
    }

    private void TriggerButtonClick()
    {
        if (Time.frameCount == lastClickFrame)
            return;

        lastClickFrame = Time.frameCount;
        isPressed = true;
        pressTimer = 0.12f;

        PlayClickSound();

        if (button != null && button.interactable)
        {
            button.onClick.Invoke();
        }
    }

    public void SetRayHover(bool hovered)
    {
        isRayHovered = hovered;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerHovered = true;
        PlayHoverSound();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerHovered = false;
        isPressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        pressTimer = 0.15f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TriggerButtonClick();
    }

    private void EnsureAudio()
    {
        if (s_AudioSource == null)
        {
            GameObject aGo = new GameObject("VR_UI_AudioSource");
            UnityEngine.Object.DontDestroyOnLoad(aGo);
            s_AudioSource = aGo.AddComponent<AudioSource>();
            s_AudioSource.spatialBlend = 0f;
            s_AudioSource.playOnAwake = false;
            s_AudioSource.volume = 0.75f;
        }

        if (s_ClickSound == null)
            s_ClickSound = CreateTone(780f, 0.05f);

        if (s_HoverSound == null)
            s_HoverSound = CreateTone(520f, 0.03f);
    }

    private void PlayClickSound()
    {
        if (s_AudioSource != null && s_ClickSound != null)
            s_AudioSource.PlayOneShot(s_ClickSound, 0.85f);
    }

    private void PlayHoverSound()
    {
        if (s_AudioSource != null && s_HoverSound != null)
            s_AudioSource.PlayOneShot(s_HoverSound, 0.35f);
    }

    private AudioClip CreateTone(float frequency, float duration)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (float)i / sampleCount;
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
        }

        AudioClip clip = AudioClip.Create("Tone_" + frequency, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

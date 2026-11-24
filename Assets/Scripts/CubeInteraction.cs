using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class CubeInteraction : MonoBehaviour
{
    [Header("Color Settings")]
    public Color defaultColor = new Color(0.13f, 0.77f, 0.36f, 1f); // Vibrant Green when NOT hovered
    public Color hoverColor = new Color(0f, 0.65f, 1f, 1f);          // Luminous Blue / Cyan when hovered
    public Color successColor = new Color(0.13f, 0.77f, 0.36f, 1f); // Green on success snap
    public Color failureColor = new Color(0.97f, 0.30f, 0.30f, 1f); // Red on rejection

    [Header("Hover Affordance Animations")]
    public float hoverScale = 1.15f;
    public float hoverSpeed = 10f;
    public float hoverFloatHeight = 0.035f; // Lifts 3.5cm up from container
    public float hoverBobSpeed = 3.5f;      // Bobbing frequency
    public float hoverBobAmount = 0.015f;   // Bobbing amplitude (1.5cm)

    [Header("UI Feedback")]
    public TMP_Text feedbackText;

    [Header("Audio (Procedural Synth)")]
    public AudioSource audioSource;
    public AudioClip grabSound;
    public AudioClip successSound;
    public AudioClip failureSound;

    [Header("Raycast Interaction Reach")]
    public float interactionReachDistance = 8.0f;

    [Header("Raycast Distance Manipulation (Push / Pull)")]
    public float minRayDistance = 0.35f;
    public float maxRayDistance = 8.0f;
    public float keyboardDistanceSpeed = 2.0f; // meters per second with Q and E
    public float controllerDistanceSpeed = 2.5f; // meters per second with VR Thumbstick
    public float scrollDistanceSpeed = 0.003f; // mouse scroll wheel sensitivity

    // Events for Tutorial State Machine
    public event Action OnCubeGrabbed;
    public event Action OnCubeSuccess;
    public event Action OnCubeFailure;

    [Header("Rendering")]
    public Renderer cubeRenderer;

    // Internal State
    private XRGrabInteractable grab;
    private Rigidbody rb;
    private Material cubeMaterial;
    private Color currentColor;
    private int colorPropertyId;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 startScale;
    private Collider[] colliders;
    private InteractionLayerMask originalInteractionLayers;

    private bool completed = false;
    private bool busy = false;
    private bool isTargetedByRay = false;
    private float grabbedRayDistance = 1.0f;
    private float grabCooldownTimer = 0f;
    private bool grabbedViaKey = false;
    private bool grabbedViaMouse = false;
    private bool releaseRequested = false;
    private ContainerZone currentZone = null;
    private IXRSelectInteractor cachedInteractor = null;
    private Camera mainCamera;

    public bool IsGrabbed => grab != null && grab.isSelected;
    public bool IsHovered => !completed && isTargetedByRay;
    public bool IsCompleted => completed;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        startPosition = transform.position;
        startRotation = transform.rotation;
        startScale = transform.localScale;

        if (grab != null)
        {
            grab.useDynamicAttach = true;
            grab.throwOnDetach = false;
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            originalInteractionLayers = grab.interactionLayers;
        }

        if (cubeRenderer == null)
            cubeRenderer = GetComponent<Renderer>();

        cubeMaterial = cubeRenderer.material;

        string propName = cubeMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
        colorPropertyId = Shader.PropertyToID(propName);

        // Enforce valid default and hover colors even if deserialized as default(Color)
        if (defaultColor.a < 0.1f)
            defaultColor = new Color(0.13f, 0.77f, 0.36f, 1f);
        if (hoverColor.a < 0.1f)
            hoverColor = new Color(0f, 0.65f, 1f, 1f);
        if (successColor.a < 0.1f)
            successColor = new Color(0.13f, 0.77f, 0.36f, 1f);
        if (failureColor.a < 0.1f)
            failureColor = new Color(0.97f, 0.30f, 0.30f, 1f);

        // Initialize color to GREEN (not hovered)
        currentColor = defaultColor;
        cubeMaterial.SetColor(colorPropertyId, defaultColor);

        // Cache all colliders on this cube
        colliders = GetComponents<Collider>();
        foreach (var c in colliders)
        {
            if (c != null && !grab.colliders.Contains(c))
                grab.colliders.Add(c);
        }

        mainCamera = Camera.main;
        GenerateProceduralAudio();
    }

    void Start()
    {
        // Enforce green at start
        currentColor = defaultColor;
        if (cubeMaterial != null)
            cubeMaterial.SetColor(colorPropertyId, defaultColor);
    }

    void OnEnable()
    {
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrab);
            grab.selectExited.AddListener(OnRelease);
        }
    }

    void OnDisable()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        ContainerZone zone = other.GetComponent<ContainerZone>() ?? other.GetComponentInParent<ContainerZone>();
        if (zone != null)
        {
            currentZone = zone;
        }
    }

    void OnTriggerExit(Collider other)
    {
        ContainerZone zone = other.GetComponent<ContainerZone>() ?? other.GetComponentInParent<ContainerZone>();
        if (zone != null && currentZone == zone)
        {
            currentZone = null;
        }
    }

    void Update()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (grabCooldownTimer > 0f)
            grabCooldownTimer -= Time.deltaTime;

        // Check controller ray targeting (True when ray passes through or hovers over cube)
        UpdateTargetingAndRayInput();

        if (busy)
            return;

        float dt = Time.deltaTime * hoverSpeed;

        // 1. CARRIED BY CONTROLLER RAYCAST: 3D Movement with Distance Manipulation (Q/E & Controllers)
        if (IsGrabbed)
        {
            Ray ray = GetActiveControllerRay();

            // Distance scaling (push forward / pull backward along ray)
            float distDelta = 0f;

            // Keyboard: E pushes forward (away), Q pulls backward (closer)
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.eKey.isPressed)
                    distDelta += keyboardDistanceSpeed * Time.deltaTime;
                if (kb.qKey.isPressed)
                    distDelta -= keyboardDistanceSpeed * Time.deltaTime;
            }

            // Controller Thumbstick / Joystick (Primary2DAxis Y)
            Vector2 stickInput = GetControllerThumbstick();
            if (Mathf.Abs(stickInput.y) > 0.12f)
            {
                distDelta += stickInput.y * controllerDistanceSpeed * Time.deltaTime;
            }

            // Mouse Scroll Wheel (Desktop / Simulator fallback)
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.y.ReadValue();
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    distDelta += scroll * scrollDistanceSpeed;
                }
            }

            grabbedRayDistance = Mathf.Clamp(grabbedRayDistance + distDelta, minRayDistance, maxRayDistance);

            Vector3 targetPosition = ray.origin + ray.direction * grabbedRayDistance;

            // Upright orientation: keep cube level and face horizontally along the ray
            Vector3 flatDir = Vector3.ProjectOnPlane(ray.direction, Vector3.up);
            Quaternion targetRotation = flatDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(flatDir, Vector3.up)
                : transform.rotation;

            int layerMask = ~LayerMask.GetMask("Ignore Raycast");
            float halfHeight = transform.localScale.y * 0.5f;

            // Collision prevention: stop if ray hits an obstacle before reaching grabbedRayDistance
            RaycastHit[] hits = Physics.RaycastAll(ray, grabbedRayDistance, layerMask, QueryTriggerInteraction.Ignore);
            float nearestObstacle = grabbedRayDistance;
            bool hitObstacle = false;
            RaycastHit obstacleHit = default;

            foreach (var h in hits)
            {
                if (h.transform == transform || h.transform.IsChildOf(transform))
                    continue;

                if (h.distance < nearestObstacle)
                {
                    nearestObstacle = h.distance;
                    obstacleHit = h;
                    hitObstacle = true;
                }
            }

            if (hitObstacle)
            {
                // Surface/wall cushion
                targetPosition = obstacleHit.point + obstacleHit.normal * (halfHeight + 0.02f);
            }
            else
            {
                // Downward surface check to prevent sinking below tabletop or floor
                if (Physics.Raycast(targetPosition + Vector3.up * 0.4f, Vector3.down, out RaycastHit downHit, 0.6f, layerMask, QueryTriggerInteraction.Ignore))
                {
                    if (downHit.transform != transform && !downHit.transform.IsChildOf(transform))
                    {
                        float floorLevel = downHit.point.y + halfHeight + 0.02f;
                        if (targetPosition.y < floorLevel)
                        {
                            targetPosition.y = floorLevel;
                        }
                    }
                }
            }

            // High-precision smooth damping with deadzone to eliminate all jitter/wiggling
            float distToTarget = Vector3.Distance(transform.position, targetPosition);
            if (distToTarget > 0.0005f)
            {
                float moveLerp = 1f - Mathf.Exp(-22f * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, targetPosition, moveLerp);
            }

            float rotLerp = 1f - Mathf.Exp(-16f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotLerp);

            // Keep Rigidbody completely kinematic during carry
            SetKinematic(true);

            // Keep BLUE while carried
            currentColor = Color.Lerp(currentColor, hoverColor, dt);
            cubeMaterial.SetColor(colorPropertyId, currentColor);
            transform.localScale = Vector3.Lerp(transform.localScale, startScale * 1.05f, dt);

            // Inform player of drop zones and distance scaling
            ContainerZone targetedZone = GetContainerBIfInside();
            if (targetedZone != null && targetedZone.isCorrect)
            {
                SetFeedback("<color=#34d399>Over Green Container (B)! Release [G] to drop & place.</color>", Color.white);
            }
            else if (IsOverContainerC())
            {
                SetFeedback("<color=#f87171>Over Container C (Incorrect)! Dropping here will reject.</color>", Color.white);
            }
            else
            {
                SetFeedback($"<color=#38bdf8>Carrying Cube ({grabbedRayDistance:F1}m)! Hold [G] | [Q/E] or Thumbstick to push/pull | Release [G] to drop.</color>", Color.white);
            }
            return;
        }

        // 2. HOVER ANIMATION & COLOR CHANGE ONLY WHILE CONTROLLER RAY HOVERS THROUGH CUBE
        bool rayHovering = !completed && isTargetedByRay;

        if (rayHovering)
        {
            // Scale expansion with breathing pulse (1.15x)
            float pulseFactor = 1f + 0.035f * Mathf.Sin(Time.time * 6f);
            Vector3 targetScale = startScale * (hoverScale * pulseFactor);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, dt);

            // Color change to vibrant BLUE / Cyan
            currentColor = Color.Lerp(currentColor, hoverColor, dt);
            cubeMaterial.SetColor(colorPropertyId, currentColor);

            // Floating bobbing & wobble hover animation
            float bob = Mathf.Sin(Time.time * hoverBobSpeed) * hoverBobAmount;
            Vector3 targetPosition = startPosition + Vector3.up * (hoverFloatHeight + bob);
            transform.position = Vector3.Lerp(transform.position, targetPosition, dt);

            float wobble = Mathf.Sin(Time.time * 3f) * 2.5f;
            Quaternion targetRotation = startRotation * Quaternion.Euler(0f, wobble, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, dt);

            SetFeedback("<color=#00a6ff><b>Ray Hovering Cube (BLUE)!</b></color> Hold [G] / Grip to grab.", Color.white);
        }
        else if (!completed)
        {
            // Hover settle back down ONLY if currently resting near spawn point in Container A
            bool isNearSpawn = Vector3.Distance(transform.position, startPosition) < 0.20f;
            if (isNearSpawn)
            {
                if (Vector3.SqrMagnitude(transform.position - startPosition) > 0.00001f)
                {
                    transform.position = Vector3.Lerp(transform.position, startPosition, dt);
                }
                if (Quaternion.Angle(transform.rotation, startRotation) > 0.1f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, startRotation, dt);
                }
            }

            // Return to base scale
            if (Vector3.SqrMagnitude(transform.localScale - startScale) > 0.00001f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, startScale, dt);
            }

            // Return to default GREEN color
            if (currentColor != defaultColor)
            {
                currentColor = Color.Lerp(currentColor, defaultColor, dt);
                cubeMaterial.SetColor(colorPropertyId, currentColor);
            }

            SetFeedback("Waiting for interaction...", Color.white);
        }
    }

    public bool IsRayPassingThroughCube(XRBaseInteractor interactor)
    {
        if (interactor == null || !interactor.isActiveAndEnabled)
            return false;

        // 1. Direct hit check from XRI Raycaster
        if (interactor is IXRRayProvider rayProvider)
        {
            Transform hitTrans = rayProvider.rayEndTransform;
            if (hitTrans == transform || (hitTrans != null && hitTrans.IsChildOf(transform)))
                return true;
        }

        // 2. Geometric beam proximity check (Ray line to Cube center distance)
        Ray beamRay = GetInteractorRay(interactor);
        Vector3 toCube = transform.position - beamRay.origin;
        float projection = Vector3.Dot(toCube, beamRay.direction);

        if (projection > 0f && projection <= interactionReachDistance)
        {
            Vector3 closestPointOnRay = beamRay.origin + beamRay.direction * projection;
            float distToBeam = Vector3.Distance(closestPointOnRay, transform.position);

            if (distToBeam <= 0.22f)
                return true;
        }

        // 3. Physics RaycastAll along beamRay through all colliders
        RaycastHit[] hits = Physics.RaycastAll(beamRay, interactionReachDistance, ~LayerMask.GetMask("Ignore Raycast"), QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    private Ray GetInteractorRay(XRBaseInteractor interactor)
    {
        if (interactor is IXRRayProvider rayProv)
        {
            Transform origin = rayProv.GetOrCreateRayOrigin() ?? interactor.transform;
            return new Ray(origin.position, origin.forward);
        }
        return new Ray(interactor.transform.position, interactor.transform.forward);
    }

    public bool IsOverContainerC()
    {
        // Container C is located at X = 0.55, Y = 0.82, Z = 1.6
        float xDiff = Mathf.Abs(transform.position.x - 0.55f);
        float zDiff = Mathf.Abs(transform.position.z - 1.60f);
        return xDiff <= 0.20f && zDiff <= 0.20f;
    }

    public ContainerZone GetContainerBIfInside()
    {
        // Container B is located at X = 0.0, Y = 0.82, Z = 1.6 (SnapPoint at X=0, Y=0.90, Z=1.6)
        ContainerZone containerBZone = null;
        var allZones = UnityEngine.Object.FindObjectsByType<ContainerZone>(FindObjectsInactive.Exclude);
        foreach (var z in allZones)
        {
            if (z != null && z.isCorrect)
            {
                containerBZone = z;
                break;
            }
        }

        if (containerBZone == null)
            return null;

        Vector3 center = containerBZone.snapPoint != null ? containerBZone.snapPoint.position : containerBZone.transform.position;

        // Container B inner basin dimensions: 0.34m outer width, 0.27m inner width.
        // Strict boundary check: half-width is 0.15m.
        float xDiff = Mathf.Abs(transform.position.x - center.x);
        float zDiff = Mathf.Abs(transform.position.z - center.z);
        float yDiff = Mathf.Abs(transform.position.y - center.y);

        // Strict basin check: cube must genuinely be inside Container B
        if (xDiff <= 0.15f && zDiff <= 0.15f && yDiff <= 0.30f)
        {
            return containerBZone;
        }

        return null;
    }

    public Ray GetActiveControllerRay()
    {
        if (cachedInteractor != null && (cachedInteractor as MonoBehaviour) != null)
        {
            if (cachedInteractor is IXRRayProvider rayProv)
            {
                Transform origin = rayProv.GetOrCreateRayOrigin() ?? (cachedInteractor as MonoBehaviour).transform;
                return new Ray(origin.position, origin.forward);
            }
            Transform t = (cachedInteractor as MonoBehaviour).transform;
            return new Ray(t.position, t.forward);
        }

        var interactors = UnityEngine.Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Exclude);
        XRBaseInteractor fallback = null;
        foreach (var interactor in interactors)
        {
            if (interactor == null || !interactor.isActiveAndEnabled) continue;

            if (IsRightHandInteractor(interactor))
            {
                if (interactor is IXRRayProvider rayProv)
                {
                    Transform origin = rayProv.GetOrCreateRayOrigin() ?? interactor.transform;
                    return new Ray(origin.position, origin.forward);
                }
                return new Ray(interactor.transform.position, interactor.transform.forward);
            }

            if (fallback == null) fallback = interactor;
        }

        if (fallback != null)
        {
            if (fallback is IXRRayProvider rayProv)
            {
                Transform origin = rayProv.GetOrCreateRayOrigin() ?? fallback.transform;
                return new Ray(origin.position, origin.forward);
            }
            return new Ray(fallback.transform.position, fallback.transform.forward);
        }

        if (mainCamera != null)
            return new Ray(mainCamera.transform.position, mainCamera.transform.forward);

        return new Ray(transform.position + Vector3.back, Vector3.forward);
    }

    public Vector2 GetControllerThumbstick()
    {
        // 1. Try CommonUsages on XR InputDevices
        var devices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.Right | UnityEngine.XR.InputDeviceCharacteristics.Controller, 
            devices);
        
        foreach (var dev in devices)
        {
            if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis))
            {
                if (axis.sqrMagnitude > 0.01f)
                    return axis;
            }
        }

        devices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.Controller, 
            devices);
        foreach (var dev in devices)
        {
            if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis))
            {
                if (axis.sqrMagnitude > 0.01f)
                    return axis;
            }
            if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondary2DAxis, out Vector2 secAxis))
            {
                if (secAxis.sqrMagnitude > 0.01f)
                    return secAxis;
            }
        }

        // 2. Check InputSystem devices
        foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
        {
            if (device is UnityEngine.InputSystem.XR.XRController)
            {
                var thumb = device.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("thumbstick")
                         ?? device.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("primary2DAxis")
                         ?? device.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("joystick");
                if (thumb != null)
                {
                    Vector2 v = thumb.ReadValue();
                    if (v.sqrMagnitude > 0.01f) return v;
                }
            }
        }

        // 3. Fallback: Gamepad thumbsticks
        if (Gamepad.current != null)
        {
            Vector2 rightStick = Gamepad.current.rightStick.ReadValue();
            if (rightStick.sqrMagnitude > 0.01f) return rightStick;
            Vector2 leftStick = Gamepad.current.leftStick.ReadValue();
            if (leftStick.sqrMagnitude > 0.01f) return leftStick;
        }

        return Vector2.zero;
    }

    private void UpdateTargetingAndRayInput()
    {
        if (busy || completed)
            return;

        // 1. Check if ANY active controller ray is pointing at / hovering the cube
        bool targeted = false;
        IXRSelectInteractor activeAimingInteractor = null;

        // Check native XRI hover status
        if (grab != null && grab.isHovered)
        {
            targeted = true;
            if (grab.interactorsHovering.Count > 0 && grab.interactorsHovering[0] is IXRSelectInteractor sInt)
            {
                activeAimingInteractor = sInt;
                cachedInteractor = sInt;
            }
        }

        if (!targeted)
        {
            var interactors = UnityEngine.Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Exclude);
            foreach (var interactor in interactors)
            {
                if (interactor == null || !interactor.isActiveAndEnabled) continue;

                if (IsRayPassingThroughCube(interactor))
                {
                    targeted = true;
                    if (interactor is IXRSelectInteractor sInt)
                    {
                        activeAimingInteractor = sInt;
                        cachedInteractor = sInt;
                    }
                    break;
                }
            }
        }

        // Editor fallback: Mouse pointer ray in Game view
        if (!targeted && mainCamera != null && Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (mousePos.x >= 0 && mousePos.x <= Screen.width && mousePos.y >= 0 && mousePos.y <= Screen.height)
            {
                Ray mouseRay = mainCamera.ScreenPointToRay(mousePos);
                Vector3 toCubeM = transform.position - mouseRay.origin;
                float projM = Vector3.Dot(toCubeM, mouseRay.direction);
                if (projM > 0f && projM <= interactionReachDistance)
                {
                    Vector3 closestM = mouseRay.origin + mouseRay.direction * projM;
                    if (Vector3.Distance(closestM, transform.position) <= 0.22f)
                    {
                        targeted = true;
                    }
                }
            }
        }

        isTargetedByRay = targeted;

        // Keep originalInteractionLayers so XRI raycasting remains enabled
        if (grab != null)
        {
            grab.interactionLayers = originalInteractionLayers;
        }

        // 2. Input handling: Controller Grip/Trigger + Simulator Keys (HOLD G TO CARRY, RELEASE G TO DROP)
        Keyboard kb = Keyboard.current;
        Mouse mouseCurrent = Mouse.current;

        bool gDown = kb != null && kb.gKey.wasPressedThisFrame;
        bool gHeld = kb != null && kb.gKey.isPressed;
        bool gReleased = kb != null && kb.gKey.wasReleasedThisFrame;

        bool leftDown = false;
        bool leftHeld = false;
        bool leftReleased = false;

        if (mouseCurrent != null)
        {
            bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!isOverUI)
            {
                if (mouseCurrent.leftButton.wasPressedThisFrame) leftDown = true;
                if (mouseCurrent.leftButton.isPressed) leftHeld = true;
                if (mouseCurrent.leftButton.wasReleasedThisFrame) leftReleased = true;
            }
        }

        // GRAB: Aim ray at cube and press/hold [G] or Left Click
        if (!IsGrabbed && isTargetedByRay)
        {
            if (gDown || (gHeld && kb.gKey.wasPressedThisFrame))
            {
                grabbedViaKey = true;
                grabbedViaMouse = false;
                TriggerGrab(activeAimingInteractor);
            }
            else if (leftDown)
            {
                grabbedViaMouse = true;
                grabbedViaKey = false;
                TriggerGrab(activeAimingInteractor);
            }
        }
        // HOLD TO CARRY & RELEASE G TO DROP:
        else if (IsGrabbed)
        {
            bool wantsRelease = false;

            if (grabbedViaKey)
            {
                if (gReleased || !gHeld)
                    wantsRelease = true;
            }
            else if (grabbedViaMouse)
            {
                if (leftReleased || !leftHeld)
                    wantsRelease = true;
            }
            else
            {
                if (gReleased || leftReleased)
                    wantsRelease = true;
            }

            if (wantsRelease)
            {
                if (grabCooldownTimer > 0f)
                {
                    releaseRequested = true;
                }
                else
                {
                    TriggerRelease();
                }
            }
            else if (releaseRequested && grabCooldownTimer <= 0f)
            {
                releaseRequested = false;
                TriggerRelease();
            }
        }
    }

    public void TriggerGrab(IXRSelectInteractor preferredInteractor = null)
    {
        if (busy || completed || IsGrabbed || !isTargetedByRay)
            return;

        IXRSelectInteractor interactor = preferredInteractor ?? GetBestInteractor();
        if (interactor != null && grab != null && grab.interactionManager != null)
        {
            grabCooldownTimer = 0.12f;
            releaseRequested = false;
            grab.interactionManager.SelectEnter(interactor, grab);
        }
    }

    public void TriggerRelease()
    {
        if (!IsGrabbed || grab == null || grab.interactionManager == null)
            return;

        grabbedViaKey = false;
        grabbedViaMouse = false;
        releaseRequested = false;

        if (grab.interactorsSelecting.Count > 0)
        {
            var interactor = grab.interactorsSelecting[0];
            grab.interactionManager.SelectExit(interactor, grab);
        }
    }

    private IXRSelectInteractor GetBestInteractor()
    {
        if (cachedInteractor != null && (cachedInteractor as MonoBehaviour) != null)
            return cachedInteractor;

        var allInteractors = UnityEngine.Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Exclude);
        IXRSelectInteractor rightInteractor = null;
        IXRSelectInteractor fallback = null;

        foreach (var interactor in allInteractors)
        {
            if (interactor == null || !interactor.isActiveAndEnabled) continue;

            if (interactor is IXRSelectInteractor selectInt)
            {
                if (IsRightHandInteractor(interactor))
                {
                    rightInteractor = selectInt;
                    break;
                }

                if (fallback == null)
                    fallback = selectInt;
            }
        }

        return rightInteractor ?? fallback;
    }

    private bool IsRightHandInteractor(XRBaseInteractor interactor)
    {
        if (interactor == null) return false;

        if (interactor is NearFarInteractor nfi)
        {
            if (nfi.handedness == InteractorHandedness.Right)
                return true;
        }

        Transform curr = interactor.transform;
        while (curr != null)
        {
            string n = curr.name.ToLower();
            if (n.Contains("right"))
                return true;
            if (n.Contains("left"))
                return false;
            curr = curr.parent;
        }

        return false;
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        if (busy || completed)
            return;

        cachedInteractor = args.interactorObject;
        grabCooldownTimer = 0.12f;
        releaseRequested = false;

        // Custom smooth ray positioning
        if (grab != null)
        {
            grab.trackPosition = false;
            grab.trackRotation = false;
        }

        SetKinematic(true);

        Ray ray = GetActiveControllerRay();
        grabbedRayDistance = Mathf.Clamp(Vector3.Distance(ray.origin, transform.position), minRayDistance, maxRayDistance);

        // Sound effect triggers once per grab, never loops
        if (audioSource != null && grabSound != null)
            audioSource.PlayOneShot(grabSound);

        SetFeedback($"Cube gripped! Hold [G] to carry | [Q/E] to push/pull ({grabbedRayDistance:F1}m) | Release [G] to drop.", Color.white);

        OnCubeGrabbed?.Invoke();
    }

    void OnRelease(SelectExitEventArgs args)
    {
        grabbedViaKey = false;
        grabbedViaMouse = false;
        releaseRequested = false;

        if (grab != null)
        {
            grab.trackPosition = false;
            grab.trackRotation = false;
        }

        SetKinematic(true);

        // CRITICAL: Immediately lock busy state so Update() never lerps the cube away before CheckPlacement runs!
        if (!busy && !completed)
        {
            busy = true;
            StartCoroutine(CheckPlacement());
        }
    }

    IEnumerator CheckPlacement()
    {
        // 1 frame yield so release event fully finishes
        yield return null;

        if (grab != null && grab.isSelected)
        {
            busy = false;
            yield break;
        }

        if (completed)
        {
            busy = false;
            yield break;
        }

        // Check if the drop position is genuinely inside Container B
        ContainerZone containerB = GetContainerBIfInside();

        if (containerB != null)
        {
            // Dropped inside Container B: SUCCESS!
            yield return StartCoroutine(PlaySuccess(containerB.snapPoint));
        }
        else
        {
            // Dropped anywhere else (Container C, Container A, table): INCORRECT!
            // Turns Red, shakes at drop point, and arcs back to spawn in Container A
            yield return StartCoroutine(PlayFailure());
        }
    }

    private void SetKinematic(bool kinematic)
    {
        if (rb == null) return;
        if (kinematic)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            rb.useGravity = false;
        }
        else
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void FreezeCube()
    {
        SetKinematic(true);
        if (grab != null)
        {
            grab.enabled = false;
        }
    }

    IEnumerator PlaySuccess(Transform snapPoint)
    {
        busy = true;
        FreezeCube();

        SetFeedback("SUCCESS! Correct container (B)!", successColor);

        OnCubeSuccess?.Invoke();

        if (audioSource != null && successSound != null)
            audioSource.PlayOneShot(successSound);

        Vector3 from = transform.position;
        Quaternion fromRotation = transform.rotation;

        Vector3 destination = snapPoint != null
            ? snapPoint.position
            : transform.position;

        Quaternion destinationRotation = snapPoint != null
            ? snapPoint.rotation
            : transform.rotation;

        float duration = 0.45f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);
            float smooth = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(from, destination, smooth);
            transform.rotation = Quaternion.Slerp(fromRotation, destinationRotation, smooth);
            transform.localScale = startScale * (1f + 0.25f * Mathf.Sin(t * Mathf.PI));

            yield return null;
        }

        transform.position = destination;
        transform.rotation = destinationRotation;
        transform.localScale = startScale;

        // Keep vibrant GREEN on success
        currentColor = successColor;
        cubeMaterial.SetColor(colorPropertyId, successColor);

        completed = true;
        busy = false;
    }

    IEnumerator PlayFailure()
    {
        busy = true;
        FreezeCube();

        bool isContC = IsOverContainerC();
        if (isContC)
            SetFeedback("INCORRECT! Placed in Container C (Wrong container).", failureColor);
        else
            SetFeedback("INCORRECT! Must place in Green Container B.", failureColor);

        OnCubeFailure?.Invoke();

        if (audioSource != null && failureSound != null)
            audioSource.PlayOneShot(failureSound);

        // Turn RED during rejection feedback
        currentColor = failureColor;
        cubeMaterial.SetColor(colorPropertyId, failureColor);

        Vector3 wrongPosition = transform.position;
        Quaternion wrongRotation = transform.rotation;

        // 1. Shaking animation right at the rejected position
        float duration = 0.35f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float shake = Mathf.Sin(t * 35f) * 0.04f * (1f - t);
            transform.position = wrongPosition + Vector3.right * shake;
            yield return null;
        }

        // 2. Arc bounce-back animation directly to Container A spawn point (never touching Container B)
        Vector3 returnFrom = transform.position;
        timer = 0f;
        duration = 0.55f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float smooth = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(returnFrom, startPosition, smooth);
            transform.position += Vector3.up * Mathf.Sin(smooth * Mathf.PI) * 0.18f;
            transform.rotation = Quaternion.Slerp(wrongRotation, startRotation, smooth);

            yield return null;
        }

        transform.SetPositionAndRotation(startPosition, startRotation);
        transform.localScale = startScale;

        // Smoothly return to GREEN when resting back in Container A
        currentColor = defaultColor;
        cubeMaterial.SetColor(colorPropertyId, defaultColor);

        SetKinematic(false);
        if (grab != null)
        {
            grab.enabled = true;
            grab.trackPosition = true;
            grab.trackRotation = true;
        }

        currentZone = null;
        busy = false;

        SetFeedback("Waiting for interaction...", Color.white);
    }

    public void ResetCubeToStart()
    {
        StopAllCoroutines();
        busy = false;
        completed = false;
        currentZone = null;

        transform.SetPositionAndRotation(startPosition, startRotation);
        transform.localScale = startScale;

        // Reset to default GREEN
        currentColor = defaultColor;
        if (cubeMaterial != null)
            cubeMaterial.SetColor(colorPropertyId, defaultColor);

        SetKinematic(false);

        if (grab != null)
        {
            grab.enabled = true;
            grab.trackPosition = true;
            grab.trackRotation = true;
        }

        SetFeedback("Waiting for Controller Ray interaction...", Color.cyan);
    }

    void SetFeedback(string message, Color color)
    {
        if (feedbackText == null)
            return;

        string hex = ColorUtility.ToHtmlStringRGB(color);
        feedbackText.text = $"<size=80%><color=#94a3b8>STATUS</color></size>\n<size=105%><color=#{hex}><b>{message}</b></color></size>";
    }

    private void GenerateProceduralAudio()
    {
        if (grabSound == null)
            grabSound = CreateTone(620f, 0.08f, 0.8f);

        if (successSound == null)
            successSound = CreateChime(880f, 1320f, 0.4f);

        if (failureSound == null)
            failureSound = CreateTone(180f, 0.25f, 0.9f);
    }

    private AudioClip CreateTone(float frequency, float duration, float volume = 1f)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (float)i / sampleCount;
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * volume;
        }

        AudioClip clip = AudioClip.Create("Tone_" + frequency, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateChime(float freq1, float freq2, float duration)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (float)i / sampleCount;
            float s = (Mathf.Sin(2f * Mathf.PI * freq1 * t) + Mathf.Sin(2f * Mathf.PI * freq2 * t)) * 0.5f;
            samples[i] = s * envelope;
        }

        AudioClip clip = AudioClip.Create("Chime", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

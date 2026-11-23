using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Feedback;

public class PerformanceOptimizer : MonoBehaviour
{
    [Header("VR Frame Rate Settings")]
    [SerializeField] private int targetFPS = 90;
    [SerializeField] private bool disableVSyncInEditor = true;
    [SerializeField] private bool optimizePhysics = true;
    [SerializeField] private bool optimizeUIRaycasts = true;

    void Awake()
    {
        // 1. High-Performance Frame Rate & V-Sync for VR Headsets & Simulators
        if (disableVSyncInEditor)
        {
            QualitySettings.vSyncCount = 0;
        }

        Application.targetFrameRate = targetFPS;

        // 2. VR Physics Timestep (Smooth 90 Hz physics synchronization without micro-stutters)
        if (optimizePhysics)
        {
            Time.fixedDeltaTime = 1f / targetFPS; // 0.0111s instead of default 0.02s
            Time.maximumDeltaTime = 0.1f;         // Prevent physics spiral on frame drops

            // Physics GC & CPU optimizations
            Physics.reuseCollisionCallbacks = true;
        }

        // 3. Texture & Rendering Settings
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

        // 4. Disable SimpleHapticFeedback on simulated controllers to avoid exception stack-traces
        DisableSimulatedHaptics();

        // 5. Optimize UI Canvas Raycasting in VR
        if (optimizeUIRaycasts)
        {
            OptimizeCanvasRaycasts();
        }

        Debug.Log($"<color=green>[PerformanceOptimizer] VR Optimization Active: {targetFPS} FPS target, 90Hz FixedDeltaTime, ReuseCollisions, Clean UI Raycasts.</color>");
    }

    private void DisableSimulatedHaptics()
    {
        SimpleHapticFeedback[] haptics = Object.FindObjectsByType<SimpleHapticFeedback>(FindObjectsInactive.Exclude);
        foreach (var haptic in haptics)
        {
            if (haptic != null && haptic.enabled)
            {
                haptic.enabled = false;
            }
        }
    }

    private void OptimizeCanvasRaycasts()
    {
        Graphic[] allGraphics = Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include);
        foreach (var g in allGraphics)
        {
            if (g != null && g.GetComponent<Button>() == null)
            {
                g.raycastTarget = false;
            }
        }
    }
}

using UnityEngine;
using System.Collections;

namespace Starter.Platformer
{
    // Ensures KeyShape components are properly initialized
    public class InitializeKeyShapeSystem : MonoBehaviour
    {
        // Layer settings
        [Header("Layer Settings")]
        public LayerMask PlayerLayerMask;
        
        // Debug options
        public bool DebugMode = true;
        
        void Start()
        {
            // Wait a moment for all objects to be properly initialized
            StartCoroutine(DelayedInit());
        }
        
        IEnumerator DelayedInit()
        {
            // Wait for 2 frames to ensure all objects are initialized
            yield return null;
            yield return null;
            
            // Find all KeyShapes in the scene
            KeyShape[] allKeyShapes = FindObjectsOfType<KeyShape>();
            
            if (DebugMode)
                Debug.Log($"InitializeKeyShapeSystem found {allKeyShapes.Length} KeyShapes to initialize");
            
            foreach (KeyShape keyShape in allKeyShapes)
            {
                if (keyShape == null || keyShape.gameObject == null)
                    continue;
                
                // Ensure the PickupTrigger is properly set up
                if (keyShape.PickupTrigger != null)
                {
                    // Ensure the trigger is set up with the proper settings
                    keyShape.PickupTrigger.radius = 2.5f; // Larger radius for easier pickup
                    keyShape.PickupTrigger.isTrigger = true;
                    
                    // Start with the trigger enabled for testing
                    // Once verified, this can be removed and let the normal FallingPlatform handle it
                    keyShape.PickupTrigger.enabled = true;
                    
                    if (DebugMode)
                        Debug.Log($"Initialized PickupTrigger for {keyShape.gameObject.name} - enabled: {keyShape.PickupTrigger.enabled}");
                }
                else
                {
                    if (DebugMode)
                        Debug.LogWarning($"KeyShape {keyShape.gameObject.name} has no PickupTrigger assigned");
                }
                
                // Check if the KeyShape's GameObject is on the correct layer
                // You can adjust this if needed
                if (keyShape.gameObject.layer != LayerMask.NameToLayer("Default"))
                {
                    if (DebugMode)
                        Debug.Log($"KeyShape {keyShape.gameObject.name} is on layer {LayerMask.LayerToName(keyShape.gameObject.layer)}, moving to Default layer");
                    
                    keyShape.gameObject.layer = LayerMask.NameToLayer("Default");
                }
            }
            
            if (DebugMode)
                Debug.Log("InitializeKeyShapeSystem completed initialization");
        }
    }
} 
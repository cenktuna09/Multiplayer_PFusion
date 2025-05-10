using Fusion;
using UnityEngine;

namespace Starter.Platformer
{
    // UnlockShape areas that can be unlocked with non-matching KeyShapes
    public class UnlockShape : NetworkBehaviour
    {
        [Networked]
        public ShapeType Type { get; set; }

        [Networked]
        public NetworkBool IsUnlocked { get; set; }

        // Reference to GameManager
        public GameManager GameManager; 

        [Header("Visual Elements")]
        // Visual for unlocked state
        public GameObject LockedVisual;
        public GameObject UnlockedVisual;
        
        // Visual effects for success
        public ParticleSystem SuccessParticles;

        [Header("Audio")]
        // Audio for unlock
        public AudioClip UnlockSound;
        public float UnlockSoundVolume = 1.0f;

        // Reference to the unlock trigger area
        public Collider TriggerArea;
        
        // Debug settings
        public bool ShowDebugLogs = true;

        public override void Spawned()
        {
            // Find GameManager if not assigned
            if (GameManager == null)
            {
                GameManager = FindObjectOfType<GameManager>();
                DebugLog("Found GameManager: " + (GameManager != null));
            }
            
            // Initialize unlock state visual
            UpdateUnlockVisual();
            
            DebugLog($"UnlockShape spawned. Type: {Type}, IsUnlocked: {IsUnlocked}");
        }

        // Set the shape type and update visuals
        public void SetShapeType(ShapeType type)
        {
            Type = type;
            DebugLog($"ShapeType set to: {type}");
        }

        // Update visual state based on unlock state
        private void UpdateUnlockVisual()
        {
            if (LockedVisual != null) LockedVisual.SetActive(!IsUnlocked);
            if (UnlockedVisual != null) UnlockedVisual.SetActive(IsUnlocked);
            
            // Play success particles if newly unlocked
            if (IsUnlocked && SuccessParticles != null)
            {
                SuccessParticles.Play();
            }
        }

        // Try to unlock with a key shape
        public bool TryUnlock(KeyShape keyShape)
        {
            DebugLog($"TryUnlock called with KeyShape type: {keyShape.Type}. Current UnlockShape type: {Type}");
            
            if (HasStateAuthority && !IsUnlocked)
            {
                DebugLog($"Has state authority and not unlocked yet.");
                
                // "Wrong Answers Only" mechanic:
                // Can only unlock if the KeyShape type DOESN'T match the UnlockShape type
                if (keyShape.Type != Type)
                {
                    DebugLog($"KeyShape type ({keyShape.Type}) doesn't match UnlockShape type ({Type}). Unlocking!");
                    IsUnlocked = true;
                    
                    // Play unlock sound
                    if (UnlockSound != null)
                        AudioSource.PlayClipAtPoint(UnlockSound, transform.position, UnlockSoundVolume);
                    
                    // Update visuals
                    UpdateUnlockVisual();
                    
                    // Notify GameManager about the solved puzzle
                    NotifyGameManager();
                    
                    return true;
                }
                else
                {
                    DebugLog($"KeyShape type ({keyShape.Type}) MATCHES UnlockShape type ({Type}). Cannot unlock!");
                }
            }
            else
            {
                DebugLog($"Cannot unlock. HasStateAuthority: {HasStateAuthority}, IsUnlocked: {IsUnlocked}");
            }
            return false;
        }
        
        // Notify GameManager about this unlock
        private void NotifyGameManager()
        {
            if (HasStateAuthority && GameManager != null)
            {
                DebugLog("Notifying GameManager about solved puzzle");
                // Notify the GameManager that another puzzle is solved
                GameManager.RPC_PuzzleSolved();
            }
            else
            {
                DebugLog($"Cannot notify GameManager. HasStateAuthority: {HasStateAuthority}, GameManager is null: {GameManager == null}");
            }
        }
        
        // Reset the unlock state (for reusing/resetting levels)
        public void ResetLock()
        {
            if (HasStateAuthority)
            {
                IsUnlocked = false;
                UpdateUnlockVisual();
                DebugLog("Lock reset");
            }
        }
        
        // Helper method for debug logging
        private void DebugLog(string message)
        {
            if (ShowDebugLogs)
            {
                Debug.Log($"[UnlockShape {gameObject.name}] {message}");
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // Only process on state authority
            if (HasStateAuthority == false)
            {
                DebugLog("OnTriggerEnter: No state authority");
                return;
            }
                
            if (IsUnlocked)
            {
                DebugLog("OnTriggerEnter: Already unlocked");
                return;
            }
            
            DebugLog($"OnTriggerEnter: {other.gameObject.name} entered trigger area");
            
            // Check if a KeyShape directly entered the trigger area
            KeyShape keyShape = other.GetComponent<KeyShape>();
            if (keyShape != null && !keyShape.IsPickedUp)
            {
                DebugLog($"KeyShape detected: {keyShape.gameObject.name}, Type: {keyShape.Type}");
                if (TryUnlock(keyShape))
                {
                    DebugLog("Successfully unlocked with KeyShape");
                }
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (!HasStateAuthority) return;
            
            DebugLog($"OnTriggerExit: {other.gameObject.name} left trigger area");
        }
    }
} 
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
        
        [Networked]
        private NetworkBool _hasUpdatedParticleColor { get; set; }

        [Networked]
        private NetworkBool _previousLockState { get; set; }
        
        [Networked]
        private NetworkBool _wasUnlockedByDisconnectedPlayer { get; set; }
        
        // New: Track particle color state via network
        [Networked]
        private int _networkParticleColorState { get; set; } // 0=yellow, 1=green, 2=red
        
        // Last change tick
        [Networked]
        private int _particleColorChangedTick { get; set; }

        // Reference to GameManager
        public GameManager GameManager; 

        [Header("Visual Elements")]
        // Visual for unlocked state
        public GameObject LockedVisual;
        public GameObject UnlockedVisual;
        
        // Visual effects for success
        public ParticleSystem ZoneParticles;

        [Header("Audio")]
        // Audio for unlock
        public AudioClip UnlockSound;
        public float UnlockSoundVolume = 1.0f;

        // Reference to the unlock trigger area
        public Collider TriggerArea;
        
        // Debug settings
        public bool ShowDebugLogs = true;
        
        // Store initial unlocked state for reconnection
        private bool _initialUnlockState = false;
        
        // State for last applied particle color change
        private int _lastAppliedColorState = -1;

        public override void Spawned()
        {
            // Find GameManager if not assigned
            if (GameManager == null)
            {
                GameManager = FindObjectOfType<GameManager>();
                DebugLog("Found GameManager: " + (GameManager != null));
            }
            
            // Initialize the previous state
            _previousLockState = IsUnlocked;
            _initialUnlockState = IsUnlocked;
            
            // Starting Color : Yellow
            _networkParticleColorState = 0;
            
            DebugLog($"UnlockShape spawned. Type: {Type}, IsUnlocked: {IsUnlocked}");
            
            // Ensure visuals match current state immediately
            UpdateVisuals();
        }
        
        // Update visual state to match current unlock state
        private void UpdateVisuals()
        {
            if (LockedVisual != null) LockedVisual.SetActive(!IsUnlocked);
            if (UnlockedVisual != null) UnlockedVisual.SetActive(IsUnlocked);
            
            // Update particle color based on unlock state
            UpdateParticleColor();
        }
        
        // Update particle color based on networked state
        private void UpdateParticleColor()
        {
            // If the particle system exists and the last applied color is different
            if (ZoneParticles != null && _lastAppliedColorState != _networkParticleColorState)
            {
                var main = ZoneParticles.main;
                
                // Apply the color change based on the networked state
                switch (_networkParticleColorState)
                {
                    case 0: // Yellow (locked)
                        main.startColor = Color.yellow;
                        DebugLog("Setting particle color to yellow (from network state)");
                        break;
                    case 1: // Green (unlocked)
                        main.startColor = Color.green;
                        DebugLog("Setting particle color to green (from network state)");
                        break;
                    case 2: // Red (wrong match)
                        main.startColor = Color.red;
                        DebugLog("Setting particle color to red (from network state)");
                        break;
                }
                
                // Apply the color change
                _lastAppliedColorState = _networkParticleColorState;
            }
        }

        // Set the shape type and update visuals
        public void SetShapeType(ShapeType type)
        {
            Type = type;
            DebugLog($"ShapeType set to: {type}");
        }

        public override void Render()
        {
            // Update visual state in Render for all clients
            if (LockedVisual != null) LockedVisual.SetActive(!IsUnlocked);
            if (UnlockedVisual != null) UnlockedVisual.SetActive(IsUnlocked);
            
            // Check and update particle color every frame
            UpdateParticleColor();
            
            // Only log when the state changes
            if (_previousLockState != IsUnlocked)
            {
                if (IsUnlocked)
                {
                    DebugLog("UnlockShape is now unlocked, updating visuals");
                }
                else
                {
                    DebugLog("UnlockShape is now locked, updating visuals");
                }
                _previousLockState = IsUnlocked;
            }
        }
        
        // Public method to set particle color - all changes should go through here
        public void SetParticleColor(Color color)
        {
            if (!HasStateAuthority) return;
            
            if (color == Color.yellow)
            {
                _networkParticleColorState = 0;
                DebugLog("Set networked particle color to YELLOW");
            }
            else if (color == Color.green)
            {
                _networkParticleColorState = 1;
                DebugLog("Set networked particle color to GREEN");
            }
            else if (color == Color.red)
            {
                _networkParticleColorState = 2;
                DebugLog("Set networked particle color to RED");
            }
            
            // Save the color change tick
            _particleColorChangedTick = Runner.Tick;
        }

        // Try to unlock with a key shape
        public bool TryUnlock(KeyShape keyShape)
        {
            DebugLog($"TryUnlock called with KeyShape type: {keyShape.Type}. Current UnlockShape type: {Type}");
            
            if (IsUnlocked)
            {
                DebugLog("Already unlocked, cannot unlock again");
                return false;
            }
            
            if (HasStateAuthority)
            {
                DebugLog($"Has state authority and trying to unlock.");
                
                // Set particle color based on match result
                if (ZoneParticles != null)
                {
                    if (keyShape.Type == Type)
                    {
                        // Red color for incorrect placement (matching shapes)
                        SetParticleColor(Color.red);
                        DebugLog($"KeyShape type ({keyShape.Type}) MATCHES UnlockShape type ({Type}). Cannot unlock!");
                        return false;
                    }
                }
                
                // "Wrong Answers Only" mechanic:
                // Can only unlock if the KeyShape type DOESN'T match the UnlockShape type
                if (keyShape.Type != Type)
                {
                    DebugLog($"KeyShape type ({keyShape.Type}) doesn't match UnlockShape type ({Type}). Unlocking!");
                    
                    // Explicitly set all related properties
                    IsUnlocked = true;
                    _previousLockState = true;
                    _hasUpdatedParticleColor = true;
                    _wasUnlockedByDisconnectedPlayer = false;
                    
                    // Green color for successful unlock
                    SetParticleColor(Color.green);
                    
                    // Play unlock sound
                    if (UnlockSound != null)
                        AudioSource.PlayClipAtPoint(UnlockSound, transform.position, UnlockSoundVolume);
                    
                    // Update visuals immediately 
                    UpdateVisuals();
                    
                    // Important: Notify GameManager about the puzzle is solved
                    NotifyGameManager();
                    
                    return true;
                }
            }
            else
            {
                DebugLog($"Cannot unlock. HasStateAuthority: {HasStateAuthority}, IsUnlocked: {IsUnlocked}");
                // Try requesting authority
                Object.RequestStateAuthority();
            }
            return false;
        }
        
        // Notify GameManager about this unlock
        private void NotifyGameManager()
        {
            if (GameManager != null)
            {
                DebugLog("Notifying GameManager about solved puzzle");
                
                if (HasStateAuthority)
                {
                    // Notify the GameManager that another puzzle is solved
                    GameManager.RPC_PuzzleSolved();
                }
                else
                {
                    DebugLog("No state authority, requesting...");
                    Object.RequestStateAuthority();
                }
            }
            else
            {
                DebugLog("Cannot notify GameManager - GameManager is null");
                // Try to find GameManager if it wasn't set
                GameManager = FindObjectOfType<GameManager>();
                if (GameManager != null)
                {
                    DebugLog("Found GameManager, notifying about solved puzzle");
                    if (HasStateAuthority)
                    {
                        GameManager.RPC_PuzzleSolved();
                    }
                }
            }
        }
        
        // Reset the unlock state (for reusing/resetting levels)
        public void ResetLock()
        {
            if (HasStateAuthority)
            {
                DebugLog($"ResetLock called. Current state - IsUnlocked: {IsUnlocked}, _wasUnlockedByDisconnectedPlayer: {_wasUnlockedByDisconnectedPlayer}");
                
                // Forcefully lock the shape
                IsUnlocked = false;
                _hasUpdatedParticleColor = false;
                _previousLockState = false;
                _wasUnlockedByDisconnectedPlayer = false;
                
                // Reset the particle color to yellow (locked state)
                SetParticleColor(Color.yellow);
                
                // Update visuals immediately
                UpdateVisuals();
                
                DebugLog("Lock reset complete and visuals updated");
            }
            else
            {
                DebugLog("Cannot reset lock - no state authority");
                // Try to request authority
                Object.RequestStateAuthority();
            }
        }
        
        // Force reset with additional safeguards
        public void ForceReset()
        {
            DebugLog("ForceReset called - will ensure UnlockShape is fully reset");
            
            if (!HasStateAuthority)
            {
                DebugLog("ForceReset: No state authority, requesting...");
                Object.RequestStateAuthority();
                return;
            }
            
            // Resetle tüm değerleri
            IsUnlocked = false;
            _hasUpdatedParticleColor = false;
            _previousLockState = false;
            _wasUnlockedByDisconnectedPlayer = false;
            
            // Reset the particle color to yellow
            SetParticleColor(Color.yellow);
            
            // Update visuals
            UpdateVisuals();
            
            // Extra check
            if (LockedVisual != null) LockedVisual.SetActive(true);
            if (UnlockedVisual != null) UnlockedVisual.SetActive(false);
            
            DebugLog("ForceReset completed - UnlockShape is now fully reset");
        }
        
        // Fix unlock state after a player disconnect
        public void ForceUnlock()
        {
            if (HasStateAuthority)
            {
                if (!IsUnlocked)
                {
                    DebugLog("Forcing unlock after player disconnect");
                    IsUnlocked = true;
                    _wasUnlockedByDisconnectedPlayer = true;
                    
                    // Update visuals
                    UpdateVisuals();
                    
                    // Notify GameManager if needed
                    NotifyGameManager();
                }
            }
        }
        
        // Handle player disconnection - called from GameManager.OnPlayerLeft
        public void HandlePlayerLeft(PlayerRef player)
        {
            DebugLog($"Handling player {player} left event");
            
            if (!HasStateAuthority)
            {
                // Try to request authority if we need it
                DebugLog($"No state authority in HandlePlayerLeft, requesting...");
                Object.RequestStateAuthority();
                return;
            }
            
            // If the shape was already unlocked, make sure it stays that way
            if (IsUnlocked || _wasUnlockedByDisconnectedPlayer)
            {
                DebugLog($"Shape was unlocked by a disconnected player, ensuring it stays unlocked");
                IsUnlocked = true;
                _wasUnlockedByDisconnectedPlayer = true;
                
                // Ensure GameManager is notified
                if (GameManager != null)
                {
                    DebugLog("Re-notifying GameManager about solved puzzle after disconnect");
                    GameManager.RPC_PuzzleSolved();
                }
            }
            
            // Always update visuals to ensure clients see the correct state
            UpdateVisuals();
            
            DebugLog($"UnlockShape state after player {player} left: IsUnlocked={IsUnlocked}");
        }
        
        // Handle network shutdown event - called from GameManager.OnShutdown
        public void HandleNetworkShutdown()
        {
            DebugLog("Handling network shutdown");
            
            // Ensure the UnlockShape maintains correct state during shutdown
            if (HasStateAuthority)
            {
                // If this shape was unlocked, make sure it stays that way
                if (IsUnlocked || _wasUnlockedByDisconnectedPlayer)
                {
                    DebugLog("Ensuring UnlockShape stays unlocked during shutdown");
                    IsUnlocked = true;
                    _wasUnlockedByDisconnectedPlayer = true;
                }
                
                // Always update visuals to ensure proper state is displayed
                UpdateVisuals();
            }
            else
            {
                DebugLog("No state authority during network shutdown");
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
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

        // Visual representation for each shape type
        public GameObject TriangleVisual;
        public GameObject SquareVisual;
        public GameObject RectangleVisual;

        // Visual for unlocked state
        public GameObject LockedVisual;
        public GameObject UnlockedVisual;

        // Audio for unlock
        public AudioClip UnlockSound;
        public float UnlockSoundVolume = 1.0f;

        // Reference to the unlock trigger area
        public Collider TriggerArea;

        public override void Spawned()
        {
            // Initialize visuals based on assigned type
            UpdateVisuals();
            
            // Initialize unlock state visual
            UpdateUnlockVisual();
        }

        // Set the shape type and update visuals
        public void SetShapeType(ShapeType type)
        {
            Type = type;
            UpdateVisuals();
        }

        // Enable the correct visual based on shape type
        private void UpdateVisuals()
        {
            if (TriangleVisual != null) TriangleVisual.SetActive(Type == ShapeType.Triangle);
            if (SquareVisual != null) SquareVisual.SetActive(Type == ShapeType.Square);
            if (RectangleVisual != null) RectangleVisual.SetActive(Type == ShapeType.Rectangle);
        }

        // Update visual state based on unlock state
        private void UpdateUnlockVisual()
        {
            if (LockedVisual != null) LockedVisual.SetActive(!IsUnlocked);
            if (UnlockedVisual != null) UnlockedVisual.SetActive(IsUnlocked);
        }

        // Try to unlock with a key shape
        public bool TryUnlock(KeyShape keyShape)
        {
            if (HasStateAuthority && !IsUnlocked)
            {
                // "Wrong Answers Only" mechanic:
                // Can only unlock if the KeyShape type DOESN'T match the UnlockShape type
                if (keyShape.Type != Type)
                {
                    IsUnlocked = true;
                    
                    // Play unlock sound
                    if (UnlockSound != null)
                        AudioSource.PlayClipAtPoint(UnlockSound, transform.position, UnlockSoundVolume);
                    
                    UpdateUnlockVisual();
                    return true;
                }
            }
            return false;
        }
        
        // Reset the unlock state (for reusing/resetting levels)
        public void ResetLock()
        {
            if (HasStateAuthority)
            {
                IsUnlocked = false;
                UpdateUnlockVisual();
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // Only process on state authority
            if (HasStateAuthority == false)
                return;
                
            if (IsUnlocked)
                return;
                
            // Check if a player with a key entered the trigger area
            Player player = other.GetComponent<Player>();
            if (player != null && player.HeldKeyShape != null)
            {
                TryUnlock(player.HeldKeyShape);
                if (IsUnlocked)
                {
                    // Remove key from player if unlocking was successful
                    player.HeldKeyShape.Drop();
                    player.HeldKeyShape = null;
                }
            }
        }
    }
} 
using Fusion;
using UnityEngine;

namespace Starter.Platformer
{
    // KeyShape falls from platforms and can be picked up by players
    public class KeyShape : NetworkBehaviour
    {
        [Networked]
        public ShapeType Type { get; set; }
        
        [Networked]
        public NetworkBool IsPickedUp { get; set; }
        
        [Networked]
        public PlayerRef PickedUpBy { get; set; }
        
        public Rigidbody Rigidbody;
        public Collider ShapeCollider;
        
        // Visual representation to make shape type visible
        public GameObject TriangleVisual;
        public GameObject SquareVisual;
        public GameObject RectangleVisual;
        
        // Sound when picked up 
        public AudioClip PickupSound;
        public float PickupSoundVolume = 0.5f;
        
        // Original Y position above ground for when held by player
        private float _heldYOffset = 1.5f;
        
        public override void Spawned()
        {
            // Ensure rigidbody reference is set
            if (Rigidbody == null)
                Rigidbody = GetComponent<Rigidbody>();
                
            // Ensure collider reference is set
            if (ShapeCollider == null)
                ShapeCollider = GetComponent<Collider>();
                
            // Update visual based on assigned type
            UpdateVisuals();
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
        
        // Make the shape fall when platform falls
        public void StartFalling()
        {
            Rigidbody.isKinematic = false;
        }
        
        // Handle player picking up the shape
        public void PickUp(PlayerRef player)
        {
            if (HasStateAuthority)
            {
                IsPickedUp = true;
                PickedUpBy = player;
                Rigidbody.isKinematic = true;
                ShapeCollider.enabled = false;
                
                // Play pickup sound
                if (PickupSound != null)
                    AudioSource.PlayClipAtPoint(PickupSound, transform.position, PickupSoundVolume);
            }
        }
        
        // Handle player dropping the shape
        public void Drop()
        {
            if (HasStateAuthority)
            {
                IsPickedUp = false;
                PickedUpBy = default;
                Rigidbody.isKinematic = false;
                ShapeCollider.enabled = true;
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            // If picked up, follow the player
            if (IsPickedUp)
            {
                if (Runner.TryGetPlayerObject(PickedUpBy, out NetworkObject playerObject))
                {
                    Player player = playerObject.GetComponent<Player>();
                    if (player != null)
                    {
                        // Position the shape in front of the player
                        Vector3 holdPosition = player.transform.position + player.transform.forward * 1.0f;
                        holdPosition.y = player.transform.position.y + _heldYOffset;
                        transform.position = holdPosition;
                    }
                }
            }
        }
    }
} 
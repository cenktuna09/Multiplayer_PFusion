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
        
        // Trigger collider for easier pickup
        public SphereCollider PickupTrigger;
        public float PickupTriggerRadius = 3.5f;
        
        // Visual representation to make shape type visible
        public GameObject TriangleVisual;
        public GameObject SquareVisual;
        public GameObject RectangleVisual;
        
        // Sound when picked up 
        public AudioClip PickupSound;
        public float PickupSoundVolume = 0.5f;
        
        // Original Y position above ground for when held by player
        private float _heldYOffset = 1.5f;

        // Player currently in pickup range
        private Player _playerInRange;
        
        // Spawned flag to check if networked properties can be accessed
        private bool _isFullySpawned = false;
        
        public override void Spawned()
        {
            // Ensure rigidbody reference is set
            if (Rigidbody == null)
                Rigidbody = GetComponent<Rigidbody>();
                
            // Ensure collider reference is set
            if (ShapeCollider == null)
                ShapeCollider = GetComponent<Collider>();
            
            // Setup pickup trigger if not assigned
            if (PickupTrigger == null)
            {
                Debug.Log($"Creating PickupTrigger for {gameObject.name}");
                PickupTrigger = gameObject.AddComponent<SphereCollider>();
                PickupTrigger.radius = PickupTriggerRadius;
                PickupTrigger.isTrigger = true;
            }
            
            // Increase radius to make pickup easier
            PickupTrigger.radius = 2.5f; // Increased from default value
            
            // Disable pickup trigger until the shape falls
            PickupTrigger.enabled = false;
            Debug.Log($"KeyShape {gameObject.name} spawned. PickupTrigger enabled: {PickupTrigger.enabled}");
                
            // Update visual based on assigned type
            UpdateVisuals();
            
            // Mark as fully spawned to allow networked property access
            _isFullySpawned = true;
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
            
            // Enable pickup trigger when the shape falls
            PickupTrigger.enabled = true;
            Debug.Log($"KeyShape {gameObject.name} started falling. PickupTrigger enabled: {PickupTrigger.enabled}");
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
                
                // Disable pickup trigger
                PickupTrigger.enabled = false;
                
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
                
                // Re-enable pickup trigger
                PickupTrigger.enabled = true;
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
                        // Position the shape 5 units in front of the player
                        Vector3 holdPosition = player.transform.position + new Vector3(0, 0, 5.0f);
                        
                        // Keep the same height as the player plus offset
                        holdPosition.y = player.transform.position.y + _heldYOffset;

                        // Smoothly move the shape to the hold position
                        transform.position = Vector3.Lerp(transform.position, holdPosition, Runner.DeltaTime * 10f);
                        
                        // Make the shape face the same direction as the player
                        transform.rotation = Quaternion.Slerp(transform.rotation, player.transform.rotation, Runner.DeltaTime * 8f);
                        
                    }
                }
            }
        }
        
        // Register player entering pickup range
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"OnTriggerEnter: {other.gameObject.name} entered trigger of {gameObject.name}");
            
            // Safety check to prevent accessing networked properties before spawn
            if (!_isFullySpawned || !Object || !Object.IsValid)
            {
                Debug.LogWarning($"KeyShape {gameObject.name} not fully spawned or Object not valid");
                return;
            }
                
            // Now it's safe to check IsPickedUp
            if (Object.IsValid && IsPickedUp)
            {
                Debug.Log($"KeyShape {gameObject.name} is already picked up");
                return;
            }

            // Check if the collider belongs to a SimpleKCC
            var kcc = other.GetComponent<Fusion.Addons.SimpleKCC.SimpleKCC>();
            if (kcc != null)
            {
                // Get the Player component from the KCC's gameObject
                Player player = kcc.gameObject.GetComponent<Player>();
                if (player != null)
                {
                    Debug.Log($"Setting player {player.gameObject.name} as in range for {gameObject.name} (via KCC)");
                    _playerInRange = player;
                }
            }
        }
        
        // Unregister player leaving pickup range
        private void OnTriggerExit(Collider other)
        {
            Debug.Log($"OnTriggerExit: {other.gameObject.name} exited trigger of {gameObject.name}");
            
            // Safety check 
            if (!_isFullySpawned || !Object || !Object.IsValid)
                return;
                
            // Check if the collider belongs to a SimpleKCC
            var kcc = other.GetComponent<Fusion.Addons.SimpleKCC.SimpleKCC>();
            if (kcc != null)
            {
                // Get the Player component from the KCC's gameObject
                Player player = kcc.gameObject.GetComponent<Player>();
                if (player != null && player == _playerInRange)
                {
                    Debug.Log($"Clearing player {player.gameObject.name} from range of {gameObject.name} (via KCC)");
                    _playerInRange = null;
                }
            }
        }
        
        // Get the player in range, if any
        public Player GetPlayerInRange()
        {
            // Only return player if we're fully spawned
            if (!_isFullySpawned || !Object || !Object.IsValid)
            {
                Debug.LogWarning($"GetPlayerInRange called on {gameObject.name} but not fully spawned or Object not valid");
                return null;
            }
                
            return _playerInRange;
        }
    }
} 
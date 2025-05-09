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
        
        // Network position for more accurate synchronization
        [Networked]
        private Vector3 NetworkPosition { get; set; }
        
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
            
            // Initialize network position
            NetworkPosition = transform.position;
            
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
            
            // Apply downward force like FallingPlatform
            Rigidbody.AddForce(Vector3.down * 20f, ForceMode.Impulse);
            
            // Enable pickup trigger when the shape falls
            PickupTrigger.enabled = true;
            Debug.Log($"KeyShape {gameObject.name} started falling. PickupTrigger enabled: {PickupTrigger.enabled}");
        }
        
        // Request pickup via RPC - call this from the client
        public void RequestPickup(PlayerRef player)
        {
            if (Object && Object.IsValid && !IsPickedUp)
            {
                Debug.Log($"Player {player} requesting pickup of {gameObject.name}");
                RPC_PickUp(player);
            }
        }
        
        // Handle player picking up the shape
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_PickUp(PlayerRef player)
        {
            Debug.Log($"RPC_PickUp called for player {player} on {gameObject.name}");
            if (Object && Object.IsValid && !IsPickedUp)
            {
                IsPickedUp = true;
                PickedUpBy = player;
                Rigidbody.isKinematic = true;
                
                // Collider Trigger
                ShapeCollider.isTrigger = true;
                ShapeCollider.enabled = true;
                
                // Disable pickup trigger
                PickupTrigger.enabled = false;
                
                // Random Up Force
                float randomUpForce = Random.Range(5f, 10f);
                transform.position += Vector3.up * 0.5f; 
                Rigidbody.AddForce(Vector3.up * randomUpForce, ForceMode.Impulse);
                
                // Play pickup sound
                if (PickupSound != null)
                    AudioSource.PlayClipAtPoint(PickupSound, transform.position, PickupSoundVolume);
            }
        }
        
        // Request drop via RPC - call this from the client
        public void RequestDrop()
        {
            if (Object && Object.IsValid && IsPickedUp)
            {
                Debug.Log($"Player {PickedUpBy} requesting drop of {gameObject.name}");
                RPC_Drop();
            }
        }
        
        // Handle player dropping the shape
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Drop()
        {
            Debug.Log($"RPC_Drop called for {gameObject.name}");
            if (Object && Object.IsValid && IsPickedUp)
            {
                IsPickedUp = false;
                PickedUpBy = default;
                Rigidbody.isKinematic = false;
                
                // Collider Trigger 
                ShapeCollider.isTrigger = false;
                ShapeCollider.enabled = true;
                
                // Re-enable pickup trigger
                PickupTrigger.enabled = true;
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            // Safety check
            if (!Object || !Object.IsValid)
                return;
                
            // If picked up, follow the player
            if (IsPickedUp)
            {
                if (Runner.TryGetPlayerObject(PickedUpBy, out NetworkObject playerObject))
                {
                    Player player = playerObject.GetComponent<Player>();
                    if (player != null)
                    {
                        // Only state authority should update the network position
                        if (HasStateAuthority)
                        {
                            // Position the shape in front of the player
                            Vector3 holdPosition = player.transform.position;
                            
                            // Keep the same height as the player plus offset
                            holdPosition.y = player.transform.position.y + 2.0f;
    
                            // Update networked positions
                            NetworkPosition = holdPosition;
                            
                            // Smoothly move to the desired position using Slerp
                            transform.position = Vector3.Slerp(transform.position, holdPosition, Runner.DeltaTime * 20.0f);
                        }
                    }
                }
            }
            else if (HasStateAuthority && !Rigidbody.isKinematic)
            {
                // Only state authority updates network position from physics
                NetworkPosition = transform.position;
            }
        }
        
        // Handle position updates for non-state authority clients
        public override void Render()
        {
            // Only non-state authority needs to update position in Render
            if (!HasStateAuthority && Object && Object.IsValid)
            {
                // Use Slerp for smooth movement
                transform.position = Vector3.Slerp(transform.position, NetworkPosition, Time.deltaTime * 2.0f);
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
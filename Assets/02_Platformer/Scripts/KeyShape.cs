using Fusion;
using UnityEngine;
using TMPro;

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
        
        // Network collider state to ensure sync across clients
        [Networked]
        public NetworkBool IsColliderTrigger { get; set; }
        
        [Networked]
        public NetworkBool IsColliderEnabled { get; set; }
        
        [Networked]
        public NetworkBool IsPickupTriggerEnabled { get; set; }
        
        [Networked]
        public NetworkBool IsKinematic { get; set; }
        
        public Rigidbody Rigidbody;
        public Collider ShapeCollider;
        
        // Trigger collider for easier pickup
        public SphereCollider PickupTrigger;
        public float PickupTriggerRadius = 3.5f;
        
        // Visual representation to make shape type visible
        public GameObject TriangleVisual;
        public GameObject SquareVisual;
        public GameObject CircleVisual;
        
        // Sound when picked up 
        public AudioClip PickupSound;
        public float PickupSoundVolume = 0.5f;
        
        // # UI Elements
        [Header("UI")]
        public Canvas WorldSpaceCanvas;
        public TextMeshProUGUI InteractionText;
        public string PickupPrompt = "Press E to Pickup";
        public string DropPrompt = "Press E to Drop";
        
        // Original Y position above ground for when held by player
        private float _heldYOffset = 1.5f;

        // Player currently in pickup range
        private Player _playerInRange;
        
        // Spawned flag to check if networked properties can be accessed
        private bool _isFullySpawned = false;
        
        // Debug settings
        [Header("Debug")]
        public bool ShowDebugLogs = true;
        
        private void DebugLog(string message)
        {
            if (ShowDebugLogs)
            {
                Debug.Log($"[KeyShape {gameObject.name}] {message}");
            }
        }

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
                DebugLog("Creating PickupTrigger");
                PickupTrigger = gameObject.AddComponent<SphereCollider>();
                PickupTrigger.radius = PickupTriggerRadius;
                PickupTrigger.isTrigger = true;
            }
            
            // Increase radius to make pickup easier
            PickupTrigger.radius = 2.5f; // Increased from default value
            
            // Initialize networked collider state
            IsColliderTrigger = ShapeCollider.isTrigger;
            IsColliderEnabled = ShapeCollider.enabled;
            IsPickupTriggerEnabled = false;
            IsKinematic = Rigidbody.isKinematic;
            
            // Disable pickup trigger until the shape falls
            PickupTrigger.enabled = false;
            DebugLog($"Spawned. PickupTrigger enabled: {PickupTrigger.enabled}");

            // Hide interaction text at start
            if (InteractionText != null)
            {
                InteractionText.text = PickupPrompt;
                InteractionText.gameObject.SetActive(false);
            }
                
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
            if (CircleVisual != null) CircleVisual.SetActive(Type == ShapeType.Circle);
        }
        
        // Make the shape fall when platform falls
        public void StartFalling()
        {
            if (HasStateAuthority)
            {
                IsKinematic = false;
                IsPickupTriggerEnabled = true;
                Rigidbody.isKinematic = false;
                
                // Apply downward force like FallingPlatform
                Rigidbody.AddForce(Vector3.down * 20f, ForceMode.Impulse);
            }
            
            DebugLog($"started falling. PickupTrigger enabled: {PickupTrigger.enabled}");
        }
        
        // Request pickup via RPC - call this from the client
        public void RequestPickup(PlayerRef player)
        {
            if (Object && Object.IsValid && !IsPickedUp)
            {
                DebugLog($"Player {player} requesting pickup");
                RPC_PickUp(player);
            }
        }
        
        // Handle player picking up the shape
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_PickUp(PlayerRef player)
        {
            DebugLog($"RPC_PickUp called for player {player}");
            if (Object && Object.IsValid && !IsPickedUp)
            {
                IsPickedUp = true;
                PickedUpBy = player;
                
                // Update networked state
                IsKinematic = true;
                IsColliderTrigger = true;
                IsColliderEnabled = true;
                IsPickupTriggerEnabled = false;
                
                // Apply changes locally on state authority
                Rigidbody.isKinematic = true;
                ShapeCollider.isTrigger = true;
                ShapeCollider.enabled = true;
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
                DebugLog($"Player {PickedUpBy} requesting drop");
                RPC_Drop();
            }
        }
        
        // Handle player dropping the shape
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Drop()
        {
            DebugLog("RPC_Drop called");
            if (Object && Object.IsValid && IsPickedUp)
            {
                IsPickedUp = false;
                PickedUpBy = default;
                
                // Update networked state
                IsKinematic = false;
                IsColliderTrigger = false;
                IsColliderEnabled = true;
                IsPickupTriggerEnabled = true;
                
                // Apply changes locally on state authority
                Rigidbody.isKinematic = false;
                ShapeCollider.isTrigger = false;
                ShapeCollider.enabled = true;
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
                            transform.position = Vector3.Slerp(transform.position, holdPosition, Runner.DeltaTime * 2.5f);
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
            // Safety check
            if (!Object || !Object.IsValid)
                return;

            // Update physical state based on networked values for all clients
            ShapeCollider.isTrigger = IsColliderTrigger;
            ShapeCollider.enabled = IsColliderEnabled;
            PickupTrigger.enabled = IsPickupTriggerEnabled;
            Rigidbody.isKinematic = IsKinematic;
                
            // Position updates for non-state authority clients
            if (!HasStateAuthority)
            {
                // Use direct assignment for better sync when picked up
                if (IsPickedUp)
                {
                    transform.position = NetworkPosition;
                }
                else
                {
                    // Use smoother interpolation for physics objects
                    transform.position = Vector3.Slerp(transform.position, NetworkPosition, Time.deltaTime * 10f);
                }
            }

            // Update UI text state
            if (InteractionText != null)
            {
                // Update text visibility based on pickup trigger state
                InteractionText.gameObject.SetActive(IsPickupTriggerEnabled || IsPickedUp);

                // Update text content based on pickup state
                InteractionText.text = IsPickedUp ? DropPrompt : PickupPrompt;

                // Make text face camera
                if (Camera.main != null && InteractionText.gameObject.activeInHierarchy)
                {
                    InteractionText.transform.rotation = Camera.main.transform.rotation;
                }
            }
        }
        
        // Register player entering pickup range
        private void OnTriggerEnter(Collider other)
        {
            DebugLog($"OnTriggerEnter: {other.gameObject.name} entered trigger");
            
            // Safety check to prevent accessing networked properties before spawn
            if (!_isFullySpawned || !Object || !Object.IsValid)
            {
                DebugLog("Not fully spawned or Object not valid");
                return;
            }
                
            // Now it's safe to check IsPickedUp
            if (IsPickedUp)
            {
                DebugLog("Already picked up");
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
                    DebugLog($"Setting player {player.gameObject.name} as in range (via KCC)");
                    _playerInRange = player;
                }
            }
        }
        
        // Unregister player leaving pickup range
        private void OnTriggerExit(Collider other)
        {
            DebugLog($"OnTriggerExit: {other.gameObject.name} exited trigger");
            
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
                    DebugLog($"Clearing player {player.gameObject.name} from range");
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
                DebugLog("GetPlayerInRange called but not fully spawned or Object not valid");
                return null;
            }
                
            return _playerInRange;
        }
        
        // Reset the KeyShape to its initial state when the game restarts
        public void ResetKeyShape()
        {
            if (!HasStateAuthority) return;
            
            DebugLog("Resetting KeyShape");
            
            // If the key was being held by a player, release it
            if (IsPickedUp)
            {
                IsPickedUp = false;
                PickedUpBy = default;
            }
            
            // Reset networked state
            IsKinematic = true;
            IsColliderTrigger = false;
            IsColliderEnabled = true;
            IsPickupTriggerEnabled = true;
            
            // Apply changes locally
            Rigidbody.isKinematic = true;
            ShapeCollider.isTrigger = false;
            ShapeCollider.enabled = true;
            PickupTrigger.enabled = true;
            
            // Move key shape back to its original spawn position if possible
            if (transform.parent != null)
            {
                // Move to parent position (its original platform)
                transform.position = transform.parent.position;
                DebugLog($"Moved KeyShape back to parent position: {transform.position}");
            }
            else
            {
                // If no parent, move to a safe position
                Vector3 safePosition = transform.position;
                safePosition.y = 1f; // Just above ground level
                transform.position = safePosition;
                DebugLog($"No parent found, moved to safe position: {safePosition}");
            }
            
            // Update network position
            NetworkPosition = transform.position;
        }
        
        // Handle player disconnection - called from GameManager.OnPlayerLeft
        public void HandlePlayerLeft(PlayerRef player)
        {
            if (!HasStateAuthority) 
            {
                DebugLog($"No state authority in HandlePlayerLeft, requesting...");
                Object.RequestStateAuthority();
                return;
            }
            
            DebugLog($"Handling player {player} left event");
            
            // Check if this KeyShape was being held by the disconnected player
            if (IsPickedUp && PickedUpBy == player)
            {
                DebugLog($"KeyShape was held by disconnected player {player}, dropping it");
                
                // First drop the key shape
                IsPickedUp = false;
                PickedUpBy = default;
                
                // Update networked state for proper state recovery
                IsKinematic = false;
                IsColliderTrigger = false;
                IsColliderEnabled = true;
                IsPickupTriggerEnabled = true;
                
                // Apply changes locally
                Rigidbody.isKinematic = false;
                ShapeCollider.isTrigger = false;
                ShapeCollider.enabled = true;
                PickupTrigger.enabled = true;
                
                // Update network position to current position
                NetworkPosition = transform.position;
                
                // Add some random position offset so it's not exactly at the player's last position
                Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f));
                transform.position += randomOffset;
                NetworkPosition = transform.position;
                
                // Add some force to make it bounce a bit
                if (Rigidbody != null)
                {
                    Rigidbody.AddForce(Vector3.up * 5f + randomOffset * 2f, ForceMode.Impulse);
                }
                
                DebugLog("KeyShape dropped due to player disconnect");
            }
            
            // Check if we need to reset on a new platform
            if (transform.position.y < -10f)
            {
                DebugLog("KeyShape fell too low, resetting position");
                ResetKeyShape();
            }
        }
        
        // Handle network shutdown event - called from GameManager.OnShutdown
        public void HandleNetworkShutdown()
        {
            DebugLog("Handling network shutdown");
            
            // Ensure the KeyShape stays in a valid state
            if (HasStateAuthority)
            {
                // If being held by a player who's disconnecting, drop it
                if (IsPickedUp)
                {
                    DebugLog("Dropping KeyShape during network shutdown");
                    IsPickedUp = false;
                    PickedUpBy = default;
                    
                    // Update networked state
                    IsKinematic = false;
                    IsColliderTrigger = false;
                    IsColliderEnabled = true;
                    IsPickupTriggerEnabled = true;
                    
                    // Apply changes locally
                    Rigidbody.isKinematic = false;
                    ShapeCollider.isTrigger = false;
                    ShapeCollider.enabled = true;
                    PickupTrigger.enabled = true;
                }
                
                // Move to a safe position if needed
                if (transform.position.y < -10f)
                {
                    DebugLog("KeyShape in unsafe position, resetting");
                    ResetKeyShape();
                }
            }
        }
    }
} 
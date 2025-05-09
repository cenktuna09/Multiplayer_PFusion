using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Starter.Platformer
{
    // UI element to show when player can interact with a KeyShape
    public class UIKeyShapeInteraction : MonoBehaviour
    {
        public Player LocalPlayer;
        public GameObject InteractionPanel;
        public TextMeshProUGUI InteractionText;
        
        [Header("Text")]
        public string PickupText = "Press E to Pickup";
        public string DropText = "Press E to Drop";
        
        private void Start()
        {
            if (InteractionPanel == null)
            {
                Debug.LogError("InteractionPanel not assigned in UIKeyShapeInteraction");
            }
            
            // Hide panel at start
            if (InteractionPanel != null)
            {
                InteractionPanel.SetActive(false);
            }
            
            // Find local player if not assigned
            if (LocalPlayer == null)
            {
                LocalPlayer = FindObjectOfType<GameManager>()?.LocalPlayer;
            }
        }
        
        private void Update()
        {
            if (LocalPlayer == null)
            {
                LocalPlayer = FindObjectOfType<GameManager>()?.LocalPlayer;
                return;
            }
            
            // Safety check for networked player
            if (LocalPlayer.Object == null || !LocalPlayer.Object.IsValid)
            {
                if (InteractionPanel != null)
                {
                    InteractionPanel.SetActive(false);
                }
                return;
            }
            
            bool shouldShowPanel = false;
            string interactionMessage = "";
            
            // Check if player has a KeyShape
            if (LocalPlayer.HeldKeyShape != null)
            {
                shouldShowPanel = true;
                interactionMessage = DropText;
            }
            // Check if player is near a KeyShape
            else
            {
                KeyShape[] allKeyShapes = FindObjectsOfType<KeyShape>();
                foreach (KeyShape keyShape in allKeyShapes)
                {
                    // Safety check for networked keyShape
                    if (keyShape != null && keyShape.Object != null && keyShape.Object.IsValid)
                    {
                        Player playerInRange = keyShape.GetPlayerInRange();
                        if (playerInRange == LocalPlayer && !keyShape.IsPickedUp)
                        {
                            shouldShowPanel = true;
                            interactionMessage = PickupText;
                            break;
                        }
                    }
                }
            }
            
            // Update UI visibility
            if (InteractionPanel != null)
            {
                InteractionPanel.SetActive(shouldShowPanel);
                
                if (shouldShowPanel && InteractionText != null)
                {
                    InteractionText.text = interactionMessage;
                }
            }
        }
    }
} 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

namespace Starter.Platformer
{
	/// <summary>
	/// The door's NetworkBehaviour that handles their opening and closing.
	/// </summary>
	public class Door : NetworkBehaviour
	{
		[Header("Setup")]
		public float openDistance = 2f;
		public float openDuration = 1f;
		public float holdDuration = 1f;
		public float autoCloseDelay = 2f;
		public bool RequiresPuzzleCompletion = true;
		
		[Header("References")]
		public Transform leftDoor;
		public Transform rightDoor;
		public GameManager GameManager;
		
		[Header("Animation")]
		public AnimationCurve openCurve = AnimationCurve.Linear(0, 0, 1, 1);
		public AnimationCurve closeCurve = AnimationCurve.Linear(0, 0, 1, 1);
		
		[Header("Audio")]
		public AudioClip openAudioClip;
		public AudioClip closeAudioClip;
		public float audioVolume = 1f;

		[Networked]
		public NetworkBool IsOpen { get; set; }

		[Networked, OnChangedRender(nameof(TickActivatedChanged))]
		public int TickActivated { get; set; }
		
		[Networked]
		private TickTimer _cooldown { get; set; }
		
		[Networked]
		public NetworkBool IsUnlocked { get; set; }

		private bool isAnimating = false;
		private int _playerCount = 0;
		
		public override void Spawned()
		{
			// Find GameManager if not assigned
			if (GameManager == null)
			{
				GameManager = FindObjectOfType<GameManager>();
			}
		}

		public override void Render()
		{
			if (isAnimating)
			{
				float animProgress = (float)(Runner.LocalRenderTime - TickActivated * Runner.DeltaTime) / openDuration;

				float sourcePos = IsOpen ? 0 : openDistance;
				float targetPos = IsOpen ? openDistance : 0;
				leftDoor.localPosition = new Vector3(
					Mathf.Lerp(sourcePos, targetPos, 
						(IsOpen ? openCurve : closeCurve).Evaluate(animProgress)),
					0, 0);
				rightDoor.localPosition = new Vector3(
					Mathf.Lerp(-sourcePos, -targetPos,
						(IsOpen ? openCurve : closeCurve).Evaluate(animProgress)),
					0, 0);

				if (animProgress >= 1)
				{
					leftDoor.localPosition = new Vector3(targetPos, 0, 0);
					rightDoor.localPosition = new Vector3(-targetPos, 0, 0);
					isAnimating = false;
				}
			}
		}

		public override void FixedUpdateNetwork()
		{
			// Check if the auto-close timer has expired
			if (IsOpen && _playerCount == 0 && _cooldown.Expired(Runner))
			{
				IsOpen = false;
				TickActivated = Runner.Tick;
			}
		}

		void TickActivatedChanged()
		{
			isAnimating = true;
			
			// Play audio when door state changes
			if (IsOpen && openAudioClip != null)
			{
				AudioSource.PlayClipAtPoint(openAudioClip, transform.position, audioVolume);
			}
			else if (!IsOpen && closeAudioClip != null) 
			{
				AudioSource.PlayClipAtPoint(closeAudioClip, transform.position, audioVolume);
			}
		}
		
		// RPC to set the door's unlock state
		[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
		public void RPC_SetUnlocked(bool unlocked)
		{
			if (HasStateAuthority)
			{
				IsUnlocked = unlocked;
				
				// If door is being locked, also ensure it's closed
				if (!unlocked && IsOpen)
				{
					IsOpen = false;
					TickActivated = Runner.Tick;
				}
				
				Debug.Log($"Door unlocked state set to: {unlocked}");
			}
		}
		
		// Called when the game resets - resets the door to closed and locked state
		public void ResetDoor()
		{
			if (HasStateAuthority)
			{
				IsUnlocked = false;
				
				// Ensure door is closed
				if (IsOpen)
				{
					IsOpen = false;
					TickActivated = Runner.Tick;
				}
				
				Debug.Log("Door reset to locked and closed state");
			}
		}
		
		// Checks if the door can be opened
		private bool CanOpen()
		{
			// If door doesn't require puzzle completion OR it's unlocked
			return !RequiresPuzzleCompletion || IsUnlocked || 
			       (GameManager != null && GameManager.ArePuzzlesSolved);
		}
		
		// Called when a player enters the door's activation area
		private void OnTriggerEnter(Collider other)
		{
			// Door opening is initiated only on state authority
			if (HasStateAuthority == false)
				return;
				
			if (other.gameObject.layer != LayerMask.NameToLayer("Player"))
				return;
				
			_playerCount++;
				
			// Only open the door when puzzle is completed (if required)
			if (!IsOpen && CanOpen())
			{
				IsOpen = true;
				TickActivated = Runner.Tick;
				_cooldown = TickTimer.None;
			}
			else if (!IsOpen && RequiresPuzzleCompletion && !CanOpen())
			{
				// Door is locked - maybe play a locked sound or show a hint
				Debug.Log("Door is locked! Solve the puzzles first.");
			}
		}
		
		// Called when a player exits the door's activation area
		private void OnTriggerExit(Collider other)
		{
			// Door closing is initiated only on state authority
			if (HasStateAuthority == false)
				return;
				
			if (other.gameObject.layer != LayerMask.NameToLayer("Player"))
				return;
				
			_playerCount--;
				
			// Schedule door to close after delay when no players remain in trigger
			if (_playerCount == 0)
			{
				_cooldown = TickTimer.CreateFromSeconds(Runner, autoCloseDelay);
			}
		}
		
		// Handle player disconnection - called from GameManager.OnPlayerLeft
		public void HandlePlayerLeft(PlayerRef player)
		{
			Debug.Log($"Door {gameObject.name} handling player {player} left event");
			
			if (!HasStateAuthority) 
			{
				Debug.Log($"HandlePlayerLeft: No state authority for door {gameObject.name}");
				// Try to request authority if we don't have it
				if (Object != null && Object.IsValid)
				{
					Object.RequestStateAuthority();
				}
				return;
			}
			
			// Ensure proper state when player leaves
			// This helps ensure doors stay in a consistent state even after abrupt disconnects
			if (IsOpen && _playerCount > 0)
			{
				// Reduce player count to prevent door from staying open indefinitely
				// This assumes the disconnected player might have been in the trigger
				_playerCount = Mathf.Max(0, _playerCount - 1);
				
				// If no players remain in trigger, schedule door to close
				if (_playerCount == 0)
				{
					_cooldown = TickTimer.CreateFromSeconds(Runner, autoCloseDelay);
					Debug.Log($"Door will close after player {player} left");
				}
			}
		}
		
		// Handle network shutdown event - called from GameManager.OnShutdown
		public void HandleNetworkShutdown()
		{
			Debug.Log($"Door {gameObject.name} handling network shutdown");
			
			// Reset door position to avoid weird states after reconnecting
			if (IsOpen)
			{
				IsOpen = false;
				
				// When network is shutting down, update door visuals immediately
				float targetPos = 0; // Closed position
				
				if (leftDoor != null)
				{
					leftDoor.localPosition = new Vector3(targetPos, 0, 0);
				}
				
				if (rightDoor != null)
				{
					rightDoor.localPosition = new Vector3(-targetPos, 0, 0);
				}
				
				Debug.Log("Door closed due to network shutdown");
			}
		}
	}
}
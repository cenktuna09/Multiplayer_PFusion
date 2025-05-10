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
				Debug.Log($"Door unlocked state set to: {unlocked}");
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
	}
}
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
		
		[Header("References")]
		public Transform leftDoor;
		public Transform rightDoor;
		
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

		private bool isAnimating = false;
		private int _playerCount = 0;

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
				
			// Open the door when player enters
			if (!IsOpen)
			{
				IsOpen = true;
				TickActivated = Runner.Tick;
				_cooldown = TickTimer.None;
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
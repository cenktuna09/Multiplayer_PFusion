﻿using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using System.Collections.Generic;

namespace Starter.Platformer
{
	/// <summary>
	/// Main player scrip - controls player movement and animations.
	/// </summary>
	public sealed class Player : NetworkBehaviour
	{
		[Header("References")]
		public SimpleKCC KCC;
		public PlayerInput PlayerInput;
		public Animator Animator;
		public Transform CameraPivot;
		public Transform CameraHandle;
		public Transform ScalingRoot;
		public UINameplate Nameplate;

		[Header("Movement Setup")]
		public float WalkSpeed = 2f;
		public float SprintSpeed = 5f;
		public float JumpImpulse = 10f;
		public float UpGravity = 25f;
		public float DownGravity = 40f;
		public float RotationSpeed = 8f;

		[Header("Movement Accelerations")]
		public float GroundAcceleration = 55f;
		public float GroundDeceleration = 25f;
		public float AirAcceleration = 25f;
		public float AirDeceleration = 1.3f;
		
		[Header("KeyShape Interaction")]
		public float InteractionDistance = 2.0f;
		public Transform KeyShapeSlot;
		public AudioClip KeyShapePickupSound;
		public float KeyShapePickupVolume = 0.5f;

		[Header("Sounds")]
		public AudioSource FootstepSound;
		public AudioClip JumpAudioClip;
		public AudioClip LandAudioClip;
		public AudioClip CoinCollectedAudioClip;

		[Header("VFX")]
		public ParticleSystem DustParticles;

		[Networked, HideInInspector, Capacity(24), OnChangedRender(nameof(OnNicknameChanged))]
		public string Nickname { get; set; }
		[Networked, HideInInspector, OnChangedRender(nameof(OnCollectedCoinsChanged))]
		public int CollectedCoins { get; set; }
		
		[Networked]
		public KeyShape HeldKeyShape { get; set; }

		[Networked, OnChangedRender(nameof(OnJumpingChanged))]
		private NetworkBool _isJumping { get; set; }
		
		// Animation IDs
		private int _animIDSpeed;
		private int _animIDGrounded;

		private Vector3 _moveVelocity;

		private GameManager _gameManager;

		public void Respawn(Vector3 position, bool resetCoins)
		{
			KCC.SetPosition(position);
			KCC.SetLookRotation(0f, 0f);

			_moveVelocity = Vector3.zero;

			if (resetCoins)
			{
				CollectedCoins = 0;
			}
			
			// Drop any held key shape when respawning
			DropKeyShape();
		}
		
		// Method to attempt picking up a KeyShape
		public void TryPickupKeyShape()
		{
			if (!HasStateAuthority)
			{
				Debug.Log("TryPickupKeyShape: Player does not have state authority");
				return;
			}
			
			if (HeldKeyShape != null)
			{
				Debug.Log("TryPickupKeyShape: Player already has a KeyShape");
				return;
			}
			
			Debug.Log("TryPickupKeyShape: Searching for nearby KeyShapes");
			
			// Find all key shapes in the scene
			KeyShape[] allKeyShapes = FindObjectsOfType<KeyShape>();
			Debug.Log($"Found {allKeyShapes.Length} KeyShapes in scene");
			
			KeyShape closestKeyShape = null;
			float closestDistance = float.MaxValue;
			
			// Find closest key shape that has player in range
			foreach (KeyShape keyShape in allKeyShapes)
			{
				// Ensure the keyShape is valid and networked before accessing networked properties
				if (keyShape == null)
				{
					Debug.LogWarning("Skipping null KeyShape");
					continue;
				}
				
				if (keyShape.Object == null || !keyShape.Object.IsValid)
				{
					Debug.LogWarning($"Skipping KeyShape {keyShape.gameObject.name} - Object is null or invalid");
					continue;
				}
				
				// Check if we're in range of this key shape's trigger
				float distance = Vector3.Distance(transform.position, keyShape.transform.position);
				bool isInRange = distance <= keyShape.PickupTrigger.radius * 1.5f; // Add some buffer to the radius
				bool isPickedUp = keyShape.IsPickedUp;
				
				Debug.Log($"Checking KeyShape {keyShape.gameObject.name}: " +
						 $"distance={distance}, " +
						 $"radius={keyShape.PickupTrigger.radius}, " +
						 $"isInRange={isInRange}, " +
						 $"isPickedUp={isPickedUp}");
				
				if (isInRange && !isPickedUp)
				{
					// We're in range of this key shape
					Debug.Log($"In range of KeyShape {keyShape.gameObject.name} at distance {distance}");
					
					if (closestKeyShape == null || distance < closestDistance)
					{
						closestKeyShape = keyShape;
						closestDistance = distance;
						Debug.Log($"KeyShape {keyShape.gameObject.name} is now the closest at {distance}");
					}
				}
			}
			
			// If we found a key shape in range, pick it up
			if (closestKeyShape != null)
			{
				Debug.Log($"Picking up KeyShape {closestKeyShape.gameObject.name}");
				HeldKeyShape = closestKeyShape;
				closestKeyShape.RequestPickup(Object.InputAuthority);
				
				if (KeyShapePickupSound != null)
				{
					AudioSource.PlayClipAtPoint(KeyShapePickupSound, transform.position, KeyShapePickupVolume);
				}
			}
			else
			{
				Debug.Log("No KeyShape in range to pick up");
			}
		}
		
		// Method to drop a held KeyShape
		public void DropKeyShape()
		{
			if (!HasStateAuthority)
			{
				Debug.Log("DropKeyShape: Player does not have state authority");
				return;
			}
				
			if (HeldKeyShape == null)
			{
				Debug.Log("DropKeyShape: No KeyShape to drop");
				return;
			}
			
			Debug.Log($"Dropping KeyShape {HeldKeyShape.gameObject.name}");
			HeldKeyShape.RequestDrop();
			HeldKeyShape = null;
		}

		public override void Spawned()
		{
			if (HasStateAuthority)
			{
				_gameManager = FindObjectOfType<GameManager>();

				// Set player nickname that is saved in UIGameMenu
				Nickname = PlayerPrefs.GetString("PlayerName");
			}

			// In case the nickname is already changed,
			// we need to trigger the change manually
			OnNicknameChanged();
		}

		public override void FixedUpdateNetwork()
		{
			if (_gameManager.IsGameFinished)
			{
				// Let players fall even when game is finished (KCC.Move is called)
				ProcessInput(default);
				return;
			}

			if (KCC.Position.y < -15f)
			{
				// Player fell, let's respawn
				Respawn(_gameManager.GetSpawnPosition(), false);
			}

			ProcessInput(PlayerInput.CurrentInput);

			if (KCC.IsGrounded)
			{
				// Stop jumping
				_isJumping = false;
			}

			PlayerInput.ResetInput();
		}

		public override void Render()
		{
			Animator.SetFloat(_animIDSpeed, KCC.RealSpeed);
			Animator.SetBool(_animIDGrounded, KCC.IsGrounded);

			FootstepSound.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;
			FootstepSound.pitch = KCC.RealSpeed > SprintSpeed - 1 ? 1.5f : 1f;

			ScalingRoot.localScale = Vector3.Lerp(ScalingRoot.localScale, Vector3.one, Time.deltaTime * 8f);

			var emission = DustParticles.emission;
			emission.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;
		}

		private void Awake()
		{
			AssignAnimationIDs();
		}

		private void LateUpdate()
		{
			// Only local player needs to update the camera
			if (HasStateAuthority == false)
				return;

			if (_gameManager.IsGameFinished)
				return;

			// Update camera pivot and transfer properties from camera handle to Main Camera.
			CameraPivot.rotation = Quaternion.Euler(PlayerInput.CurrentInput.LookRotation);
			Camera.main.transform.SetPositionAndRotation(CameraHandle.position, CameraHandle.rotation);
		}

		private void ProcessInput(GameplayInput input)
		{
			float jumpImpulse = 0f;

			if (KCC.IsGrounded && input.Jump)
			{
				// Set world space jump vector
				jumpImpulse = JumpImpulse;
				_isJumping = true;
			}
			
			// Handle KeyShape pickup/drop
			if (input.Interact)
			{
				Debug.Log("Interact button pressed");
				if (HeldKeyShape == null)
				{
					Debug.Log("Trying to pick up a KeyShape");
					TryPickupKeyShape();
				}
				else
				{
					Debug.Log("Trying to drop a KeyShape");
					DropKeyShape();
				}
			}

			// It feels better when the player falls quicker
			KCC.SetGravity(KCC.RealVelocity.y >= 0f ? UpGravity : DownGravity);

			float speed = input.Sprint ? SprintSpeed : WalkSpeed;

			var lookRotation = Quaternion.Euler(0f, input.LookRotation.y, 0f);

			// Calculate correct move direction from input (rotated based on camera look)
			var moveDirection = lookRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
			var desiredMoveVelocity = moveDirection * speed;

			float acceleration;
			if (desiredMoveVelocity == Vector3.zero)
			{
				// No desired move velocity - we are stopping
				acceleration = KCC.IsGrounded ? GroundDeceleration : AirDeceleration;
			}
			else
			{
				// Rotate the character towards move direction over time
				var currentRotation = KCC.TransformRotation;
				var targetRotation = Quaternion.LookRotation(moveDirection);
				var nextRotation = Quaternion.Lerp(currentRotation, targetRotation, RotationSpeed * Runner.DeltaTime);

				KCC.SetLookRotation(nextRotation.eulerAngles);

				acceleration = KCC.IsGrounded ? GroundAcceleration : AirAcceleration;
			}

			_moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);

			KCC.Move(_moveVelocity, jumpImpulse);
		}

		private void AssignAnimationIDs()
		{
			_animIDSpeed = Animator.StringToHash("Speed");
			_animIDGrounded = Animator.StringToHash("Grounded");
		}

		private void OnTriggerEnter(Collider other)
		{
			// Coins are collected only for local player
			if (HasStateAuthority == false)
				return;

			var coin = other.GetComponent<Coin>();
			if (coin != null)
			{
				coin.CoinCollected = OnCoinCollected;
				coin.RequestCollect();
			}
		}

		private void OnJumpingChanged()
		{
			if (_isJumping)
			{
				AudioSource.PlayClipAtPoint(JumpAudioClip, KCC.Position, 1f);
				ScalingRoot.localScale = new Vector3(0.5f, 1.5f, 0.5f);
			}
			else
			{
				AudioSource.PlayClipAtPoint(LandAudioClip, KCC.Position, 1f);
				ScalingRoot.localScale = new Vector3(1.25f, 0.75f, 1.25f);
			}
		}

		private void OnCoinCollected()
		{
			CollectedCoins++;
		}

		private void OnCollectedCoinsChanged()
		{
			if (CollectedCoins <= 0)
				return; // Just coins reset

			AudioSource.PlayClipAtPoint(CoinCollectedAudioClip, KCC.Position, 1f);
		}

		private void OnNicknameChanged()
		{
			if (HasStateAuthority)
				return; // Do not show nickname for local player

			Nameplate.SetNickname(Nickname);
		}
	}
}

using Fusion;
using Fusion.Addons.SimpleKCC;
using Helpers.Bits;
using Helpers.Physics;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the movement of player
/// </summary>
public class PlayerMovement : NetworkBehaviour
{
	/// <summary>
	/// Reference to the local player
	/// </summary>
	public static PlayerMovement Local { get; protected set; }

	[Tooltip("The rate at which the player looks when rotating")]
	public float lookTurnRate = 1.5f;

	private List<PlayerObject> nearbyPlayers = new List<PlayerObject>(10);

	int _playerRadiusLayer;

	public KCC cc { get; protected set; }
	public SimpleKCC simpleCC { get; protected set; }

	public bool TransformLocal = false;

	[Networked]
	public float Speed { get; set; } = 6f;

	/// <summary>
	/// The list of lag compenstated hits.
	/// </summary>
	List<LagCompensatedHit> lagCompensatedHits = new List<LagCompensatedHit>();

	/// <summary>
	/// The current player data.
	/// </summary>
    PlayerData playerData;

	// This will prevent players from killing and calling a meeting at the same time.
	private bool actionPerformed = false;

	private void Awake()
    {
		_playerRadiusLayer = LayerMask.NameToLayer("PlayerRadius");
		playerData = GetComponent<PlayerData>();
	}

	public override void Spawned()
	{
		if (HasInputAuthority)
		{
			Local = this;
		}

		cc = GetComponent<KCC>();
		simpleCC = cc as SimpleKCC;

		if (HasStateAuthority)
		{
			//GameSettings settings = GameManager.Instance.Settings;

			int playerLayer = LayerMask.NameToLayer("Player");
			cc.SetColliderLayer(LayerMask.NameToLayer("Player"));
			//cc.SetCollisionLayerMask(cc.Settings.CollisionLayerMask.value.OverrideBit(playerLayer, settings.playerCollision));
			//Speed = settings.walkSpeed;
		}
	}

	public override void Render()
	{
		//playerData.UpdateAnimation(this);
	}

	public override void FixedUpdateNetwork()
	{
		bool hasInput = GetInput(out PlayerInput input);

		if (hasInput && input.IsDown(PlayerInputBehaviour.BUTTON_START_GAME))
		{
			//GameManager.Instance.Server_StartGame();
		}

		Vector3 direction = default;
		bool canMoveOrUseInteractables = hasInput;

		if (canMoveOrUseInteractables)
		{
			// BUTTON_WALK is representing left mouse button
			if (input.IsDown(PlayerInputBehaviour.BUTTON_WALK))
			{
				direction = new Vector3(
					Mathf.Cos((float)input.Yaw * Mathf.Deg2Rad),
					0,
					Mathf.Sin((float)input.Yaw * Mathf.Deg2Rad)
				);
			}
			else
			{
				if (input.IsDown(PlayerInputBehaviour.BUTTON_FORWARD))
				{
					direction += TransformLocal ? transform.forward : Vector3.forward;
				}

				if (input.IsDown(PlayerInputBehaviour.BUTTON_BACKWARD))
				{
					direction -= TransformLocal ? transform.forward : Vector3.forward;
				}

				if (input.IsDown(PlayerInputBehaviour.BUTTON_LEFT))
				{
					direction -= TransformLocal ? transform.right : Vector3.right;
				}

				if (input.IsDown(PlayerInputBehaviour.BUTTON_RIGHT))
				{
					direction += TransformLocal ? transform.right : Vector3.right;
				}

				direction = direction.normalized;
			}
		}

		simpleCC.Move(direction * Speed);

		if (direction != Vector3.zero)
		{
			Quaternion targetQ = Quaternion.AngleAxis(Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg - 90, Vector3.down);
			cc.SetLookRotation(Quaternion.RotateTowards(transform.rotation, targetQ, lookTurnRate * 360 * Runner.DeltaTime));
		}


		// The lists of nearby players and interactables are cleared with every check.
		//nearbyInteractables.Clear();
		nearbyPlayers.Clear();


		if (HasInputAuthority)
		{
			//GameManager.im.gameUI.reportButton.interactable = canReport;
			//GameManager.im.gameUI.killButton.interactable = canKill;
			//GameManager.im.gameUI.useButton.interactable = canUse;
		}


		if (!canMoveOrUseInteractables)
			return;

		actionPerformed = false;
	}
	
	
}

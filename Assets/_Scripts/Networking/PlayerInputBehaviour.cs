using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles player input; originally based on the Player Input Prototype behaviour
/// </summary>
public class PlayerInputBehaviour : SimulationBehaviour, INetworkRunnerCallbacks
{
	public static PlayerInputBehaviour Instance { get; private set; }

	public const int BUTTON_FORWARD = 0;
	public const int BUTTON_BACKWARD = 1;
	public const int BUTTON_LEFT = 2;
	public const int BUTTON_RIGHT = 3;

	public const int BUTTON_WALK = 4;

	public const int BUTTON_START_GAME = 5;
	public const int BUTTON_INTERACT = 6;

	public const int BUTTON_USE = 7;
	public const int BUTTON_REPORT = 8;
	public const int BUTTON_KILL = 9;

	/// <summary>
	/// Allows interact and start game to be treated more like button presses.
	/// </summary>
	bool interactDown = false;

	bool useBtnDown = false;
	bool reportBtnDown = false;
	bool killBtnDown = false;

	bool startGameDown = false;

	public void OnInput(NetworkRunner runner, NetworkInput input)
	{
		var frameworkInput = new PlayerInput();

		if (PlayerMovement.Local /*&& GameManager.Instance && GameManager.State.AllowInput*/)
		{
			if (Input.GetKey(KeyCode.W))
			{
				frameworkInput.Buttons.Set(BUTTON_FORWARD, true);
			}

			if (Input.GetKey(KeyCode.S))
			{
				frameworkInput.Buttons.Set(BUTTON_BACKWARD, true);
			}

			if (Input.GetKey(KeyCode.A))
			{
				frameworkInput.Buttons.Set(BUTTON_LEFT, true);
			}

			if (Input.GetKey(KeyCode.D))
			{
				frameworkInput.Buttons.Set(BUTTON_RIGHT, true);
			}

			if (Input.GetMouseButton(0) && EventSystem.current.IsPointerOverGameObject() == false)
			{
				frameworkInput.Buttons.Set(BUTTON_WALK, true);

				Vector2 mouseVec = new Vector2(Input.mousePosition.x / Screen.width - 0.5f, Input.mousePosition.y / Screen.height - 0.5f);
				frameworkInput.Yaw = Mathf.Atan2(mouseVec.y, mouseVec.x) * Mathf.Rad2Deg;
			}

			// Sets interact to true if interact has been detected.
			if (interactDown)
			{
				frameworkInput.Buttons.Set(BUTTON_INTERACT, true);
			}

			if (killBtnDown)
			{
				frameworkInput.Buttons.Set(BUTTON_KILL, true);
			}

			if (useBtnDown)
            {
				frameworkInput.Buttons.Set(BUTTON_USE, true);
            }

			if (reportBtnDown)
			{
				frameworkInput.Buttons.Set(BUTTON_REPORT, true);
			}

			// Only the server can start the game
			if (runner.IsServer && startGameDown)
			{
				frameworkInput.Buttons.Set(BUTTON_START_GAME, true);
			}

		}

		// Resets interaction ui inputs so these can act more like singular button presses.
		interactDown = false;
		startGameDown = false;
		useBtnDown = false;
		killBtnDown = false;
		reportBtnDown = false;

		input.Set(frameworkInput);
	}

	public void PressInteractUI()
	{
		useBtnDown = true;
	}

	public void PressKillUI()
	{
		killBtnDown = true;
	}

	public void PressReportUI()
	{
		reportBtnDown = true;
	}

	void Update()
	{
		startGameDown = startGameDown | Input.GetKeyDown(KeyCode.KeypadEnter);

		interactDown = interactDown | Input.GetKeyDown(KeyCode.E);
	}

	void OnDestroy()
	{
		Instance = null;
	}
	public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
	public void OnConnectedToServer(NetworkRunner runner) { }
	public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
	public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
	public void OnDisconnectedFromServer(NetworkRunner runner) { }
	public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { if (runner.LocalPlayer == player) { Instance = this; } }
	public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
	public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
	public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
	public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
	public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
	public void OnSceneLoadDone(NetworkRunner runner) { }
	public void OnSceneLoadStart(NetworkRunner runner) { }
	public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
	public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

	public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

	public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

	public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

	public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

	public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}

/// <summary>
/// The player input struct
/// </summary>
public struct PlayerInput : INetworkInput
{
	public NetworkButtons Buttons;
	public Angle Yaw;

	public bool IsUp(int button)
	{
		return Buttons.IsSet(button) == false;
	}

	public bool IsDown(int button)
	{
		return Buttons.IsSet(button);
	}
}

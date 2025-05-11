using UnityEngine;
using Fusion;
using System.Collections;

namespace Starter.Platformer
{
	/// <summary>
	/// Handles player connections (spawning of Player instances).
	/// </summary>
	public sealed class GameManager : NetworkBehaviour
	{
		public int MinCoinsToWin = 10;
		public float GameOverTime = 4f;
		public Player PlayerPrefab;
		public float SpawnRadius = 3f;
		
		[Header("Puzzle System")]
		public int RequiredPuzzlesToUnlock = 2;
		public Door[] DoorsToUnlock;

		public Player LocalPlayer { get; private set; }
		public bool IsGameFinished  => GameOverTimer.IsRunning;

		[Networked]
		public PlayerRef Winner { get; set; }
		[Networked]
		public TickTimer GameOverTimer { get; set; }
		
		[Networked]
		public int SolvedPuzzleCount { get; set; }
		
		[Networked]
		public NetworkBool ArePuzzlesSolved { get; set; }

		// Called from UnityEvent on Flag gameobject
		public void OnFlagReached(Player player)
		{
			if (HasStateAuthority == false)
				return;

			if (Winner != PlayerRef.None)
				return; // Someone was faster

			if (SolvedPuzzleCount < RequiredPuzzlesToUnlock)
				return; // Not enough puzzles

			Winner = player.Object.StateAuthority;
			GameOverTimer = TickTimer.CreateFromSeconds(Runner, GameOverTime);
		}

		public Vector3 GetSpawnPosition()
		{
			var randomPositionOffset = Random.insideUnitCircle * SpawnRadius;
			return transform.position + new Vector3(randomPositionOffset.x, transform.position.y, randomPositionOffset.y);
		}
		
		// Called when an UnlockShape is successfully unlocked
		[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
		public void RPC_PuzzleSolved()
		{
			if (!HasStateAuthority) return;
			
			SolvedPuzzleCount++;
			Debug.Log($"Puzzle solved! Progress: {SolvedPuzzleCount}/{RequiredPuzzlesToUnlock}");
			
			// Check if all required puzzles are solved
			if (SolvedPuzzleCount >= RequiredPuzzlesToUnlock && !ArePuzzlesSolved)
			{
				ArePuzzlesSolved = true;
				UnlockAllDoors();
				Debug.Log("All puzzles solved! Doors can now be opened.");
			}
		}
		
		// Unlocks all doors in the level
		private void UnlockAllDoors()
		{
			foreach (Door door in DoorsToUnlock)
			{
				if (door != null)
				{
					door.RPC_SetUnlocked(true);
				}
			}
		}
		
		// Reset puzzle state (for level restart)
		public void ResetPuzzles()
		{
			if (!HasStateAuthority) return;
			
			Debug.Log("Resetting all puzzles, keys and doors...");
			
			// Önce puzzle durumunu sıfırla
			SolvedPuzzleCount = 0;
			ArePuzzlesSolved = false;
			
			// Re-lock all doors
			foreach (Door door in DoorsToUnlock)
			{
				if (door != null)
				{
					door.ResetDoor();
					Debug.Log($"Door {door.gameObject.name} reset and locked");
				}
			}
			
			// Find and reset all KeyShapes in the scene
			KeyShape[] allKeyShapes = FindObjectsOfType<KeyShape>();
			foreach (KeyShape keyShape in allKeyShapes)
			{
				if (keyShape != null)
				{
					keyShape.ResetKeyShape();
					Debug.Log($"KeyShape {keyShape.gameObject.name} reset");
				}
			}
			
			// Find and reset all UnlockShapes in the scene
			UnlockShape[] allUnlockShapes = FindObjectsOfType<UnlockShape>();
			foreach (UnlockShape unlockShape in allUnlockShapes)
			{
				if (unlockShape != null)
				{
					// Önce durumu logla
					Debug.Log($"UnlockShape {unlockShape.gameObject.name} before reset, IsUnlocked: {unlockShape.IsUnlocked}");
					
					// Kritik: ResetLock'u çağır
					unlockShape.ResetLock();
					
					// Reset sonrası logla
					Debug.Log($"UnlockShape {unlockShape.gameObject.name} after reset, IsUnlocked: {unlockShape.IsUnlocked}");
				}
			}
			
			// Flag'i yeniden denetle
			Debug.Log($"After reset - SolvedPuzzleCount: {SolvedPuzzleCount}, ArePuzzlesSolved: {ArePuzzlesSolved}");
			
			// Son olarak oyun başlatıldıktan 1 frame sonra durumu tekrar kontrol et
			StartCoroutine(VerifyReset());
			
			Debug.Log("All puzzles, keys and doors reset!");
		}

		// Reset sonrası durumu tekrar kontrol et
		private IEnumerator VerifyReset()
		{
			// 1 frame bekle
			yield return null;
			
			// Tüm UnlockShape'leri tekrar kontrol et ve görsellerini güncelle
			UnlockShape[] allUnlockShapes = FindObjectsOfType<UnlockShape>();
			foreach (UnlockShape unlockShape in allUnlockShapes)
			{
				if (unlockShape != null && unlockShape.IsUnlocked)
				{
					Debug.LogWarning($"UnlockShape {unlockShape.gameObject.name} is still showing as unlocked after reset!");
					// Zorla sıfırla
					if (HasStateAuthority)
					{
						unlockShape.ForceReset();
					}
				}
			}
			
			// SolvedPuzzleCount'u tekrar kontrol et
			if (SolvedPuzzleCount > 0)
			{
				Debug.LogWarning($"SolvedPuzzleCount is still {SolvedPuzzleCount} after reset! Forcing to 0.");
				SolvedPuzzleCount = 0;
			}
			
			// ArePuzzlesSolved'u tekrar kontrol et
			if (ArePuzzlesSolved)
			{
				Debug.LogWarning("ArePuzzlesSolved is still true after reset! Forcing to false.");
				ArePuzzlesSolved = false;
			}
		}

		public override void Spawned()
		{
			LocalPlayer = Runner.Spawn(PlayerPrefab, GetSpawnPosition(), Quaternion.identity, Runner.LocalPlayer);
			Runner.SetPlayerObject(Runner.LocalPlayer, LocalPlayer.Object);
		}

		public override void FixedUpdateNetwork()
		{
			if (GameOverTimer.Expired(Runner))
			{
				Debug.Log("Game over timer expired. Resetting the game...");
				
				// Restart the game
				Winner = PlayerRef.None;

				// Reset timer
				GameOverTimer = default;
				
				// Önce puzzle durumunu sıfırla (reset)
				ResetPuzzles();
				
				// Sonra oyuncuları sıfırla (respawn)
				foreach (var playerRef in Runner.ActivePlayers)
				{
					RPC_RespawnPlayer(playerRef, GetSpawnPosition(), true);
					Debug.Log($"Respawned player {playerRef}");
				}
				
				// Ekstra güvenlik: Puzzle durumunu tekrar kontrol et
				if (SolvedPuzzleCount != 0 || ArePuzzlesSolved != false)
				{
					Debug.LogWarning($"After game reset, puzzle state is still not clean: SolvedPuzzleCount={SolvedPuzzleCount}, ArePuzzlesSolved={ArePuzzlesSolved}");
					SolvedPuzzleCount = 0;
					ArePuzzlesSolved = false;
				}
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			// Clear the reference because UI can try to access it even after despawn
			LocalPlayer = null;
		}

		[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
		private void RPC_RespawnPlayer([RpcTarget] PlayerRef playerRef, Vector3 position, bool resetCoins)
		{
			LocalPlayer.Respawn(position, resetCoins);
		}

		private void OnDrawGizmosSelected()
		{
			Gizmos.DrawWireSphere(transform.position, SpawnRadius);
		}

		// Handle player disconnection (including ALT+F4)
		public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
		{
			Debug.Log($"Player {player} left the game");
			
			// Collect information about puzzle completion first
			bool anyShapesUnlocked = false;
			
			// Check if any puzzles were solved by the disconnected player
			UnlockShape[] allUnlockShapes = FindObjectsOfType<UnlockShape>();
			foreach (UnlockShape unlockShape in allUnlockShapes)
			{
				if (unlockShape != null && unlockShape.IsUnlocked)
				{
					anyShapesUnlocked = true;
					Debug.Log($"Found unlocked shape: {unlockShape.gameObject.name}");
				}
			}
				
			if (HasStateAuthority)
			{
				// First, handle all KeyShapes to drop any held by disconnected player
				KeyShape[] allKeyShapes = FindObjectsOfType<KeyShape>();
				foreach (KeyShape keyShape in allKeyShapes)
				{
					if (keyShape != null)
					{
						Debug.Log($"Handling KeyShape {keyShape.gameObject.name} after player {player} disconnect");
						// Call the handler method directly
						keyShape.HandlePlayerLeft(player);
					}
				}
				
				// If puzzles were already solved when this player was connected,
				// make sure the UnlockShapes maintain their state
				foreach (UnlockShape unlockShape in allUnlockShapes)
				{
					if (unlockShape != null)
					{
						Debug.Log($"Handling UnlockShape {unlockShape.gameObject.name} after player {player} disconnect");
						
						// If any shapes were unlocked, we'll maintain puzzle progress
						if (anyShapesUnlocked && !unlockShape.IsUnlocked && SolvedPuzzleCount > 0)
						{
							Debug.Log($"Forcing UnlockShape {unlockShape.gameObject.name} to maintain puzzle state");
							unlockShape.ForceUnlock();
						}
						else
						{
							// Normal handling
							unlockShape.HandlePlayerLeft(player);
						}
					}
				}
					
				// Finally, handle doors
				Door[] allDoors = FindObjectsOfType<Door>();
				foreach (Door door in allDoors)
				{
					if (door != null)
					{
						Debug.Log($"Handling Door {door.gameObject.name} after player {player} disconnect");
						
						if (door.Object != null && door.Object.IsValid)
						{
							// If puzzles were solved, make sure doors reflect this state
							if (anyShapesUnlocked && ArePuzzlesSolved)
							{
								Debug.Log($"Setting door {door.gameObject.name} as unlocked to maintain game state");
								door.RPC_SetUnlocked(true);
							}
							
							// Call normal handler
							if (door.gameObject.activeInHierarchy)
							{
								try 
								{
									door.HandlePlayerLeft(player);
								}
								catch (System.Exception e)
								{
									Debug.LogError($"Error handling player left for door: {e.Message}");
								}
							}
						}
					}
				}
				
				// Make sure puzzle state is consistent
				Debug.Log($"After player left: SolvedPuzzleCount={SolvedPuzzleCount}, ArePuzzlesSolved={ArePuzzlesSolved}");
				if (anyShapesUnlocked && !ArePuzzlesSolved && SolvedPuzzleCount >= RequiredPuzzlesToUnlock)
				{
					ArePuzzlesSolved = true;
					Debug.Log("Setting ArePuzzlesSolved=true to maintain game state after disconnect");
					UnlockAllDoors();
				}
			}
		}
		
		// Handle network shutdown (including ALT+F4)
		public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
		{
			Debug.Log($"Network shutdown: {shutdownReason}");
			
			// When a client disconnects abnormally, ensure proper cleanup
			if (shutdownReason == ShutdownReason.Ok || 
				shutdownReason == ShutdownReason.Error || 
				shutdownReason == ShutdownReason.ConnectionTimeout)
			{
				// First collect game state info
				bool anyShapesUnlocked = false;
				UnlockShape[] allUnlockShapes = FindObjectsOfType<UnlockShape>();
				foreach (var shape in allUnlockShapes)
				{
					if (shape != null && shape.IsUnlocked)
					{
						anyShapesUnlocked = true;
						break;
					}
				}
				
				// Similar to OnPlayerLeft but runs on all clients
				if (runner.IsServer || runner.IsSharedModeMasterClient)
				{
					Debug.Log("Server performing cleanup after shutdown");
					
					// Call HandleNetworkShutdown on all KeyShapes
					KeyShape[] allKeyShapes = FindObjectsOfType<KeyShape>();
					foreach (KeyShape keyShape in allKeyShapes)
					{
						if (keyShape != null)
						{
							Debug.Log($"Preserving KeyShape {keyShape.gameObject.name} state during shutdown");
							keyShape.HandleNetworkShutdown();
						}
					}
					
					// Call HandleNetworkShutdown on all UnlockShapes
					foreach (UnlockShape unlockShape in allUnlockShapes)
					{
						if (unlockShape != null)
						{
							Debug.Log($"Preserving UnlockShape {unlockShape.gameObject.name} state during shutdown");
							
							// If puzzles were in progress, ensure they stay unlocked
							if (anyShapesUnlocked && SolvedPuzzleCount > 0 && !unlockShape.IsUnlocked)
							{
								unlockShape.ForceUnlock();
							}
							else if (unlockShape.gameObject.activeInHierarchy)
							{
								// Call HandleNetworkShutdown
								try {
									unlockShape.HandleNetworkShutdown();
								}
								catch (System.Exception e)
								{
									Debug.LogError($"Error handling shutdown for unlock shape: {e.Message}");
								}
							}
						}
					}
					
					// Call HandleNetworkShutdown on all doors
					Door[] allDoors = FindObjectsOfType<Door>();
					foreach (Door door in allDoors)
					{
						if (door != null)
						{
							Debug.Log($"Preserving Door {door.gameObject.name} state during shutdown");
							
							if (door.Object != null && door.Object.IsValid)
							{
								if (anyShapesUnlocked && ArePuzzlesSolved)
								{
									Debug.Log($"Setting door {door.gameObject.name} as unlocked to maintain game state");
									door.RPC_SetUnlocked(true);
								}
								
								if (door.gameObject.activeInHierarchy)
								{
									try 
									{
										door.HandleNetworkShutdown();
									}
									catch (System.Exception e)
									{
										Debug.LogError($"Error handling shutdown for door: {e.Message}");
									}
								}
							}
						}
					}
					
					// Reset puzzles as well
					if (!anyShapesUnlocked)
					{
						ResetPuzzles();
					}
					else
					{
						Debug.Log("Not resetting puzzles since shapes were unlocked");
						if (!ArePuzzlesSolved && SolvedPuzzleCount >= RequiredPuzzlesToUnlock)
						{
							Debug.Log("Setting ArePuzzlesSolved=true during shutdown to maintain game state");
							ArePuzzlesSolved = true;
							UnlockAllDoors();
						}
					}
				}
				else
				{
					Debug.Log("Client handling shutdown");
					
					// Even on client, try to preserve state
					KeyShape[] allKeyShapes = FindObjectsOfType<KeyShape>();
					foreach (KeyShape keyShape in allKeyShapes)
					{
						if (keyShape != null)
						{
							Debug.Log($"Client preserving KeyShape {keyShape.gameObject.name} state during shutdown");
							keyShape.HandleNetworkShutdown();
						}
					}
					
					Door[] allDoors = FindObjectsOfType<Door>();
					foreach (Door door in allDoors)
					{
						if (door != null && door.Object != null && door.Object.IsValid && door.gameObject.activeInHierarchy)
						{
							Debug.Log($"Client preserving Door {door.gameObject.name} state during shutdown");
							door.HandleNetworkShutdown();
						}
					}
				}
			}
		}
	}
}

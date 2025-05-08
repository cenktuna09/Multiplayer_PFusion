using Fusion;
using UnityEngine;

/// <summary>
/// Network Behaviour that defines information for the players other than movement.
/// </summary>
public class PlayerObject : NetworkBehaviour
{
	/// <summary>
	/// A static reference to the local player
	/// </summary>
	public static PlayerObject Local { get; private set; }

	[Networked]
	public PlayerRef Ref { get; set; }
	[Networked]
	public byte Index { get; set; }
	[Networked, OnChangedRender(nameof(NicknameChanged))]
	public NetworkString<_16> Nickname { get; set; }
	//public string GetStyledNickname => $"<color=#{ColorUtility.ToHtmlStringRGB(GetColor)}>{Nickname}</color>";

	[field: Header("References"), SerializeField] public PlayerMovement Controller { get; private set; }

	public void Server_Init(PlayerRef pRef, byte index)
	{
		Debug.Assert(Runner.IsServer);

		Ref = pRef;
		Index = index;
	}

	public override void Spawned()
	{
		base.Spawned();

		if (Object.HasStateAuthority)
		{
			PlayerRegistry.Server_Add(Runner, Object.InputAuthority, this);
		}

		if (Object.HasInputAuthority)
		{
			Local = this;
			Rpc_SetNickname(PlayerPrefs.GetString("nickname"));
		}

		// Sets the proper nicknae and color on spawn.
		NicknameChanged();
	}

	[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
	void Rpc_SetNickname(string nick)
	{
		Nickname = nick;
	}

	void NicknameChanged()
	{
		//GetComponent<PlayerData>().SetNickname(Nickname.Value);
	}

	internal void AttemptKill(PlayerRef killerInputAuthority)
	{
		if (!Runner.IsServer || !HasStateAuthority)
		{
			Debug.LogWarning("Only the state authority and server can determine when to kill a player.");
			return;
		}

		PlayerObject src = PlayerRegistry.GetPlayer(killerInputAuthority);	
	}
}

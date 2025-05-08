using Helpers.Collections;
using UnityEngine;

/// <summary>
/// Behaviour that handles non-networked information for players such as animation location on the map.
/// </summary>
public class PlayerData : MonoBehaviour
{
	PlayerObject pObj;

	public Animator anim;
	public Renderer[] modelParts;
    public Transform uiPoint;

	public float animationBlending = 5f;

	//WorldCanvasNickname nicknameUI;
	public Material BodyMaterial { get; private set; }
	public Material TrimMaterial { get; private set; }
	public Material VisorMaterial { get; private set; }

	float moveSpeed = 0;

	const string MOVE_SPEED_PARAMETER = "MoveSpeed";

	private void Awake()
	{
		pObj = GetComponent<PlayerObject>();
		Debug.Log($"PlayerObject Added", pObj);
	}

/*
	public void SetNickname(string nickname)
    {
		if (nicknameUI == null)
		{
			nicknameUI = Instantiate(
				GameManager.rm.worldCanvasNicknamePrefab,
				uiPoint.transform.position,
				Quaternion.identity,
				GameManager.im.nicknameHolder);
			nicknameUI.target = uiPoint;
		}
		nicknameUI.worldNicknameText.text = nickname;
    }
	
    public void SetColour(Color col)
    {
		Debug.Log($"{pObj.Nickname} changed color <color=#{ColorUtility.ToHtmlStringRGB(col)}>\u2588</color>");
		BodyMaterial.color = col;
    }

	public void SetGhost(bool on)
	{
		if (on)
		{
			if (PlayerMovement.Local.IsDead)
			{
				// semi-visible
				modelParts.ForEach(m => m.enabled = true);
				BodyMaterial.shader = GameManager.rm.ghostShader;
				TrimMaterial.shader = GameManager.rm.ghostShader;
				VisorMaterial.shader = GameManager.rm.ghostShader;
			}
			else
			{
				// full invisible
				modelParts.ForEach(m => m.enabled = false);
			}
		}
		else
		{
			modelParts.ForEach(m => m.enabled = true);
			BodyMaterial.shader = GameManager.rm.playerBodyMaterial.shader;
			TrimMaterial.shader = GameManager.rm.playerTrimMaterial.shader;
			VisorMaterial.shader = GameManager.rm.playerVisorMaterial.shader;
		}
	}

    internal void UpdateAnimation(PlayerMovement playerMovement)
    {
		moveSpeed = Mathf.Min(1f, Mathf.MoveTowards(moveSpeed, playerMovement.simpleCC.RealSpeed, Time.deltaTime * animationBlending));
		anim.SetFloat(MOVE_SPEED_PARAMETER, moveSpeed);
	}
    */
}

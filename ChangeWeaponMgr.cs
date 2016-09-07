using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class ChangeWeaponMgr {

	static public ChangeWeaponMgr instance = new ChangeWeaponMgr();

	public ChangeWeaponMgr()
	{
		CoreEntry.eventMgr.AddListener("beginChangePlayer", OnChangePlayer);

	}

	~ChangeWeaponMgr()
	{
		CoreEntry.eventMgr.RemoveListener(this);
		Scheduler.RemoveSchedule(this);
	}

	//是否在切换武器动画
	private bool isChangingWeapon = false;
	//武器动画背景Prefab
	private const string backBroundAnim = "p_ui_WQZH_djf01";
	//武器动画Prefab命名统一前缀
	private const string prefix = "weapon_";
	//动画文件路径
	private const string ANIM_PATH = "Prefabs/Effect/scene/";
	//武器特效时长
	private const float EFFECT_TIME = 1.8f; 
	//武器背景Prefab
	private GameObject weaponBgObj = null;
	//武器特效Prefab
	private GameObject weaponObj;
	//最后一次播放的动画中武器CID
	private byte[] usingWeaponCID = null;
	//ActionId
	private int actionId = -1;
	private int effectActionId = -1;

	public void BeginSwitchWeapon(byte[] weaponCID)
	{
		//Clear
		//Close main camera
		//Instantiate weaponPrefab&& bg Prefab
		//Pause game&&ai
		//Wait Until finished
		//Open main camera
		if (!CanSwitchWeapon())
			return;
		Clear();
		//var weaponCID = CoreEntry.portalMgr.GetCurrentWeaponInstanceId();
		if (weaponCID == null)
			//当前底座上没有武器
			return;
		int weaponResId = CoreEntry.portalMgr.GetResIdByCID(weaponCID);
		string weaponAnim = prefix + weaponResId.ToString();
		string bgAnimPath = ANIM_PATH + backBroundAnim;
		string weaponAnimPath = ANIM_PATH + weaponAnim;
		weaponObj = CoreEntry.resourceMgr.InstantiateObject(weaponAnimPath, SceneMgr.FarAway, Quaternion.identity);
		weaponBgObj = CoreEntry.resourceMgr.InstantiateObject(bgAnimPath, SceneMgr.FarAway, Quaternion.identity);
		if (weaponBgObj == null || weaponBgObj == null)
		{
			EndOfSwitch();
			return;
		}

		//Begin Play ChangeWeapon Anim
		isChangingWeapon = true;
		usingWeaponCID = weaponCID;
		CoreEntry.eventMgr.TriggerEvent("beginChangeWeapon");
		CoreEntry.cameraMgr.CloseMainCamera();
		CoreEntry.cameraMgr.CloseUICamera();
		CoreEntry.sceneMgr.Pause(PauseType.PauseAI, StopInputType.None);


		effectActionId = Scheduler.Create(this, (sche, t, s) =>
			{
				EndOfSwitch();
			}, 0, 0, EFFECT_TIME).actionId;

	}

	/// <summary>
	/// 是否在切换武器接口
	/// </summary>
	/// <returns></returns>
	public bool IsChanging()
	{
		return isChangingWeapon;
	}
	private void EndOfSwitch()
	{
		Clear();
		CoreEntry.sceneMgr.Pause(PauseType.ResumeAI, StopInputType.None);
		CoreEntry.cameraMgr.OpenMainCamera();
		CoreEntry.cameraMgr.OpenUICamera();
		CoreEntry.eventMgr.TriggerEvent("endChangeWeapon");
	}

	private void Clear()
	{
		if(weaponBgObj !=null)
			GameObject.Destroy(weaponBgObj);
		if (weaponObj != null)
			GameObject.Destroy(weaponObj);
		isChangingWeapon = false;
		Scheduler.RemoveSchedule(actionId);
		Scheduler.RemoveSchedule(effectActionId);
	}

	private bool CanSwitchWeapon()
	{
		if (ChangeHeroMgr.instance.IsChanging() == true ||	//切换英雄时不切换
			CoreEntry.uiMgr.isPlayingStageAnimate == true ||	//播放过场动画时不切换
			DialogueMgr.instance.isTalking == true ||	//正在对话时不切换
			CoreEntry.GetGameStage() == GameStage.SkillLevel ||	//超能关卡时不切换
			CoreEntry.GetGameStage() == GameStage.SummonerHouse ||	//召唤师小屋时不切换
			isChangingWeapon == true ||	//正在切换武器时不切换
			SceneLoadMgr.instance.IsLoadingScene() == true || //加载场景时不切换 
			CoreEntry.isStageEnter == false 
			)
			return false;
		return true;
	}

	private void OnChangePlayer(string gameEvent)
	{
		if (isChangingWeapon)
		{
			Clear(); 
		}
	}
}

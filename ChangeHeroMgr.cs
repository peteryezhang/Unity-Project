
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ChangeType
{
	NEW_HERO,
	NO_HERO,
	HERO_DEAD,
	PVE_HERO_DEAD,
	PVE_NEW_HERO,
	VIRTUAL_HERO,
	NONE,
}

public class ChangeHeroMgr
{

	static public ChangeHeroMgr instance = new ChangeHeroMgr();

	private byte[] usingHeroCID = null;
	public byte[] UsingHeroCID
	{
		get { return usingHeroCID; }
	}
	bool isShowHeroAnim = false;
	public bool IsShowHeroAnim
	{
		get { return isShowHeroAnim; }
	}

	private bool needDelay = false;
	public bool NeedDelay
	{
		get { return needDelay; }
		set { needDelay = value; }
	}
	private byte[] evolutionCID = null;
	public byte[] EvolutionCID
	{
		get { return evolutionCID; }
		set { evolutionCID = value; }
	}
	const float EFFECT_SHOW_TIME = 3.3f;
	const float EFFECT_DIE_TIME = 0.3f;
	const float EFFECT_FINISH = 3.8f;
	const float EFFECT_FINISH_EVOLUTION = 7.0f;
	const float EFFECT_PRE_FINISH = 3.0f;
	const float EFFECT_SHOW_NAME = 2.5f;

	GameObject switchEmpty;
	GameObject heroObj;
	GameObject evolutionObj;
	GameObject wingObj;
	GameObject switchEffect;
	PanelBase virtualPanel;
	HeroData currentHeroData;
	ChangeType changeType;

	int usingHeroResId = -1;
	bool isChanging = false;
	bool isShowingEmptyBase = true;
	bool isPlayingHeroDead = false;	//是否在播放英雄死亡画面
	bool isAllDead = false;
	bool isRevolution = false;

	uint soundId = 0;
	int nameAnimActionId = -1;
	int changeHeroActionId = -1;
	int soundActionId = -1;
	byte[] showingCID = null;	//适用于只播英雄动画但英雄不进场对应的英雄CID
	System.Action deadAction;
	System.Action createHeroAction;
	System.Action pveDeadAction;
	System.Action virtualAction;
	PlayerData playerData = new PlayerData();
	string switchBg = "p_scene_switchball_commBg";
	string evolutionBase = "p_shengji_djf01";
	string evolutionWing = "p_shengji_djf02";
	string wing = "wings";
#if false
	Dictionary<ElementType, string> switchballAnim = new Dictionary<ElementType, string>()
    {
        {ElementType.HERO_TYPE_BOLT, "p_scene_switchball_Bolt"},
        {ElementType.HERO_TYPE_SOIL, "p_scene_switchball_Stone"},
        {ElementType.HERO_TYPE_WIND, "p_scene_switchball_Wind"},
        {ElementType.HERO_TYPE_FIRE, "p_scene_switchball_Fire"},
        {ElementType.HERO_TYPE_MECHANICS, "p_scene_switchball_Gold"},        
    };
#endif
	public ChangeHeroMgr()
	{
		//Stage改变消息
		CoreEntry.eventMgr.AddListener<GameStage>("onGameStagePrepareEnter", OnStagePrepareEnter);
		//玩具进入消息
		CoreEntry.eventMgr.AddListener<LitJson.JsonData>("toyEnter", OnToyEnter);
	
	}

	~ChangeHeroMgr()
	{
		CoreEntry.eventMgr.RemoveListener(this);
	}

	private void OnToyEnter(string gameEvent, LitJson.JsonData data)
	{
		Scheduler.RemoveSchedule(soundActionId);
		CoreEntry.soundMgr.StopSound(soundId);
		
	}
	private void OnStagePrepareEnter(string gameEvent, GameStage stage)
	{
		if (isChanging)
			isChanging = false;

		isAllDead = false;
	}


	// 一般来说，切换不会被中断，直接走新流程
	// 而是等待完整流程自然退出才再次轮询，然后再走新流程
	public void BeginSwitch(ChangeType changeType,byte[] showCID = null)
	{
		//1 Go Through CanSwitch()
		//2 If CanSwitch, Init Params and Dispatch ChangeType Here
		//3 When Change Hero End,Dispatch Event 'endChangePlayer'
		this.changeType = changeType;
		if (ToyRecoverMgr.instance.CanSwitch() == false) return;
		Init(true,showCID);
		switch (changeType)
		{
			case ChangeType.NO_HERO:
				{
					//Show Waiting Animation
					ShowEmptyAnim();
				}
				break;
			case ChangeType.NEW_HERO:
			case ChangeType.PVE_NEW_HERO:
				{
					//Show Hero Animation
					ShowHeroAnim();
				}
				break;
			case ChangeType.VIRTUAL_HERO:
				{
					//Show VirtualHero Animation
					ShowVirtualHeroAnim();

				}
				break;
			case ChangeType.HERO_DEAD:
			case ChangeType.PVE_HERO_DEAD:
				{
					//Show HeroDead Animation
					ShowHeroDeadAnim(changeType);
				}
				break;
		};


	}

	void Init(bool isInit = true,byte[] showingCID = null)
	{
		isChanging = isInit;
		this.showingCID = showingCID;
		CoreEntry.uiMgr.SetHudVisible(!isInit);
		if (isInit == false)
		{
            // 还原soundListener
            CoreEntry.sceneMgr.soundPlayerTransform = CoreEntry.sceneMgr.playerTransform;
			//派发切换英雄结束事件
			CoreEntry.eventMgr.TriggerEvent("endChangePlayer");
			//还原evolutionCID
			if (evolutionCID != null)
			{
				CoreEntry.eventMgr.TriggerEvent("HeroEvolutionEnd");
				evolutionCID = null;
			}
		}
		else
		{
			//派发切换英雄开始事件
			CoreEntry.eventMgr.TriggerEvent("beginChangePlayer");
			//派发面板隐藏事件
			CoreEntry.eventMgr.TriggerEvent<bool>("doSetupMainPanel", false);
		}
	}

	public void Clear(bool lastChanging = true)
	{
		// 只有是由切换英雄导致的镜头关闭才需要恢复
		if (lastChanging)
			CoreEntry.cameraMgr.OpenMainCamera();

		CloseEmpty();

		ClearEffect();

		CoreEntry.soundMgr.StopSound(soundId);

		CoreEntry.uiMgr.DestroyPanel("DeadPanel");
		isPlayingHeroDead = false;
		isShowHeroAnim = false;
		isChanging = false;
	}

	void CloseEmpty()
	{
		if (switchEmpty != null)
		{
			GameObject.Destroy(switchEmpty);
			CoreEntry.uiMgr.DestroyPanel("BluetoothDisconnectPanel");
		}
	}

	void InitParams(bool isInit)
	{
		if(isInit)
		{
			isShowHeroAnim = true;
			isPlayingHeroDead = false;
		}
		else
		{
			isShowHeroAnim = false;
		}
	}

	void ShowHeroAnim()
	{
		//初始化切换英雄
		CloseEmpty();
		ClearEffect();
		InitParams(true);
		usingHeroCID = showingCID == null ? CoreEntry.portalMgr.GetLastHeroCID() : showingCID;
		var data = PlayerDataMgr.instance.GetHeroDataByInstanceId(usingHeroCID);
		if (data != null)
		{
			CoreEntry.eventMgr.TriggerEvent<bool>("showHeroSwitch", false);
			//设置切换类型
			this.changeType = ChangeType.NEW_HERO;
			//回调函数
			System.Action cb = () =>
				{
					FinishEffect("switchIdle", () =>
					{
						ResetUsingCID();
						InitParams(false);
						ClearEffect();
						EndSwitch();
					});
				};
			if (CoreEntry.IsStageUsingHero(CoreEntry.GetGameStage()))
			{
				CoreEntry.globalObject.GetComponent<CoreEntry>().StartCoroutine(SceneMgr.DelayToInvoke(() =>
				{
					//播放切换英雄特效
					CreateEffect(data.resId, "idle", cb);
				}));
				needDelay = false;
			}
			else
			{
				//播放切换英雄特效
				CreateEffect(data.resId, "idle", cb);
			}


		}

	}

	void ShowVirtualHeroAnim()
	{
		virtualAction = () =>
			{
				CoreEntry.virtualHeroMgr.IsVirtualSwitchHero = true;
				//Clear Effect Before
				ClearEffect();
				//Close Main Camera
				CoreEntry.cameraMgr.CloseMainCamera();

				//Get VirtualSwitchPanel
				virtualPanel = CoreEntry.uiMgr.GetPanelSimply(AIToyMgr.VIRTUAL_PANEL);

				//Mount Sound
				if (virtualPanel != null)
					soundId = CoreEntry.soundMgr.PlaySound("MainUIsfx", "Play_Hero_Change_Before", virtualPanel.gameObject);

			};

		if (virtualAction == null) return;
		if (CoreEntry.IsStageUsingHero(CoreEntry.GetGameStage()))
		{
			//Wait for two frames to init new object ignoreTimeScale
			CoreEntry.globalObject.GetComponent<CoreEntry>().StartCoroutine(SceneMgr.DelayToInvoke(() =>
			{
				virtualAction();
			}));
		}
		else
		{
			virtualAction();
		}

	}
	GameObject ShowEmptyAnim()
	{
		//清除之前特效 Effect
		ClearEffect();
		ResetUsingCID();
		evolutionCID = null;
		//正在播放等待动画时不响应新的请求
		if (switchEmpty != null)
			return switchEmpty;
		//关闭场景中主摄像机，使用美术摄像机
		CoreEntry.cameraMgr.CloseMainCamera();
		switchEmpty = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Effect/scene/switchball/p_switchball_empty", SceneMgr.FarAway, Quaternion.identity);
		isShowingEmptyBase = true;
		isPlayingHeroDead = false;
		CoreEntry.uiMgr.GetPanelSimply("BluetoothDisconnectPanel");
		//播放英雄进入前声音
		soundId = CoreEntry.soundMgr.PlaySound("MainUIsfx", "Play_Hero_Change_Before", switchEmpty);
		soundActionId = Scheduler.Create(this,(sch,t,s)=>
		{
			if(switchEmpty != null)	
				soundId = CoreEntry.soundMgr.PlaySound("voice_zhiyin_group_20", "Play_voice_zhiyin_130", switchEmpty); 
		},0,0,1.5f,ActionMgr.Priority.Normal,true).actionId;

		return switchEmpty;
	}


	void ShowHeroDeadAnim(ChangeType changeType)
	{
		switch (changeType)
		{
			case ChangeType.HERO_DEAD:
                {

					// play dead and wait for hero
					int hero = CoreEntry.portalMgr.GetLastHeroResId();
					this.changeType = ChangeType.HERO_DEAD;

					//正在播放本英雄死亡动画时，不再切换
					if (isPlayingHeroDead == true 
                        && PortalMgr.CompareCID(UsingHeroCID,CoreEntry.portalMgr.GetCurrentHeroInstanceId()) == true) 
                        return;

					isPlayingHeroDead = true;
					//回调函数
					System.Action cb = () =>
						{
							CoreEntry.eventMgr.TriggerEvent<bool>("showHeroSwitch", true);
							DeadPanel deadPanel = null;
							VirtualHeroSwitchPanel virtualPanel = null;

							if (CoreEntry.toyMgr.GameMode == GameMode.TOY)
							{
								deadPanel = CoreEntry.uiMgr.GetPanelSimply("DeadPanel") as DeadPanel;

								soundId = CoreEntry.soundMgr.PlaySound("MainUIsfx", "Play_Hero_Change_Before", deadPanel.gameObject);
							}
							else
							{
								virtualPanel = CoreEntry.uiMgr.GetPanelSimply(AIToyMgr.VIRTUAL_PANEL) as VirtualHeroSwitchPanel;

								soundId = CoreEntry.soundMgr.PlaySound("MainUIsfx", "Play_Hero_Change_Before", virtualPanel.gameObject);
							}

							deadAction = () =>
							{

								isAllDead = true;
								CoreEntry.eventMgr.TriggerEvent("HeroDeadShowEnd");
								EndSwitch();
								CoreEntry.eventMgr.TriggerEvent(EventDefine.EVENT_LOSE_BATTLE);
							};
							if (deadPanel != null)
							{
								deadPanel.onRestart = deadAction;
							}
							else
							{
								virtualPanel.onRestart = deadAction;
							}
						};
					//播放特效
					CreateEffect(hero, "hero_dead", cb);
				}
				break;
			case ChangeType.PVE_HERO_DEAD:
				{
					AiToyDebug.Log("pve英雄死亡, 检查英雄是否已死亡完");

					if (CoreEntry.uiMgr.GetPanel(PvePanelMgr.PveResultPanel) != null)
					{
						isChanging = false;
						return;
					}

					if (CoreEntry.areaMgr.IsDoublePveScene() && PveMgr.instance.heroMgr.IsAllDie())
					{
						isChanging = false;
						return;
					}
					// play dead and wait for hero
					int hero = CoreEntry.portalMgr.GetLastHeroResId();
					this.changeType = ChangeType.PVE_HERO_DEAD;
					isPlayingHeroDead = true;

					CreateEffect(hero, "hero_dead", () =>
					{

						pveDeadAction = () =>
						{
							isAllDead = true;
							isChanging = false;
							isPlayingHeroDead = false;
							CoreEntry.uiMgr.DestroyPanel("DeadPanel");
							CoreEntry.eventMgr.TriggerEvent("CancelPauseChallengeScene");
						};
						CoreEntry.eventMgr.TriggerEvent<bool>("showHeroSwitch", true);
						DeadPanel deadPanel = null;
						VirtualHeroSwitchPanel virtualPanel = null;
						if (CoreEntry.toyMgr.GameMode == GameMode.TOY)
							deadPanel = CoreEntry.uiMgr.GetPanelSimply("DeadPanel") as DeadPanel;
						else
							virtualPanel = CoreEntry.uiMgr.GetPanelSimply(AIToyMgr.VIRTUAL_PANEL) as VirtualHeroSwitchPanel;

						if (deadPanel != null)
						{
							deadPanel.onRestart = pveDeadAction;
						}
						else
						{
							virtualPanel.onRestart = pveDeadAction;
						} 
					
					});
				}
				break;
		}
	}


	public void ClearEffect()
	{
		if (heroObj != null)
		{
			GameObject.Destroy(heroObj);
		}

		if (switchEffect != null)
		{
			GameObject.Destroy(switchEffect);
		}
		//Scheduler.RemoveSchedule(soundActionId);
		CoreEntry.uiMgr.DestroyPanel("DeadPanel");
	}


	public void ClearChange()
	{
		//外部调用接口
		if (CoreEntry.toyMgr.GameMode == GameMode.VIRTUAL)
		{
			CoreEntry.uiMgr.DestroyPanel(AIToyMgr.VIRTUAL_PANEL);
		}

		Clear(isChanging); 
		Scheduler.RemoveSchedule(this);
	}

	void ResetUsingCID()
	{
		usingHeroCID = CoreEntry.portalMgr.GetLastHeroCID();
	}
	void CreateEffect(int heroResId, string actionName, System.Action cb)
	{
		// 清除旧的特效
		CloseEmpty();
		ClearEffect();


		// 切换过程中都只认这个CID
		usingHeroCID = showingCID == null ? CoreEntry.portalMgr.GetLastHeroCID() : showingCID;
		usingHeroResId = heroResId;
		isShowingEmptyBase = false;

		// 创建特效
		float costTime = 0;
		Transform heroPoint = null;
		//ChardLi需求，进化后英雄第一次进入时，应播放进化之前对应的英雄动画
		if (evolutionCID != null && PortalMgr.CompareCID(usingHeroCID, evolutionCID))
		{
			var heroAttr = CoreEntry.gameDataMgr.heroAttr.GetRecord(heroResId);
			if (heroAttr != null)
			{
				heroResId = heroAttr.iInitialID;
			}
		}
		createHeroAction = () =>
		{
			//创建英雄
			if (CoreEntry.gameDataMgr.GetPlayerDataByResId(heroResId, out playerData))
			{
				if (heroObj != null)
				{
					UnityEngine.Object.Destroy(heroObj);
				}
				heroObj = CoreEntry.resourceMgr.InstantiateObject(playerData.resPath + "_hd", Vector3.one, Quaternion.identity);
			}
			if (heroObj == null)
			{
				cb();
				return;
			}
			//加载翅膀
			CoreEntry.resourceMgr.LoadWing(heroObj,heroResId);
			// 关闭脚本
			var cs = heroObj.GetComponents<Creature>();
			for (int i = 0; i < cs.Length; ++i)
			{
				cs[i].enabled = false;
			}
			GlobalFunctions.ChangeLayer(heroObj, LayerMask.NameToLayer("ChangePlayer"), "Untagged");
			if (CoreEntry.sceneMgr.isPauseGame)
				GlobalFunctions.AnimationIgnoreTime(heroObj);
		};


		if (IsDeadType(changeType))
		{

			if (CoreEntry.toyMgr.GameMode == GameMode.VIRTUAL)
			{
				//如果是虚拟英雄的话走单独流程
				CoreEntry.globalObject.GetComponent<CoreEntry>().StartCoroutine(SceneMgr.DelayToInvoke(() =>
				{
					// 关闭主摄像机，直接使用美术的        
					CoreEntry.cameraMgr.CloseMainCamera();
					var panel = CoreEntry.uiMgr.GetPanelSimply(AIToyMgr.VIRTUAL_PANEL);
					cb();
				}));
				return;
			}

			// 关闭主摄像机，直接使用美术的        
			CoreEntry.cameraMgr.CloseMainCamera();
			//英雄死亡的逻辑走这里
			switchEffect = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Effect/pet/p_pet_catch_lose", SceneMgr.FarAway, Quaternion.identity);
			costTime = EFFECT_DIE_TIME;

			// 检查是否还有可以更换的英雄玩偶
			bool isAllDead = CoreEntry.virtualHeroMgr.ToyHeroAllDead();

			// 获取文字特效
			Transform goShowText1 = switchEffect.transform.Find("HeroPoint/p_pet_lose");
			if (null != goShowText1)
			{
				goShowText1.gameObject.SetActive(isAllDead);
			}
			Transform goShowText2 = switchEffect.transform.FindChild("HeroPoint/p_pet_lose02");
			if (null != goShowText2)
			{
				goShowText2.gameObject.SetActive(!isAllDead);
			}

			heroPoint = GlobalFunctions.GetTransform(switchEffect.transform, "HeroPoint");
			if (heroPoint != null)
			{
				//延后执行，不显示英雄生成时站立画面，直接显示失败动画
				Scheduler.Create(this, (sche, t, s) =>
				{
					createHeroAction();
					if (heroObj != null)
						GlobalFunctions.MountTransform(heroObj.transform, heroPoint);
					// 播放英雄死亡公共特效
					CoreEntry.actionMgr.PlayAction("hero_dead", heroObj);
					//执行回调函数
					cb();
				}, 0, 0, costTime, ActionMgr.Priority.Normal, true);

			}
		}
		else
		{

			// 关闭主摄像机，直接使用美术的        
			CoreEntry.cameraMgr.CloseMainCamera();
			//英雄正常进入走这里
			createHeroAction();
			switchEffect = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Effect/scene/switchball/" + switchBg, SceneMgr.FarAway * 2, Quaternion.identity);
			costTime = EFFECT_SHOW_TIME;

			// 使用美术坐标

			heroPoint = GlobalFunctions.GetTransform(switchEffect.transform, "NonHeroPoint");
#if true
			if (heroPoint != null && heroObj != null)
			{
				//播放切换英雄时音效
				//GlobalFunctions.ChangeLayer(heroPoint.gameObject, LayerMask.NameToLayer("ChangePlayer"));
				GlobalFunctions.MountTransform(heroObj.transform, heroPoint);
				var actor = heroObj.GetComponent<Actor>();
				if (actor != null)
					actor.ignoreTimeScale = true;
				CoreEntry.actionMgr.PlayAction("switchFlyIn", heroObj);

			}
#endif

			// 创建名字特效
			Scheduler.Create(this, (sche, t, s) =>
			{
				// mount name
				if (switchEffect == null)
				{
					cb();
					return;
				}
				var namePoint = GlobalFunctions.GetTransform(switchEffect.transform, "NamePoint");
				if (playerData != null && namePoint != null && evolutionCID == null)	//进化特效不展示英雄名字
				{
					var name = CoreEntry.resourceMgr.InstantiateObject(playerData.namePath, SceneMgr.FarAway, Quaternion.identity);
					if (name != null)
					{
						name.transform.parent = namePoint;
						name.transform.localPosition = Vector3.zero;
						name.transform.localRotation = Quaternion.identity;
					}
				}
				cb();
			}, 0, 0, costTime, ActionMgr.Priority.Normal, true);
		}

		// 设置场景对象，用于设置soundListener
		CoreEntry.sceneMgr.soundPlayerTransform = heroPoint.transform;
	}

	public void FinishEffect(string actionName, System.Action cb)
	{
		float finishTime = EFFECT_FINISH;
		//是否需要展示进化相关特效
		if (evolutionCID != null && PortalMgr.CompareCID(usingHeroCID, evolutionCID))
		{
			finishTime = EFFECT_FINISH_EVOLUTION;
			Scheduler.Create(this, (sche, t, s) =>
			{
				evolutionObj = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Effect/common/" + evolutionBase, SceneMgr.FarAway, Quaternion.identity);
				if (heroObj != null && evolutionObj != null)
				{
					GlobalFunctions.MountTransform(evolutionObj.transform, heroObj.transform);
					GlobalFunctions.ChangeLayer(evolutionObj, LayerMask.NameToLayer("ChangePlayer"), "Untagged");
				}

			}, 0, 0, EFFECT_PRE_FINISH, ActionMgr.Priority.Normal, true);
			Scheduler.Create(this, (sche, t, s) =>
			{
				wingObj = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Character/Hero/Wings/" + wing, SceneMgr.FarAway, Quaternion.identity);
				var evolutionWingObj = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Effect/common/" + evolutionWing, SceneMgr.FarAway, Quaternion.identity);
				
				if (heroObj != null)
				{
					var wingPoint = GlobalFunctions.GetTransform(heroObj.transform, "FloatingItem");
					if (wingPoint != null && wingObj != null && evolutionWingObj != null)
					{
						GlobalFunctions.MountTransform(wingObj.transform, wingPoint);
						GlobalFunctions.MountTransform(evolutionWingObj.transform, wingPoint);
						GlobalFunctions.ChangeLayer(wingObj, LayerMask.NameToLayer("ChangePlayer"), "Untagged");
						GlobalFunctions.ChangeLayer(evolutionWingObj, LayerMask.NameToLayer("ChangePlayer"), "Untagged");
					}
				}
			}, 0, 0, EFFECT_PRE_FINISH+0.5f, ActionMgr.Priority.Normal, true);
			Scheduler.Create(this, (sche, t, s) =>
			{
				//TODO:先看效果，英雄进化资源到位后替换
				var namePoint = GlobalFunctions.GetTransform(switchEffect.transform, "NamePoint");
				if (playerData != null && namePoint != null)	//进化特效不展示英雄名字
				{
					var name = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Effect/ui/zi/p_ui_jinhuachenggong", SceneMgr.FarAway, Quaternion.identity);
					if (name != null)
					{
						name.transform.parent = namePoint;
						name.transform.localPosition = Vector3.zero;
						name.transform.localRotation = Quaternion.identity;
					}
				}
			}, 0, 0, EFFECT_PRE_FINISH + 1.0f, ActionMgr.Priority.Normal, true);
		}

		Scheduler.Create(this, (sche, t, s) =>
		{
			if (switchEffect != null)
			{
				GameObject.Destroy(switchEffect);
			}
			if (evolutionObj != null)
			{
				GameObject.Destroy(evolutionObj); 
			}
			if (wingObj != null)
			{
				GameObject.Destroy(wingObj);
			}
			if (cb != null)
				cb();

		}, 0, 0, finishTime, ActionMgr.Priority.Normal, true);
	}


	public void EndSwitch()
	{
		CoreEntry.cameraMgr.OpenMainCamera();
		Init(false);
		ToyRecoverMgr.instance.OnRecover("");
		ToyRecoverMgr.instance.WeaponRecoverByVirtualMode();
	}


	public bool IsChanging() { return isChanging; }

	public int GetChangeHero() { return usingHeroResId; }

	public bool IsShowingEmptyBase() { return isShowingEmptyBase; }
	public bool IsShowingPlayerDead() { return isPlayingHeroDead; }

	public ChangeType GetChangeType()
	{
		return changeType;//IsChanging() ? changeType : ChangeType.NONE;
	}

	public ChangeType GetNewType()
	{
		return CoreEntry.areaMgr.IsPvEScene() ? ChangeType.PVE_NEW_HERO : ChangeType.NEW_HERO;
	}
	public ChangeType GetDeadType()
	{
		return CoreEntry.areaMgr.IsPvEScene() ? ChangeType.PVE_HERO_DEAD : ChangeType.HERO_DEAD;
	}

	public bool IsNewType(ChangeType changeType)
	{
		return changeType == ChangeType.NEW_HERO ||
			changeType == ChangeType.PVE_NEW_HERO;
	}

	public bool IsDeadType(ChangeType changeType)
	{
		return changeType == ChangeType.PVE_HERO_DEAD ||
			changeType == ChangeType.HERO_DEAD;
	}


}

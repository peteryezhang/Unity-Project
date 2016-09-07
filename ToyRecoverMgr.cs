
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ToyRecoverMgr
{

	static public ToyRecoverMgr instance = new ToyRecoverMgr();

	public ToyRecoverMgr()
	{
		//从忽略玩具恢复需检查
		CoreEntry.eventMgr.AddListener<bool>("IgnoreToy", OnRecoverFromIgnoreToy);
		//从暂停中恢复需检查
		CoreEntry.eventMgr.AddListener("updatePauseState", OnRecover);
		//网络连接成功需检查
		CoreEntry.eventMgr.AddListener<bool>("networkConnection", OnRecoverWithBool);
		//游戏Stage切换完成需要检测
		CoreEntry.eventMgr.AddListener<GameStage>("onGameStageAllDone", OnRecoverFromStage);
		//对话结束需要检测
		CoreEntry.eventMgr.AddListener<string>("talkingFinish", OnRecoverFromDialogue);
		//新手引导阶段性结束需检测
		CoreEntry.eventMgr.AddListener<bool>("showNoviceGuidePanel", OnRecoverFromNoviceGuide);
		//从关卡中退出不再切换
		CoreEntry.eventMgr.AddListener("exitSettingPanel", OnRecoverFromExitLevel);
		//切换武器结束需检查
		CoreEntry.eventMgr.AddListener("endChangeWeapon", OnRecoverFromChangeWeapon);		
		//移除武器结束需要检查
		CoreEntry.eventMgr.AddListener("dropWeapon", OnRecoverFromChangeWeapon);
	}

	~ToyRecoverMgr()
	{
		CoreEntry.eventMgr.RemoveListener(this);
	}

	public bool CanSwitch()
	{
		//正在播放动画时不检测
		if (ChangeHeroMgr.instance.IsShowHeroAnim == true)
		{
			return false;
		}
		//非主城进入不检测
		if (CoreEntry.isFullGame == false)
		{
			return false;
		}
		//虚拟英雄切换时不检测
		if (CoreEntry.virtualHeroMgr.IsVirtualSwitchHero == true)
		{
			return false;
		}
		//战斗结算时不切换
		if (BattleResultMgr.instance.IsBattleEnd() == true)
		{
			//CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+ " + "Return by BattleEnd");
			return false;
		}
		if (CoreEntry.toyMgr.GameMode == GameMode.VIRTUAL)
		{
			return true;
		}
		//蓝牙未连接时不检测 无玩偶模式下忽略这项
		if (CoreEntry.portalMgr.connection == false && (GameMode.TOY == CoreEntry.toyMgr.GameMode))
		{
			//CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+" + "Return by Connection");
			return false;
		}
		//加载场景中不切换
		if (SceneLoadMgr.instance.IsLoadingScene() == true || CoreEntry.isStageEnter == false)
		{
			//CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+" + "Return by LoadingScene");
			return false; 
		}
		if (CoreEntry.portalMgr.IsIgnore() == true)
		{
			return false;
		}

#if false
		//暂停中不切换
		if (CoreEntry.sceneMgr.isPauseAI == true && isShowingEmptyBase == false && isPlayingHeroDead == false)
		{
			//CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+" + "Return by Pause");
			return false;
		}
#endif
		//处于新手引导时不切换
		if (CoreEntry.noviceGuideMgr.IsModal == true)
		{
			//CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+" + "Return by NoviceGuide");
			return false;
		}
		//只在需要切换英雄动画场景切换
		if (CoreEntry.IsStageHaveHero(CoreEntry.GetGameStage()) == false)
		{
			//CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+" + "Return by StageLimit");
			return false;
		}
		//技能关卡中不切换英雄
		if (CoreEntry.GetGameStage() == GameStage.SkillLevel)
		{
			//CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+ " + "Return by SkillLevel");
			return false;
		}
		//PVP中和开始倒计时前都不能不切换英雄
        if (CoreEntry.areaMgr.IsPvPScene() || CoreEntry.pvpMgr.GameStagePVP==PVPGameStage.BeginCountDown)
		{
			//CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+ " + "Return by PVP");
			return false;
		}


		//摄像机使用时不切换
		if (CoreEntry.cameraMgr.InAttention() == true && ChangeHeroMgr.instance.IsDeadType(ChangeHeroMgr.instance.GetChangeType()) == false)
		{
			//CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+ " + "Return by CameraAttention");
			return false;
		}
		//对话时不切换
		if (DialogueMgr.instance.isTalking == true)
		{
			//CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+ " + "Return by Dialogue");
			return false;
		}
		//播放剧情时不切换
		if (CoreEntry.uiMgr.isPlayingStageAnimate == true)
		{
			//CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+ " + "Return by StageAnim");
			return false;
		}

		return true;
	}

	void OnRecoverFromDialogue(string gameEvent,string dialogueId)
	{
		//对话中切换英雄
		//如果是在主城中对话结束不播放新英雄进入动画
		//如果是在战斗场景中对话结束后必须播放新英雄进入动画以更新数据
		if (CoreEntry.GetGameStage() == GameStage.Town)
		{
			CoreEntry.portalMgr.ResetChangeToyStatus(ToyType.TOY_HERO);
		}
		OnRecover(gameEvent);
	}

	void OnRecoverFromExitLevel(string gameEvent)
	{
		OnRecoverFromStage(gameEvent,GameStage.Nothing);
	}

	void OnRecoverFromNoviceGuide(string gameEvent,bool isEnter)
	{
		//新手引导中切换英雄
		//如果是在主城引导中引导结束不播放新英雄进入动画
		//如果是在战斗场景中对话结束必须播放新英雄进入动画以更新数据
		if(CoreEntry.GetGameStage() == GameStage.Town)
		{
			CoreEntry.portalMgr.ResetChangeToyStatus(ToyType.TOY_HERO);
		}
		OnRecover(gameEvent);
	}

	void OnRecoverWithBool(string gameEvent,bool flag)
	{
		OnRecover(gameEvent);
	}

	void OnRecoverFromIgnoreToy(string gameEvent, bool ignore)
	{
		if (!ignore)
			OnRecover("");
	}

	void OnRecoverFromStage(string gameEvent,GameStage stage)
	{
		//加载场景结束后统一不播放切换英雄动画
		//优化加载场景时切换英雄的行为，在战斗场景中OnRecover部分延迟2帧执行
		CoreEntry.portalMgr.ResetChangeToyStatus(ToyType.TOY_HERO);
		CoreEntry.globalObject.GetComponent<CoreEntry>().StartCoroutine(SceneMgr.DelayToInvoke(() =>
			{
				OnRecover(gameEvent);
			}));
	}

	void OnRecoverFromChangeWeapon(string gameEvent)
	{
		//AfterChangeHeroEnd();
	}

	public void OnRecover(string gameEvent)
	{
		//特定场景不能切换
		if (CanSwitch() == false) return;
		var currentHeroCID = CoreEntry.portalMgr.GetCurrentHeroInstanceId();
		//虚拟模式不用Recover
		if (CoreEntry.toyMgr.GameMode == GameMode.VIRTUAL)
		{
			return; 
		}

		//当前播放英雄CID与当前底座上CID一致，重置切换英雄状态
		//usingHeroCID不为null
		if (currentHeroCID == null)
		{
			CoreEntry.portalMgr.ResetChangeToyStatus(ToyType.TOY_HERO);
			ChangeHeroMgr.instance.BeginSwitch(ChangeType.NO_HERO);
			CoreEntry.logMgr.Log(LogLevel.INFO, "ChangHeroMgr+ ", "OnRecover+ " + "Return by EmptyBase");
			return;
		}

		//播放A动画时切换成B，仍处于A动画时切换成A，此时不再播放一遍A的进入动画
		if(PortalMgr.CompareCID(currentHeroCID, ChangeHeroMgr.instance.UsingHeroCID) == true)
		{
			CoreEntry.portalMgr.ResetChangeToyStatus(ToyType.TOY_HERO);
		}


		if (CoreEntry.portalMgr.NeedChangeToy(ToyType.TOY_HERO) == true)
		{
			//当前需要切换英雄，调用SceneMgr中接口，分发需要切换的类型
			CoreEntry.sceneMgr.ChangePlayer(true);
			CoreEntry.portalMgr.ResetChangeToyStatus(ToyType.TOY_HERO);
		}
		else
		{
			//当前不需要切换英雄，分为两种情况
			var heroData = PlayerDataMgr.instance.GetHeroDataByInstanceId(currentHeroCID);
			if (heroData != null)
			{
				//1、放入的是已绑定的英雄
				CoreEntry.toyBindMgr.OnToyBindRecover();
				//捕捉棒、武器只需在切换英雄最终结束后Recover一次
				//CoreEntry.portalMgr.OnRecover(null);
				PetRecover();
				WeaponRecoverByToyMode();
			}
			else
			{
				//2、放入的是未绑定的英雄
				NetworkHelper.HeroSelectNotifyServer(currentHeroCID);
			}

		}
		
	}

	/// <summary>
	/// 虚拟模式下在每次切换英雄结束后恢复当前英雄可能佩戴的武器信息
	/// </summary>
	public void WeaponRecoverByVirtualMode(bool ignoreMode = false)
	{
		if (ignoreMode || CoreEntry.toyMgr.GameMode == GameMode.VIRTUAL)
		{
#if true
			//当前使用的是虚拟英雄，尝试恢复HeroInfo.szCurVirtualWeaponID上面该英雄携带的武器
			var currentHeroCID = CoreEntry.portalMgr.GetCurrentHeroInstanceId();
			var heroInfo = PlayerDataMgr.instance.GetHeroInfoByCID(currentHeroCID);
			if (heroInfo != null)
			{
				var virtualWeaponCID = heroInfo.szCurVirtualWeaponID;
				bool needChangeWeapon = WeaponMgr.instance.NeedChangeWeapon(currentHeroCID, virtualWeaponCID);
				if (virtualWeaponCID != null && needChangeWeapon)
					CoreEntry.portalMgr.OnSwitchSelect(virtualWeaponCID);
			}
#endif
		}
	}

	/// <summary>
	/// 玩偶模式下存在切换英雄时再次切换的情况，因此在最后一次切换结束后恢复当前英雄可能佩戴的武器信息
	/// </summary>
	private void WeaponRecoverByToyMode()
	{
		//TODO:切换武器二期新需求：实体玩具可以用虚拟武器,并记录
		//1、如果底座上没有武器，尝试切换HeroInfo中的虚拟武器
		//2、如果底座上有武器，且武器类型匹配，尝试切换实体武器
		//3、如果底座上有武器，且武器类型不匹配，尝试切换HeroInfo中的虚拟武器
		if (CoreEntry.toyMgr.GameMode == GameMode.TOY)
		{
			var currentHeroCID = CoreEntry.portalMgr.GetCurrentHeroInstanceId();
			string heroCID = GlobalFunctions.TrimByteToString(currentHeroCID);
			var currentWeaponCID = CoreEntry.portalMgr.GetCurrentWeaponInstanceId();
			if (currentWeaponCID != null)
			{
				bool needChange = WeaponMgr.instance.NeedChangeWeapon(currentHeroCID, currentWeaponCID);
				if (needChange)
					CoreEntry.portalMgr.ChangeWeapon(currentWeaponCID);

			}
			else
			{
				//2016/6/27 暂时关闭实体英雄检测虚拟武器的入口
				//武器二期需求完成后打开入口即可
				//WeaponRecoverByVirtualMode(true);
			}
		}

	}

	private void PetRecover( )
	{
		var currentPetId = PlayerDataMgr.instance.GetCurrentPetId();
		var currentPetStickId = CoreEntry.portalMgr.GetCurrentPetStickInstanceId();

		// pet
		if (PlayerDataMgr.instance.GetCurrentPetId() != CoreEntry.portalMgr.GetLastPetResId() ||
			0 == PlayerDataMgr.instance.GetCurrentPetId() &&
			null != CoreEntry.portalMgr.GetCurrentPetStickInstanceId())
		{

			if (CoreEntry.sceneMgr.IsPause() == false && CoreEntry.portalMgr.connection == true)
			{
				//TODO: Print log to debug
				CoreEntry.portalMgr.ChangePet(CoreEntry.portalMgr.GetCurrentPetStickInstanceId());
				AiToyDebug.Log("SwitchPetStick");
			}
		}
		else
		{
			if (null == CoreEntry.portalMgr.GetCurrentPetStickInstanceId())
			{
				Scheduler.Create(this, (sche, t, s) =>
				{
					CoreEntry.portalMgr.ChangePet(null);
				}, 0, 0, 1);
			}
		}
	}


}

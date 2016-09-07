using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


class ToyData
{
    public ToyType toyType;
    public byte[] currentCID;                               // 当前cid，有可能为空
    public byte[] lastCID;                                  // 最新的cid，包括当前的，但只要得过，就不为空
    public List<byte[]> toyList = new List<byte[]>();       // 当前底座上放置的英雄
}

class CurrentToyData
{
	//尽可能封闭接口，只提供Get Param方法以及Set全部参数的方法
	private ToyType toyType = ToyType.TOY_NONE;
	public ToyType ToyType
	{
		get { return toyType; }
		set { toyType = value; }
	}
	private byte[] currentCID = null;
	public byte[] CurrentCID
	{
		get { return currentCID; }
	}
	private int currentResID = 0;
	public int CurrentResID
	{
		get { return currentResID; }
	}
	private bool hasChanged = false;
	public bool HasChanged
	{
		get { return hasChanged; }
		set { hasChanged = value; }
	}
	public CurrentToyData(ToyType type)
	{
		this.ToyType = type;
	}

	public void SetCurrentToyData(byte[] cid,ToyType type)
	{
		//数据有可能为null
		this.toyType = type;
		this.currentResID = CoreEntry.portalMgr.GetResIdByCID(cid);
		this.currentCID = cid;
		this.hasChanged = true;
	}
}
public class PortalMgr : MonoBehaviour
{
    static public int CID_BYTE_NUM = 16;
    static public int UID_BYTE_NUM = 14;
    public const int TYPE_BYTE_NUM = 4;
    static public byte[] CID_ZERO = new byte[CID_BYTE_NUM];
    static public byte[] UID_ZERO = new byte[UID_BYTE_NUM];
	public bool connection = false;


    private bool ignoreToy = false;
    private bool canSwitchToy = true;
	private List<bool> canSwitchToyList = new List<bool>();
    private const int TOY_HERO_LIMIT_PASS = 40001;//实体英雄切换开启关卡限制

	private Dictionary<ToyType, ToyData> toyInfo = new Dictionary<ToyType, ToyData>();
	private Dictionary<ToyType, CurrentToyData> currentToyDataDic = new Dictionary<ToyType, CurrentToyData>();
    private List<byte[]> heroCidList = new List<byte[]>();
	private System.Action clearPanel;
	private uint soundId = 0;
	//默认空CID
	public byte[] emptyCID = new byte[16];


    void Start()
    {
        CoreEntry.eventMgr.AddListener<LitJson.JsonData>("toyEnter", OnToyEnter);
        CoreEntry.eventMgr.AddListener<LitJson.JsonData>("toyExit", OnToyExit);
        CoreEntry.eventMgr.AddListener<LitJson.JsonData>("onToyPanel", OnToyPanelEnter);
        CoreEntry.eventMgr.AddListener<LitJson.JsonData>("offToyPanel", OnToyPanelExit);
        CoreEntry.eventMgr.AddListener<LitJson.JsonData>("noToy", OnNoToy);
        CoreEntry.eventMgr.AddListener("HeroPanelDestroyed", OnHeroPanelDestroyed);
        CoreEntry.eventMgr.AddListener("afterLogin", AfterLogin);
		CoreEntry.eventMgr.AddListener("BattleEnd",OnBattleEnd);
		CoreEntry.eventMgr.AddListener("SkillLevelEndClean", OnBattleEnd);
		CoreEntry.eventMgr.AddListener<GameStage>("onGameStageAllDone", OnRecoverFromStage);
		//虚拟英雄切换时需要将底座上武器信息清除
		CoreEntry.eventMgr.AddListener(AIToyMgr.VIRTUAL_HERO_SUMMON, OnVirtualHeroSummoned);
        RebuildToyInfo();
		RebuildCurrentToyData();
		
    }

    void OnDestroy()
    {
        CoreEntry.eventMgr.RemoveListener(this);
    }

    void AfterLogin(string gameEvent)
    {
    }

	void OnVirtualHeroSummoned(string gameEvent)
	{
		toyInfo[ToyType.TOY_WEAPON].currentCID = null;
		toyInfo[ToyType.TOY_WEAPON].toyList.Clear();
	}
	void OnRecoverFromStage(string gameEvent, GameStage stage)
	{
		//加载场景结束后恢复
		if(stage == GameStage.Town)
			IgnoreToy(false);
	}
	void OnBattleEnd(string gameEvent)
	{
		//通关时即使在关卡结算界面切换了英雄，也要重置需要切换的flag
		ResetChangeToyStatus(ToyType.TOY_HERO);
	}
	void RebuildCurrentToyData()
	{
		currentToyDataDic.Clear();
		currentToyDataDic[ToyType.TOY_HERO] = new CurrentToyData(ToyType.TOY_HERO);
		currentToyDataDic[ToyType.TOY_WEAPON] = new CurrentToyData(ToyType.TOY_WEAPON);
		currentToyDataDic[ToyType.TOY_PET] = new CurrentToyData(ToyType.TOY_PET);

	}

	void SetCurrentToyData(byte[] CID,ToyType type)
	{
		//玩具数据的重置以玩具的唯一标识CID为准
		//CID == null时也重置数据
		if (type == ToyType.TOY_NONE) return;
		var currentToyData = currentToyDataDic[type];
		//玩具尚未绑定不重置FLAG
		bool hasToy = true;
		switch(type)
		{
			case ToyType.TOY_HERO:
				{
					var heroData = PlayerDataMgr.instance.GetHeroDataByInstanceId(CID);
					hasToy = (heroData != null);
				}
				break;
			case ToyType.TOY_PET:
				{
 					//TODO:
				}
				break;
			case ToyType.TOY_WEAPON:
				{
					var weaponData = PlayerDataMgr.instance.GetWeaponDataByCID(CID);
					hasToy = (weaponData != null);
				}
				break;
		}
		if (CID == null)
		{
 			if(CompareCID(currentToyData.CurrentCID, CID) == false)
				currentToyData.SetCurrentToyData(CID,type);

		}
		else
		{
 			if(CompareCID(currentToyData.CurrentCID, CID) == false&& hasToy == true)
				currentToyData.SetCurrentToyData(CID,type);
		}

	}

	public bool NeedChangeToy(ToyType toyType)
	{
		if (CoreEntry.toyMgr.GameMode == GameMode.VIRTUAL) return false;
		if ((int)toyType > 2) return false;
		var currentToyData = currentToyDataDic[toyType];
		if (currentToyData == null)
		{
			return false;
		}
		else
		{
			return currentToyData.HasChanged;
		}
		
	}


	public void ResetChangeToyStatus(ToyType toyType,bool hasChanged = false)
	{
		var currentToyData = currentToyDataDic[toyType];
		if ((int)toyType > 2) return ;
		if (currentToyData != null)
		{
			currentToyData.HasChanged = hasChanged;
		}
	}
    void OnHeroPanelDestroyed(string gameEvent)
    {
        if (connection && IsEmptyBase() && CoreEntry.toyMgr.GameMode == GameMode.TOY)
        {
            OnSwitchUnSelect(ToyType.TOY_HERO);
        }
    }

    //存储使用过的非实体玩具英雄的CID
    void InsertCIDToHeroList(byte[] curCID)
    {
        bool isContained = heroCidList.Contains(curCID);
        if (isContained)
        {
            RemoveCIDFromHeroList(curCID);
            heroCidList.Add(curCID);
        }
        else
        {
            heroCidList.Add(curCID);
        }
    }

    void RemoveCIDFromHeroList(byte[] cid)
    {
        for (int i = 0; i < heroCidList.Count; i++)
        {
            bool isEqual = CompareCID(heroCidList[i], cid);
            if (isEqual)
            {
                heroCidList.RemoveAt(i);
                break;
            }
        }
    }


    //返回上次使用的英雄
    public byte[] LastUseHero()
    {
        int length = heroCidList.Count;
        if (length != 0)
        {
            byte[] cid = heroCidList[length - 1];
            RemoveCIDFromHeroList(cid);
            return cid;
        }
        else
        {
            //Stack为空，默认弹出免费龙战士
            return PlayerDataMgr.instance.GetFreeHeroCid();
        }
    }


    void OnToyEnter(string gameEvent, LitJson.JsonData data)
    {
        OnToyPortalAction(System.Text.Encoding.UTF8.GetBytes((string)data["CID"]), true,true);
    }

    void OnToyExit(string gameEvent, LitJson.JsonData data)
    {
        OnToyPortalAction(System.Text.Encoding.UTF8.GetBytes((string)data["CID"]), false,true);
    }



	public void OnToyPanelEnter(string gameEvent, LitJson.JsonData data)
	{
		if (connection == true) return;

		connection = true;
		if (CoreEntry.toyMgr.GameMode == GameMode.TOY)
		{
			RebuildToyInfo();
			SetCurrentCID(ToyType.TOY_HERO, null);
			ChangeHero(null);
		}
		CoreEntry.eventMgr.TriggerEvent<bool>("toyPanelEnter", connection);

	}

	public void OnToyPanelExit(string gameEvent, LitJson.JsonData data)
	{
		//底盘断开，切换到上回使用的英雄
		if (connection == false) return;

		connection = false;
		if (CoreEntry.toyMgr.GameMode == GameMode.TOY)
		{
			RebuildToyInfo();
			//ToyOnly模式，底座退出不切换
		}
		CoreEntry.eventMgr.TriggerEvent<bool>("toyPanelEnter", connection);
#if false
		if (GameMode.VIRTUAL == CoreEntry.toyMgr.GameMode)
		{
			byte[] cid = LastUseHero();
			SetCurrentCID(ToyType.TOY_HERO, cid);
			ChangeHero(cid);
		}
#endif

	}

    void OnNoToy(string gameEvent, LitJson.JsonData data)
    {

    }

    public void IgnoreToy(bool isIg)
    {
        if (isIg == ignoreToy) return;

        ignoreToy = isIg;
		CoreEntry.eventMgr.TriggerEvent<bool>("IgnoreToy",isIg);
		if (isIg == false)
        {
			var heroCID = CoreEntry.portalMgr.GetCurrentHeroInstanceId();
			NetworkHelper.HeroSelectNotifyServer(heroCID);
        }
    }

    public bool IsIgnore()
    {
        return ignoreToy;
    }

	public void OnToyPortalAction(byte[] cid,bool enter,bool isByUser)
	{
		//处理游戏内程序将玩偶踢出的行为
		//如果在玩偶绑定过程中玩家主动拿走玩具，则将ToyBindDara移除
		//如果是程序调用，则不将ToyBindDara移除
		OnToyPortalAction(cid,enter);
		if (isByUser)
		{
			CoreEntry.toyBindMgr.RemoveToyBindDataByCID(cid);
		}
	}
    // 由于现在底座与虚拟底座不是同一个东西，需要区分接口
    // 这是物理底座接口，物理接口会存在放入n个英雄的情况，需要跟踪
    // 断开连接之后，会把记录清空
    public void OnToyPortalAction(byte[] cid, bool enter )
    {

        var toyType = GetToyType(cid);

        var toyData = toyInfo[toyType];

        bool alreadyExist = false;
        for (int i = 0; i < toyData.toyList.Count; ++i)
        {
            if (CompareCID(toyData.toyList[i], cid))
            {
                alreadyExist = true;
                // 在这里移除，避免重复比较
                if (enter == false)
                {
                    toyData.toyList.RemoveAt(i);
                }
                break;
            }
        }

        if ((enter && alreadyExist) ||
            (enter == false && alreadyExist == false))
        {
            CoreEntry.logMgr.Log(LogLevel.WARNING, "PortalMgr::OnToyPortalAction", "wrong enter");            
            return;
        }

		if (enter)
		{
			toyData.toyList.Add(cid);
		}
		else
		{
			CoreEntry.soundMgr.StopSound(soundId); 
		}

        CoreEntry.logMgr.Log(LogLevel.INFO, "PortalMgr::OnToyPortalAction", "toy count:" + toyData.toyList.Count.ToString());
        for (int i = 0; i < toyData.toyList.Count; ++i)
        {
            CoreEntry.logMgr.Log(LogLevel.INFO, "PortalMgr::OnToyPortalAction", "toy cid:" + GlobalFunctions.TrimByteToString(toyData.toyList[i]));
        }

        if (toyData.toyList.Count > 1)
		{
			string name = GetToyName(toyData.toyList[toyData.toyList.Count - 1]);
            CoreEntry.uiMgr.TipsString(string.Format(CoreEntry.gameDataMgr.GetString("tooManyToy"), name));
            CoreEntry.eventMgr.TriggerEvent("tooManyToy");
			soundId = CoreEntry.soundMgr.PlaySound("voice_zhiyin_group_21", "Play_voice_zhiyin_154", gameObject); 
            return;
        }

        if (toyData.toyList.Count == 0)
            cid = null;
        else
            cid = toyData.toyList[0];

        SetCurrentCID(toyType, cid);

        switch (toyType)
        {
            case ToyType.TOY_HERO:
                ChangeHero(cid);
                break;
            case ToyType.TOY_PET:
                ChangePet(cid);
                break;
            case ToyType.TOY_WEAPON:
                
                ChangeWeapon(cid);
                break;
        }
    }
	bool CanChangePet(byte[] cid)
	{
		if (IsIgnore()) 
			return false;
		//空底座时不切换捕捉棒
		var curHeroCid = GetCurrentHeroInstanceId();
		if (curHeroCid == null)
			return false;
		else
			return true;
	}
	
	public bool CompareWeaponWithHero(byte[] heroCID, byte[] weaponCID)
	{
		//判断武器与英雄类型是否匹配接口
		if (heroCID == null || weaponCID == null) return false;
		var weaponResId = GetResIdByCID(weaponCID);
		var cWeapon = CoreEntry.gameDataMgr.resWeapon.GetRecord(weaponResId);
		var heroId = GetResIdByCID(heroCID);
		var cHero = CoreEntry.gameDataMgr.heroAttr.GetRecord(heroId);
		if (cWeapon == null || cHero == null) return false;

		return (cHero.nClass == cWeapon.nHeroClass); 
	}

    public bool CanChangeWeapon(byte[] cid)
	{
		// 不能忽略玩具
		if ( IsIgnore())
			return false;
		
		//检测英雄与武器是否匹配
#if true
		if (cid == null)
			return true;
		var weaponId = GetResIdByCID(cid);
		var cWeapon = CoreEntry.gameDataMgr.resWeapon.GetRecord(weaponId);
		var curHeroCid = GetCurrentHeroInstanceId();
		var curHeroId = GetResIdByCID(curHeroCid);

		if (curHeroId == 0)
		{
			CoreEntry.uiMgr.TipsString(CoreEntry.gameDataMgr.GetString("needToy"));
			return false;
		}
		var cHero = CoreEntry.gameDataMgr.heroAttr.GetRecord(curHeroId);
		if (cWeapon == null || cHero == null) return false;
		if (cHero.nClass == cWeapon.nHeroClass)
			return true;
		else
		{
			CoreEntry.uiMgr.TipsString(CoreEntry.gameDataMgr.GetString("HeroWeaponUnMatch"));
			return false;
		}
#endif
	}

	string GetToyName(byte[] cid)
	{
		var type = PortalMgr.GetToyType(cid);
		switch (type)
		{
 			case ToyType.TOY_HERO:
				return "英雄";
			case ToyType.TOY_PET:
				return "宠物";
			case ToyType.TOY_WEAPON:
				return "武器";
			default:
				return "玩偶";

		}
	}
    // 按钮接口，直接切换
    public void OnSwitchSelect(byte[] cid)
    {
        var toyType = GetToyType(cid);
        SetCurrentCID(toyType, cid);

        switch (toyType)
        {
            case ToyType.TOY_HERO:
                InsertCIDToHeroList(cid);
                ChangeHero(cid);
                break;
            case ToyType.TOY_PET:
                ChangePet(cid);
                break;
            case ToyType.TOY_WEAPON:
                ChangeWeapon(cid);
                break;
        }
    }

    public void SetCurrentCID(byte[] cid)
    {
        var toyType = GetToyType(cid);
        SetCurrentCID(toyType, cid);
    }

    public void OnSwitchUnSelect(ToyType toyType)
    {
        SetCurrentCID(toyType, null);

        switch (toyType)
        {
            case ToyType.TOY_HERO:
                ChangeHero(null);
                break;
            case ToyType.TOY_PET:
                ChangePet(null);
                break;
            case ToyType.TOY_WEAPON:
                ChangeWeapon(null);
                break;
        }
    }

	public void ChangeHeroForNoviceGuide(byte[] cid)
	{
		//设置PortalMgr中当前cid
		SetCurrentCID(ToyType.TOY_HERO, cid);
		//记录日志
		CoreEntry.logMgr.Log(LogLevel.INFO, "PortalMgr::ChangeHero", " ChangeHeroForNoviceGuide " + GlobalFunctions.TrimByteToString(cid));
		//获取当前英雄数据
		ChangeHero(cid);
	}

    public void ChangeHero(byte[] CID)
    {
        CoreEntry.logMgr.Log(LogLevel.INFO, "PortalMgr::ChangeHero", "HeroSelectNotifyServer CID is " + GlobalFunctions.TrimByteToString(CID));
		SetCurrentToyData(CID, ToyType.TOY_HERO);
		if (IsIgnore() == true || CanSwitchToy() == false)
		{
			CoreEntry.logMgr.Log(LogLevel.INFO, "PortalMgr::ChangeHero", "Return by Ignored");
			return;
			//技能关卡\PVP场景内更换英雄不通知服务器
			//CoreEntry.skillLevelMgr.ChangeHero(CID);
		}
#if false
        else if (CoreEntry.areaMgr.IsPvPScene() && !CoreEntry.areaMgr.IsDoublePveScene())
        {
            //PVP中切换英雄不通知服务器
            CoreEntry.pvpMgr.ChangeHero(CID);
        }
#endif
        else
        {
            //通知服务器进行英雄切换
            NetworkHelper.HeroSelectNotifyServer(CID);
        }

		if (CanSwitchToy() == false)
		{
			//目前只保留:其他系统主动调用的玩具切换开关 或者 忽略玩具的开关
			return;
		}

		var heroData = PlayerDataMgr.instance.GetHeroDataByInstanceId(CID);
		CoreEntry.eventMgr.TriggerEvent("tryPetChangeBack");
		if (CID != null && heroData == null) return;
		CoreEntry.logMgr.Log(LogLevel.INFO, "PortalMgr::ChangeHero", " PortalPlayerChange is triggered" );
		//派发事件，通知其他系统进行英雄切换
		CoreEntry.eventMgr.TriggerEvent<int, byte[]>("portalPlayerChange", heroData == null ? 0 : heroData.resId, CID); 

    }



	public void ChangePet(byte[] CID)
	{
		if (!CanChangePet(CID))
		{
#if false
			if (CoreEntry.toyMgr.IsCheckBLE == false)
				CoreEntry.uiMgr.TipsString(CoreEntry.gameDataMgr.GetString("needToy"));
#endif
			return;
		}

        if (CID == null)                    
            CoreEntry.eventMgr.TriggerEvent("portalPetChanged");

		if (CoreEntry.sceneMgr.IsPause() && !BattleResultMgr.instance.needCatchPet)
			return;
	
		NetworkHelper.PetStickSelectNotifyServer(CID);
	}
	                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           
	public void ChangeWeapon(byte[] CID )
	{
#if false
		CoreEntry.logMgr.Log(LogLevel.INFO, "PortalMgr::ChangeWeapon", "WeaponSelectNotifyServer CID is " + GlobalFunctions.TrimByteToString(CID));
		SetCurrentToyData(CID, ToyType.TOY_WEAPON);
		if (IsIgnore())
		{
			CoreEntry.logMgr.Log(LogLevel.INFO, "PortalMgr::ChangeWeapon", "Return by Ignored");
			//CoreEntry.eventMgr.TriggerEvent("dropWeapon");
			return;
		}
		if (CoreEntry.sceneMgr.IsPause())
			return;

		if (CID == null)
		{
			CoreEntry.eventMgr.TriggerEvent("dropWeapon");
		}
		NetworkHelper.WeaponSelectNotifyServer(CID);
#endif
		CoreEntry.logMgr.Log(LogLevel.INFO, "PortalMgr::ChangeWeapon", "WeaponSelectNotifyServer CID is " + GlobalFunctions.TrimByteToString(CID));

		if (!CanChangeWeapon(CID))
		{
			CoreEntry.eventMgr.TriggerEvent("dropWeapon");
			return;
		}

		if (CoreEntry.sceneMgr.IsPause())
			return;

		if (CID == null)
		{
			CoreEntry.eventMgr.TriggerEvent("dropWeapon");

		}
		SetCurrentToyData(CID, ToyType.TOY_WEAPON);
		NetworkHelper.WeaponSelectNotifyServer(CID);
	}

    public void RebuildToyInfo()
    {
        toyInfo.Clear();
        for (ToyType i = ToyType.TOY_HERO; i < ToyType.TOY_NONE; ++i)
        {
            var data = new ToyData();
            data.toyType = i;
			data.currentCID = null;
			data.lastCID = null;
			toyInfo[i] = data;
        }
    }

    public int GetCurrentHeroResId()
    {
        if (toyInfo.ContainsKey(ToyType.TOY_HERO) == false)
            return 0;
        var data = PlayerDataMgr.instance.GetHeroDataByInstanceId(toyInfo[ToyType.TOY_HERO].currentCID);
        return data != null ? (int)data.resId : 0;
    }

    public int GetLastHeroResId()
    {
        var data = PlayerDataMgr.instance.GetHeroDataByInstanceId(toyInfo[ToyType.TOY_HERO].lastCID);
        return data != null ? (int)data.resId : 0;
    }

    public int GetLastPetResId()
    {
        var data = PlayerDataMgr.instance.GetCurrentStickData();
        return 0;
        //return data != null ? (int)data.petResId : 0;
    }

    byte[] GetCurrentCID(ToyType toyType)
    {
        return toyInfo[toyType].currentCID;
    }

    public int GetResIdByCID(byte[] cid)
    {
        if (cid == null || CompareCID(emptyCID,cid)) return 0;        
        var headBytes = GetHeadBytes(cid);
        var headString = GlobalFunctions.TrimByteToString(headBytes);
        var resId = Convert.ToInt16(headString, 16);
        return resId;
    }

    void SetCurrentCID(ToyType toyType, byte[] cid)
    {
		if (toyType >= ToyType.TOY_NONE) return;
        var data = toyInfo[toyType];
        if (cid != null)
        {
            data.lastCID = cid;
        }
        data.currentCID = cid;
        toyInfo[toyType] = data;
#if false
        if (PlayerDataMgr.instance.GetHeroDataByInstanceId(cid) == null)
        {
            NetworkHelper.HeroSelectNotifyServer(cid);
        }
#endif
    }

    public byte[] GetCurrentHeroInstanceId()
    {
        if (toyInfo.ContainsKey(ToyType.TOY_HERO))
        {
            return toyInfo[ToyType.TOY_HERO].currentCID;
        }

        return emptyCID;
        
    }

    public byte[] GetCurrentPetStickInstanceId()
    {
        return toyInfo[ToyType.TOY_PET].currentCID;
    }

    public byte[] GetCurrentWeaponInstanceId()
    {
        return toyInfo[ToyType.TOY_WEAPON].currentCID;
    }

    public byte[] GetLastHeroCID()
    {
        return toyInfo[ToyType.TOY_HERO].lastCID;
    }

    public byte[] GetLastPetCID()
    {
        return toyInfo[ToyType.TOY_PET].lastCID;
    }

    public byte[] GetLastWeaponCID()
    {
        return toyInfo[ToyType.TOY_WEAPON].lastCID;
    }

    public byte[] GetUIDByCID(byte[] CID)
    {
        if (CID == null) return null;
        byte[] UID = CoreEntry.toyMgr.QueryUID(CID);
        //return UID == null ? UID_ZERO : UID;
#if true
        if (UID != null) return UID;

        var toyType = GetToyType(CID);

        CoreEntry.logMgr.Log(LogLevel.INFO, "ProtalMgr", "portal UID is null");
        if (ToyType.TOY_HERO == toyType)
        {
			var data = PlayerDataMgr.instance.GetHeroInfoByCID(CID);/*GetHeroDataByInstanceId*/

            conf.HeroIniAttr attr = null;
			if (data != null)
				UID = data.szUID;//attr = CoreEntry.gameDataMgr.heroAttr.GetRecord(data.resId);
#if false
            if (attr == null)
            {
                CoreEntry.logMgr.Log(LogLevel.INFO, "ProtalMgr", "no data in excel UID");
            }
            else
            {
                CoreEntry.logMgr.Log(LogLevel.INFO, "ProtalMgr", "no data using excel UID");
                UID = attr.szUID;
            }
#endif
        }
        else if (ToyType.TOY_PET == toyType)
        {
			//暂无虚拟捕捉棒
            var petCaptureAttr = CoreEntry.gameDataMgr.GetPetStickAttrByCID(CID);
            if (null != petCaptureAttr)
                UID = petCaptureAttr.szUID;
        }
        else if (ToyType.TOY_WEAPON == toyType)
        {
			var weaponAttr = PlayerDataMgr.instance.GetWeaponDataByCID(CID);//CoreEntry.gameDataMgr.GetWeaponAttrByCID(CID);
            if (weaponAttr != null)
                UID = weaponAttr.szUID;
        }

        return UID;
#endif
    }

    static public ToyType GetToyType(byte[] CID)
    {
        if (CID == null) return ToyType.TOY_NONE;

		var gameDataMgr = CoreEntry.IsInit() ? CoreEntry.gameDataMgr : null;
        if (null == gameDataMgr)
            return ToyType.TOY_NONE;

        var headBytes = GetHeadBytes(CID);

        if (headBytes == null)
            return ToyType.TOY_NONE;
        var headStr = BitConverter.ToString(headBytes);
        if (gameDataMgr.resCID2HeroMap.ContainsKey(headStr))
            return ToyType.TOY_HERO;

        if (gameDataMgr.resCID2PetMap.ContainsKey(headStr))
            return ToyType.TOY_PET;

        if (gameDataMgr.resCID2WeaponMap.ContainsKey(headStr))
            return ToyType.TOY_WEAPON;

        return ToyType.TOY_NONE;
    }

    static public bool CompareLimitedCID(byte[] b1, byte[] b2, int length = TYPE_BYTE_NUM)
    {
        if (null == b1 || null == b2) return false;
        if (b1.Length < length || b2.Length < length) return false;
        for (int i = 0; i < length; ++i)
            if (b1[i] != b2[i]) return false;

        return true;
    }

    static public bool CompareCID(byte[] b1, byte[] b2)
    {
        if (b1 == null || b2 == null) return false;
        for (int i = 0; i < CID_BYTE_NUM; ++i)
        {
            if (b1[i] != b2[i]) return false;
        }
        return true;
    }

    static public bool CompareUID(byte[] b1, byte[] b2)
    {
        if (b1 == null || b2 == null) return false;
        for (int i = 0; i < UID_BYTE_NUM; ++i)
        {
            if (b1[i] != b2[i]) return false;
        }
        return true;
    }

    static public void WriteCID(byte[] b1, byte[] read)
    {
        if (b1 == null || read == null) return;

        for (int i = 0; i < CID_BYTE_NUM; ++i)
        {
            b1[i] = read[i];
        }
    }

    static public void WriteUID(byte[] b1, byte[] read)
    {
        if (b1 == null || read == null) return;

        for (int i = 0; i < UID_BYTE_NUM; ++i)
        {
            b1[i] = read[i];
        }
    }

    static public bool CopyTo(byte[] b1, byte[] b2, int length = TYPE_BYTE_NUM)
    {
        if (null == b1 || null == b2) return false;
        if (b1.Length < length || b2.Length < length) return false;

        System.Array.Copy(b1, b2, length);
        return true;
    }

    static public byte[] GetHeadBytes(byte[] b1, int length = TYPE_BYTE_NUM)
    {
        if (null == b1) return null;
        if (b1.Length < length) return null;

        var outBytes = new byte[length];
        System.Array.Copy(b1, outBytes, length);
        return outBytes;
    }

    public bool IsEmptyBase()
    {
        if (toyInfo[ToyType.TOY_HERO].toyList.Count != 0)
            return false;
        else
            return true;
    }

	bool CanSwitchToy()
	{
		if (canSwitchToyList.Count == 0)
			return true;
		return false;
	}



}

using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class WeaponMgr {

	static public WeaponMgr instance = new WeaponMgr();

	//默认空CID
	byte[] emptyCID = new byte[16];

    //英雄heroResID为key，武器list，英雄对应的武器列表
    private Dictionary<int, List<int>> m_heroMatchWeaponDic = new Dictionary<int, List<int>>();

	public WeaponMgr()
	{
		//处理当前场景英雄携带武器状态
		CoreEntry.eventMgr.AddListener(AIToyMgr.VIRTUAL_HERO_SUMMON, OnClearCurrentWeapon);
		CoreEntry.eventMgr.AddListener("dropWeapon", OnClearCurrentWeapon);
		CoreEntry.eventMgr.AddListener(AIToyMgr.BLE_EVENT_NO_PANEL, OnClearCurrentWeapon);
		CoreEntry.eventMgr.AddListener("BLEPowerOff", OnClearCurrentWeapon);
		CoreEntry.eventMgr.AddListener<GameMode>(AIToyMgr.GAME_MODE_CHANGED, OnGameModeChanged);
		CoreEntry.eventMgr.AddListener<int, byte[]>("portalPlayerChange", OnPortalPlayerChange);

        //初始化武器列表
        _onHeroMatchWeapon();
	}

	~WeaponMgr()
	{
		CoreEntry.eventMgr.RemoveListener(this);
		Scheduler.RemoveSchedule(this);
	}

	/// <summary>
	/// 设置HeroInfo.szCurVirtualWeaponID
	/// 虚拟英雄如果装备武器，szCurVirtualWeaponID不为emptyCID,下次进入游戏自动装备武器
	/// 虚拟英雄如果没有装备武器，szCurVirtualWeaponID为emptyCID
	/// TODO:二期需求，实体英雄可以装备虚拟武器，所以理论上实体英雄HeroInfo.szCurVirtualWeaponID也可以不为emptyCID
	/// </summary>
	/// <param name="heroCID"></param>
	/// <param name="weaponCID"></param>
	/// <param name="putWeapon"></param>
	/// <param name="send2Server"></param>
	/// <param name="showAnim"></param>
	public void SetVirtualWeaponByCID(byte[] heroCID, byte[] weaponCID, bool putWeapon = true, bool send2Server = true, bool showAnim = true)
	{
		var heroInfo = PlayerDataMgr.instance.GetHeroInfoByCID(heroCID);
		if (heroInfo == null) return;
		bool canSet = CoreEntry.portalMgr.CompareWeaponWithHero(heroCID, weaponCID);
		if (canSet == false) return;
		if (putWeapon)
			heroInfo.szCurVirtualWeaponID = weaponCID;
		else
			heroInfo.szCurVirtualWeaponID = emptyCID;
		//TODO: Send to Server
		if (send2Server)
			NetworkHelper.VirtualWeaponSelectNotifyServer(heroCID, weaponCID, putWeapon);
		//Play Weapon Anim
		if (showAnim && putWeapon)
			ChangeWeaponMgr.instance.BeginSwitchWeapon(weaponCID);
	}

	/// <summary>
	/// 设置HeroInfo.szClientWeaponCID
	/// szClientWeaponCID用来判断当前HeroInfo是否装备武器
	/// 并在英雄离开、武器离开、模式切换时清空szClientWeaponCID的数据
	/// </summary>
	/// <param name="weaponCID"></param>
	public void SetCurrentHeroWeapon(byte[] weaponCID)
	{
		var heroInfo = PlayerDataMgr.instance.GetHeroInfoByCID(CoreEntry.portalMgr.GetLastHeroCID());
		if (heroInfo != null)
		{
			heroInfo.szClientWeaponCID = weaponCID;
		}
	}
	
	/// <summary>
	/// 判断HeroInfo.szClientWeaponCID与WeaponCID是否相同
	/// </summary>
	/// <param name="heroCID"></param>
	/// <param name="weaponCID"></param>
	/// <returns></returns>
	public bool NeedChangeWeapon(byte[] heroCID, byte[] weaponCID)
	{
		var heroInfo = PlayerDataMgr.instance.GetHeroInfoByCID(heroCID);
		if (heroInfo == null || weaponCID == null || PortalMgr.CompareCID(weaponCID, emptyCID))
			return false;
		else
			return !PortalMgr.CompareCID(heroInfo.szClientWeaponCID, weaponCID);
	}

	/// <summary>
	/// 设置HeroInfo.szClientWeaponCID为null
	/// </summary>
	void ClearCurrentHeroWeapon()
	{
		//1、英雄离开
		//2、武器离开时
		//清空对应英雄的heroInfo.szClientWeaponCID
		var heroInfo = PlayerDataMgr.instance.GetHeroInfoByCID(CoreEntry.portalMgr.GetLastHeroCID());
		if (heroInfo != null)
		{
			heroInfo.szClientWeaponCID = null;
		}
	}

	void OnClearCurrentWeapon(string gameEvent)
	{
		//武器离开时清除HeroInfo.szClientWeaponCID
		ClearCurrentHeroWeapon();
	}
	void OnGameModeChanged(string gameEvent, GameMode mode)
	{
		//游戏模式切换时清除HeroInfo.szClientWeaponCID
		ClearCurrentHeroWeapon();
	}
	void OnPortalPlayerChange(string gameEvent, int resId, byte[] cid)
	{
		//英雄离开时清除HeroInfo.szClientWeaponCID
		if (resId == 0 && cid == null)
			ClearCurrentHeroWeapon();
	}


    //返回英雄匹配的武器列表
    public List<int> GetWeaponListMatchHeroResID(int heroResID)
    {
        if (m_heroMatchWeaponDic.ContainsKey(heroResID))
        {
            return m_heroMatchWeaponDic[heroResID];
        }

        return null;
    }

    //伦武器表，对应英雄所属
    private void _onHeroMatchWeapon()
    {
        m_heroMatchWeaponDic.Clear();
        int _len = CoreEntry.gameDataMgr.resWeapon.GetCount();
        for (int i = 0; i < _len; i++)
        {
            var _cfg = CoreEntry.gameDataMgr.resWeapon.GetRecordByIndex(i);
            if (_cfg != null)
            {
                if (_cfg.wHeroID1 > 0)
                {
                    _onProcessMatchWeaponData(_cfg.wHeroID1, _cfg.nID);
                }

                if (_cfg.wHeroID2 > 0)
                {
                    _onProcessMatchWeaponData(_cfg.wHeroID2, _cfg.nID);
                }

                if (_cfg.wHeroID3 > 0)
                {
                    _onProcessMatchWeaponData(_cfg.wHeroID3, _cfg.nID);
                }

                if (_cfg.wHeroID4 > 0)
                {
                    _onProcessMatchWeaponData(_cfg.wHeroID4, _cfg.nID);
                }                
            }
        }
        
    }

    private void _onProcessMatchWeaponData(int heroResID, int weaponID)
    {
        if (m_heroMatchWeaponDic.ContainsKey(heroResID))
        {
            m_heroMatchWeaponDic[heroResID].Add(weaponID);
        }
        else
        {
            List<int> weaponList = new List<int>();
            weaponList.Add(weaponID);
            m_heroMatchWeaponDic[heroResID] = weaponList;
        }
    }
	
}

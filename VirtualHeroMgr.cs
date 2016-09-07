
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using cs;
using conf;

public enum VirtualHeroStatus
{
	OWNED = 0,
	NOT_OWNED,
	DEAD,
	NONE,
}
public class VirtualHero
{
	HeroInfo heroinfo = null;
	public cs.HeroInfo Heroinfo
	{
		get { return heroinfo; }
		set { heroinfo = value; }
	}
	VirtualHeroStatus status = VirtualHeroStatus.NOT_OWNED;
	public VirtualHeroStatus Status
	{
		get { return status; }
		set { status = value; }
	}
	int resId = 0;
	public int ResId
	{
		get { return resId; }
		set { resId = value; }
	}
}
public class VirtualHeroMgr : MonoBehaviour
{
	//static public VirtualHeroMgr instance = new VirtualHeroMgr();
	public const string UPDATE_VIRTUAL_DATA = "UpdateVirtualData";
	public const string CLEAR_VIRTUAL_DATA = "ClearVirtualData";

	//虚拟英雄选择界面排序辅助列表
	List<HeroData> listVirtualHeroData = new List<HeroData>();	//存储当前拥有的虚拟英雄HeroData
	List<HeroInfo> listVirtualHeroInfo = new List<HeroInfo>();	//存储当前拥有的虚拟英雄HeroInfo
	List<VirtualHero> listVirtualHero = new List<VirtualHero>();	//存储所有虚拟英雄的VirtualHero
	public List<VirtualHero> ListVirtualHero
	{
		get { return listVirtualHero; }
	}
	//是否处于虚拟英雄切换场景
	bool isVirtualSwitchHero = false;
	public bool IsVirtualSwitchHero
	{
		get { return isVirtualSwitchHero; }
		set { isVirtualSwitchHero = value; }
	}

	public VirtualHeroMgr()
	{
		CoreEntry.eventMgr.AddListener(VirtualHeroMgr.UPDATE_VIRTUAL_DATA, OnVirtualDataUpdate);
		CoreEntry.eventMgr.AddListener(VirtualHeroMgr.CLEAR_VIRTUAL_DATA, OnVirtualDataClear);
	}

	~VirtualHeroMgr()
	{
		CoreEntry.eventMgr.RemoveListener(this);
	}

	/// <summary>
	/// 更新虚拟底座需要显示的已拥有、尚未拥有、死亡的非实体英雄列表
	/// </summary>
	public void UpdateVirtualHero()
	{
		//实体模式下不需要更新
		if (CoreEntry.toyMgr.GameMode == GameMode.TOY) return;

		//更新虚拟英雄VirtualHero
		//更新时机：添加新英雄\有虚拟英雄死亡\退出关卡
		if (listVirtualHeroInfo == null) return;
		listVirtualHero.Clear();
		List<VirtualHero> ownedHeroList = new List<VirtualHero>();
		List<VirtualHero> notOwnedHeroList = new List<VirtualHero>();
		List<VirtualHero> deadHeroList = new List<VirtualHero>();
		VirtualHero currentHero = null;

		//区分还需购买哪些虚拟英雄
		List<int> listResIds = GlobalFunctions.GetHeroResIds((sbyte)HERO_KIND_GROUP.HERO_KIND_ADVANCED);
		for (int i = 0; i < listResIds.Count; i++)
		{
			HeroInfo info = PlayerDataMgr.instance.GetHeroInfoByResId(listResIds[i]);
			if (info == null)
			{
				//尚未拥有该类型虚拟英雄
				var data = BuildVirtualHero(listResIds[i], null, VirtualHeroStatus.NOT_OWNED);
				if (!notOwnedHeroList.Contains(data))
					notOwnedHeroList.Add(data);
			}
		}

		//区分虚拟英雄是否死亡
		foreach (HeroInfo hero in listVirtualHeroInfo)
		{
			//如果英雄数据是当前英雄数据，不加入list
			var currentCID = CoreEntry.portalMgr.GetCurrentHeroInstanceId();
			bool isCurrentHero = PortalMgr.CompareCID(currentCID, hero.szCID);

			var heroData = PlayerDataMgr.instance.GetHeroDataByInstanceId(hero.szCID);
			if (heroData != null)
			{
				if (heroData.currentHp > 0)
				{
					var data = BuildVirtualHero(hero.nResID, hero, VirtualHeroStatus.OWNED);
					if (!isCurrentHero)
						ownedHeroList.Add(data);
					else
						currentHero = data;
				}
				else
				{
					var data = BuildVirtualHero(hero.nResID, hero, VirtualHeroStatus.DEAD);
					if (!isCurrentHero)
						deadHeroList.Add(data);
					else
						currentHero = data;


				}
			}
		}
		//listVirtualHeroInfo分为三部分
		//1、已经拥有且未死亡的英雄
		//2、尚未拥有的英雄
		//3、已经拥有且死亡的英雄
		//排序按照resId,相同resId按照英雄等级排序
		if (currentHero != null)	//理论上虚拟模式下当前底座CID不会为空，因此currentHero数据不会为空。不过为了未考虑到的异常，增加判空
			listVirtualHero.Add(currentHero);
		listVirtualHero.AddRange(ownedHeroList);
		listVirtualHero.AddRange(notOwnedHeroList);
		listVirtualHero.AddRange(deadHeroList);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <returns></returns>
	public bool VirtualHeroAllDead()
	{
		if (listVirtualHeroData == null) return false;
		foreach (HeroData data in listVirtualHeroData)
		{
			if (data.currentHp > 0)
			{
				return false;
			}
		}
		return true;
	}

	public bool ToyHeroAllDead()
	{
		List<HeroData> listHeroData = PlayerDataMgr.instance.GetHeroDatas();
		foreach(HeroData data in listHeroData)
		{
			if (data.toyType == (sbyte)HERO_KIND_GROUP.HERO_KIND_ENTITY && data.currentHp > 0)
				return false;
		}
		return true;
	}

	public byte[] GetLastVirtualHeroCID()
	{
		var heroCID = PlayerDataMgr.instance.getLastHeroCid();
		var heroInfo = PlayerDataMgr.instance.GetHeroInfoByCID(heroCID);
		if (heroInfo != null && heroInfo.chToyType != (sbyte)HERO_KIND_GROUP.HERO_KIND_ENTITY)
		{
			//返回最后一个使用的非实体英雄
			return heroCID;
		}
		else
		{
			//返回listVirtualHeroInfo中的第一个
			if (listVirtualHeroInfo.Count > 0)
				return listVirtualHeroInfo[0].szCID;
			else
				return null;
		}
	}
	private VirtualHero BuildVirtualHero(int resId, HeroInfo info = null, VirtualHeroStatus status = VirtualHeroStatus.NOT_OWNED)
	{
		VirtualHero hero = new VirtualHero();
		hero.ResId = resId;
		hero.Heroinfo = info;
		hero.Status = status;
		return hero;
	}

	private void OnVirtualDataUpdate(string gameEvent)
	{
		UpdateVirtualHeroData();
		UpdateVirtualHeroInfo();
	}
	private void OnVirtualDataClear(string gameEvent)
	{
		listVirtualHero.Clear();
		listVirtualHeroInfo.Clear();
		listVirtualHeroData.Clear();
	}
	private void UpdateVirtualHeroData()
	{
#if true
		//更新非实体英雄HeroData
		listVirtualHeroData.Clear();
		var listHeros = PlayerDataMgr.instance.GetListHeroData();
		foreach (HeroData data in listHeros)
		{
			if ((data.toyType != (sbyte)cs.TOY_TYPE_GROUP.TOY_TYPE_ENTITY) && !listVirtualHeroData.Contains(data))
			{
				listVirtualHeroData.Add(data);
			}
		}
		//VirtualHeroDataSort();
#endif
	}

	private void UpdateVirtualHeroInfo()
	{
		//更新非实体英雄HeroInfo
		listVirtualHeroInfo.Clear();
		var listHeroInfo = PlayerDataMgr.instance.GetListHeroInfo();
		foreach (HeroInfo data in listHeroInfo)
		{
			if (data.chToyType != (sbyte)cs.TOY_TYPE_GROUP.TOY_TYPE_ENTITY && !listVirtualHeroInfo.Contains(data))
			{
				listVirtualHeroInfo.Add(data);
			}
		}
		VirtualHeroInfoSort();
		//UpdateVirtualHero();
	}

	private void VirtualHeroDataSort()
	{
		//对HeroData排序
		//默认以英雄ID大小从小到大排序
		//死亡后放于未拥有的英雄之后
		//若有同样的2个或者以上英雄，按照等级排序。若等级也一样，相同等级的英雄随机排序
		if (listVirtualHeroData == null) return;
		listVirtualHeroData.Sort(delegate(HeroData x, HeroData y)
		{
			var resIdX = x.resId;
			var resIdY = y.resId;
			if (resIdX == 0 && resIdY == 0) return 0;
			else if (resIdX == 0) return -1;
			else if (resIdY == 0) return 1;
			else
			{
				int res = resIdX.CompareTo(resIdY);
				//英雄的resId相同，比较英雄等级
				//英雄等级也是升序排列
				if (res == 0)
				{
					res = x.level < y.level ? -1 : 1;
				}
				return res;
			}
		});
	}

	private void VirtualHeroInfoSort()
	{
		//对HeroInfo升序排序
		//默认以英雄ID大小从小到大排序
		//死亡后放于未拥有的英雄之后
		//若有同样的2个或者以上英雄，按照等级排序。若等级也一样，相同等级的英雄随机排序
		if (listVirtualHeroInfo == null) return;
		listVirtualHeroInfo.Sort(delegate(HeroInfo x, HeroInfo y)
		{
			var resIdX = x.nResID;
			var resIdY = y.nResID;
			if (resIdX == 0 && resIdY == 0) return 0;
			else if (resIdX == 0) return -1;
			else if (resIdY == 0) return 1;
			else
			{
				int res = resIdX.CompareTo(resIdY);
				//英雄的resId相同，比较英雄等级
				//英雄等级也是升序排列
				if (res == 0)
				{
					res = x.nLevel < y.nLevel ? -1 : 1;
				}
				return res;
			}
		});

		//排序更新之后需更新虚拟英雄列表
		//UpdateVirtualHero();
	}
}
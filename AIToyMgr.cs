using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public enum ToyType
{
	TOY_HERO = 0,
	TOY_PET = 1,
	TOY_WEAPON = 2,
	TOY_NONE,
}
/// <summary>
/// 搜索到的蓝牙信号源强度
/// </summary>
public enum SignalIntensity
{
	WEAK = 0,
	NORMAL,
	STRONG,
}
/// <summary>
/// 目前游戏有两种模式。
/// TOY模式连接蓝牙底座，放入玩偶进行游戏
/// VIRTUAL模式直接召唤虚拟英雄进行游戏
/// </summary>
public enum GameMode
{
 	VIRTUAL = 0,
	TOY = 1,
}
/// <summary>
/// 包含单个蓝牙信号源所有信息的类
/// </summary>
public class BluetoothData
{
	int index;
	public int Index
	{
		get { return index; }
		set { index = value; }
	}
	string name;
	public string Name
	{
		get { return name; }
		set { name = value; }
	}
	int rssi;
	public int Rssi
	{
		get { return rssi; }
		set { rssi = value; }
	}
	string addr;
	public string Addr
	{
		get { return addr; }
		set { addr = value; }
	}
}
/// <summary>
/// 包含单个玩偶所有信息的类，用于在特定时间点恢复数据
/// </summary>
public class ToyRecoverData
{
	byte[] cid = null;
	public byte[] Cid
	{
		get { return cid; }
		set { cid = value; }
	}
	byte[] uid = null;
	public byte[] Uid
	{
		get { return uid; }
		set { uid = value; }
	}
	ToyType type;
	public ToyType Type
	{
		get { return type; }
		set { type = value; }
	}
}



public class AIToyMgr : MonoBehaviour
{
#if UNITY_EDITOR
	public bool isDebug = true;
	private bool isSysBLEOn = true;		//手机蓝牙是否处于开启状态
#else
	public bool isDebug = false;
	private bool isSysBLEOn = false;
#endif

	//手机蓝牙是否开启
	public bool IsSysBLEOn
	{
		get { return isSysBLEOn; }
	}
#if false
    // 逻辑上是否应该搜索，在用户关掉搜索会为false，用户点击“我有玩具”会为true。
    // 避免不断地搜索，导致不断地弹出NoBasePanel
	private bool isLogicalBLEOn = true;
	public bool IsLogicalBLEOn
	{
		get { return isLogicalBLEOn; }
		set { isLogicalBLEOn = value; }
	}
#endif

	//当前蓝牙是否处于搜索中
	private bool isScanning = false;
	public bool IsScanning	
	{
		get { return isScanning; }
	}

	private bool isConnecting = false;
	//当前蓝牙是否处于连接中
	public bool IsConnecting
	{
		get { return isConnecting; }
	}

	//是否正在使用免费英雄
	private bool isUsingFreeHero = false;
	public bool IsUsingFreeHero
	{
		get { return isUsingFreeHero; }
		set { isUsingFreeHero = value; }
	}

	//在本次游戏中是否登出
	private bool hasLogout = false;//登出蓝牙机制改变，该变量暂时不用
	public bool HasLogout
	{
		get { return hasLogout; }
		set { hasLogout = value; }
	}

	//是否处于蓝牙检测阶段
	private bool isCheckBLE = false;
	public bool IsCheckBLE
	{
		get { return isCheckBLE; }
	}

	//AIToyMgr中判断当前底座是否连接参数
	private bool connection = false;
	public bool Connection
	{
		get { return connection; }
	}

	//是否处于新手流程
	private bool isNoviceProcess = false;
	public bool IsNoviceProcess
	{
		get { return isNoviceProcess; }
		set { isNoviceProcess = value; }
	}
	//当前游戏模式
	private GameMode gameMode = GameMode.TOY;
	public GameMode GameMode
	{
		get { return gameMode; }
	}
	private bool needShowModeSelect = true;	//首次进入主城，显示模式选择界面
	public bool NeedShowModeSelect
	{
		get { return needShowModeSelect; }
		set { needShowModeSelect = value; }
	}
	
	//新手关卡名称
	public const string NOVICE_GUIDE_LEVEL = "40001";
	//蓝牙相关面板
	public const string BLUETOOTH_LOCAL_ADDR = "BluetoothLocalAddr";	//本地存储的蓝牙MAC
	public const string BLUETOOTH_ADDR = "BluetoothAddr";	//存储的蓝牙MAC
	public const string BLE_VERSION = "BleVersion";	//蓝牙固件版本号
	public const string BLE_CONTROL_PANEL = "BluetoothControlPanel";	//蓝牙购买页面
	public const string BLE_LIST_PANEL = "BluetoothListPanel";	//蓝牙列表页面
	public const string BLE_LIST_SUB_PANEL = "BluetoothListSubPanel";	//蓝牙列表子页面
	public const string BLE_NO_BASE_PANEL = "NoBasePanel";	//蓝牙开启引导页面
	public const string BLE_GUIDE_PANEL = "BluetoothGuidePanel";	//引导开启系统蓝牙开关页面
	public const string BLE_CONFIRM_PANEL = "BluetoothConfirmPanel";	//断开蓝牙确认界面
	public const string Toy_Novice_PANEL = "ToyNovicePanel";	//断开蓝牙确认界面
	//蓝牙相关事件
	public const string BLE_EVENT_OPEN_CONTROL_PANEL = "openBluetoothControlPanel";	//打开蓝牙购买页面
	public const string BLE_EVENT_SCAN_START = "BluetoothScanStart";	//蓝牙发起搜索
	public const string BLE_EVENT_SCAN_END = "BluetoothScanEnd";	//蓝牙结束搜索
	public const string BLE_EVENT_CHECK_BLE = "CheckBLE";	//开始/结束 蓝牙流程
	public const string BLE_EVENT_NO_PANEL = "BLENoPanel";	//蓝牙连接断开消息
	public const string BLE_EVENT_SCAN_ING = "BluetoothScanning";	//蓝牙正在搜索
	//虚拟英雄相关事件
	public const string VIRTUAL_SWITCH_BACK = "VirtualSwitchBack";	//ABORT
	public const string VIRTUAL_HERO_SUMMON = "VirtualHeroSummoned";	//召唤了虚拟英雄
	public const string GAME_MODE_CHANGED = "GameModeChanged";	//游戏模式改变
	public const string BLE_LIST_DESTROYED = " BLEPanelDestroyed";	//蓝牙列表界面销毁
	
	//Conditions for Recover
	public const string CUR_BLE_PANEL = "CurBluetoothPanel";	//需要恢复 当前蓝牙页面
	public const string SETTING_PANEL = "NoviceGuideSettingPanel";	//需要恢复 设置页面
	public const string VIRTUAL_PANEL = "VirtualHeroSwitchPanel";	//需要恢复 虚拟英雄选择页面
	public const ChangeType CHANGETYPE_VIRTUAL_SWITCH = ChangeType.VIRTUAL_HERO;	//需要恢复 虚拟英雄切换
	public const ChangeType CHANGETYPE_DEAD_SWITCH = ChangeType.HERO_DEAD;	//需要恢复 英雄死亡
	public const ChangeType CHANGETYPE_EMPTY_SWITCH = ChangeType.NO_HERO;	//需要恢复 新英雄进入
	public const ChangeType CHANGETYPE_NONE = ChangeType.NONE;	//Default

	private bool isInit = false;	//游戏是否初始化
	private bool isForcedUpdate = false;	//蓝牙固件是否强制升级
	private bool isCheckBLEFromVirtual = false;	//是否由虚拟切换流程发起CheckBluetooth()
	private bool cancelFirmwareUpdateByUser = false;	//用户主动取消升级
	private string heroMsg = null;//登出蓝牙机制改变，该变量暂时不用
	private string petMsg = null;//登出蓝牙机制改变，该变量暂时不用
	private string weaponMsg = null;//登出蓝牙机制改变，该变量暂时不用
	private string version = "00.00.00";	//
	private string cardVersion = "00.00.00";	//蓝牙底座版本号
	private string bleVersion = "00.00.00";	//蓝牙固件版本号
	private int scanActionId = -1;	//循环搜索Action
	private int cycleScanActionId = -1;	//循环断开蓝牙连接Action
	private const float SCAN_PERIOD = 8.0f;	//固定时间搜索周期

	private LitJson.JsonData panelData = null;	//登出蓝牙机制改变，该变量暂时不用
	private LitJson.JsonData connectedData = null;	//存储蓝牙连接的JsonData
	private BluetoothData currentBluetoothData;	//存储当前蓝牙底座的数据
	private System.Object currentRecover;	//当前需要在切换模式时恢复的数据
	private Dictionary<string, string> toyInfo = new Dictionary<string, string>();	//存储玩具CID UID对
	private Dictionary<ToyType, ToyRecoverData> switchModeToyInfo = new Dictionary<ToyType, ToyRecoverData>();//存储模式切换时底座上的玩具数据
	private List<LitJson.JsonData> toyDataList = new List<LitJson.JsonData>();	//处理连接蓝牙后，弹出二次确认对话框提示是否固件升级时，点击否 的情况
	private List<BluetoothData> bluetoothList = new List<BluetoothData>();	//本次搜索搜到的蓝牙底座列表
	private List<BluetoothData> tempBluetoothList = new List<BluetoothData>();	//暂时的蓝牙列表
	private List<ToyRecoverData> reloginToyList = new List<ToyRecoverData>();	//ABORT
	private List<string> panelStack = new List<string>();	//当前打开的蓝牙界面Stack

	void Awake()
	{
	}

	void Start()
	{
		CoreEntry.eventMgr.AddListener("PlayerLogout", OnLogout);
		CoreEntry.eventMgr.AddListener("OnServerReturnError",OnServerError);
		CoreEntry.eventMgr.AddListener(AIToyMgr.BLE_EVENT_OPEN_CONTROL_PANEL, OnOpenBluetoothControlPanel);
		CoreEntry.eventMgr.AddListener<bool>("networkConnection", OnConnect);
		CoreEntry.eventMgr.AddListener("beginChangePlayer", OnBeginChangePlayer, false);
		
	}

	/// <summary>
	/// 进入主城首先是模式选择界面
	/// </summary>
	public void CheckBluetoothByUiMgr()
	{
		if (needShowModeSelect &&
			CoreEntry.toyMgr.gameMode == GameMode.TOY &&
			CoreEntry.noviceGuideMgr.CanOpenBluetoothGuidePanel)
		{
			CoreEntry.uiMgr.GetPanelSimply(AIToyMgr.BLE_CONTROL_PANEL);
			needShowModeSelect = false;
		}
		else if (CoreEntry.toyMgr.gameMode == GameMode.TOY)
			CheckBluetooth();
	}

	/// <summary>
	/// 切换游戏模式接口（带二次确认页面）
	/// </summary>
	/// <param name="mode"> 要切换的游戏模式 </param>
	/// <param name="cb"> 回调函数 </param>
	/// <param name="cbFirst"> 回调函数执行的顺序 </param>
	public void SwitchModeWithConfirm(GameMode mode,System.Action cb = null,bool cbFirst = false)
	{
		if (this.gameMode == mode) return;
		CoreEntry.uiMgr.PopConfirm(GetSwitchString(gameMode, mode),
			() =>
			{
				gameMode = mode;
				CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "+ SwitchMode" + mode.ToString());
#if UNITY_EDITOR
				PlayerPrefs.SetInt("PlayModel", (int)gameMode);
#endif
				CoreEntry.eventMgr.TriggerEvent<GameMode>(AIToyMgr.GAME_MODE_CHANGED, gameMode);
				if (cbFirst)
				{
					if (cb != null)
						cb();
				}
				ProcessSwitch(mode);
				if (!cbFirst)
				{
					if (cb != null)
						cb();
				}

			},null);
	}
	/// <summary>
	/// 切换游戏模式接口（仅模式切换，不作其他处理）
	/// </summary>
	/// <param name="mode"></param>
	public void SwitchMode(GameMode mode)
	{
		if (this.gameMode == mode) return;
		gameMode = mode;
		CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "+ SwitchMode" + mode.ToString());
#if UNITY_EDITOR
		PlayerPrefs.SetInt("PlayModel", (int)gameMode);
#endif
		CoreEntry.eventMgr.TriggerEvent<GameMode>(AIToyMgr.GAME_MODE_CHANGED,gameMode);
	}

	/// <summary>
	/// TOY/VIRTUAL模式切换有些界面提供返回前一级页面的按钮 本函数用于设置需要返回的数据
	/// </summary>
	/// <param name="obj"></param>
	public void SetRecoverType(System.Object obj)
	{
		currentRecover = obj;
	}

	/// <summary>
	///当前是否有需要返回的数据 
	/// </summary>
	/// <returns></returns>
	public bool NeedSwitchModeRecover()
	{
		return currentRecover != null;
	}

	public bool IsHeroChange()
	{
		return currentRecover == null ? false : currentRecover.GetType().ToString().Equals("ChangeType");
	}
	/// <summary>
	/// 返回前一级页面时的统一接口
	/// </summary>
	public void OnPanelBack()
	{
 		//用于在玩具/模拟模式切换时，点按返回按钮的恢复行为
		if (currentRecover == null) return;
		var type = currentRecover.GetType().ToString();
		switch(type)
		{
			case "System.String":
				{
					CoreEntry.uiMgr.GetPanelSimply((string)currentRecover);
				}
				break;
			case "ChangeType":
				{
					if ((ChangeType)currentRecover != ChangeType.NONE)
						ChangeHeroMgr.instance.BeginSwitch((ChangeType)currentRecover);
				}
				break;
		}
		currentRecover = null;
		if(gameMode == GameMode.TOY)
			ProcessSwitch(GameMode.TOY);
	}

	/// <summary>
	/// GameDataMgr初始化完成后的回调函数
	/// </summary>
	/// <param name="version"></param>
	/// <param name="isForced"></param>
	public void OnInit(string version = "00.00.00", bool isForced = false)
	{
		CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "OnInit" + version);
		this.version = version;
		this.isForcedUpdate = isForced;
		isInit = true;
#if !UNITY_EDITOR
		AIToyBLE.Init();
#endif
	}

	/// <summary>
	/// 玩具CID UID对查询函数
	/// </summary>
	/// <param name="cid"></param>
	/// <returns></returns>
	public byte[] QueryUID(byte[] cid)
	{
		var k = GlobalFunctions.TrimByteToString(cid);
		string v;
		bool ret = toyInfo.TryGetValue(k, out v);
		return !ret ? null : System.Text.Encoding.UTF8.GetBytes(v);
	}

	

#if false
	public void SetReloginData()
	{

		//登录重入机制，暂不使用
		var heroCid = CoreEntry.portalMgr.GetCurrentHeroInstanceId();
		var petCid = CoreEntry.portalMgr.GetCurrentPetStickInstanceId();
		var weaponCid = CoreEntry.portalMgr.GetCurrentWeaponInstanceId();
		if (heroCid != null)
		{
			var heroUid = QueryUID(heroCid);
			ToyRecoverData data = new ToyRecoverData();
			data.Cid = heroCid;
			data.Uid = heroUid;
			data.Type = ToyType.TOY_HERO;
			reloginToyList.Add(data);

		}
		if (petCid != null)
		{
			var petUid = QueryUID(petCid);
			ToyRecoverData data = new ToyRecoverData();
			data.Cid = petCid;
			data.Uid = petUid;
			data.Type = ToyType.TOY_PET;
			reloginToyList.Add(data);

		}
		if (weaponCid != null)
		{
			var weaponUid = QueryUID(weaponCid);
			ToyRecoverData data = new ToyRecoverData();
			data.Cid = weaponCid;
			data.Uid = weaponUid;
			data.Type = ToyType.TOY_WEAPON;
			reloginToyList.Add(data);
		}
		ReloginListSortByType();

	}

	public void ReLogin()
	{
		//底座重新进入一次
		foreach (var toyData in reloginToyList)
		{
			var cid = GlobalFunctions.TrimByteToString(toyData.Cid);
			var uid = GlobalFunctions.TrimByteToString(toyData.Uid);
			string msg = BuildToyEnterMsg(cid, uid);
			AIToyMessage(msg);
		}
		if (reloginToyList.Count == 0)
			CoreEntry.portalMgr.ChangeHero(null);
		reloginToyList.Clear();
		hasLogout = false;
	}
#endif
	/// <summary>
	/// 蓝牙连接成功后弹出的模态/非模态确认页面 引导玩家强制/非强制升级蓝牙底座版本
	/// </summary>
	/// <param name="bluetoothData"></param>
	/// <param name="isForced"></param>
	private void PopBlueToothConfirm(LitJson.JsonData bluetoothData, bool isForced)
	{
		if (bluetoothData != null)
		{
			if (isForced)
			{
				//没有取消按钮
				CoreEntry.uiMgr.PopConfirm2(CoreEntry.gameDataMgr.GetString("BlueToothUpdate"),
				() =>
				{
					AIToyBLE.AIToyUpdateBloothFirmWare(true);
					CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "PopBlueToothConfirm" + "UpdateBLE True");
				});
			}
			else
			{
				//有取消按钮
				CoreEntry.uiMgr.PopConfirm(CoreEntry.gameDataMgr.GetString("BlueToothUpdate"),
				() =>
				{
					AIToyBLE.AIToyUpdateBloothFirmWare(true);
					CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "PopBlueToothConfirm" + "UpdateBLE True");
				},
				() =>
				{
					cancelFirmwareUpdateByUser = true;
					AIToyBLE.AIToyUpdateBloothFirmWare(false);
					CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "PopBlueToothConfirm" + "UpdateBLE False");
					OnBluetoothUpdateFinish("");
				});
			}
		}
		CoreEntry.eventMgr.TriggerEvent("endLoading");
		CoreEntry.eventMgr.TriggerEvent("endLoading2", "");

	}

	private void ClearReloginMsg()
	{
		heroMsg = null;
		petMsg = null;
		weaponMsg = null;
	}

	/// <summary>
	/// 蓝牙启动流程：初始化AiToyMgr->AIToyBLE.Init()主动发起搜索请求->等待搜索结果返回并显示列表
	/// 蓝牙连接流程：主动发起AIToyBLE.AIToyConnect()->收到连接消息->收到蓝牙是否升级消息->先选择是否升级蓝牙，再进入游戏
	/// </summary>
	/// <param name="msg"></param>
	public void AIToyMessage(string msg)
	{
		var stage = CoreEntry.GetGameStage();
		AiToyDebug.Log("AIToyMgr +" + msg);
		AiToyDebug.Log("AIToyMgr +" + stage.ToString());

		LitJson.JsonData jsonData = null;
		try
		{
			jsonData = LitJson.JsonMapper.ToObject(msg);
		}
		catch
		{
			AiToyDebug.Log("invalid json msg from bluetooth");
			return;
		}

		if (jsonData != null)
		{
			var CID = System.Text.Encoding.UTF8.GetBytes((string)jsonData["CID"]);
			var toyType = PortalMgr.GetToyType(CID);

#if true
			var errCode = (string)jsonData["errCode"];
			switch (errCode)
			{
                    // 二维码扫描结果返回
                case "SCAN_QRCODE":
                    {
                        var result = (string)jsonData["action"];
                        CoreEntry.logMgr.Log(LogLevel.ERROR, "SCAN_QRCODE", result);
                        CoreEntry.eventMgr.TriggerEvent("ScanQRResult", result);
                    }
                    break;

                // 销毁扫描
                case "EXIT_QRCODE":
                    {
                        var result = (string)jsonData["action"];
                        CoreEntry.logMgr.Log(LogLevel.ERROR, "EXIT_QRCODE", result);
                        CardMgr.instance.Dispose();
                    }
                    break;

				case "BLUETOOTH_SUCCESS":
					{

						var actionCode = (string)jsonData["action"];
						if (actionCode == "BLUETOOTH_ACTION_PUT")
						{
							if (CoreEntry.portalMgr.connection == false)
							{
								//connection只有在主城中点击确定后才置为true,但此之前玩偶的信息已能响应，需要存储
								//不直接进入下面的ToyEnter()而是这里return应对的情况是在BluetoothListPanel已点击蓝牙进行连接
								//蓝牙返回消息提示是否进行蓝牙固件升级，点击否时（此时蓝牙已连接），直接进入游戏的情况
								if (CanAddToyData(jsonData))
								{
									toyDataList.Add(jsonData);
									CoreEntry.logMgr.Log(LogLevel.INFO, "TOY_ENTER", (string)jsonData["CID"]);
								}
								return;
							}
#if false
							if (CoreEntry.portalMgr.connection == true && hasLogout == true && CoreEntry.GetGameStage() == GameStage.Login)
							{
								//切换帐号登出至login界面切换玩偶
								ToyRecoverData data = new ToyRecoverData();
								var toyCID = (string)jsonData["CID"];
								var toyUID = (string)jsonData["UID"];
								data.Cid = System.Text.Encoding.UTF8.GetBytes(toyCID);
								data.Uid = System.Text.Encoding.UTF8.GetBytes(toyUID);
								data.Type = PortalMgr.GetToyType(data.Cid);
								reloginToyList.Add(data);
								return;
							}
#endif
							switch (toyType)
							{
								case ToyType.TOY_PET:
								case ToyType.TOY_WEAPON:
								case ToyType.TOY_HERO:
									ToyEnter(jsonData);
									break;
								case ToyType.TOY_NONE:
									CoreEntry.logMgr.Log(LogLevel.INFO, "TOY_Enter", "TOY_NONE");
									break;
							}
						}
						else if (actionCode == "BLUETOOTH_ACTION_LEAVE")
						{
							if (CoreEntry.portalMgr.connection == false)
							{
								RemoveToyData(jsonData);
								CoreEntry.logMgr.Log(LogLevel.INFO, "TOY_LEAVE", (string)jsonData["CID"]);
								return;
							}

							switch (toyType)
							{
								case ToyType.TOY_PET:
								case ToyType.TOY_WEAPON:
								case ToyType.TOY_HERO:
									ToyExit(jsonData);
									break;
								case ToyType.TOY_NONE:
									CoreEntry.logMgr.Log(LogLevel.INFO, "TOY_LEAVE", "TOY_NONE");
									break;
							}
						}
						else if (actionCode == "BLUETOOTH_ACTION_SEARCHSTART")
						{
							//蓝牙搜索开始
							tempBluetoothList.Clear();
							isScanning = true;
							CoreEntry.eventMgr.TriggerEvent(AIToyMgr.BLE_EVENT_SCAN_START);
						}
						else if (actionCode == "BLUETOOTH_ACTION_SEARCHEND")
						{
							if (isSysBLEOn == false ||isScanning == false) return;
							//蓝牙搜索停止
							bluetoothList.Clear();
							isScanning = false;
							foreach (BluetoothData data in tempBluetoothList)
							{
								if (!bluetoothList.Contains(data))
								{
									var newData = new BluetoothData();
									bluetoothList.Add(data);
								}
							}
							ListSortByRssi(bluetoothList);
							CoreEntry.eventMgr.TriggerEvent(AIToyMgr.BLE_EVENT_SCAN_END);
							CheckBluetooth();
						}
						else if (actionCode == "BLUETOOTH_ACTION_SEARCHNAME")
						{
							//开始接收搜索到的蓝牙名
							var UID = (string)jsonData["UID"];
							var newData = new BluetoothData();
							newData = BuildBluetoothData(jsonData);
							if (UID != null && !tempBluetoothList.Contains(newData))
							{
								tempBluetoothList.Add(newData);
							}
						}
						else if (actionCode == "BLUETOOTH_ACTION_CONNECTFAILED")
						{
							var bluetoothListPanel = CoreEntry.uiMgr.GetPanel(AIToyMgr.BLE_LIST_PANEL);
							if (bluetoothListPanel != null)
							{
								var panel = CoreEntry.uiMgr.GetPanelSimply("BluetoothNoticePanel");
							}
							isConnecting = false;
							CoreEntry.eventMgr.TriggerEvent("endLoading2","");
							CoreEntry.toyMgr.AIToyStartSearch();
						}
						else if (actionCode == "BLUETOOTH_ACTION_POWERON")
						{
							if (isSysBLEOn == true) return;
							isSysBLEOn = true;
							if (isScanning == false)
								CoreEntry.toyMgr.AIToyStartSearch();
						}
						else if (actionCode == "BLUETOOTH_ACTION_POWEROFF")
						{

							if (isSysBLEOn == false) return;
							isScanning = false;
							isSysBLEOn = false;
							ClearBLEInfo(jsonData);
							CoreEntry.eventMgr.TriggerEvent("BLEPowerOff");
							CoreEntry.eventMgr.TriggerEvent("endLoading2","");
							CheckBluetooth();
							
						}
					}
					break;
				case "BLUETOOTH_NO_CARD":
					CoreEntry.eventMgr.TriggerEvent<LitJson.JsonData>("noToy", jsonData);
					break;
				case "BLUETOOTH_BASE_CONNECTED":
					{
						if (isScanning == true) return;	//防止蓝牙在扫描结束之前自动连接设备
						connection = true;
						isConnecting = false;
						connectedData = jsonData;

					}
					break;
				case "BLUETOOTH_NO_PANEL":
					{
						//处理底座主动断开
						if (connection == true)
						{	//只响应蓝牙连接状态的请求
							var tipsPanel = CoreEntry.uiMgr.TipsString(CoreEntry.gameDataMgr.GetString("BluetoothDisconnected"), 1.0f);
							GlobalFunctions.ChangeLayer(tipsPanel, LayerMask.NameToLayer("TopUI"));							
							CoreEntry.soundMgr.PlaySound("MainUIsfx", "Play_bluetooth_disconnect", tipsPanel);
							CoreEntry.eventMgr.TriggerEvent(AIToyMgr.BLE_EVENT_NO_PANEL);
							ClearBLEInfo(jsonData);
							CoreEntry.toyMgr.AIToyStartSearch();
							isScanning = true;
							CoreEntry.eventMgr.TriggerEvent("endLoading2","");
							var curBluetoothPanel = CoreEntry.uiMgr.GetPanel("CurBluetoothPanel");
							if (curBluetoothPanel != null)
								CoreEntry.uiMgr.DestroyPanel("CurBluetoothPanel");
							if (isDebug)
							{
								AiToyDebugMgr.instance.SendBluetoothInfo(4);
							}
							CoreEntry.toyMgr.CheckBluetooth();
						}
					}
					break;
				case "BLUETOOTH_UPDATE_WAIT_CONFIRM":
					{
						var actionCode = (string)jsonData["action"];
						switch (actionCode)
						{
							case "BLUETOOTH_UPDATE_WAIT_CONFIRM":
								{
									//非强制蓝牙升级	
									CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "BLUETOOTH_UPDATE_WAIT_CONFIRM");
									PopBlueToothConfirm(jsonData,false);
								}
								break;
							case "BLUETOOTH_UPDATE_WAIT_CONFIRM_FORCE":
								{
									//	强制蓝牙升级
									CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "BLUETOOTH_UPDATE_WAIT_CONFIRM_FORCE");
									PopBlueToothConfirm(jsonData, true);
								}
								break;
							case "BLUETOOTH_UPDATE_CONFIRMED":
								{
									FirmwareMgr.instance.TryUpdateFirmware(UpdateFirmwareDownloadProgress,OnFirmwareDownloadSuccess,OnFirmwareDownloadFail);
									CoreEntry.uiMgr.CreatePanel("ProgressUpdatePanel");
									CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "BLUETOOTH_UPDATE_CONFIRMED");
								}
								break;
							default:
								break;
						}
					}
					break;
				case "BLUETOOTH_UPDATE_PROGRESS":
					{
						var actionCode = (string)jsonData["action"];
						CoreEntry.eventMgr.TriggerEvent<string>("BluetoothUpdate", actionCode);
					}
					break;
				case "BLUETOOTH_UPDATE_ERROR":
					{

						var tipsPanel = CoreEntry.uiMgr.TipsString(CoreEntry.gameDataMgr.GetString("BlueToothUpdateError"), 2.0f);
						var progressPanel = CoreEntry.uiMgr.GetPanel("ProgressUpdatePanel");
						if (tipsPanel != null && progressPanel != null)
						{
							tipsPanel.GetComponent<UIPanel>().depth = progressPanel.GetComponent<UIPanel>().depth + 1;
						}
						CoreEntry.uiMgr.DestroyPanel("ProgressUpdatePanel");
					}
					break;
				case "BLUETOOTH_ACTION_CARDVERSION":
					{
						cardVersion = (string)jsonData["action"];
					}
					break;
				case "BLUETOOTH_ACTION_BLEVERSION":
					{
						bleVersion = (string)jsonData["action"];
						PlayerPrefs.SetString(AIToyMgr.BLE_VERSION, bleVersion);
						CoreEntry.eventMgr.TriggerEvent("BleVersionUpdate",bleVersion);
					}
					break;
				case "BLUETOOTH_UPDATE_NO":
						{
							var actionCode = (string)jsonData["action"];
							switch (actionCode)
							{
								case "BLUETOOTH_UPDATE_NO":
									{
										//蓝牙不升级
										OnBluetoothUpdateFinish("");
									}
									break;
							}
						}
						break;
				case "BLUETOOTH_UPDATE_CANCEL":
						{
							//蓝牙升级取消发送时机：
							//1、在confirmPanel升级取消
							//2、固件下载失败
							//3、固件下载成功，但新下载固件版本号低于目前版本号
							CoreEntry.eventMgr.TriggerEvent("BluetoothUpdateCancel");
							if(cancelFirmwareUpdateByUser == false)
							{
								OnBluetoothUpdateFinish("");
							}
							else
							{
								cancelFirmwareUpdateByUser = false;
							}
						}
						break;

			}
#endif
		}

	}

	/// <summary>
	/// 获得本轮搜索得到的蓝牙底座数据
	/// </summary>
	/// <returns></returns>
	public List<BluetoothData> GetBluetoothList()
	{
		return bluetoothList;
	}

	/// <summary>
	/// 蓝牙流程处理函数，用于判断当前需要进行的操作以及打开的界面
	/// </summary>
	public void CheckBluetooth()
	{
		if (CoreEntry.IsInit() == false)
			return;
		if (GameMode.VIRTUAL == CoreEntry.toyMgr.GameMode)
			return;
#if false
        // 用户主动关闭，不检测
		if (isLogicalBLEOn == false)
		{
			CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "CheckBluetooth" + "Return by isLogicalBLEOn");
			return;
		}
#endif
		//玩偶选择界面未弹出之前不检测
		if (needShowModeSelect == true)
		{
			CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "CheckBluetooth" + "Return by NeedShowModeSelect");
			return; 
		}
		//已连接不检测
		if (CoreEntry.portalMgr.connection == true)
		{
			CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "CheckBluetooth" + "Return by portalMgr.connection");
			return;
		}

		//PVP中即使断开底座断开蓝牙也不检测
		if (CoreEntry.areaMgr.IsPvPScene() == true)
		{
			CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "CheckBluetooth" + "Return by PVP");
			return;  
		}
		//使用试玩英雄时不检测
		if (isUsingFreeHero == true)
		{
			CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "CheckBluetooth" + "Return by isUsingFreeHero");
			return; 
		}
		//新手场景不检测
		if (CoreEntry.areaMgr.GetCurrentSceneName() == AIToyMgr.NOVICE_GUIDE_LEVEL)
			return;
		//尚未完成新手引导步骤ID==54不检测
		if (!CoreEntry.noviceGuideMgr.CanOpenBluetoothGuidePanel)
		{
			CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "CheckBluetooth" + "Return by noviceGuideLimit");
			return;
		}
		//不能切换英雄的场景不检测
		if (CoreEntry.IsStageCanSwitchHero(CoreEntry.GetGameStage()) == false)
			return;
		//当前服务器断开不检测
		if (CoreEntry.gameServer.IsConnect() == false)
		{
			CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "CheckBluetooth" + "Return by Disconnect");
			CoreEntry.gameServer.TryReconnect();
			return; 
		}
		//切换英雄时检测 清除掉切换英雄特效
		if (ChangeHeroMgr.instance.IsChanging() && ChangeHeroMgr.instance.IsShowingPlayerDead() == false)
		{
			ChangeHeroMgr.instance.Clear();
		}

		//如果系统蓝牙未开启则优先显示蓝牙设置界面
		if (isSysBLEOn == false && isDebug == false)
		{
			var panel = CoreEntry.uiMgr.GetPanelSimply(AIToyMgr.BLE_GUIDE_PANEL);
			if (panel != null && isCheckBLEFromVirtual && currentRecover != null)
			{
				var guidePanel = panel.GetComponent<BluetoothGuidePanel>();
				if (guidePanel != null)
					guidePanel.ShowBackBtn();
				isCheckBLEFromVirtual = false;
			}
			return;
		}

		CoreEntry.eventMgr.TriggerEvent("endLoading");
		CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "CheckBluetooth" + "Begin BLE Process");
#if false
		if (isNoviceProcess)
		{
			//新手流程首先打开蓝牙购买页
			CoreEntry.uiMgr.GetPanelSimply(AIToyMgr.BLE_CONTROL_PANEL);
			//在新手轮断开蓝牙重新连接，走非新手流程2016/5/17策划需求
			isNoviceProcess = false;
			return;
		}

#endif

#if false
		//如果没有绑定过实体玩偶，且当前没有处于其他蓝牙相关界面（如BluetoothListPanel），则显示BluetoothControlPanel
		if (PlayerDataMgr.instance.GetEntityHeroCount() < 1 && isCheckBLE == false)	
		{
			CoreEntry.uiMgr.GetPanelSimply(AIToyMgr.BLE_CONTROL_PANEL);
			CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "CheckBluetooth" + "Return by HeroCount<1");
			return;
		}

		var bluetoothControlPanel = CoreEntry.uiMgr.GetPanel(AIToyMgr.BLE_CONTROL_PANEL);
		if (bluetoothControlPanel != null)
		{
			CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "CheckBluetooth" + "Return by BLE_Control_Panel");
			return;
		}
		//此处有两个分支
		//1、当搜索不到蓝牙且没有在搜索时的显示
		//2、搜索到蓝牙或者正在搜索时的显示
		var noBasePanel = CoreEntry.uiMgr.GetPanel(AIToyMgr.BLE_NO_BASE_PANEL);
		if (noBasePanel != null)
			return;
		if (tempBluetoothList.Count == 0 && bluetoothList.Count == 0 && isScanning == false)
		{
			//没有搜索到蓝牙并且没有在搜索中

			//扫描结束未搜索到蓝牙
			var panel = CoreEntry.uiMgr.GetPanel(AIToyMgr.BLE_NO_BASE_PANEL);
			if (panel == null)
			{
				CoreEntry.uiMgr.CreatePanel(AIToyMgr.BLE_NO_BASE_PANEL);
				return;
			}
		}
#endif

		var bleControlPanel = CoreEntry.uiMgr.GetPanel(AIToyMgr.BLE_CONTROL_PANEL);
		if (bleControlPanel != null)
		{
			return;
		}
		var noBasePanel = CoreEntry.uiMgr.GetPanel(AIToyMgr.BLE_NO_BASE_PANEL);
		if (noBasePanel != null)
		{
			return;
		}
		var toyNovicePanel = CoreEntry.uiMgr.GetPanel(AIToyMgr.Toy_Novice_PANEL);
		if (toyNovicePanel != null)
		{
			return;
		}

		if (true/*tempBluetoothList.Count != 0 || bluetoothList.Count != 0 || isScanning == true*/)
		{
			//搜索到了蓝牙或者正在搜索中

			var panel = CoreEntry.uiMgr.GetPanel(AIToyMgr.BLE_LIST_PANEL);
			if (panel == null)
			{
				panel = CoreEntry.uiMgr.GetPanelSimply(AIToyMgr.BLE_LIST_PANEL).gameObject;
				if (panel != null && isCheckBLEFromVirtual && currentRecover != null)
				{
					var bleListPanel = panel.GetComponent<BluetoothListPanel>();
					if (bleListPanel != null)
						bleListPanel.ShowBackBtn();
					isCheckBLEFromVirtual = false;
				}
				return;
			}
			else
			{
				if (isScanning)
				{
					CoreEntry.eventMgr.TriggerEvent(AIToyMgr.BLE_EVENT_SCAN_ING);
				}
				else
				{
					CoreEntry.eventMgr.TriggerEvent(AIToyMgr.BLE_EVENT_SCAN_END); 
				}
				if (panel != null && isCheckBLEFromVirtual && currentRecover != null)
				{
					var bleListPanel = panel.GetComponent<BluetoothListPanel>();
					if (bleListPanel != null)
						bleListPanel.ShowBackBtn();
					isCheckBLEFromVirtual = false;
				}
			}
			StopCycleScan();
		}

	}
	
	/// <summary>
	/// 获得本轮搜索得到的蓝牙列表数目
	/// </summary>
	/// <returns></returns>
	public int GetCurrentBluetoothListCount()
	{
		if (bluetoothList != null)
			return bluetoothList.Count;
		else
			return 0;
	}

	/// <summary>
	/// 将蓝牙相关界面Push/Pop 并派发事件
	/// </summary>
	/// <param name="panelName"></param>
	/// <param name="isCreate"></param>
	public void AdjustPanelStack(string panelName, bool isCreate)
	{
		//用来在开始蓝牙检测以及结束检测时派发事件
		if (isCreate)
		{
			if (!panelStack.Contains(panelName))
			{
				panelStack.Add(panelName);
			}
			if (panelStack.Count == 1)
			{
				CoreEntry.eventMgr.TriggerEvent(AIToyMgr.BLE_EVENT_CHECK_BLE, true);
				isCheckBLE = true;
			}
		}
		else
		{
			if (panelStack.Contains(panelName) && CoreEntry.uiMgr.GetPanel(panelName) == null)
			{
				panelStack.Remove(panelName);
			}
			if (panelStack.Count == 0)
			{
				CoreEntry.eventMgr.TriggerEvent(AIToyMgr.BLE_EVENT_CHECK_BLE, false);
				isCheckBLE = false;
			}
		}
	}

	public bool GetBLEPanel(string panelName)
	{
		return panelStack.Contains(panelName);

	}
	/// <summary>
	/// 循环发起搜索接口
	/// </summary>
	public void InitCycleScan()
	{
#if true
		if (isScanning) return;
		CoreEntry.toyMgr.AIToyStartSearch();
		//周期性搜索蓝牙信号
		scanActionId = Scheduler.Create(this, (sche, t, s) =>
		{
			InitCycleScan();
		}, 0.0f, 0.0f, SCAN_PERIOD,ActionMgr.Priority.Normal,true).actionId;
#endif
	}

	/// <summary>
	/// 循环搜索结束接口
	/// </summary>
	public void StopCycleScan()
	{
		//停止周期性搜索蓝牙信号
		Scheduler.RemoveSchedule(scanActionId);
	}

	/// <summary>
	/// 通过MAC地址获得蓝牙名称
	/// </summary>
	/// <param name="addr"></param>
	/// <returns></returns>
	public string GetBluetoothNameByAddr(string addr)
	{
		//蓝牙改为自动循环搜索，该方法失效Abort
		//原因：首次搜索SetData了蓝牙A，在第二次搜索开始，但尚未返回蓝牙A时，向蓝牙A发起连接。此时蓝牙不会继续返回新数据，因此刷新后的bluetoothlist中没有蓝牙A
		if (bluetoothList == null || bluetoothList.Count == 0)
			return "";
		foreach (BluetoothData data in bluetoothList)
		{
			if (string.Equals(addr, data.Addr))
				return data.Name;
		}
		return "";
	}

	public string GetBluetoothNameByLocalData(string addr)
	{
		if (currentBluetoothData == null || addr == null) 
			return "";
		if (string.Equals(currentBluetoothData.Addr, addr))
		{
			return currentBluetoothData.Name;
		}
		else
		{
			return "";
		}
	}

	/// <summary>
	/// 存储当前所连接蓝牙底座的数据
	/// </summary>
	/// <param name="data"></param>
	public void SetCurrentBluetoothData(BluetoothData data)
	{
		currentBluetoothData = data;
	}

	/// <summary>
	/// 获得蓝牙信号源强度的接口
	/// </summary>
	/// <param name="rssi"></param>
	/// <returns></returns>
	public static SignalIntensity GetSignalIntenByRssi(int rssi)
	{
		if (rssi > -65)
		{
			return SignalIntensity.STRONG;
		}
		else if (rssi > -80)
		{
			return SignalIntensity.NORMAL;
		}
		else
		{
			return SignalIntensity.WEAK;
		}
	}

	/// <summary>
	/// 发起蓝牙搜索的接口
	/// </summary>
	public void AIToyStartSearch()
	{
		if (isScanning == true || connection == true || isSysBLEOn == false || isConnecting == true) return;
		CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "AIToyStartSearch" );
		AIToyBLE.AIToyStartSearch(this.version,this.isForcedUpdate);
#if UNITY_EDITOR
		AiToyDebugMgr.instance.SendBluetoothInfo(4);
#endif
	}
	
	/// <summary>
	/// 发起蓝牙连接的接口
	/// </summary>
	/// <param name="address"></param>
	public void AIToyStartConnect(string address)
	{
		CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "AIToyStartConnect" );
		isConnecting = true;
		AIToyBLE.AIToyConnect(address);
	}

	/// <summary>
	/// 断开蓝牙二次确认接口
	/// </summary>
	/// <param name="text"></param>
	/// <param name="confirm"></param>
	/// <param name="cancel"></param>
	/// <returns></returns>
	public ConfirmPanel PopBLEConfirm(string text, ConfirmPanel.ConfirmDelegate confirm,ConfirmPanel.ConfirmDelegate cancel = null)
	{
		var panel = (ConfirmPanel)CoreEntry.uiMgr.CreatePanel(AIToyMgr.BLE_CONFIRM_PANEL);
		panel.tip = text;
		panel.onConfirm = confirm;
		panel.onCancel = cancel;
		return panel;
	}

	/// <summary>
	/// 获得当前蓝牙固件版本接口
	/// </summary>
	/// <returns></returns>
	public string GetCurrentBLEFirmWareVersion()
	{
		return cardVersion;
	}

	/// <summary>
	/// 蓝牙底座连接调用函数，用于派发事件
	/// </summary>
	/// <param name="jsonData"></param>
	private void PanelEnter(LitJson.JsonData jsonData)
	{
		//派发蓝牙连接消息
		CoreEntry.eventMgr.TriggerEvent<LitJson.JsonData>("onToyPanel", jsonData);
	}

	/// <summary>
	/// 蓝牙底座断开连接调用函数，用于派发事件
	/// </summary>
	/// <param name="jsonData"></param>
	private void PanelExit(LitJson.JsonData jsonData)
	{
		//派发蓝牙断开消息
		CoreEntry.eventMgr.TriggerEvent<LitJson.JsonData>("offToyPanel", jsonData);
	}

	/// <summary>
	///	监听服务器错误码函数，并在游戏退出到登录界面时断开蓝牙 
	/// </summary>
	/// <param name="gameEvent"></param>
	private void OnServerError(string gameEvent)
	{
		CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "OnServerError " + gameEvent);
		OnDisconnectBLE();
	}

	/// <summary>
	/// 玩家主动登出处理函数,并在游戏退出到登录界面时断开蓝牙 
	/// </summary>
	/// <param name="gameEvent"></param>
	private void OnLogout(string gameEvent)
	{
		if (CoreEntry.portalMgr.connection)
		{
			//登出之后蓝牙也会登出
			OnDisconnectBLE(); 
		}
		//重置新手流程状态
		isNoviceProcess = false;
		needShowModeSelect = true;
		//之前机制，登出之后蓝牙保持连接，玩偶数据保持连接，下次重入
#if false
		hasLogout = true;
		SetReloginData();
#endif
	}
	private void OnOpenBluetoothControlPanel(string gameEvent)
	{
		
		//系统蓝牙已打开
		string path = "Video/BLEVedio/BLE_Novice.mp4";
		PlayVidieo(path, () =>
			{
				CoreEntry.uiMgr.GetPanelSimply(AIToyMgr.Toy_Novice_PANEL);
				needShowModeSelect = false;
			});
	}

	/// <summary>
	/// 播放视频
	/// </summary>
	/// <param name="go"></param>
	private void PlayVidieo(string videoPath,System.Action cb)
	{
		CoreEntry.soundMgr.MuteBGM(true);
		VideoPanel panel = CoreEntry.uiMgr.GetPanelSimply("VideoPanel") as VideoPanel;
		//CoreEntry.cameraMgr.CloseMainCamera();
		if (panel != null)
			panel.Play(videoPath, () =>
			{
				CoreEntry.cameraMgr.OpenMainCamera();
				CoreEntry.soundMgr.MuteBGM(false);
				Scheduler.Create(this, (sche, t, s) =>
				{
					CoreEntry.uiMgr.DestroyPanel("BlackPanel");
					if (cb != null)
					{
						cb();
					}
				}, 0, 0, 0.1f);
			});
	}

	/// <summary>
	/// 监听服务器连接成功函数，并在断开连接-重连成功之后检测当前蓝牙流程
	/// </summary>
	/// <param name="gameEvent"></param>
	/// <param name="isConnect"></param>
	private void OnConnect(string gameEvent, bool isConnect)
	{
		CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "OnConnect " + isConnect.ToString());
		if (isConnect)
		{
			CheckBluetooth();
			Scheduler.RemoveSchedule(cycleScanActionId);
		}
	}

	/// <summary>
	/// 蓝牙连接成功，进入下一阶段统一入口
	/// </summary>
	/// <param name="gameEvent"></param>
	private void OnBluetoothUpdateFinish(string gameEvent)
	{
		CoreEntry.logMgr.Log(LogLevel.INFO, "OnBluetoothUpdateFinish", " + Begin");
		if (connection == true && connectedData != null)
		{
			CoreEntry.eventMgr.TriggerEvent("endLoading");
			CoreEntry.eventMgr.TriggerEvent("endLoading2", "");
			if (CoreEntry.IsStageCanSwitchHero(CoreEntry.GetGameStage()) == true)
			{
				//只在特定场景接收蓝牙连接消息
				if (connectedData == null)
					return;
				var addr = (string)connectedData["UID"];
				PlayerPrefs.SetString(AIToyMgr.BLUETOOTH_ADDR, addr);
				CoreEntry.logMgr.Log(LogLevel.INFO, "CurBluetoothAddr", addr);
				var bluetoothListPanel = CoreEntry.uiMgr.GetPanel(AIToyMgr.BLE_LIST_PANEL);
				var bluetoothListSubPanel = CoreEntry.uiMgr.GetPanel(AIToyMgr.BLE_LIST_SUB_PANEL);
				var noPanel = CoreEntry.uiMgr.GetPanel(AIToyMgr.BLE_NO_BASE_PANEL);
				var tipsPanel = CoreEntry.uiMgr.TipsString(CoreEntry.gameDataMgr.GetString("BluetoothConnected"), 1.0f);
				CoreEntry.soundMgr.PlaySound("MainUIsfx", "Play_bluetooth_connect", tipsPanel);
				Scheduler.Create(this, (sch, t, s) =>
				{
					if (bluetoothListPanel != null)
					{
						CoreEntry.uiMgr.DestroyPanel(AIToyMgr.BLE_LIST_PANEL);
					}
					if (bluetoothListSubPanel != null)
					{
						CoreEntry.uiMgr.DestroyPanel(AIToyMgr.BLE_LIST_SUB_PANEL);
					}
					if (noPanel != null)
					{
						CoreEntry.uiMgr.DestroyPanel(AIToyMgr.BLE_NO_BASE_PANEL);
					}
					PanelEnter(connectedData);
					ToyRecover();
					connectedData = null;
					CoreEntry.logMgr.Log(LogLevel.INFO, "OnBluetoothUpdateFinish", " + End");
				}, 0, 0, 1.0f);
			}

		}
	}
	/// <summary>
	/// 在Login阶段主动断开系统蓝牙
	/// </summary>
	private void OnDisconnectBLE()
	{
		Scheduler.RemoveSchedule(cycleScanActionId);
		if (CoreEntry.GetGameStage() != GameStage.Login)
		{
			cycleScanActionId = Scheduler.Create(this, (sche, t, s) =>
			{
				if (t >= s)
				{
					//每0.5s扫描一次，直到退出到Login场景再断开连接（显示效果优化）
					OnDisconnectBLE();
				}
			}, 0.0f, 0.0f, 0.5f, ActionMgr.Priority.Normal, true).actionId;
		}
		else
		{
			if (isDebug == false)
			{
				AIToyBLE.AITOYDisconnect();
			}
			else
			{
				var msg = AiToyDebugMgr.BuildPanelBaseExitMsg();
				CoreEntry.toyMgr.AIToyMessage(msg);
			}
		}
	}

	/// <summary>
	/// 玩偶离开调用函数，用于派发事件
	/// </summary>
	/// <param name="jsonData"></param>
	private void ToyExit(LitJson.JsonData jsonData)
	{
		CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "ToyExit" + (string)jsonData["CID"]);
		toyInfo[(string)jsonData["CID"]] = (string)jsonData["UID"];
		if (CoreEntry.toyMgr.gameMode == GameMode.TOY)
		{
			CoreEntry.eventMgr.TriggerEvent<LitJson.JsonData>("toyExit", jsonData);
		}
		else
		{
			var CID = System.Text.Encoding.UTF8.GetBytes((string)jsonData["CID"]);
			var UID = CoreEntry.portalMgr.GetUIDByCID(CID);
			SetSwitchModeData(CID, UID, false);
		}
	}

	/// <summary>
	/// 玩偶进入调用函数，用于派发事件
	/// </summary>
	/// <param name="jsonData"></param>
	private void ToyEnter(LitJson.JsonData jsonData)
	{
		CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "ToyEnter" + (string)jsonData["CID"]);
		toyInfo[(string)jsonData["CID"]] = (string)jsonData["UID"];
		if (CoreEntry.toyMgr.gameMode == GameMode.TOY)
		{
			CoreEntry.eventMgr.TriggerEvent<LitJson.JsonData>("toyEnter", jsonData);
		}
		else
		{
			var CID = System.Text.Encoding.UTF8.GetBytes((string)jsonData["CID"]);
			var UID = CoreEntry.portalMgr.GetUIDByCID(CID);
			SetSwitchModeData(CID,UID,true);
		}
	}

	/// <summary>
	/// 将重入玩偶的数据排序，ABORT
	/// </summary>
	private void ReloginListSortByType()
	{
		//按照ToyType排序，确定开始游戏时进入顺序：英雄->宠物->武器->NONE
		reloginToyList.Sort(delegate(ToyRecoverData x, ToyRecoverData y)
		{
			var toyTypeX = x.Type;
			var toyTypeY = y.Type;
			if (toyTypeX == 0 && toyTypeY == 0) return 0;
			else if (toyTypeX == 0) return -1;
			else if (toyTypeY == 0) return 1;
			else return toyTypeX.CompareTo(toyTypeY);
		});
	}
	/// <summary>
	/// 按照玩偶类型将toyDataList排序
	/// </summary>
	private void ToyDataListSortByType()
	{
		//按照ToyType排序，确定开始游戏时进入顺序：英雄->宠物->武器->NONE
		toyDataList.Sort(delegate(LitJson.JsonData x, LitJson.JsonData y)
		{
			var toyTypeX = PortalMgr.GetToyType(System.Text.Encoding.UTF8.GetBytes((string)x["CID"]));
			var toyTypeY = PortalMgr.GetToyType(System.Text.Encoding.UTF8.GetBytes((string)y["CID"]));
			if (toyTypeX == 0 && toyTypeY == 0) return 0;
			else if (toyTypeX == 0) return -1;
			else if (toyTypeY == 0) return 1;
			else return toyTypeX.CompareTo(toyTypeY);
		});
	}

	/// <summary>
	/// 按照信号强度将蓝牙列表排序
	/// </summary>
	/// <param name="list"></param>
	private void ListSortByRssi(List<BluetoothData> list)
	{
		//根据信号强度排序 强->弱
		if (list == null || list.Count == 0)
			return;
		list.Sort(delegate(BluetoothData x, BluetoothData y)
		{
			if (x.Rssi == 0 && y.Rssi == 0) return 0;
			else if (x.Rssi == 0) return 1;
			else if (y.Rssi == 0) return -1;
			else return x.Rssi.CompareTo(y.Rssi);
		});
		list.Reverse();

	}

	/// <summary>
	/// 构造BluetoothData
	/// </summary>
	/// <param name="jsonData"></param>
	/// <returns></returns>
	private BluetoothData BuildBluetoothData(LitJson.JsonData jsonData)
	{
		if (jsonData == null)
			return null;
		BluetoothData bluetoothData = new BluetoothData();
		bluetoothData.Name = (string)jsonData["UID"];
		var macRSSI = ((string)jsonData["CID"]).Split('+');
		bluetoothData.Addr = macRSSI[0];
		bluetoothData.Rssi = int.Parse(macRSSI[1]);
		return bluetoothData;

	}

	/// <summary>
	/// 蓝牙固件下载成功回调函数
	/// </summary>
	private void OnFirmwareDownloadSuccess()
	{
		//下载成功自动开始升级过程
		CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "OnFirmwareDownloadSuccess" + "Firmware download success");
		AIToyBLE.AITOYDownloadFirmwareStatus(true);
	}

	/// <summary>
	/// 蓝牙固件下载失败回调函数
	/// </summary>
	private void OnFirmwareDownloadFail()
	{
		//下载失败不会发送No_Panel消息,可直接进入游戏
		CoreEntry.logMgr.Log(LogLevel.INFO, "AIToyMgr", "OnFirmwareDownloadFail" + "Firmware download fail");
		AIToyBLE.AITOYDownloadFirmwareStatus(false);
		CoreEntry.eventMgr.TriggerEvent("BluetoothUpdateCancel");
		//OnBluetoothUpdateFinish("");
	}
	/// <summary>
	/// 更新蓝牙固件升级进度
	/// </summary>
	/// <param name="percent"></param>
	private void UpdateFirmwareDownloadProgress(float percent)
	{
		//下载进度
		CoreEntry.eventMgr.TriggerEvent("BluetoothFirmwareDownload",percent);
	}
	/// <summary>
	/// 用于在游戏进入/离开前台时记录
	/// </summary>
	/// <param name="state"></param>
	void OnApplicationPause(bool state)
	{
		if (ShopMgr.instance.isPaying)
		{
			AiToyDebug.Log("AIToyMgr.OnApplicationPause " + "Return By Paying");
			return; 
		}
		if (isInit == true)
		{
			AIToyBLE.AITOYIsInBackground(!state);
			AiToyDebug.Log("AIToyMgr.OnApplicationPause " + state);
		}
	}

	/// <summary>
	/// ABORT
	/// </summary>
	/// <param name="toyData"></param>
	private void RemoveReloginToyData(LitJson.JsonData toyData)
	{
		var CIDToRemove = System.Text.Encoding.UTF8.GetBytes((string)toyData["CID"]);
		ToyRecoverData tempToyData = null;
		foreach (ToyRecoverData toy in reloginToyList)
		{
			var CID = toy.Cid;
			if (PortalMgr.CompareCID(CID, CIDToRemove))
			{
				tempToyData = toy;
			}
		}
		if (tempToyData != null)
		{
			reloginToyList.Remove(tempToyData);
		}
	}

	/// <summary>
	/// 处理连接蓝牙后，弹出二次确认对话框提示是否固件升级时，点击否 的情况
	/// 即此时蓝牙已实际连接，但是玩偶的消息还不能进入下一层
	/// </summary>
	/// <param name="toyData"></param>
	private void RemoveToyData(LitJson.JsonData toyData)
	{
		var CIDToRemove = System.Text.Encoding.UTF8.GetBytes((string)toyData["CID"]);
		LitJson.JsonData tempToyData = null;
		foreach (LitJson.JsonData toy in toyDataList)
		{
			var CID = System.Text.Encoding.UTF8.GetBytes((string)toy["CID"]);
			if (PortalMgr.CompareCID(CID, CIDToRemove))
			{
				tempToyData = toy;
			}
		}
		if (tempToyData != null)
		{
			toyDataList.Remove(tempToyData);
		}
	}

	/// <summary>
	/// 处理连接蓝牙后，弹出二次确认对话框提示是否固件升级时，点击否 的情况
	/// 即此时蓝牙已实际连接，但是玩偶的消息还不能进入下一层
	/// </summary>
	/// <param name="toyData"></param>
	/// <returns></returns>
	private bool CanAddToyData(LitJson.JsonData toyData)
	{
		var CIDToCompare = System.Text.Encoding.UTF8.GetBytes((string)toyData["CID"]);
		foreach (LitJson.JsonData toy in toyDataList)
		{
			var CID = System.Text.Encoding.UTF8.GetBytes((string)toy["CID"]);
			if (PortalMgr.CompareCID(CID, CIDToCompare))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// 清空数据
	/// </summary>
	/// <param name="jsonData"></param>
	private void ClearBLEInfo(LitJson.JsonData jsonData)
	{
		connection = false;
		isConnecting = false;
		toyDataList.Clear();
		tempBluetoothList.Clear();
		bluetoothList.Clear();
		reloginToyList.Clear();
		switchModeToyInfo.Clear();
		hasLogout = false;
		CoreEntry.toyBindMgr.Clear();
		PanelExit(jsonData);

	}

	/// <summary>
	/// 切换模式时提示字符串函数
	/// </summary>
	/// <param name="oriMode"></param>
	/// <param name="newMode"></param>
	/// <returns></returns>
	private string GetSwitchString(GameMode oriMode,GameMode newMode)
	{
		string baseString = CoreEntry.gameDataMgr.GetString("SwitchNotice");
		string oriString = CoreEntry.gameDataMgr.GetString(oriMode.ToString());
		string newString = CoreEntry.gameDataMgr.GetString(newMode.ToString());
		return string.Format(baseString,oriString,newString);
	}

	/// <summary>
	/// 切换模式时需要执行的公共操作
	/// </summary>
	/// <param name="newMode"></param>
	public void ProcessSwitch(GameMode newMode)
	{
		//模式切换公共接口，只保留各个模式切换接口 并集功能
		if (newMode == GameMode.VIRTUAL)
		{
			//虚拟模式
			if(connection)
				InitSwitchModeData();
			ChangeHeroMgr.instance.Clear();
			ChangeHeroMgr.instance.BeginSwitch(ChangeType.VIRTUAL_HERO);
		}
		else if (newMode == GameMode.TOY)
		{
			//玩具模式
			//CoreEntry.sceneMgr.Pause(PauseType.Resume, StopInputType.ResumeInput);
			if (connection)
			{
				//如果此时蓝牙仍然保持连接
				CoreEntry.sceneMgr.Pause(PauseType.Resume,StopInputType.ResumeInput);
				SwitchModeRecover();
			}
			else
			{
				//发起一次搜索，并显示蓝牙列表界面
				isCheckBLEFromVirtual = true;
				CoreEntry.toyMgr.AIToyStartSearch();
				isScanning = true;	//TODO:逻辑上改变蓝牙的物理状态是危险的，正确行为应该是等待蓝牙返回真实的SearchStart结果
				CheckBluetooth();
			}
		}
	}
	private static string BuildToyEnterMsg(string CID, string UID)
	{
		return "{\"errCode\":\"BLUETOOTH_SUCCESS\",\"action\":\"BLUETOOTH_ACTION_PUT\",\"UID\":\"" + UID + "\",\"CID\":\"" + CID + "\"}";
	}

	/// <summary>
	/// 处理连接蓝牙后，弹出二次确认对话框提示是否固件升级时，点击否 的情况
	/// 即此时蓝牙已实际连接，但是玩偶的消息还不能进入下一层
	/// </summary>
	private void ToyRecover()
	{
		//只用于进入游戏底座有玩具的行为

		if (toyDataList.Count != 0)
		{
			ToyDataListSortByType();
			foreach (LitJson.JsonData toyData in toyDataList)
			{
				var CID = (string)toyData["CID"];
				var UID = (string)toyData["UID"];
				var msg = BuildToyEnterMsg(CID, UID);
				AIToyMessage(msg);
			}
			toyDataList.Clear();
		}
	}

	/// <summary>
	/// 从VIRTUAL->TOY模式时用于存储底座上玩偶的数据
	/// 以便从TOY->VIRTUAL模式时恢复数据
	/// </summary>
	private void InitSwitchModeData()
	{
		//存储当前底座上的玩具数据
		switchModeToyInfo.Clear();
		for (ToyType i = ToyType.TOY_HERO; i < ToyType.TOY_NONE; ++i)
		{
			var data = new ToyRecoverData();
			data.Type = i;
			switchModeToyInfo[i] = data;
		}
		var heroCID = CoreEntry.portalMgr.GetCurrentHeroInstanceId();
		var heroInfo = PlayerDataMgr.instance.GetHeroInfoByCID(heroCID);
		if (null != heroInfo && heroInfo.chToyType == (sbyte)cs.TOY_TYPE_GROUP.TOY_TYPE_ENTITY)
		{
			switchModeToyInfo[ToyType.TOY_HERO].Cid = heroInfo.szCID;
			switchModeToyInfo[ToyType.TOY_HERO].Uid = CoreEntry.portalMgr.GetUIDByCID(switchModeToyInfo[ToyType.TOY_HERO].Cid);
		}
		if (null != CoreEntry.portalMgr.GetCurrentPetStickInstanceId())
		{
			switchModeToyInfo[ToyType.TOY_PET].Cid = CoreEntry.portalMgr.GetCurrentPetStickInstanceId();
			switchModeToyInfo[ToyType.TOY_PET].Uid = CoreEntry.portalMgr.GetUIDByCID(switchModeToyInfo[ToyType.TOY_PET].Cid);
		}
		if (null != CoreEntry.portalMgr.GetCurrentWeaponInstanceId())
		{
			switchModeToyInfo[ToyType.TOY_WEAPON].Cid = CoreEntry.portalMgr.GetCurrentWeaponInstanceId();
			switchModeToyInfo[ToyType.TOY_WEAPON].Uid = CoreEntry.portalMgr.GetUIDByCID(switchModeToyInfo[ToyType.TOY_WEAPON].Cid);
		}
		//CoreEntry.eventMgr.TriggerEvent("tryCallPetOff");
		CoreEntry.portalMgr.RebuildToyInfo();

	}

	/// <summary>
	/// 蓝牙连接使用VIRTUAL模式时更新底座玩偶的数据
	/// </summary>
	/// <param name="cid"></param>
	/// <param name="uid"></param>
	/// <param name="isEnter"></param>
	private void SetSwitchModeData(byte[] cid, byte[] uid, bool isEnter)
	{
		if (switchModeToyInfo == null || cid == null) return;
		var type = PortalMgr.GetToyType(cid);
		if ((int)type >= switchModeToyInfo.Count) return;
		var curCID = switchModeToyInfo[type].Cid;
		if (isEnter == true && PortalMgr.CompareCID(curCID, cid) == false)
		{
			switchModeToyInfo[type].Cid = cid;
			switchModeToyInfo[type].Uid = CoreEntry.portalMgr.GetUIDByCID(cid);
		}
		if (isEnter == false && PortalMgr.CompareCID(curCID, cid) == true)
		{
			switchModeToyInfo[type].Cid = null;
			switchModeToyInfo[type].Uid = null;
		}

	}
	/// <summary>
	/// 从VIRTUAL->TOY模式时用于存储底座上玩偶的数据
	/// 以便从TOY->VIRTUAL模式时恢复数据 
	/// </summary>
	private void SwitchModeRecover()
	{
		CoreEntry.portalMgr.RebuildToyInfo();

		foreach (KeyValuePair<ToyType, ToyRecoverData> data in switchModeToyInfo)
		{
			if (data.Value.Type == ToyType.TOY_HERO && data.Value.Cid == null)
			{
				ChangeHeroMgr.instance.BeginSwitch(ChangeType.NO_HERO);
			}
			if (data.Value.Cid != null)
			{
				string cid = GlobalFunctions.TrimByteToString(data.Value.Cid);
				string uid = GlobalFunctions.TrimByteToString(data.Value.Uid);
				string msg = BuildToyEnterMsg(cid, uid);
				CoreEntry.toyMgr.AIToyMessage(msg);

			}
		}
	}

	/// <summary>
	/// 开始切换英雄时清空 模式切换需要返回的数据
	/// </summary>
	/// <param name="gameEvent"></param>
	void OnBeginChangePlayer(string gameEvent)
	{
		currentRecover = null;
	}

}

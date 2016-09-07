#define SKIP_TIPS_CHECKING
#define SHOW_FPS
//#define LOGIN_PERFORMACE_TEST

using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public delegate void OnStageAnimationEnd();

public class UiMgr : MonoBehaviour
{
    private Camera m_uiCamera;
    private Camera m_uiTopCamera;
    Transform uiCameraTransform;
    public GameObject m_uiRoot;
    public int uiHight = 0;

    // 是否处于gm指令输入状态
    public bool gmInputModel = false;
    private UILabel m_msgLabel;
    Dictionary<string, List<GameObject>> panelTable = new Dictionary<string, List<GameObject>>();
    List<string> removeList = new List<string>();
    int stageAnimation = -1;
    int frontModelPanelNum = 0;
    UIPanel blockPanel = null;
    UIPanel oriBlockPanel = null;


    public Transform hudPanel;

#if SHOW_FPS || UNITY_EDITOR
    private Transform mFps;
#endif
    // UI堆栈，用于对全世界压栈
    static List<UiMgr> uiMgrStack = new List<UiMgr>();
    public bool isPlayingStageAnimate = false;
    int[] depthTypeValue = new int[PanelBase.DepthValue.Length];

    // hud
    public bool showHud = true;
    Transform fingerTransform;

    //存储最近使用BlcokPanel的面板
    private string lastPanelWithBlock = null;
    private List<PanelBase> panelBaseWithBlock = new List<PanelBase>();

    //存储当前所有UI的active状态
    private Dictionary<int, bool> panelActive = new Dictionary<int, bool>();
    private Dictionary<int, GameObject> panelActiveObj = new Dictionary<int, GameObject>();

    public Camera uiCamera
    {
        get
        {
            if (m_uiRoot == null)
            {
                FetchRootAndCamera();
            }

            return m_uiCamera;
        }
    }

    public Camera topUICamera
    {
        get
        {
            if (m_uiRoot == null)
            {
                FetchRootAndCamera();
            }

            return m_uiTopCamera;
        }
    }

    public GameObject uiRoot
    {
        get
        {
            if (m_uiRoot == null)
            {
                FetchRootAndCamera();
            }

            return m_uiRoot;
        }
    }

    UILabel msgLabel
    {
        get
        {
            if (m_msgLabel == null)
            {
                var obj = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Ui/MsgLabel");
                m_msgLabel = obj.GetComponent<UILabel>();

                m_msgLabel.transform.parent = uiCamera.transform;
                m_msgLabel.transform.localScale = Vector3.one;
            }
            return m_msgLabel;
        }
    }

    void FetchRootAndCamera()
    {
        m_uiRoot = GameObject.Find("UI Root");
        if (m_uiRoot == null)
        {
            m_uiRoot = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Ui/UI Root");
            m_uiRoot.name = "UI Root";
            var ur = m_uiRoot.GetComponent<UIRoot>();
            uiHight = ur.activeHeight;

#if SHOW_FPS || UNITY_EDITOR
            GameObject fpsHolder = CoreEntry.resourceMgr.InstantiateObject("FPSHolder");
            mFps = fpsHolder.transform;
            mFps.parent = m_uiRoot.transform;
            mFps.localScale = Vector3.one;
#endif
        }

        if (uiHight == 0)
        {
            var ur = m_uiRoot.GetComponent<UIRoot>();
            uiHight = ur.activeHeight;
        }

        m_uiCamera = m_uiRoot.transform.GetChild(0).GetComponent<Camera>();
        m_uiTopCamera = m_uiRoot.transform.GetChild(1).GetComponent<Camera>();
        uiCameraTransform = m_uiCamera.transform;

        hudPanel = m_uiRoot.transform.GetChild(2);
		UIPanel uiPanel = hudPanel.gameObject.AddComponent<UIPanel>();
		uiPanel.depth = 1;

        DontDestroyOnLoad(m_uiRoot);
    }

    void OnEnable()
    {
        CoreEntry.eventMgr.AddListener<GameStage>(EventDefine.EVENT_GAME_STAGE_EXIT, OnClear, false);
        CoreEntry.eventMgr.AddListener("beginChangePlayer", OnBeginChangePlayer, false);
        CoreEntry.eventMgr.AddListener("endChangePlayer", OnEndChangePlayer, false);
        CoreEntry.eventMgr.AddListener<GameStage>("onGameStageInitEnter", OnGameStageInitEnter, false);
        CoreEntry.eventMgr.AddListener<GameStage>(EventDefine.EVENT_GAME_STAGE_ENTER, OnGameStageEnter, false);
        CoreEntry.eventMgr.AddListener<string>(EventDefine.EVENT_CREATE_PANEL, OnPanelCreate, false);
        CoreEntry.eventMgr.AddListener<string>(EventDefine.EVENT_DESTROY_PANEL, OnPanelDestroy, false);
        CoreEntry.eventMgr.AddListener<string, float>("makeSceneTips", OnTipsMake, false);
        CoreEntry.eventMgr.AddListener("removeSceneTips", OnTipsRemove, false);
        CoreEntry.eventMgr.AddListener("startLoading", OnStartLoading, false);
        CoreEntry.eventMgr.AddListener<string>("startLoading2", OnStartLoading2);
        CoreEntry.eventMgr.AddListener("endLoading", OnEndLoading, false);
        CoreEntry.eventMgr.AddListener<string>("endLoading2", OnEndLoading2);
        CoreEntry.eventMgr.AddListener("fadeIn", OnFadeIn);
        CoreEntry.eventMgr.AddListener("fadeOut", OnFadeOut);
        CoreEntry.eventMgr.AddListener("fadeInOut", OnFadeInOut);
        CoreEntry.eventMgr.AddListener("showScreenWarning", OnShowScreenWarning);
        CoreEntry.eventMgr.AddListener("unshowScreenWarning", OnUnShowScreenWarning);        

        //用来升级面板弹出
        CoreEntry.eventMgr.AddListener("loadSceneEnd", OnLoadSceneEndProcess);

        //召唤师等级变更
        CoreEntry.eventMgr.AddListener<int>(EventDefine.EVENT_ATTRUPDATE_LEVEL_UP, OnAttrLevelUP);
    }

    void OnDisable()
    {
        CoreEntry.eventMgr.RemoveListener(this);
    }

    void OnDestroy()
    {
        foreach (var panel in panelTable)
        {
            for (int i = 0; i < panel.Value.Count; ++i)
            {
                if (panel.Value[i] != null)
                {
                    GameObject.Destroy(panel.Value[i]);
                }
            }
        }
        Scheduler.RemoveSchedule(this);
    }

    void OnStartLoading(string gameEvent)
    {
        if (GetPanel("LoadingPanel") == null)
            CreatePanel("LoadingPanel");
    }

    void OnStartLoading2(string gameEvent, string content)
    {
        if (GetPanel("LoadingPanel") == null)
        {
            var panelBase = GetPanelSimply("LoadingPanel3");
            var panel = panelBase.gameObject.GetComponent<LoadingPanel3>();
            if (panel != null)
            {
                panel.SetData(content);
            }
        }
    }
    void OnEndLoading(string gameEvent)
    {
        AiToyDebug.Log("OnEndLoading");
        DestroyPanel("LoadingPanel");
    }

    void OnEndLoading2(string gameEvent, string content)
    {
        AiToyDebug.Log("OnEndLoading");
        DestroyPanel("LoadingPanel3");
    }

    void OnGameStageInitEnter(string gameEvent, GameStage stage)
    {
        switch (stage)
        {
            case GameStage.SummonerHouse:
                CoreEntry.eventMgr.TriggerEvent("fadeIn");
                break;
            case GameStage.Logo:
                {
                    CreatePanel("LoadingLogoPanel");
                }
                break;
            case GameStage.Login:
                {
                    if (GetPanel("LoginLayer") == null)
                    {
                        //if (CoreEntry.m_bMoviePlayed)
                        {
                            CreatePanel("LoginLayer");
                        }
                        //else
                        {
                            //FetchRootAndCamera();
                        }
                        //先解压，在跑versionMgr
#if UNITY_ANDROID && !UNITY_EDITOR
                        UnZipVFsMgr.instance.Unzip(()=>
                        {
                            VersionMgr.instance.CheckUpdate(() => { CoreEntry.CoreInit(true); });
                        }
                    );
#else
                        VersionMgr.instance.CheckUpdate(() => { CoreEntry.CoreInit(true); });
# endif
                    }

                }
                break;
            case GameStage.Town:
                {

                }
                break;
        }
    }

    void OnGameStageEnter(string gameEvent, GameStage stage)
    {
#if LOGIN_PERFORMACE_TEST
        StartCoroutine(InitCoroutine(stage));
#else
        Init(stage);
#endif
    }

    private IEnumerator InitCoroutine(GameStage stage)
    {
        yield return null;
        Init(stage);
    }

    public StageAnimation PlayStageAnimation(string animName, OnStageAnimationEnd func)
    {

        do
        {
            var obj = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Ui/" + animName) as GameObject;
            if (obj == null) break;
            CoreEntry.sceneMgr.Pause(PauseType.PauseAI, StopInputType.StopInputSource);

            // 挂到uiCamera
            var uiCamera = CoreEntry.uiMgr.uiCamera;
            if (uiCamera != null)
            {
                obj.transform.parent = uiCamera.transform;
                obj.transform.localScale = Vector3.one;
            }

            var stageAnim = obj.GetComponent<StageAnimation>();
            stageAnim.BeginPlay();

            EventMgr.EventFunction skipFunc = null;

            skipFunc = (s) =>
            {
                CoreEntry.sceneMgr.Pause(PauseType.ResumeAI, StopInputType.ResumeInputSource);
                ClearStageAnimation();
                EndStageAnimation(obj, func);
                CoreEntry.eventMgr.RemoveListener("skipStageAnimation", skipFunc);
                CoreEntry.sceneMgr.Pause(PauseType.Resume, StopInputType.ResumeInputSource);
                isPlayingStageAnimate = false;
                CoreEntry.eventMgr.TriggerEvent("fadeIn");
            };

            CoreEntry.sceneMgr.Pause(PauseType.PauseAI, StopInputType.StopInputSource);
            CoreEntry.eventMgr.AddListener("skipStageAnimation", skipFunc);
            isPlayingStageAnimate = true;

            if (func != null)
            {
                stageAnimation = Scheduler.Create(this, (sche, t, s) =>
                {
                    skipFunc(null);
                }, 0, 0, stageAnim.Length()).actionId;

                // unload start animation
                Scheduler.Create(this, (sche, t, s) =>
                {
                    Resources.UnloadUnusedAssets();
                }, 0, 0, stageAnim.Length() + 2.0f);
            }
            return stageAnim;
        } while (false);

        func();
        // enter level normaly
        return null;
    }

    void EndStageAnimation(GameObject obj, OnStageAnimationEnd func)
    {
        if (obj == null) return;
        var stageAnim = obj.GetComponent<StageAnimation>();
        if (stageAnim != null) stageAnim.EndPlay();
        if (func != null) func();
        GameObject.Destroy(obj);
    }

    void ClearStageAnimation()
    {
        stageAnimation = Scheduler.RemoveSchedule(stageAnimation);
    }

    void OnPanelCreate(string gameEvent, string panelName)
    {
        CreatePanel(panelName);
    }

    void OnPanelDestroy(string gameEvent, string panelName)
    {
        DestroyPanel(panelName);
    }

    public PanelBase GetPanelSimply(string panelName)
    {
        var panelBase = GetPanelBase(panelName);
        if (null != panelBase)
            return panelBase;

        return CreatePanel(panelName);
    }

    string GetBlocklNameByBgType(PanelBase.BackGroundType bgType)
    {
        if (bgType == PanelBase.BackGroundType.None)
            return null;
        //return PanelBase.BackGroundType.BlackBlock.ToString();
        return bgType.ToString();
    }

    public string GetLastPanelNameWithBlock()
    {
        //获取最后一个使用BlockPanel的面板名字
        return lastPanelWithBlock;
    }

    public void DestroyLastPanelWithBlock()
    {
        CoreEntry.eventMgr.TriggerEvent("destroyPanelByBlock", lastPanelWithBlock);
        DestroyPanel(lastPanelWithBlock);
    }
    // 如果原来没有blockpanel或者类型不一致，重新创建一个
    void RecreateBlockPanel(PanelBase panel, string newBlock)
    {
        if (newBlock == null)
        {
            CoreEntry.logMgr.Log(LogLevel.ERROR, LogTag.UIMgr, "newBlock can not be null");
            return;
        }

        if (blockPanel != null && newBlock != blockPanel.name)
        {
            DestroyPanel(blockPanel.name);
            blockPanel = null;
        }

        if (blockPanel == null || newBlock != blockPanel.name)
        {
            blockPanel = CreatePanel(newBlock).GetComponent<UIPanel>();
            blockPanel.name = newBlock;
        }

        if ("BlurBlock" == newBlock)
        {
            UITexture texture = GlobalFunctions.GetComponent<UITexture>(blockPanel.transform, "BgTexture");//blockPanel.GetComponent<UITexture>();
            texture.mainTexture = null;

            blockPanel.GetComponent<PanelBase>().clickResponse = panel.clickResponse;

            StartCoroutine(GetCapture2(texture, blockPanel));
        }

        if ("BlackBlock" == newBlock)
            AdjustBlockAlpha(panel, blockPanel);

        AdjustResponse(panel);
        AdjustToyAction(panel);
    }

    void AdjustResponse(PanelBase panel)
    {
        if (blockPanel != null)
        {
            var pb = blockPanel.GetComponent<PanelBase>();
            if (pb != null)
                pb.clickResponse = panel.clickResponse;
        }
    }

    void AdjustToyAction(PanelBase panel)
    {
        if (blockPanel != null)
        {
            var pb = blockPanel.GetComponent<PanelBase>();
            if (pb != null)
                pb.toyAction = panel.toyAction;
        }
    }

    void AdjustBlockAlpha(PanelBase basePanel, UIPanel blockPanel)
    {        
        if (null != basePanel && null != blockPanel)
        {
            var sprite = blockPanel.GetComponentInChildren<UISprite>();
            if (null != sprite)
            {
                Color32 color = sprite.color;
                color.a = basePanel.blockAlpha;
                sprite.color = color;
            }
        }
    }

    public PanelBase CreatePanel(string panelName)
    {
        CoreEntry.logMgr.Log(LogLevel.INFO, LogTag.UIMgr,string.Format("CreatePanel:{0}" ,panelName));
        //校验此PanelName 所在的系统是否开放。
        bool canCreatePanel = SystemSwitchMgr.instance.CanCreatePanel(panelName);
        if (canCreatePanel == false)
        {
            TipsString(CoreEntry.gameDataMgr.GetString("unopen"));
            return null;
        }

        // core mechanism:
        // 1. create obj from prefab with same name
        // 2. call PanelBase.init
        // 3. done, add to panellist

        if (!panelTable.ContainsKey(panelName))
        {
            panelTable.Add(panelName, new List<GameObject>());
        }
        var panels = panelTable[panelName];

        var panelPath = "Prefabs/Ui/" + panelName;
#if false
		GameObject obj = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Ui/" + panelName);
#else
        Object prefab = Resources.Load(panelPath);
        if (prefab == null)
        {
            CoreEntry.logMgr.Log(LogLevel.ERROR, LogTag.UIMgr, string.Format("can't load panel , pathName:{0},pathPath:{1}",panelName,panelPath));
            return null;
        }

        GameObject obj = GameObject.Instantiate(prefab) as GameObject;

#endif
        if (obj == null)
        {
            CoreEntry.logMgr.Log(LogLevel.ERROR, LogTag.UIMgr, string.Format("can't instantiate prefab, panelName: {0},panelPath: {1}",panelName,panelPath));
            return null;
        }
        
        var panelBase = obj.GetComponent<PanelBase>();

        if (panelBase == null)
        {
            CoreEntry.logMgr.Log(LogLevel.ERROR, LogTag.UIMgr, string.Format("PanelBase script miss,panelName:{0},panelPath{1}", panelName,panelPath));
            return null;
        }
        panels.Add(obj);

        panelBase.panelName = panelName;
        panelBase._Init(obj);

        
        // 自动维护depth，保证最新创建的panel在其他panel的上方
        // 因此prefab上的depth只需要保证相对顺序正确即可
        var uipanels = obj.GetComponentsInChildren<UIPanel>(true);
        if (panelBase.depthType != PanelBase.DepthType.None)
        {
            AdjustPanels(uipanels, panelBase.depthType);
        }


        // 创建一个比自己深度小一点的blockpanel,blockpanel不能block，否则死循环
        if (IsBlock(panelBase))
        {
            var uiPanel = obj.GetComponent<UIPanel>();
            if (uiPanel != null)
            {
                if (blockPanel == null)

                {
                    // begin of block
                    //CoreEntry.sceneMgr.Pause(PauseType.PauseAI, StopInputType.StopInput);
                }

                string blockName = GetBlocklNameByBgType(panelBase.bgType);
                if (blockName != null)
                {
                    lastPanelWithBlock = panelName;
                    panelBaseWithBlock.Add(panelBase);
                    if (blockPanel != null && oriBlockPanel == null)
                    {
                        oriBlockPanel = GameObject.Instantiate(blockPanel) as UIPanel;
                        oriBlockPanel.transform.parent = blockPanel.transform.parent;
                        UITexture texture = GlobalFunctions.GetComponent<UITexture>(blockPanel.transform, "BgTexture");
                        UITexture oriTexture = GlobalFunctions.GetComponent<UITexture>(oriBlockPanel.transform, "BgTexture");
                        oriTexture = texture;

                    }
                    RecreateBlockPanel(panelBase, blockName);
                }

                blockPanel.depth = uiPanel.depth - 1;
                //if (oriBlockPanel != null)
                //Destroy(oriBlockPanel.gameObject);
            }
        }

        // 在拥有front面板之后，新创建的都添加到topcamera上
        Vector3 localPosition = obj.transform.localPosition;
        if ((frontModelPanelNum > 0))
        {
            {
                GlobalFunctions.ChangeLayer(obj, LayerMask.NameToLayer("TopUI"));
                obj.transform.parent = topUICamera.transform;
                obj.transform.localScale = Vector3.one;
            }
        }
        else
        {
            obj.transform.parent = uiCamera.transform;
            obj.transform.localScale = Vector3.one;
        }
        // 新创建界面可能也是block的，再设置了界面的layer之后调整对应的blockPanel的layer
        if (IsBlock(panelBase) && blockPanel != null)
            GlobalFunctions.ChangeLayer(blockPanel.gameObject, obj.layer);
        obj.transform.localPosition = localPosition;

        // 界面被初始化之后才派发此消息
        CoreEntry.eventMgr.TriggerEvent<string>("panelCreate", panelName);

        // 进行过渡效果
        if (panelBase != null)
        {
            switch (panelBase.transitionStyle)
            {
                case PanelBase.TransitionStyle.FadeInSlowly:
                case PanelBase.TransitionStyle.FadeSlowly:
                case PanelBase.TransitionStyle.FadeQuickly:
                case PanelBase.TransitionStyle.Fade:
                    {
                        var uiPanel = obj.GetComponent<UIPanel>();
                        uiPanel.alpha = 0.0f;
                        Scheduler.Create(this, (sche, t, s) =>
                        {
                            if (uiPanel != null)
                                uiPanel.alpha = t / s;
                        }, 0.0f, GetFadeTime(panelBase.transitionStyle));
                    }
                    break;
                case PanelBase.TransitionStyle.Popup:
                    {
                        var animObj = Resources.Load("Prefabs/Ui/PopUpAnim");
                        var anim = GameObject.Instantiate(animObj) as GameObject;
                        anim.transform.parent = obj.transform.parent;
                        anim.transform.localScale = Vector3.one;
                        anim.layer = obj.layer;
                        obj.transform.parent = anim.transform;
                        obj.transform.localScale = Vector3.one;
#if true
                        var subPanels = obj.GetComponentsInChildren<UIPanel>();
                        for (int i = 0; i < subPanels.Length; ++i)
                        {
                            if (subPanels[i].gameObject == obj) continue;
                            if (subPanels[i].clipping == UIDrawCall.Clipping.SoftClip)
                            {
                                subPanels[i].gameObject.AddComponent<PanelClipPatch>();
                            }
                        }
#endif
                    }
                    break;
                case PanelBase.TransitionStyle.ZommIn:
                case PanelBase.TransitionStyle.EquipmentAni:
                case PanelBase.TransitionStyle.TreasureAni:
                case PanelBase.TransitionStyle.WeaponDetailAni:
                    {
                        Object animObj = null;
                        if (panelBase.transitionStyle == PanelBase.TransitionStyle.ZommIn)
                        {
                            animObj = Resources.Load("Prefabs/Ui/heroSkillPanelAnim");
                        }
                        else if (panelBase.transitionStyle == PanelBase.TransitionStyle.EquipmentAni)
                        {
                            animObj = Resources.Load("Prefabs/Ui/equipmentAnim");
                        }
                        else if (panelBase.transitionStyle == PanelBase.TransitionStyle.TreasureAni)
                        {
                            animObj = Resources.Load("Prefabs/Ui/treasureWindowAnim");
                        }
                        else if (panelBase.transitionStyle == PanelBase.TransitionStyle.WeaponDetailAni)
                        {
                            animObj = Resources.Load("Prefabs/Ui/weaponDetailAnim");
                        }

                        var anim = GameObject.Instantiate(animObj) as GameObject;
                        anim.transform.parent = obj.transform.parent;
                        anim.transform.localScale = Vector3.one;
                        anim.layer = obj.layer;
                        obj.transform.parent = anim.transform;
                        obj.transform.localScale = Vector3.one;
                        var subPanels = obj.GetComponentsInChildren<UIPanel>();
                        for (int i = 0; i < subPanels.Length; ++i)
                        {
                            if (subPanels[i].gameObject == obj) continue;
                            if (subPanels[i].clipping == UIDrawCall.Clipping.SoftClip)
                            {
                                subPanels[i].gameObject.AddComponent<PanelClipPatch>();
                            }
                        }

                    }
                    break;
            }
            return panelBase;
        }
        return null;

    }

    private IEnumerator GetCapture2(UITexture image, UIPanel blockPanel)
    {
        int width = Screen.width - 1;
        int height = Screen.height - 1;
        //Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24,false);

        //tex.ReadPixels(new Rect(0,0,width,height),0,0,true);
        //tex.Apply ();

        //				byte[] byt = tex.EncodeToPNG();  
        //				//保存截图  
        //				File.WriteAllBytes(Application.dataPath + "/CutImage.png", byt); 

        Texture tex2 = CoreEntry.moblieBlur.GetBlurMainCameraTexture();

        GetNguiWidthHeight(out width, out height);
        image.width = width;
        image.height = height;

        image.mainTexture = tex2;
        //Destroy(tex);
        if (blockPanel != null)
            blockPanel.gameObject.SetActive(true);
        yield return null;
    }

    private IEnumerator GetCapture(UITexture image, GameObject obj)
    {
        bool active = obj.activeSelf;
        obj.SetActive(false);
        yield return new WaitForEndOfFrame();

        int width = Screen.width - 1;
        int height = Screen.height - 1;
        //Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24,false);

        //tex.ReadPixels(new Rect(0,0,width,height),0,0,true);
        //tex.Apply ();

        //		byte[] byt = tex.EncodeToPNG();  
        //		//保存截图  
        //		File.WriteAllBytes(Application.dataPath + "/CutImage.png", byt); 

        Texture tex2 = CoreEntry.moblieBlur.GetBlurMainCameraTexture();

        GetNguiWidthHeight(out width, out height);
        image.width = width;
        image.height = height;

        image.mainTexture = tex2;

        obj.SetActive(active);
    }

    public void GetNguiWidthHeight(out int width, out int height)
    {
        UIRoot root = GameObject.FindObjectOfType<UIRoot>();
        width = Screen.currentResolution.width;
        height = Screen.currentResolution.height;

        if (root != null)
        {
            float s = (float)root.activeHeight / Screen.height;
            height = Mathf.CeilToInt(Screen.height * s);
            width = Mathf.CeilToInt(Screen.width * s);
        }
    }
    public void DestroyPanel(string panelName, GameObject panel = null)
    {
        AiToyDebug.Log("DestroyPanel:" + panelName);

        // 是否删除指定panel，否则自己根据名字查找
        panel = panel == null ? GetPanel(panelName) : panel;


        if (panel != null)
        {
            CoreEntry.eventMgr.TriggerEvent<string>("panelDestroy", panelName);
            var uiPanel = panel.GetComponent<UIPanel>();
            var panelBase = panel.GetComponent<PanelBase>();
            if (panelBase != null)
            {
                System.Action closePanel = () =>
                {
                    // 如果需要block层的ui要删除，那么往后找新的panel，找不到则删除block层
                    if (IsBlock(panelBase))
                    {
                        if (blockPanel != null && uiPanel.depth - 1 == blockPanel.depth)
                        {
                            UIPanel maxPanel = null;
                            PanelBase maxPanelbase = null;
                            foreach (var ps in panelTable)
                            {

                                for (int i = 0; i < ps.Value.Count; ++i)
                                {
                                    CoreEntry.logMgr.Log(LogLevel.INFO, "PVPMgr", "==========" + ps.Value[i]);
                                    if (ps.Value[i] == panel || ps.Value[i] == null) continue;

                                    var psui = ps.Value[i].GetComponent<UIPanel>();
                                    var psbase = ps.Value[i].GetComponent<PanelBase>();
                                    if (psui != null && psbase != null && psbase.isMarkDestroy == false && IsBlock(psbase))
                                    {
                                        if (maxPanel == null)
                                        {
                                            maxPanel = psui;
                                            maxPanelbase = psbase;
                                        }
                                        else
                                        {
                                            if (maxPanel.depth < psui.depth)
                                            {
                                                maxPanel = psui;
                                                maxPanelbase = psbase;
                                            }
                                        }
                                    }
                                }
                            }

                            var blockPanelName = GetBlocklNameByBgType(panelBase.bgType);

                            if (maxPanel != null)
                            {
                                blockPanelName = GetBlocklNameByBgType(maxPanelbase.bgType);
                                if (panelBaseWithBlock.Count != 0)
                                {
                                    panelBaseWithBlock.RemoveAt(panelBaseWithBlock.Count - 1);
                                    lastPanelWithBlock = panelBaseWithBlock[panelBaseWithBlock.Count - 1].panelName;
                                }

                                RecreateBlockPanel(panelBaseWithBlock[panelBaseWithBlock.Count - 1], blockPanelName);   //Destroy时应传入上一个PanelBase
                                blockPanel.depth = maxPanel.depth - 1;

                                GlobalFunctions.ChangeLayer(blockPanel.gameObject, maxPanel.gameObject.layer);
                                CoreEntry.eventMgr.TriggerEvent("BlockPanelUpdate");
                            }
                            else
                            {
                                blockPanel = null;
                                //CoreEntry.sceneMgr.Pause(PauseType.ResumeAI, StopInputType.ResumeInput);
                                DestroyPanel(blockPanelName);
                                // end of block                                                 
                            }
                        }
                    }

                    panelBase.Release();
                    if (oriBlockPanel != null && oriBlockPanel.gameObject != null)
                        Destroy(oriBlockPanel.gameObject);
                    switch (panelBase.transitionStyle)
                    {
                        case PanelBase.TransitionStyle.FadeSlowly:
                        case PanelBase.TransitionStyle.Fade:
                        case PanelBase.TransitionStyle.FadeAtExitSlowly:
                            {
                                uiPanel.alpha = 1.0f;
                                Scheduler.Create(this, (sche, t, s) =>
                                {
                                    if (uiPanel == null || panel == null)
                                    {
                                        sche.Stop(false);
                                        return;
                                    }
                                    uiPanel.alpha = 1.0f - Mathf.Clamp01(t / s);
                                    if (t >= s)
                                    {
                                        DoDestroyPanel(panel, panelName);
                                    }
								}, 0.0f, GetFadeTime(panelBase.transitionStyle), 0.0f, ActionMgr.Priority.Normal, true);
                            }
                            break;
                        case PanelBase.TransitionStyle.Popup:
                            {
                                var anim = panelBase.transform.parent;
                                var popBackName = "PopUpAnimationRev";
                                float costTime = anim.animation.GetClip(popBackName).length;
                                anim.animation.Play(popBackName);

                                Scheduler.Create(this, (sche, t, s) =>
                                {
                                    DoDestroyPanel(panel, panelName);
                                    GameObject.Destroy(anim.gameObject);
                                }, 0, 0, costTime);
                            }
                            break;
                        case PanelBase.TransitionStyle.ZommIn:
                        case PanelBase.TransitionStyle.EquipmentAni:
                        case PanelBase.TransitionStyle.TreasureAni:
                        case PanelBase.TransitionStyle.WeaponDetailAni:
                            {
                                var anim = panelBase.transform.parent;
                                DoDestroyPanel(panel, panelName);
                                GameObject.Destroy(anim.gameObject);
                            }
                            break;
                        default:
                            DoDestroyPanel(panel, panelName);
                            break;
                    }
                };
                if (panelBase.exitDelayTime > 0.0f)
                {
                    panelBase.Delay();
                    Scheduler.Create(this, (sche, t, s) =>
                    {
                        closePanel();
                    }, 0, 0, panelBase.exitDelayTime,ActionMgr.Priority.Normal,true);
                }
                else
                {
                    closePanel();
                }
            }
            else
            {
                DoDestroyPanel(panel, panelName);
            }

            if (!removeList.Contains(panelName))
                removeList.Add(panelName);
        }
    }

    public float GetFadeTime(PanelBase.TransitionStyle style)
    {
        if (style == PanelBase.TransitionStyle.Fade)
            return 0.25f;
        else if (style == PanelBase.TransitionStyle.FadeQuickly)
            return 0.1f;
        return 1.0f;
    }

    void DoDestroyPanel(GameObject panel, string panelName)
    {
        if (panel == null || panelName == null) return;
        if (panelTable.ContainsKey(panelName) == false) return;

        var panels = panelTable[panelName];
        GameObject.Destroy(panel);
        panels.Remove(panel);
        removeList.Remove(panelName);

        if (0 == panels.Count)
            panelTable.Remove(panelName);

        // 可能删除中间面板，对过高值重新进行排序

        PlayerInfoMgr.instance.PopPanel(panelName);
    }


    public GameObject GetPanel(string panelName)
    {
        if (panelTable.ContainsKey(panelName))
        {
            var panels = panelTable[panelName];
            if (panels.Count == 0) return null;
            var panel = panels[panels.Count - 1];
            return panel;
        }
        return null;
    }

    public PanelBase GetPanelBase(string panelName)
    {
        var panel = GetPanel(panelName);
        if (panel != null)
            return panel.GetComponent<PanelBase>();
        return null;
    }

    public void ClearPanels()
    {
        // 不需要删除，因为切场景本来就会删
#if true
        List<string> toRemovePanelKeys = new List<string>();
        foreach (var panelList in panelTable)
        {
            List<GameObject> toDestroyPanels = new List<GameObject>();
            foreach (var panel in panelList.Value)
            {
                if (panel != null && panel.gameObject != null)
                {
                    PanelBase panelBase = panel.GetComponent<PanelBase>();
                    if (panelBase == null || panelBase.sceneExitAction == PanelBase.SceneExitAction.Destroy)
                        toDestroyPanels.Add(panel);
                }
            }
            foreach (var panel in toDestroyPanels)
            {
                panelList.Value.Remove(panel);
                GameObject.Destroy(panel.gameObject);
            }
            if (panelList.Value.Count == 0)
                toRemovePanelKeys.Add(panelList.Key);
        }
        foreach (var panelKey in toRemovePanelKeys)
            panelTable.Remove(panelKey);

        for (int i = 0; i < uiCamera.transform.childCount; ++i)
        {
            var child = uiCamera.transform.GetChild(i);
            PanelBase panelBase = child.GetComponent<PanelBase>();
            if (panelBase == null || panelBase.sceneExitAction == PanelBase.SceneExitAction.Destroy)
                GameObject.Destroy(child.gameObject);
        }
#endif
        removeList.Clear();

        // 归零
        for (int i = 0; i < depthTypeValue.Length; ++i)
        {
            depthTypeValue[i] = 0;
        }

    }

    void OnTipsMake(string gameEvent, string tipsStr, float time)
    {
        // 0. check if have show this tips
        // 1. check if tips exist
        // 2. show up
#if !SKIP_TIPS_CHECKING
		if (PlayerPrefs.GetInt("Tips." + tipsId) == 1) return;
#endif
        var str = tipsStr;//CoreEntry.gameDataMgr.GetString(tipsStr);
        if (str == null) return;

        DestroyPanel("TipsPanel");
        CreatePanel("TipsPanel");
        CoreEntry.eventMgr.TriggerEvent<string, float>("showSceneTips", str, time);

#if false
		CoreEntry.eventMgr.TriggerEvent<string>(EventDefine.EVENT_CREATE_PANEL, "TipsPanel");
		CoreEntry.eventMgr.TriggerEvent<string, float>("showSceneTips", str, time);
#endif
#if !SKIP_TIPS_CHECKING
        PlayerPrefs.SetInt("Tips." + tipsId, 1);
#endif
    }

    void OnTipsRemove(string gameEvent)
    {
        CoreEntry.eventMgr.TriggerEvent<string>(EventDefine.EVENT_DESTROY_PANEL, "TipsPanel");
        CoreEntry.eventMgr.TriggerEvent<string>(EventDefine.EVENT_DESTROY_PANEL, "ElementTipsPanel");
    }

    static bool IsBlock(PanelBase p)
    {
        return p != null &&
            (p.bgType != PanelBase.BackGroundType.None);
    }

    private IEnumerator TownCreatePanelCoroutine()
    {
        CreatePanel("TownPanel");
        yield return null;
        CreatePanel("PlayerInfoLayer");
        yield return null;
        CreatePanel("SwitchHeroPanel");
    }

    void Init(GameStage stage)
    {
		bool hudVisible = !ChangeHeroMgr.instance.IsChanging();
        // create the new one
        switch (stage)
        {
            case GameStage.Battle:
                {

                    OnStageAnimationEnd func = () =>
                    {                        
                        CoreEntry.eventMgr.TriggerEvent("fadeIn");
                        var panel = CreatePanel("MainPanel") as MainPanel;

						CreatePanel("BattleArrowPanel");
						// 增加战斗中三星过关条件显示						
                        var res = SelectLevelMgr.instance.GetSelectPassData();
						if( res != null && (res.chType == (sbyte)cs.PASS_TYPE.PASS_COMM_TYPE || res.chType == (sbyte)cs.PASS_TYPE.PASS_ELITE_TYPE)
				   			&& CoreEntry.skillLevelMgr.GetLevelTag(res.iID) == 0 
				  			 && res.iChapterID > 0 && res.chPassSeq > 0)
						{
							CreatePanel("BattleStarPanel");
						}

                        // 判断活动关卡类型
                        if (res != null)
                        {
                            if (res.nClass == conf.confMacros.RES_PASS_HERO_BOSS)
                            {
                                var bossRes = CoreEntry.gameDataMgr.resHeroActivityBossPass.GetRecord(SelectLevelMgr.instance.selectLevelId);
                                if (bossRes != null)
                                {
                                    panel.bossHeroAnchor.gameObject.SetActive(true);
                                    panel.OnCountDown(null, bossRes.iDurionTime, () => {
                                        CoreEntry.eventMgr.TriggerEvent(EventDefine.EVENT_WIN_BATTLE);
                                    });
                                }
                            }
                        }

						
                        if (SelectLevelMgr.instance.selectLevelId != SelectLevelMgr.NOVICE_GUIDE_LEVEL)
                        {
                            GetPanelSimply("SwitchHeroPanel");
                            //CreatePanel("SwitchPetPanel");
                        }
                        Scheduler.Create(this, (sche, t, s) =>
                        {
                            CoreEntry.eventMgr.TriggerEvent(EventDefine.EVENT_START_BATTLE);
                            CoreEntry.isBattleStart = true;
                        });
                    };

                    var cLevelData = CoreEntry.gameDataMgr.resPass.GetRecord(SelectLevelMgr.instance.selectLevelId);

                    if (cLevelData == null || cLevelData.szAniName.Length == 0)
                    {
                        func();
                    }
                    else
                    {
                        string animName = System.Text.Encoding.UTF8.GetString(cLevelData.szAniName);
                        PlayStageAnimation(animName, func);
                    }
					LevelReportMgr.instance.Config(LevelReportMgr.INTERVAL_NORMAL_LEVEL);
                }
                break;
            case GameStage.PlayerCreate:
                {
                    CreatePanel("PlayerCreatePanel");
                }
                break;
            case GameStage.SummonerHouse:
                {
					if (!CoreEntry.noviceGuideMgr.NeedToEnterSummonerHome)
					{
		                CreatePanel("TownPanel");
		                CreatePanel("PlayerInfoLayer");
					}
					else
					{
						hudVisible = false;
					}
                    //CreatePanel("BackToTownPanel");
                }
                break;
            case GameStage.Town:
                {
#if LOGIN_PERFORMACE_TEST
                    StartCoroutine(TownCreatePanelCoroutine());
#else

#endif
                    //CreatePanel("SwitchPetPanel");
#if true
					CreatePanel("TownPanel");
					CreatePanel("SwitchHeroPanel");
					CreatePanel("PlayerInfoLayer");

                    if (CoreEntry.areaMgr.IsFromLogin() == false &&
                        CoreEntry.areaMgr.IsFromHome() == false &&
                        CoreEntry.isFullGame &&
                        SelectLevelMgr.instance.selectLevelId != SelectLevelMgr.NOVICE_GUIDE_LEVEL)
                    {
                        if (CoreEntry.pvpMgr.IsQuitByPVP)
                        {
                            //PVP游戏结束，回到PVP房间
                            if (CoreEntry.pvpMgr.IsPVPHost && CoreEntry.pvpMgr.PVPHostPlayerID!=0)
                            {
                                var pvpPanel = CoreEntry.uiMgr.GetPanel("PVPPanel");
                                if (null == pvpPanel)
                                {
                                    CoreEntry.pvpMgr.ShowPVPAgain = true;
                                    PlayerInfoMgr.instance.PushPanel("PVPPanel");
                                }
                            }
                            else if(CoreEntry.pvpMgr.PVPGuestPlayerID!=0)
                            {
                                PlayerInfoMgr.instance.PushPanel("PVPReadyPanel");
                            }
                            CoreEntry.eventMgr.TriggerEvent<bool>("PlayerInfoLayerEnableClick", true);
                        }
                        else if (CoreEntry.pveMgr.IsQuitByPVE)
                        {
                            CoreEntry.pveMgr.ClearRoom();
                            //双人PVE游戏结束，回到PVE房间
                            if (CoreEntry.pveMgr.IsPVEHost && CoreEntry.pveMgr.PVEHostPlayerID != 0)
                            {
                                var pvePanel = CoreEntry.uiMgr.GetPanel("PVEPanel");
                                if (null == pvePanel)
                                {
                                    CoreEntry.pveMgr.ShowPVEAgain = true;
                                    PlayerInfoMgr.instance.PushPanel("PVEPanel");
                                }
                            }
                            else if (CoreEntry.pveMgr.PVEGuestPlayerID != 0)
                            {
                                PlayerInfoMgr.instance.PushPanel("PVEReadyPanel");
                            }
                        }
                        else if (PveDataMgr.instance.isQuitByPve)
                        {
                            PveDataMgr.instance.QueryPveStatusRequest();
                            PlayerInfoMgr.instance.PushPanel(PvePanelMgr.PveMainPanel);
                            if (CoreEntry.portalMgr.GetCurrentHeroResId() == 0)
                            {
                                var panel = CoreEntry.uiMgr.GetPanel(PvePanelMgr.PveMainPanel);
                                panel.SetActive(false);
                            }
                        }
                        else if (VentureDataMgr.instance.isQuitByVenture)
                        {
                            var instanceType = VentureDataMgr.instance.instanceType;
							if (VentureInstanceType.Capture != instanceType && null != CoreEntry.portalMgr.GetCurrentHeroInstanceId())
                            {
                                PlayerInfoMgr.instance.PushPanel("VentureChapterPanel");
                                CoreEntry.eventMgr.TriggerEvent(VentureMgr.EventVentureShowEntry, instanceType);
                            }                            
                        }
                        else if (TreasureMgr.Instance.IsDropRecordValid())
                        {
//                             TreasureMgr.Instance.ShowDropRecord();
//                             return;
                            /* 被潜能系统替换
                            var treasureMgr = TreasureMgr.Instance;
                            var dropRecord = treasureMgr.GetDropRecord();
                            PlayerInfoMgr.instance.PushPanel("HeroMainPanel");
                            var heroMainPanel = CoreEntry.uiMgr.GetPanelBase("HeroMainPanel") as HeroMainPanel;

                            if (DropitemDetail.DropOperationType.DROP_OPERATION_EQUIPMENT == dropRecord.operationType)
                            {                                
                                heroMainPanel.ShowEquipmentPanel();
                            }
                            else if (DropitemDetail.DropOperationType.DROP_OPERATION_TREASURE_COMPOSITE == dropRecord.operationType)
                            {
                                heroMainPanel.ShowTreasurePanel();
                            }
                            else if (DropitemDetail.DropOperationType.DROP_OPERATION_TREASURE_FRAGMENT == dropRecord.operationType)
                            {
                                heroMainPanel.ShowTreasurePanel();
                            }*/
                        }
                        else if (CoreEntry.skillLevelMgr.EnterType==EnterSkillLevelType.FromSkillLevel)
                        {
                            PlayerInfoMgr.instance.PushPanel("SkillLevelChoosePanel");
                            var panel1 = CoreEntry.uiMgr.GetPanel("SkillLevelChoosePanel");
                            panel1.GetComponent<SkillLevelChoosePanel>().NeedRecordPositon = true;
                            PlayerInfoMgr.instance.PushPanel("SkillLevelDetailPanel");
                            var panel2 = CoreEntry.uiMgr.GetPanel("SkillLevelDetailPanel");
							panel2.GetComponent<SkillLevelDetailPanel>().SetData();
                            if (CoreEntry.portalMgr.GetCurrentHeroInstanceId() == null)
                            {
                                panel1.gameObject.SetActive(false);
                                panel2.gameObject.SetActive(false);
                                var panel = CoreEntry.uiMgr.GetPanel("BlurBlock");
                                if (panel != null) panel.gameObject.SetActive(false);
                            }
						}
                        else if (CoreEntry.skillLevelMgr.EnterType == EnterSkillLevelType.FromRune)
                        {
                            //HeroMgr.instance.currentShowHeroInstanceId = m_data.mapBytes["CID"];
                            var runePanel = CoreEntry.uiMgr.GetPanel("HeroRunePanel");
                            if (null == runePanel)
                            {
                                CoreEntry.heroRuneMgr.ShowRunePanel();
                                HeroMgr.instance.shouldBeResvered = true;
                                PlayerInfoMgr.instance.PushBackStack(new PanelBackData("HeroRunePanel", "HeroMainPanel"));
                            }
                        }
                        else if (SelectLevelMgr.instance.isEnterCurrentLevel == true)
                        {
                            //从普通/精英关卡退出
                            SelectLevelMgr.instance.isEnterCurrentLevel = false;
                            int levelId = SelectLevelMgr.instance.selectLevelId;
                            var levelType = SelectLevelMgr.instance.GetCurrentLevelType();
                            switch (levelType)
                            {
                                case cs.PASS_TYPE.PASS_COMM_TYPE:
                                    {
                                        if (TaskMgr.instance.GetCurrStoryNormalTask() != null && !TaskMgr.instance.IsLevelEverPass())
                                        {
                                            //有普通关卡剧情任务且该关卡第一次通关
                                            //返回主城
                                        }
                                        else
                                        {
                                            PlayerInfoMgr.instance.PushPanel(PanelDefine.SELECT_LEVEL_LAYER);
                                        }
                                    }
                                    break;
                                case cs.PASS_TYPE.PASS_ELITE_TYPE:
                                    {
                                        if (TaskMgr.instance.GetCurrStoryEliteTask() != null && !TaskMgr.instance.IsLevelEverPass())
                                        {
                                            //有精英关卡剧情任务且该关卡第一次通关
                                            //返回主城
                                        }
                                        else
                                        {
                                            PlayerInfoMgr.instance.PushPanel(PanelDefine.SELECT_LEVEL_LAYER);
                                        }
                                    }
                                    break;
                                default:
                                    {
                                        PlayerInfoMgr.instance.PushPanel(PanelDefine.SELECT_LEVEL_LAYER);
                                    }
                                    break;
                            }

                        }

						VentureDataMgr.instance.ClearQuitState();
#if false
                        if (SelectLevelMgr.instance.isEnterCurrentLevel == true)
                        {
                            int currentLevel = SelectLevelMgr.instance.selectLevelId;
                            if (currentLevel != -1)
                            {
                                CoreEntry.eventMgr.TriggerEvent<int>(EventDefine.EVENT_OPEN_LEVEL, currentLevel);
                                SelectLevelMgr.instance.isEnterCurrentLevel = false;
                            }
                        }
#endif
						if (SelectLevelMgr.instance.isNeedAbilityUp)
						{
							CoreEntry.uiMgr.CreatePanel("AbilityUpPanel");
						}

                        if (SelectLevelMgr.instance.isEnterNextLevel == true)
                        {

							var resPass = SelectLevelMgr.instance.GetNextLevelResPass(SelectLevelMgr.instance.nextLevelId);
                            if( resPass != null)
							{
								CoreEntry.eventMgr.TriggerEvent<int>(EventDefine.EVENT_OPEN_LEVEL, resPass.iID);
							}
                            else
                            {
                                CoreEntry.eventMgr.TriggerEvent<int>(EventDefine.EVENT_OPEN_LEVEL, SelectLevelMgr.instance.selectLevelId);
                            }
                        }
                    }
                    else
                    {
                        CoreEntry.eventMgr.TriggerEvent("fadeIn");
                    }


#endif
                    //每次登录如果收益为0，则弹出提示
                    if (LoginMgr.instance.ProfitTime > 0)
                    {
                        var _sourcesStr = CoreEntry.gameDataMgr.GetString("ForbidNoProfit");
                        _processForBid(LoginMgr.instance.ProfitTime, _sourcesStr);

                        //登录只弹出一次，下次load场景则不再弹出
                        LoginMgr.instance.ProfitTime = 0;
                    }

					if (CoreEntry.toyMgr.isDebug && CoreEntry.portalMgr.connection == false && GameMode.TOY == CoreEntry.toyMgr.GameMode)
					{
						AiToyDebugMgr.instance.SendBluetoothInfo(8);
					}
					AiToyDebug.Log("CheckBluetooth By UiMgr");

					CoreEntry.toyMgr.CheckBluetoothByUiMgr();
                }
                break;
            case GameStage.Nothing:
                break;
        }

        // 舞台改变，尝试新手引导
        CoreEntry.noviceGuideMgr.TryToTrigger();

        //显示hud
		SetHudVisible(hudVisible);
	}

    public bool DoesUiGetTheTouch(Vector2 position)
    {
        Ray ray = uiCamera.ScreenPointToRay(position);

        return Physics.Raycast(ray, float.PositiveInfinity, 1 << LayerMask.NameToLayer("UI") | 1 << LayerMask.NameToLayer("TopUI"));
    }

    public bool ShouldProcessTouch(int fingerIndex, Vector2 position)
    {
        return !DoesUiGetTheTouch(position);
    }

    void OnClear(string gameEvent, GameStage stage)
    {
        CoreEntry.isBattleStart = false;

        // clear all old panel
        ClearPanels();

        ClearStageAnimation();

        ClearMarkModelUI();
    }

    void OnBeginChangePlayer(string gameEvent)
    {

        //使用到英雄界面的场景需要停掉游戏，其他场景停掉AI即可
		if (!CoreEntry.IsStageUsingHero(CoreEntry.GetGameStage()) || CoreEntry.areaMgr.IsDoublePveScene())
		{
            CoreEntry.sceneMgr.Pause(PauseType.PauseAI, StopInputType.StopInputSource);
		}
        else
		{
            CoreEntry.sceneMgr.Pause(PauseType.PauseGame, StopInputType.StopInputSource);
		}

		OnToyAction();
    }

    void OnEndChangePlayer(string gameEvent)
	{
        CoreEntry.sceneMgr.Pause(PauseType.Resume, StopInputType.ResumeInputSource);
		OnToyAction();
    }

	void OnToyAction()
	{
		int resId = CoreEntry.portalMgr.GetCurrentHeroResId();
		foreach (var panels in panelTable)
		{
			for (int i = 0; i < panels.Value.Count; ++i)
			{
				if (panels.Value[i] == null) continue;
				var panelBase = panels.Value[i].GetComponent<PanelBase>();
				if (panelBase == null) continue;
				if (resId != 0)
				{
					switch (panelBase.toyAction)
					{
					case PanelBase.ToyAction.Exit:
						if (!CoreEntry.noviceGuideMgr.IsModal)
						{
							Scheduler.Create(this, (sche, t, s) =>
							                 {
								if (panelBase != null)
									panelBase.Exit();
							}, 0, 0, 0, ActionMgr.Priority.Normal, true);
						}
						
						break;
					case PanelBase.ToyAction.Custom:
						panelBase.OnToyEnter(resId);
						break;
					}
				}
				else
				{
					switch (panelBase.toyAction)
					{
					case PanelBase.ToyAction.Custom:
						if (!CoreEntry.noviceGuideMgr.IsModal)
						{
							panelBase.OnToyExit();
						}
						break;
					case PanelBase.ToyAction.Exit:
						if (!CoreEntry.noviceGuideMgr.IsModal)
						{
							Scheduler.Create(this, (sche, t, s) =>
							                 {
								if (panelBase != null)
									panelBase.Exit();
							}, 0, 0, 0, ActionMgr.Priority.Normal, true);
						}
						break;
					}
				}
			}
		}
	}

    public void ElementTips(ElementType type, float existSec = 1.5f)
    {
        var lastPanel = GetPanel("ElementTipsPanel");
        if (lastPanel != null)
        {
            //DestroyPanel("TipsPanel2");
            // TODO: fix bug
            return;
        }
        var tipsPanel = CreatePanel("ElementTipsPanel") as ElementTipsPanel;
        if (tipsPanel != null)
        {
            tipsPanel.existSec = existSec;
            tipsPanel.SetData(type);
        }
    }


    public GameObject TipsString(string msg, float existSec = 1.5f)
    {
        // 空的就没必要弹
        if (string.IsNullOrEmpty(msg))
            return null;

#if true
        var lastPanel = GetPanel("TipsPanel2");
        if (lastPanel != null)
        {
            //DestroyPanel("TipsPanel2");
            // TODO: fix bug
            return lastPanel;
        }
#endif

        var tipsPanel = CreatePanel("TipsPanel2") as TipsPanel;
        if (tipsPanel != null)
        {
            tipsPanel.text.text = msg;
            tipsPanel.existSec = existSec;
            return tipsPanel.gameObject;
        }
        return null;
#if false
		var obj = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Ui/MsgLabel");
		var label = obj.GetComponent<UILabel>();		
		label.transform.parent = uiCamera.transform;
		label.transform.localScale = Vector3.one;
		label.text = msg;
		label.color = color;
		var trans = label.transform;
		Vector3 targetPos = new Vector3(trans.position.x, trans.position.y + 0.5f, trans.position.z);
		Scheduler.Create(this, (sche, t, s)=>{
			float precent = t/s;
			trans.position = Vector3.Lerp(trans.position, targetPos, precent);
			var tcolor = label.color;
			tcolor.a = 1-precent;
			label.color = tcolor;
			if (t>=s){
				Destroy(label);
			}
		}, 0.0f, 2.0f);
#endif
    }

    public void ShowScreenWarning(bool isShow)
    {
        if (isShow)
        {
            GetPanelSimply("WarningEffect");
        }
        else
        {
            DestroyPanel("WarningEffect");
        }
    }

    void OnShowScreenWarning(string gameEvent)
    {
        ShowScreenWarning(true);
    }

    void OnUnShowScreenWarning(string gameEvent)
    {
        ShowScreenWarning(false);
    }

    public PanelBase GetTipsPanel()
    {
        return GetPanelBase("TipsPanel2");
    }

    public bool HasTipsString()
    {
        return GetTipsPanel() != null;
    }

    public void RemoveTipsString()
    {
        DestroyPanel("TipsPanel2");
    }

	public ToyBindPreConfirmPanel PopToyBindPreConfirm(string text, string confirmText, string cancelText, ConfirmPanel.ConfirmDelegate confirm, ConfirmPanel.ConfirmDelegate cancel = null)
	{
		var panel = (ToyBindPreConfirmPanel)CreatePanel("ToyBindPreConfirmPanel");
		panel.tip = text;
		panel.onConfirm = confirm;
		panel.onCancel = cancel;
		if (confirmText != null)
			panel.okText.text = confirmText;
		if (cancelText != null)
			panel.noText.text = cancelText;
		return panel;
	}
    public ConfirmPanel PopConfirm(string text, ConfirmPanel.ConfirmDelegate confirm, ConfirmPanel.ConfirmDelegate cancel = null)
    {
        var panel = (ConfirmPanel)CreatePanel("ConfirmPanel");
        panel.tip = text;
        panel.onConfirm = confirm;
        panel.onCancel = cancel;
        return panel;
    }

    public ConfirmPanel PopConfirm(string text, string confirmText, string cancelText, ConfirmPanel.ConfirmDelegate confirm, ConfirmPanel.ConfirmDelegate cancel = null)
    {
        var panel = (ConfirmPanel)CreatePanel("ConfirmPanel");
        panel.tip = text;
        panel.onConfirm = confirm;
        panel.onCancel = cancel;
        if (confirmText != null)
            panel.okText.text = confirmText;
        if (cancelText != null)
            panel.noText.text = cancelText;
        return panel;
    }

    public ConfirmPanel PopBigConfirm(string text, ConfirmPanel.ConfirmDelegate confirm, ConfirmPanel.ConfirmDelegate cancel = null)
    {
        var panel = (ConfirmPanel)CreatePanel("BigConfirmPanel");
        panel.tip = text;
        panel.onConfirm = confirm;
        panel.onCancel = cancel;
        return panel;
    }

    public ConfirmPanel PopBigConfirm(string text, string confirmText, string cancelText, ConfirmPanel.ConfirmDelegate confirm, ConfirmPanel.ConfirmDelegate cancel = null, bool withTick = true)
    {
        var panel = (ConfirmPanel)CreatePanel("BigConfirmPanel");
        panel.tip = text;
        panel.onConfirm = confirm;
        panel.onCancel = cancel;

        if (confirmText != null)
            panel.okText.text = confirmText;

        if (cancelText != null)
            panel.noText.text = cancelText;

        panel.tickObj.SetActive(withTick);
        return panel;
    }


    public ConfirmPanel PopConfirm2(string text, ConfirmPanel.ConfirmDelegate confirm)
    {
        var panel = (ConfirmPanel)CreatePanel("ConfirmPanel2");
        panel.tip = text;
        panel.onConfirm = confirm;
        return panel;
    }

    public ConfirmPanel PopConfirm2(string text, string confirmText, ConfirmPanel.ConfirmDelegate confirm)
    {
        var panel = (ConfirmPanel)CreatePanel("ConfirmPanel2");
        panel.tip = text;
        panel.onConfirm = confirm;
        panel.okText.text = confirmText;
        return panel;
    }
    public ConfirmPanel PopConfirm3(string text, ConfirmPanel.ConfirmDelegate confirm)
    {
        var panel = (ConfirmPanel)CreatePanel("ConfirmPanel3");
        panel.onConfirm = confirm;
        panel.text.text = text;
        return panel;
    }

    public ConfirmPanel PopToyBindPreConfirmGuest(string text, string textCenter, string textBottom, string confirmText, string cancelText, ConfirmPanel.ConfirmDelegate confirm, ConfirmPanel.ConfirmDelegate cancel = null)
    {
        var panel = (ConfirmPanel)CreatePanel("ToyBindPreConfirmGuest");
        panel.tip = text;
        panel.textCenter.text = textCenter;
        panel.textBottom.text = textBottom;
        panel.onConfirm = confirm;
        panel.onCancel = cancel;
        panel.okText.text = confirmText;
        panel.noText.text = cancelText;
        return panel;
    }

    public void BackPanel(BackPanel.BackDelegate back)
    {
        var panel = (BackPanel)CreatePanel("BackPanel");
        panel.onBack = back;
    }


    // 平铺
    public void AdjustSizeAndPosition(UIRect rect)
    {
        if (rect == null) return;

        float ratio16_9 = 16.0f / 9.0f;
        float ratio = CoreEntry.screenWidth / (float)CoreEntry.screenHeight;

        float screenHeight = CoreEntry.uiMgr.uiHight;
        float screenWidth = screenHeight * ratio;

        if (ratio >= ratio16_9)
            rect.SetRect(-screenWidth / 2.0f, -screenHeight / 2.0f, screenWidth, screenHeight);
        else
        {
            float offset = -screenHeight * ratio16_9 / 2.0f;
            rect.SetRect(offset, -screenHeight / 2.0f, screenHeight * ratio16_9, screenHeight);
        }
    }

    // 平铺2
    public void AdjustSizeAndPosition2(UIRect rect)
    {
        if (rect == null) return;

        float ratio16_9 = 16.0f / 9.0f;
        float ratio = Screen.width / (float)Screen.height;

        float screenHeight = CoreEntry.uiMgr.uiHight;
        float screenWidth = screenHeight * ratio;

        if (ratio >= ratio16_9)
            rect.SetRect(0.0f, 0.0f, screenWidth, screenHeight);
        else
        {
            rect.SetRect(0.0f, 0.0f, screenHeight * ratio16_9, screenHeight);
        }
    }

    // 切上下；高度不足初始设定高度时，切左右
    public void AdjustSizeAndPosition3(UIWidget rect)
    {
        if (rect == null) return;

        float xscale, yscale;
        AdjustSizeAndPosition3(rect, out xscale, out yscale);

        var size = rect.localSize;
        float newW = size.x * xscale;
        float newH = size.y * yscale;
        if (newH < CoreEntry.uiMgr.uiHight)
        {
            newH = CoreEntry.uiMgr.uiHight;
            newW = size.x / size.y * newH;
        }

        rect.SetRect(-newW / 2.0f, -newH / 2.0f, newW, newH);
    }

    public void AdjustSizeAndPosition3(UIWidget rect, out float xscale, out float yscale)
    {
        if (rect == null)
        {
            xscale = 1;
            yscale = 1;
            return;
        }
        float ratio = CoreEntry.screenWidth / (float)CoreEntry.screenHeight;

        float screenHeight = CoreEntry.uiMgr.uiHight;
        float screenWidth = screenHeight * ratio;

        var size = rect.localSize;
        float originR = size.x / size.y;
        float newW = screenWidth;
        float newH = newW / originR;

        xscale = newW / size.x;
        yscale = newH / size.y;
    }

    public void AdjustSizeAndPosition3ForPanel(UIPanel panel)
    {
        if (panel == null) return;

        float xscale, yscale;
        var size = new Vector2(panel.baseClipRegion.y, panel.baseClipRegion.w);
        float ratio = CoreEntry.screenWidth / (float)CoreEntry.screenHeight;

        float screenHeight = CoreEntry.uiMgr.uiHight;
        float screenWidth = screenHeight * ratio;

        float originR = size.x / size.y;
        float newW = screenWidth;
        float newH = newW / originR;

        xscale = newW / size.x;
        yscale = newH / size.y;
        
        float newW2 = size.x * xscale;
        float newH2 = size.y * yscale;
        if (newH2 < CoreEntry.uiMgr.uiHight)
        {
            newH2 = CoreEntry.uiMgr.uiHight;
            newW2 = size.x / size.y * newH2;
        }
    }

    // 切两边
    public void AdjustSizeAndPosition4(UIWidget rect)
    {
        if (rect == null) return;

        float xscale, yscale;
        AdjustSizeAndPosition4(rect, out xscale, out yscale);

        var size = rect.localSize;
        float newW = size.x * xscale;
        float newH = size.y * yscale;

        rect.SetRect(-newW / 2.0f, -newH / 2.0f, newW, newH);
    }

    public void AdjustSizeAndPosition4(UIWidget rect, out float xscale, out float yscale)
    {
        float ratio = CoreEntry.screenWidth / (float)CoreEntry.screenHeight;

        float screenHeight = CoreEntry.uiMgr.uiHight;
        float screenWidth = screenHeight * ratio;

        var size = rect.localSize;
        float originR = size.x / size.y;
        float newH = screenHeight;
        float newW = originR * newH;

        xscale = newW / size.x;
        yscale = newH / size.y;
    }

    // 拉伸
    public void AdjustSizeAndPosition5(UIWidget rect, out float xscale, out float yscale)
    {
        float ratio = CoreEntry.screenWidth / (float)CoreEntry.screenHeight;
        float screenHeight = CoreEntry.uiMgr.uiHight;
        float screenWidth = screenHeight * ratio;
        var size = rect.localSize;
        xscale = screenWidth / size.x +0.01f; // 补上白边
        yscale = screenHeight / size.y + 0.01f; // 补上白边
    }

    public void ShrinkResolution()
    {
#if false
        //CoreEntry.PushUIHight(512);
        //CoreEntry.uiMgr.uiRoot.GetComponent<UIRoot>().minimumHeight = 512;
        Scheduler.Create(this, (sche, t, s) =>
        {
            CoreEntry.uiMgr.uiCamera.GetComponent<UICamera>().ForceUpdate();
        });
#endif
    }

    public void ExtendResolution()
    {
#if false
        //CoreEntry.PopUIHight();
        //CoreEntry.uiMgr.uiRoot.GetComponent<UIRoot>().minimumHeight = CoreEntry.uiMgr.uiHight;
        Scheduler.Create(this, (sche, t, s) =>
        {
            CoreEntry.uiMgr.uiCamera.GetComponent<UICamera>().ForceUpdate();
        });
#endif
    }

    public void AdjustPanels(UIPanel[] uiPanels, PanelBase.DepthType depthType)
    {
        if (uiPanels == null || uiPanels.Length == 0) return;
        //if (panel == null || (panel != null && panel.depthType == PanelBase.DepthType.Dynamic))
        {
            // adjust depth by logic depth
            int baseDepth = PanelBase.DepthValue[(int)depthType];
            int depth = ++depthTypeValue[(int)depthType];
            int logicDepth = depth * PanelBase.PanelDepthInteval + baseDepth;

            for (int i = 0; i < uiPanels.Length; ++i)
            {
                for (int j = i; j < uiPanels.Length; ++j)
                {
                    if (uiPanels[i].depth > uiPanels[j].depth)
                    {
                        var tempPanel = uiPanels[i];
                        uiPanels[i] = uiPanels[j];
                        uiPanels[j] = tempPanel;
                    }
                }
            }

            for (int i = 0; i < uiPanels.Length; ++i)
            {
                uiPanels[i].depth = logicDepth + i;
            }
        }
    }

    int GetActivePanelCount()
    {
        return panelTable.Count - removeList.Count;
    }

    void OnPush()
    {
        foreach (var panels in panelTable)
        {
            for (int i = 0; i < panels.Value.Count; ++i)
            {
                panels.Value[i].SetActive(false);
            }
        }
    }

    void OnPop()
    {
        foreach (var panels in panelTable)
        {
            for (int i = 0; i < panels.Value.Count; ++i)
            {
                // 暂时不考虑原来就false的情况
                panels.Value[i].SetActive(true);
            }
        }
    }

    static public void PushMgr()
    {
        CoreEntry.uiMgr.OnPush();
        var oldMgr = CoreEntry.uiMgr;
        CoreEntry.uiMgr = CoreEntry.uiMgr.gameObject.AddComponent<UiMgr>();
        CoreEntry.uiMgr.m_uiCamera = oldMgr.m_uiCamera;
        CoreEntry.uiMgr.m_uiRoot = oldMgr.m_uiRoot;
        CoreEntry.uiMgr.m_uiTopCamera = oldMgr.m_uiTopCamera;
        oldMgr.enabled = false;
        uiMgrStack.Add(oldMgr);
    }

    static public void PopMgr()
    {
        var oldMgr = uiMgrStack[uiMgrStack.Count - 1];
        uiMgrStack.Remove(oldMgr);
        Object.Destroy(CoreEntry.uiMgr);
        CoreEntry.uiMgr = oldMgr;
        oldMgr.enabled = true;
        oldMgr.OnPop();
    }

    // 标记UI中出现模型，只要计数大于0，此后所有面板都在topcamera上，
    // 直到再次调用unmark是的计数为0，topcamera的面板会挪到camera上
    public void MarkModelUI()
    {
        int oldNum = frontModelPanelNum;
        ++frontModelPanelNum;
        if (oldNum == 0)
        {
            // 将非Dynamic的移到topCamera下
            int childCount = uiCamera.transform.childCount;
            for (int i = 0; i < childCount; ++i)
            {
                var panel = uiCamera.transform.GetChild(i);
                var panelBase = panel.GetComponent<PanelBase>();
                if (panelBase == null) continue;
                if (//panelBase.depthType == PanelBase.DepthType.High ||
                    panelBase.depthType == PanelBase.DepthType.Highest)
                {
                    --childCount;
                    --i;
                    panel.parent = topUICamera.transform;
                    panel.localScale = Vector3.one;
                    GlobalFunctions.ChangeLayer(panel.gameObject, LayerMask.NameToLayer("TopUI"));
                }

            }
        }
    }

    public void UnMarkModelUI()
    {
        --frontModelPanelNum;
        if (frontModelPanelNum <= 0)
        {
            ClearMarkModelUI();
        }
    }

    public void ClearMarkModelUI()
    {
        frontModelPanelNum = 0;
        // 将所有添加到topCamera的都挪到uicamera
        if (m_uiRoot == null)
            return;
        int childCount = topUICamera.transform.childCount;
        for (int i = 0; i < childCount; ++i)
        {
            var topPanel = topUICamera.transform.GetChild(0);
            topPanel.parent = uiCamera.transform;
            topPanel.localScale = Vector3.one;
            GlobalFunctions.ChangeLayer(topPanel.gameObject, LayerMask.NameToLayer("UI"));
        }
    }

    public void SetHudVisible(bool isVis)
    {
        if (hudPanel == null)
            return;

        hudPanel.gameObject.SetActive(isVis);

        return;
        showHud = isVis;
        var rootTrans = uiRoot.transform;
        for (int i = 0; i < rootTrans.childCount; ++i)
        {
            var child = rootTrans.GetChild(i);
#if SHOW_FPS
            if (child == mFps) continue;
#endif

            if (child != uiCamera.transform &&
                child != topUICamera.transform)
            {
                child.gameObject.SetActive(isVis);
            }
        }
        // 以上的更新不一定正确，因为有些hud有自己的显示逻辑，所以最后再给一次特殊逻辑执行的机会
        CoreEntry.eventMgr.TriggerEvent<bool>("HudUpdate", isVis);
    }

    void OnFadeIn(string gameEvent)
    {
        CreatePanel("FadeInPanel");
    }

    void OnFadeOut(string gameEvent)
    {
        CreatePanel("FadeOutPanel");
    }

    void OnFadeInOut(string gameEvent)
    {
        CreatePanel("FadeInOutPanel");
    }

    public bool HasBlockPanel()
    {
        return blockPanel != null;
    }

    public void UpdateFingerTrail(Vector3 pos)
    {
        if (fingerTransform == null)
        {
            CreateFingerTrail();
        }

        pos.x -= Screen.width / 2;
        pos.y -= Screen.height / 2;
        pos.z = 1;

        fingerTransform.localPosition = pos;
    }

    void CreateFingerTrail()
    {
        if (fingerTransform != null)
        {
            Destroy(fingerTransform.gameObject);
        }
        var finger = GameObject.Instantiate(Resources.Load("Prefabs/Doodad/FingerTrail")) as GameObject;
        fingerTransform = finger.transform;
        fingerTransform.parent = uiCamera.transform;
        fingerTransform.localScale = Vector3.one;
        fingerTransform.localRotation = Quaternion.identity;
        fingerTransform.localPosition = Vector3.zero;
    }

    public UIPanel GetCurBlockPanel()
    {
        return blockPanel;
    }

    public Vector3 ScreenPosToNGUIPos(Vector3 screenPos, bool aa = true)
    {
        // 左上角原点改为左下角原点
        screenPos.y = CoreEntry.screenHeight - screenPos.y;
        // 世界变换
        Vector3 uiPos = uiCamera.ScreenToWorldPoint(screenPos);
        uiPos = uiCameraTransform.InverseTransformPoint(uiPos);
        // 进行AA
        if (aa)
        {
            uiPos.x = Mathf.FloorToInt(uiPos.x);
            uiPos.y = Mathf.FloorToInt(uiPos.y);
            uiPos.z = 0;
        }

        return uiPos;
    }

    //登录失败，封号处理,或者0收益的时候处理
    private void _processForBid(uint _forbidTime, string _sourcesStr)
    {
        string _resultStr = "";
        if (_forbidTime > 0)
        {
            //var _sourcesStr = CoreEntry.gameDataMgr.GetString("ForBidLogin");
            var _time = _forbidTime;
            var _dataTime = GlobalFunctions.ConvertIntDateTime(_time);
            _resultStr = string.Format(_sourcesStr, _dataTime.Year, _dataTime.Month, _dataTime.Day);
        }
        else
        {
            _resultStr = CoreEntry.gameDataMgr.GetString("ForBidTipToStageNoLimit");
        }

        var panel = CoreEntry.uiMgr.PopConfirm2(_resultStr, null);
    }

    //升级界面弹出时机处理。点击返回和下一关，后返回，检测是否需要弹出升级界面
    void OnLoadSceneEndProcess(string gameEvent)
    {
        //如果是升级,则隔0.5秒后弹出升级面板
        if (BattleResultMgr.instance.levelUp && 
            CoreEntry.areaMgr.GetCurrentSceneName() == AreaMgr.TOWN_SCENE_NAME)
        {
            CoreEntry.popUIMgr.AddQueueUI(POPTYPE.POPTYPE_ROLE_LEVELUP, delegate()
            {
                Scheduler.Create(this, delegate(Scheduler scheduler, float t, float s)
                {
                    CoreEntry.eventMgr.TriggerEvent<string>(EventDefine.EVENT_CREATE_PANEL, "LevelUpPanel");
                    var levelUpPanel = CoreEntry.uiMgr.GetPanelBase("LevelUpPanel") as LevelUpPanel;
                    levelUpPanel.LevelUpEnd();
                }, 0, 0, 0.2f);
            });

            //重置
            BattleResultMgr.instance.levelUp = false;
        }
    }

    //召唤师等级变更
    private void OnAttrLevelUP(string gameEvent, int _levelUp)
    {
        //在非关卡战斗结算的升级
        if (!BattleResultMgr.instance.levelUp &&
            CoreEntry.areaMgr.GetCurrentSceneName() == AreaMgr.TOWN_SCENE_NAME)
        {
            CoreEntry.popUIMgr.AddQueueUI(POPTYPE.POPTYPE_ROLE_LEVELUP, delegate()
            {
                CoreEntry.eventMgr.TriggerEvent<string>(EventDefine.EVENT_CREATE_PANEL, "LevelUpPanel");
                var levelUpPanel = CoreEntry.uiMgr.GetPanelBase("LevelUpPanel") as LevelUpPanel;
                levelUpPanel.LevelUpEnd();
            });
        }
    }

    //显示菊花
    public void ShowLoadingPanel(bool _bShow)
    {
        if (_bShow)
        {
            OnStartLoading("");
        }
        else
        {
            OnEndLoading("");
        }
    }

    public void AllPanelsHide()
    {
        panelActive.Clear();
        panelActiveObj.Clear();
        foreach (var ui in panelTable)
        {
            foreach (var panel in ui.Value)
            {
                int hash = panel.GetHashCode();
                panelActive[hash] = panel.activeSelf;
                panelActiveObj[hash] = panel;
                panel.SetActive(false);
            }
        }
    }

    public void AllPanelsRecover()
    {
        foreach (var ui in panelActive)
        {
            if (panelActiveObj.ContainsKey(ui.Key))
            {
                panelActiveObj[ui.Key].SetActive(ui.Value);
            }
        }
    }
}

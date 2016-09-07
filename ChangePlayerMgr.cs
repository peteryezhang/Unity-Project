#if false

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ChangePlayerMgr : MonoBehaviour
{
	private delegate void StartFunction(); 
	private delegate void EndFunction(); 
	private delegate bool UpdateFunction(ActionTrack track, float timePass, float actionLength); 

	// 退出关卡按钮图片
	Texture2D buttonTex;
	Rect rectButton;
	bool bTouch = false;
	bool bShowButton = false;
	Vector2 touchPos;

	class ActionTrack
	{
		public float startTime;
		public float endTime;
		public bool started = false;

		public StartFunction Start;
		public EndFunction End;
		public UpdateFunction Update;
	}

	List<ActionTrack> actionTlist = new List<ActionTrack>();

	float startTime;
	bool isEnd = false;
	// 是否更换了英雄， 正常流程会更换英雄，点退出按钮不会更换英雄
	public bool isPlayerChanged = true;

	// 六芒星是否出现过
	bool startShowed = false;

	// 第一阶段相机扩张的最大fov
	float largeFov = 170.0f;
	// 备份原来的相机fov
	float oldFov;

	Transform cameraAnchor;

	// 备份原来的相机绑点
	Transform oldAnchor;

	int oldCullMask;

	Vector3 oldCanmerPos;

	Transform mainCameraTransform;
	Camera mainCamera;

	Renderer skyBoxRenderer;

	Transform newPlayerTransform;
	Transform diePlayerTransform;

	Transform switchSphereTransform;
	Quaternion sphereRotation1;
	Quaternion sphereRotation2;
	Quaternion sphereRotation3;

	int lastBranchStage = 0;

	public int newPlayerId;

	public bool IsWaitingForNewPlayer()
	{
		return newPlayerId == 0;
	}

	void Start()
	{
		startTime = Time.realtimeSinceStartup;
		buttonTex = GlobalFunctions.LoadTexture("Ui/RawTextures/ExitLevel");
		float scale = CoreEntry.screenHeight / 1000.0f;
		float width = 235 * scale;
		float height = 116 * scale;
		rectButton.Set( CoreEntry.screenWidth - 10 - width, CoreEntry.screenHeight - 10 - height, width, height);

		ActionTrack actionTrack;

		// 视角从普通变成广角
		actionTrack = new ActionTrack();
		actionTrack.startTime = 0.0f;
		actionTrack.endTime = 0.5f;
		actionTrack.Update = FovNormalToLarge;
		actionTrack.End = FovNormalToLargeEnd;
		actionTlist.Add(actionTrack);

		
		// 视角从广角变普通
		actionTrack = new ActionTrack();
		actionTrack.startTime = 0.6f;
		actionTrack.endTime = 1.1f;
		actionTrack.Update = FovLargeToNormal;
		actionTlist.Add(actionTrack);

		// 球体封闭
		actionTrack = new ActionTrack();
		actionTrack.startTime = 0.4f;
		actionTrack.endTime = 0.7f;
		actionTrack.Update = BlackBallClose;
		actionTrack.End = BlackBallCloseEnd;
		actionTlist.Add(actionTrack);

        mainCamera = CoreEntry.cameraMgr.GetMainCamera();
        mainCameraTransform = mainCamera.transform;
        

		GameObject switchSphere = CoreEntry.resourceMgr.InstantiateObject("Prefabs/Effect/SwitchSphere 4");
		if( switchSphere != null )
		{
			switchSphereTransform = switchSphere.transform;
			switchSphereTransform.position = mainCameraTransform.position;

			skyBoxRenderer = GlobalFunctions.GetTransform(switchSphereTransform, "skyball 02").GetComponent<Renderer>();
			skyBoxRenderer.material.renderQueue = 5000;
			sphereRotation1 = switchSphereTransform.rotation;
			sphereRotation2 = Quaternion.LookRotation(switchSphereTransform.right, switchSphereTransform.up);
			sphereRotation3 = Quaternion.LookRotation(-switchSphereTransform.forward, switchSphereTransform.up);

			cameraAnchor = GlobalFunctions.GetTransform(switchSphereTransform, "camera_guadian");

#if true
			oldCanmerPos = cameraAnchor.position;
			oldAnchor = mainCameraTransform.parent;
			oldFov = mainCamera.fieldOfView;
			oldCullMask = mainCamera.cullingMask;
            
			mainCameraTransform.parent = cameraAnchor;
			mainCameraTransform.localRotation = Quaternion.identity;
			mainCameraTransform.localPosition = Vector3.zero;
#endif

            //CoreEntry.cameraMgr.MountCamera(cameraAnchor);

			switchSphereTransform.animation.Play("qiehuanjuese_000");
		}

		// 加载新角色资源
		newPlayerId = -1;

        CoreEntry.eventMgr.AddListener("changePlayerDone", OnDone);

        CoreEntry.eventMgr.TriggerEvent<bool>("showHeroSwitch", true);
        
	}

    void OnDestroy()
    {
        CoreEntry.eventMgr.RemoveListener("changePlayerDone", OnDone);
        CoreEntry.eventMgr.TriggerEvent<string>(EventDefine.EVENT_DESTROY_PANEL, "DeadPanel");
    }

	public void MakeNewPlayer(int playerId)
	{
		newPlayerId = playerId;

		PlayerData playerData = new PlayerData();
		if( CoreEntry.gameDataMgr.GetPlayerDataByResId(playerId, out playerData) )
		{
			GameObject player = CoreEntry.resourceMgr.InstantiateObject(playerData.resPath);
			if (newPlayerTransform)
			{
				Object.Destroy(newPlayerTransform.gameObject);
			}

			if( player != null )
			{
				Transform playerAnchor = GlobalFunctions.GetTransform(switchSphereTransform,"Character_gadian");
				if (playerAnchor)
				{
					newPlayerTransform = player.transform;
					newPlayerTransform.parent = playerAnchor;
					newPlayerTransform.localPosition = Vector3.zero;
					newPlayerTransform.localRotation = Quaternion.identity;
				}
				else
				{
					newPlayerTransform = player.transform;
					newPlayerTransform.position = cameraAnchor.position + cameraAnchor.forward * 4.0f - cameraAnchor.up * 0.5f;
					newPlayerTransform.rotation = Quaternion.LookRotation(-cameraAnchor.forward, cameraAnchor.up);
				}
				
				// 设置新角色及其子物体的layer
				int layer = LayerMask.NameToLayer("ChangePlayer");
				player.layer = layer;
				player.tag = "Untagged";

				Transform[] transforms = player.GetComponentsInChildren<Transform>(true);
				foreach(Transform tran in transforms )
				{
					tran.gameObject.layer = layer;
					tran.gameObject.tag = "Untagged";
                }
                
                // 关闭脚本
                PlayerController controller = player.GetComponent<PlayerController>();
                if( controller != null )
                    controller.enabled = false;
                
                player.SetActive(false);
			}
		}
	}

	void Update()
	{
		if( isEnd )
			return;

		float currentTime = Time.realtimeSinceStartup - startTime;

		for( int i=0; i<actionTlist.Count; )
		{
			if( currentTime >= actionTlist[i].startTime )
			{
				if( !actionTlist[i].started )
				{
					actionTlist[i].started = true;
					if( actionTlist[i].Start != null )
						actionTlist[i].Start();
				}

				if( currentTime <= actionTlist[i].endTime )
				{
					if( actionTlist[i].Update != null )
					{	if( !actionTlist[i].Update(actionTlist[i], currentTime - actionTlist[i].startTime, actionTlist[i].endTime - actionTlist[i].startTime) )
						{
							// Update函数返回false，调整结束时间终止这个action
							actionTlist[i].endTime = currentTime;
						}
					}
				}

				if( currentTime >= actionTlist[i].endTime )
				{
					if( actionTlist[i].End != null )
						actionTlist[i].End();

					actionTlist.RemoveAt(i);
				}
				else
					++i;
			}
			else
				++i;
		}

		if( bShowButton )
			CheckButton();

		return;
	}

	// 视角从普通变成广角
	bool FovNormalToLarge(ActionTrack track, float timePass, float actionLength)
	{
		float fovFactor = timePass / actionLength;
		if( fovFactor > 1.0f )
			fovFactor = 1.0f;

		float newFov = oldFov + ( largeFov - oldFov ) * fovFactor;
		mainCamera.fieldOfView = newFov;
		return true;
	}

	void FovNormalToLargeEnd()
	{
		if( newPlayerId != 0 && CoreEntry.sceneMgr.IsPlayerDied(newPlayerId) )
		{
			newPlayerTransform.gameObject.SetActive(true);
			// 播放空闲动画
			Actor actor = newPlayerTransform.GetComponent<Actor>();
		
			if( actor != null )
				actor.PlayAction("idle");
		}
	}

	// 视角从广角变普通
	bool FovLargeToNormal(ActionTrack track, float timePass, float actionLength)
	{
		float fovFactor = timePass / actionLength;
		if( fovFactor > 1.0f )
			fovFactor = 1.0f;
		
		float newFov = largeFov + ( oldFov - largeFov ) * fovFactor;
		mainCamera.fieldOfView = newFov;
		return true;
	}

	// 球体封闭
	bool BlackBallClose(ActionTrack track, float timePass, float actionLength)
	{
		float darkFactor = timePass / actionLength;
		if( darkFactor > 1.0f )
		{
			darkFactor = 1.0f;
		}

		skyBoxRenderer.material.SetFloat("_AlphaLerp", darkFactor);
		return true;
	}

	// 球体封闭
	bool BlackBallOpen(float timePass, float actionLength)
	{
		float darkFactor = timePass / actionLength;
		if( darkFactor > 1.0f )
		{
			darkFactor = 1.0f;
		}
		
		skyBoxRenderer.material.SetFloat("_AlphaLerp", 1.0f - darkFactor);
		return true;
	}

	void BlackBallCloseEnd()
	{
		mainCamera.cullingMask = 1 << LayerMask.NameToLayer("ChangePlayer");
		skyBoxRenderer.material.renderQueue = 500;

		ActionTrack actionTrack;
		float currentTime = Time.realtimeSinceStartup - startTime;
		int hero = CoreEntry.portalMgr.GetCurrentHeroResId();

		if( hero == 0 )
		{
			// 六芒星进入
			actionTrack = new ActionTrack();
			actionTrack.startTime = currentTime + 1.7f;
			actionTrack.endTime = currentTime + 1.7f;
			actionTrack.Start = StartEnter;
			actionTlist.Add(actionTrack);
		}
		
		// 等待新角色放入
		actionTrack = new ActionTrack();
		if( hero == 0 )
			actionTrack.startTime = currentTime + 1.7f + 0.5f;
		else
			actionTrack.startTime = currentTime + 1.7f;

		actionTrack.endTime = 100000.0f;
		actionTrack.Start = WaitForNewToyStart;
		actionTrack.Update = WaitForNewToy;
		actionTlist.Add(actionTrack);

	}

	// 六芒星进入
	void StartEnter()
	{
		switchSphereTransform.animation.Play("qiehuanjuese_001");
	}

	// 等待新角色放入
	void WaitForNewToyStart()
	{
	}

	bool WaitForNewToy(ActionTrack track, float timePass, float actionLength)
	{
		int hero = CoreEntry.portalMgr.GetCurrentHeroResId();

		if( hero == 0 )
		{
			if( lastBranchStage == 2 )
			{
				switchSphereTransform.animation.Play("qiehuanjuese_005");

				float currentTime = Time.realtimeSinceStartup - startTime;
				ActionTrack actionTrack = new ActionTrack();
				actionTrack.startTime = currentTime + 0.4f;
				actionTrack.endTime = 100000.0f;
				actionTrack.Update = WaitForNewToy;
				actionTlist.Add(actionTrack);

				lastBranchStage = 1;
				bShowButton = false;
				return false;
			}
			else
			{
				if( newPlayerId != hero )
				{
					if( newPlayerTransform != null )
					{
						Object.Destroy(newPlayerTransform.gameObject);
						newPlayerTransform = null;
					}

					switchSphereTransform.animation.Play("qiehuanjuese_loop");
					bShowButton = true;
					startShowed = true;
					newPlayerId = hero;
				}

				lastBranchStage = 1;
				return true;
			}
		}
		else if( CoreEntry.sceneMgr.IsPlayerDied(hero) )
		{
			if( lastBranchStage == 1 )
			{
				switchSphereTransform.animation.Play("qiehuanjuese_004");

				float currentTime = Time.realtimeSinceStartup - startTime;
				ActionTrack actionTrack = new ActionTrack();
				actionTrack.startTime = currentTime + 0.4f;
				actionTrack.endTime = 100000.0f;
				actionTrack.Update = WaitForNewToy;
				actionTlist.Add(actionTrack);

				lastBranchStage = 2;
				bShowButton = false;
				return false;
			}
			else
			{
				if( newPlayerId != hero )
				{
					switchSphereTransform.animation.Play("qiehuanjuese_fail");
					bShowButton = true;
					newPlayerId = hero;
					MakeNewPlayer(newPlayerId);

					newPlayerTransform.gameObject.SetActive(true);
					newPlayerTransform.animation.clip = null;

					// 播放空闲动画
					Actor actor = newPlayerTransform.GetComponent<Actor>();
					
					if( actor != null )
					{
						actor.PlayAction("hero_dead");
                        CoreEntry.eventMgr.TriggerEvent<bool>("showHeroSwitch", true);
					}
				}

				//if( newPlayerTransform != null )
				//	newPlayerTransform.GetComponent<Actor>().PlayAction("hero_dead");

				lastBranchStage = 2;
				return true;
			}
		}
		else 
		{
			if( PlayerDataMgr.instance.GetHeroDataByResId(hero) != null )
			{
				if( newPlayerId != hero )
				{
					newPlayerId = hero;
					MakeNewPlayer(hero);
				}

				lastBranchStage = 3;
				track.End = WaitForNewToyEnd;
				bShowButton = false;
				return false;
			}
		}

		return true;
	}

	void WaitForNewToyEnd()
	{
		float currentTime = Time.realtimeSinceStartup - startTime;
		ActionTrack actionTrack;
		float timeOffset = 0.0f;

		if( startShowed )
		{
			switchSphereTransform.animation.Play("qiehuanjuese_002");

			// 六芒星消失完毕
			actionTrack = new ActionTrack();
			actionTrack.startTime = currentTime + 0.58f;
			actionTrack.endTime = currentTime + 0.58f;
			actionTrack.Start = StartFadeDone;
			actionTlist.Add(actionTrack);

			timeOffset = 0.58f;
		}
		else
			switchSphereTransform.animation.Play("qiehuanjuese_003");


		// 主角出现
		actionTrack = new ActionTrack();
		actionTrack.startTime = currentTime + 2.92f + timeOffset;
		actionTrack.endTime = currentTime + 2.92f + timeOffset;
		actionTrack.Start = PlayerComeIn;
		actionTlist.Add(actionTrack);

		// 主角播出场动画
		actionTrack = new ActionTrack();
		actionTrack.startTime = currentTime + 5.0f + timeOffset;
		actionTrack.endTime = currentTime + 5.0f + timeOffset;
		actionTrack.Start = PlayerPlayAnimation;
		actionTlist.Add(actionTrack);

		// 全部搞定
		actionTrack = new ActionTrack();
		actionTrack.startTime = currentTime + 8.42f + timeOffset;
		actionTrack.endTime = currentTime + 8.42f + timeOffset;
		actionTrack.Start = AllDone;
		actionTlist.Add(actionTrack);
	}

	// 球体第一次旋转
	bool BallRotate1(float timePass, float actionLength)
	{
		float rotateFactor = timePass / actionLength;
		if( rotateFactor > 1.0f )
			rotateFactor = 1.0f;
		
		switchSphereTransform.rotation = Quaternion.Slerp(sphereRotation1, sphereRotation2, rotateFactor);
		return true;
	}

	// 球体第二次旋转
	bool BallRotate2(float timePass, float actionLength)
	{
		float rotateFactor = timePass / actionLength;
		if( rotateFactor > 1.0f )
			rotateFactor = 1.0f;

		switchSphereTransform.rotation = Quaternion.Slerp(sphereRotation2, sphereRotation3, rotateFactor);
		return true;
	}

	// 球体第三次旋转
	bool BallRotate3(float timePass, float actionLength)
	{
		float rotateFactor = timePass / actionLength;
		if( rotateFactor > 1.0f )
			rotateFactor = 1.0f;
		
		switchSphereTransform.rotation = Quaternion.Slerp(sphereRotation3, sphereRotation1, rotateFactor);
		return true;
	}

	void StartFadeDone()
	{
		switchSphereTransform.animation.Play("qiehuanjuese_003");
	}

	// 主角出现
	void PlayerComeIn()
	{
		newPlayerTransform.gameObject.SetActive(true);
		// 播放空闲动画
		Actor actor = newPlayerTransform.GetComponent<Actor>();

		if( actor != null )
			actor.PlayAction("idle");

		Transform transformGuadian = GlobalFunctions.GetTransform(switchSphereTransform, "Character_gadian");
		newPlayerTransform.parent = transformGuadian;
		newPlayerTransform.localRotation = Quaternion.identity;
		newPlayerTransform.localPosition = Vector3.zero;
	}

	// 主角播出场动画
	void PlayerPlayAnimation()
	{
		Actor actor = newPlayerTransform.GetComponent<Actor>();
		
		if( actor != null )
			actor.PlayAction("switchIdle");
	}

	// 全部搞定
	void AllDone()
    {
#if true
		//skyBoxRenderer.material.renderQueue = 5000;
		mainCamera.cullingMask = oldCullMask;
		mainCameraTransform.parent = oldAnchor;
		mainCameraTransform.localPosition = Vector3.zero;
		mainCameraTransform.localRotation = Quaternion.identity;
		mainCamera.fieldOfView = oldFov;
		mainCamera.cullingMask = oldCullMask;
#endif

        //CoreEntry.cameraMgr.UnMountCamera();

        Object.Destroy(cameraAnchor.gameObject);
		Object.Destroy(switchSphereTransform.gameObject);
		
		if( newPlayerTransform != null )
			Object.Destroy(newPlayerTransform.gameObject);

		isEnd = true;
	}

	public bool IsEnd()
	{
		return isEnd;
	}

	void CheckButton()
	{
#if false
		#if UNITY_EDITOR
		if( Input.GetMouseButton(0) )
		{
			bTouch = true;
			touchPos.x = Input.mousePosition.x;
			touchPos.y = CoreEntry.screenHeight - Input.mousePosition.y;
			return;
		}
		else
		{
			if( bTouch == false )
				return;
			
			bTouch = false;
		}
		
		#else
		if( Input.touchCount > 0 )
		{
			bTouch = true;
			touchPos.x = Input.touches[0].position.x;
			touchPos.y = CoreEntry.screenHeight - Input.touches[0].position.y;
			return;
		}
		else
		{
			if( bTouch == false )
				return;
			
			bTouch = false;
		}
		#endif
		

		if( bShowButton && rectButton.Contains(touchPos) )
		{            
			AllDone();
			isPlayerChanged = false;
		}
#endif
	}

#if false
	void OnGUI()
	{
		if( bShowButton )
			Graphics.DrawTexture(rectButton, buttonTex);
	}
#endif

    void OnDone(string gameEvent)
    {
        AllDone();
        isPlayerChanged = false;
    }
}

#endif
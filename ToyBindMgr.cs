
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using cs;
using conf;

public class ToyBindData
{
	public CSAntiPiracyS toyData = null;
	public bool isFirstBind = false;
}

public class ToyBindMgr : MonoBehaviour
{
	//�Ƿ�����ż������
	public bool isBindingToy = false;

	//��ǰ��Ҫ�󶨵����CID�б�
	private List<ToyBindData> toyBindDataList = new List<ToyBindData>();
	//��ǰ���ڵİ��������
	private List<string> bindPanelStack = new List<string>();
	//��ǰ��Ҫ�󶨵��������
	private CSAntiPiracyS currentToyData = null;
	//����ID
	private uint soundId = 0;


	public ToyBindMgr()
	{
		CoreEntry.gameServer.RegisterMsg(cs.CS_CMD_TYPE.CS_CMD_ANTI_PIRACY, OnCmdBindToyResponse);
		CoreEntry.eventMgr.AddListener("endChangePlayer", OnEndChangePlayerEveryTime);
	}

	~ToyBindMgr()
	{
		CoreEntry.gameServer.UnRegisterAllMsg(this);
		CoreEntry.eventMgr.RemoveListener(this);
	}

	GameServer.HandleState OnCmdBindToyResponse(cs.CS_CMD_TYPE cmd, cs.CSSvrPkgBody msg)
	{
		if (msg.stAntiPiracy.nEno != 0)
		{
			//������Ӣ�۽�����PlayerDataMgr�д���
			//ToyBindMgr����Ӣ�۰�
			switch (msg.stAntiPiracy.nEno)
			{
				case cs.csMacros.CS_CODE_ANTI_PIRACY_TOY_NEED_CHECK_BIND:
					{
						//��ż��Ҫ��
						bool canBind = false;
						if (msg.stAntiPiracy.szToyCID != null)
							canBind = GetToyBindDataByCID(msg.stAntiPiracy.szToyCID) == null ? true : false;
						else
							AiToyDebug.Log("ToyCheckBindNull");
						if (canBind)
						{
							ToyBindData toyBindData = new ToyBindData();
							toyBindData.isFirstBind = true;
							toyBindData.toyData = msg.stAntiPiracy;
							int resId = CoreEntry.portalMgr.GetResIdByCID(msg.stAntiPiracy.szToyCID);
							if (toyBindDataList !=null)
								toyBindDataList.Add(toyBindData);
						}
						//����������ߵǳ�����
						CoreEntry.portalMgr.OnToyPortalAction(msg.stAntiPiracy.szToyCID, false, false);
						OnToyBindRecover();
					}
					break;
				case cs.csMacros.CS_CODE_WEAPON_UNMATCH_HERO:
					CoreEntry.uiMgr.TipsString(CoreEntry.gameDataMgr.GetString((int)msg.stAntiPiracy.nEno));
					break;
				case cs.csMacros.CS_CODE_ANTI_PIRACY_NEED_AUTH:
					{
						//��ʾ�����������ż
						//����˳������ſյ���
						currentToyData = msg.stAntiPiracy;
						CoreEntry.portalMgr.OnToyPortalAction(msg.stAntiPiracy.szToyCID, false, false);
						//Create ToyVerifyPanel
						var panelBase = CoreEntry.uiMgr.GetPanelSimply("ToyVerifyPanel");
						var toyVerifyPanel = panelBase.GetComponent<ToyVerifyPanel>();
						toyVerifyPanel.SetData(msg.stAntiPiracy.szToyCID);

					}
					break;
				case cs.csMacros.CS_CODE_ANTI_PIRACY_AUTH_PASSWD_UNMATCH:
					{
						//������֤ʧ��
						var toyType = PortalMgr.GetToyType(msg.stAntiPiracy.szToyCID);
						int num = 0;
						switch (toyType)
						{
							case ToyType.TOY_HERO:
								num = msg.stAntiPiracy.stData.stAuth.wCheckFailedCountCurDay;
								break;
							case ToyType.TOY_PET:
								num = msg.stAntiPiracy.stData.stPetCaptureAuth.wCheckFailedCountCurDay;
								break;
							case ToyType.TOY_WEAPON:
								num = msg.stAntiPiracy.stData.stWeaponAuth.wCheckFailedCountCurDay;
								break;
							case ToyType.TOY_NONE:
								break;
						}
						CoreEntry.eventMgr.TriggerEvent<int>("ToyPasswdUnmatch", num);
					}
					break;
				case cs.csMacros.CS_CODE_ANTI_PIRACY_TOY_BIDN_BY_OTHER:
					{
						//�ѱ��󶨣�������ʾ��Ϣ
						//TODO:�ع�
						string tempBindConfirm = CoreEntry.gameDataMgr.GetString("BindConfirm4");
						string toyBindConfirm = "";
						var toyType = PortalMgr.GetToyType(msg.stAntiPiracy.szToyCID);
						string toyTypeString = CoreEntry.gameDataMgr.GetString(toyType.ToString());
						toyBindConfirm = String.Format(tempBindConfirm, toyTypeString);
						AdjustToyBindStack("ConfirmPanel2", true);
						var confirmPanel2 = CoreEntry.uiMgr.GetPanel("ConfirmPanel2");
						if (confirmPanel2 == null)
						{
							var panel = CoreEntry.uiMgr.PopConfirm2(toyBindConfirm,
								() =>
								{
									CoreEntry.portalMgr.OnToyPortalAction(msg.stAntiPiracy.szToyCID, false, false);
									AdjustToyBindStack("ConfirmPanel2", false);
								});
							soundId = CoreEntry.soundMgr.PlaySound("voice_zhiyin_group_21", "Play_voice_zhiyin_155", panel.gameObject);
						}
					}
					break;
				case cs.csMacros.CS_CODE_ANTI_PIRACY_TOY_BIND_FAIL:
					{
						//��ż��ʧ��
						CoreEntry.portalMgr.OnToyPortalAction(msg.stAntiPiracy.szToyCID, false, false);
						CoreEntry.uiMgr.TipsString(CoreEntry.gameDataMgr.GetString("BindConfirm5"));
					}
					break;
				case cs.csMacros.CS_CODE_ANTI_PIRACY_TOY_CID_UNMATCHED:
					{
						//��������ݿ���CID��ƥ��
						CoreEntry.uiMgr.PopConfirm2(CoreEntry.gameDataMgr.GetString("BindConfirm7"), null);
					}
					break;
				case cs.csMacros.CS_CODE_ANTI_PIRACY_AUTH_FAILED_TOO_MUCH:
					{
						//���հ󶨴�������
						CoreEntry.eventMgr.TriggerEvent<int>("ToyPasswdUnmatch", ToyVerifyPanel.PSW_NUM);
					}
					break;
				default:
					CoreEntry.uiMgr.TipsString(CoreEntry.gameDataMgr.GetString((int)msg.stAntiPiracy.nEno));
					break;
			}
		}
		return GameServer.HandleState.Stay;
	}

	public byte[] GetCurrentToyBindCID()
	{
		//��õ�ǰ���ڰ󶨽׶ε�CID
		if (currentToyData == null) return null;
		return currentToyData.szToyCID;
	}

	public void OnToyBindRecover()
	{
		//��һ�η��������ʱ������Ӣ��->��׽��->������˳�����߽���˳���
		//�÷���˳����AiToyMgr�����������Դ˴����յ���CID������ģ������ٴ�����
		//�״ΰ���Ҫȷ��

		if (CoreEntry.toyMgr.GameMode == GameMode.VIRTUAL)
			return;
		//���ڰ������в�Recover
		if (isBindingToy == true)
			return;
		//��������в�Recover
		if (SceneLoadMgr.instance.IsLoadingScene())
			return;
		//���б�Ϊ�ղ�Recover
		if (toyBindDataList == null || toyBindDataList.Count == 0)
			return;
		//�ѵǳ������
		if (CoreEntry.toyMgr.HasLogout == true)
			return;
		currentToyData = toyBindDataList[0].toyData;
		bool isGuestMode = CoreEntry.gameServer.IsGuestPlatform();
		if (!isGuestMode)
		{
			//���ο�ģʽ��΢��/QQ���״ΰ�ȷ������
			string textString = CoreEntry.gameDataMgr.GetString("BindConfirm6");
			ToyType type = PortalMgr.GetToyType(currentToyData.szToyCID);
			string notice = PlayerDataMgr.instance.GetToyStringByType(textString, type);
			string cancelText = CoreEntry.gameDataMgr.GetString("BindConfirmBtn1");
			string confirmText = CoreEntry.gameDataMgr.GetString("BindConfirmBtn2");
			CoreEntry.uiMgr.PopToyBindPreConfirm(notice, confirmText, cancelText,
				() =>
				{
					SendBindInfoToServer();
				}, null);
		}
		else
		{
			//�ο�ģʽ���״ΰ�ȷ������
			var roleData = PlayerDataMgr.instance.GetRoleData();
			string summonerName = "";
			if (roleData != null)
			{
				summonerName = CoreEntry.gameDataMgr.GetString("BindConfirmGuest") + GlobalFunctions.TrimByteToString(roleData.szNickname);
			}
			var confirmPanel = CoreEntry.uiMgr.PopToyBindPreConfirmGuest(CoreEntry.gameDataMgr.GetString("BindConfirm8"),
				CoreEntry.gameDataMgr.GetString("BindConfirmGuestTitle"),
				summonerName,
				CoreEntry.gameDataMgr.GetString("BindConfirmBtn1Guest"),
				CoreEntry.gameDataMgr.GetString("BindConfirmBtn2Guest"),
				() =>
				{
					LoginMgr.instance.OnLogout();
				},
				() =>
				{
					SendBindInfoToServer();
				}
				);
			confirmPanel.SetTextAlignment(ConfirmPanel.TextType.TOP, NGUIText.Alignment.Left);
			confirmPanel.SetTextAlignment(ConfirmPanel.TextType.CENTER, NGUIText.Alignment.Left);
			confirmPanel.SetTextAlignment(ConfirmPanel.TextType.BOTTOM, NGUIText.Alignment.Left);

		}

	}

	public void Clear()
	{
		toyBindDataList.Clear();
	}
	public void AdjustToyBindStack(string panelName, bool isCreate)
	{
		//��������ż�󶨿�ʼ������ConfirmPanel��ʾ�����Լ�����ż�󶨽���ʱ�ɷ��¼�
		if (isCreate)
		{
			if (!bindPanelStack.Contains(panelName))
			{
				bindPanelStack.Add(panelName);
			}
			if (bindPanelStack.Count == 1)
			{
				isBindingToy = true;
				CoreEntry.eventMgr.TriggerEvent("BindToy", true);
			}
		}
		else
		{
			if (bindPanelStack.Contains(panelName))
			{
				bindPanelStack.Remove(panelName);
			}
			if (bindPanelStack.Count == 0)
			{
				isBindingToy = false;
				CoreEntry.eventMgr.TriggerEvent("BindToy", false);
			}
		}

	}

	public ToyBindData GetToyBindDataByCID(byte[] cid)
	{
		//ͨ��cid����toyBindDataList����Ϣ
		if (toyBindDataList == null) return null;
		foreach (ToyBindData bindData in toyBindDataList)
		{
			if (bindData == null) continue;
			if (PortalMgr.CompareCID(bindData.toyData.szToyCID, cid))
			{
				return bindData;
			}
		}
		return null;
	}

	public void RemoveToyBindDataByCID(byte[] cid)
	{
		//ͨ��cidɾ��toyBindDataList����Ϣ
		//1����ż�󶨳ɹ�֮��������ذ���ɾ��
		//2���������������żʱ��ɾ��
		if (toyBindDataList == null) return;
		for (int i = 0; i < toyBindDataList.Count; i++)
		{
			var toyBindData = toyBindDataList[i];
			if (toyBindData == null) continue;
			if (PortalMgr.CompareCID(toyBindData.toyData.szToyCID, cid))
			{
				toyBindDataList.Remove(toyBindData);
			}
		}

	}

	private void SendBindInfoToServer()
	{
		//�״ΰ�ʱ����
		if (currentToyData == null) return;
		var msg = new cs.CSCltPkgBody();
		msg.construct((long)cs.CS_CMD_TYPE.CS_CMD_ANTI_PIRACY);
		msg.stAntiPiracy.construct();
		var toyType = PortalMgr.GetToyType(currentToyData.szToyCID);
		switch (toyType)
		{
			case ToyType.TOY_HERO:
				msg.stAntiPiracy.bOp = (byte)cs.CS_ANTI_PIRACY_OP_GROUP.CS_ANTI_PIRACY_OP_BIND;
				msg.stAntiPiracy.stData.construct((long)cs.CS_ANTI_PIRACY_OP_GROUP.CS_ANTI_PIRACY_OP_BIND);
				break;
			case ToyType.TOY_PET:
				msg.stAntiPiracy.bOp = (byte)cs.CS_ANTI_PIRACY_OP_GROUP.CS_PET_CAPTURE_OP_BIND;
				msg.stAntiPiracy.stData.construct((long)cs.CS_ANTI_PIRACY_OP_GROUP.CS_PET_CAPTURE_OP_BIND);
				break;
			case ToyType.TOY_WEAPON:
				msg.stAntiPiracy.bOp = (byte)cs.CS_ANTI_PIRACY_OP_GROUP.CS_WEAPON_OP_BIND;
				msg.stAntiPiracy.stData.construct((long)cs.CS_ANTI_PIRACY_OP_GROUP.CS_WEAPON_OP_BIND);
				break;
			case ToyType.TOY_NONE:
				CoreEntry.logMgr.Log(LogLevel.INFO, "FirstBindToy0", "ToyTypeError");
				return;
				break;
		}
		var CIDString = GlobalFunctions.TrimByteToString(currentToyData.szToyCID);
		CoreEntry.logMgr.Log(LogLevel.INFO, "FirstBindToy1", CIDString);
		msg.stAntiPiracy.szToyCID = currentToyData.szToyCID;
		var CIDstring2 = GlobalFunctions.TrimByteToString(msg.stAntiPiracy.szToyCID);
		CoreEntry.logMgr.Log(LogLevel.INFO, "FirstBindToy2", CIDstring2);
		msg.stAntiPiracy.szToyUID = currentToyData.szToyUID;
		CoreEntry.gameServer.SendMsg(cs.CS_CMD_TYPE.CS_CMD_ANTI_PIRACY, msg);
	}

	public void ToyAuthNotifyServer(string inputkeyCode)
	{
		if (currentToyData == null) return;
		string thisKeyCode = "";
		if (inputkeyCode != null && inputkeyCode.Length > 8)
		{
			thisKeyCode = inputkeyCode.Substring(0, 8);
		}
		else
		{
			thisKeyCode = inputkeyCode;
		}
		var msg = new cs.CSCltPkgBody();
		msg.construct((long)cs.CS_CMD_TYPE.CS_CMD_ANTI_PIRACY);
		msg.stAntiPiracy.construct();
		var CIDString = GlobalFunctions.TrimByteToString(currentToyData.szToyCID);
		CoreEntry.logMgr.Log(LogLevel.INFO, "VerifyToy1", CIDString);


#if true
		var toyType = PortalMgr.GetToyType(currentToyData.szToyCID);
		switch (toyType)
		{
			case ToyType.TOY_HERO:
				msg.stAntiPiracy.bOp = (byte)cs.CS_ANTI_PIRACY_OP_GROUP.CS_ANTI_PIRACY_OP_AUTH;
				msg.stAntiPiracy.szToyCID = currentToyData.szToyCID;
				msg.stAntiPiracy.szToyUID = currentToyData.szToyUID;
				msg.stAntiPiracy.stData.construct((long)cs.CS_ANTI_PIRACY_OP_GROUP.CS_ANTI_PIRACY_OP_AUTH);
				msg.stAntiPiracy.stData.stAuth.szCKey = System.Text.Encoding.UTF8.GetBytes(thisKeyCode);
				break;
			case ToyType.TOY_PET:
				msg.stAntiPiracy.bOp = (byte)cs.CS_ANTI_PIRACY_OP_GROUP.CS_PET_CAPTURE_OP_AUTH;
				msg.stAntiPiracy.szToyCID = currentToyData.szToyCID;
				msg.stAntiPiracy.szToyUID = currentToyData.szToyUID;
				msg.stAntiPiracy.stData.construct((long)cs.CS_ANTI_PIRACY_OP_GROUP.CS_PET_CAPTURE_OP_AUTH);
				msg.stAntiPiracy.stData.stPetCaptureAuth.szCKey = System.Text.Encoding.UTF8.GetBytes(thisKeyCode);
				break;
			case ToyType.TOY_WEAPON:
				msg.stAntiPiracy.bOp = (byte)cs.CS_ANTI_PIRACY_OP_GROUP.CS_WEAPON_OP_AUTH;
				msg.stAntiPiracy.szToyCID = currentToyData.szToyCID;
				msg.stAntiPiracy.szToyUID = currentToyData.szToyUID;
				msg.stAntiPiracy.stData.construct((long)cs.CS_ANTI_PIRACY_OP_GROUP.CS_WEAPON_OP_AUTH);
				msg.stAntiPiracy.stData.stWeaponAuth.szCKey = System.Text.Encoding.UTF8.GetBytes(thisKeyCode);
				break;
			case ToyType.TOY_NONE:
				break;
		}
#endif
		CoreEntry.gameServer.SendMsg(cs.CS_CMD_TYPE.CS_CMD_ANTI_PIRACY, msg);
	}
	private void OnEndChangePlayerEveryTime(string gameEvent)
	{
		//֮ǰ��������ע���endChangePlayer�¼�,��˺��������ֿ�
		//�л�Ӣ�۽�����鿴���Ƿ���Ҫ�󶨵���ߣ��ڵ���δ�ÿ���
		OnToyBindRecover();
	}

}
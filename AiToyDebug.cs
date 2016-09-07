using UnityEngine;
using System.Collections;

public class AiToyDebug
{  
	//static public bool EnableInfoLog = true;  
	//static public bool EnableWarningLog = false; 
	//static public bool EnableErrorLog = false; 

	static public void Log(object message)  
	{  
		Log(message,null);  
	} 

	static public void Log(object message, Object context)  
	{  
		if(LogMgr.m_openLevel <= (int)LogLevel.DEBUG)  
		{  
			Debug.Log(message,context);  
		}  
	}  

	static public void LogError(object message)  
	{
	    if (LogMgr.m_openLevel <= (int)LogLevel.ERROR)
	    {
	        LogError(message, null);
	    }
	}  

	static public void LogError(object message, Object context)  
	{
        if (LogMgr.m_openLevel <= (int)LogLevel.ERROR)
        {
			Debug.LogError(message,context);  
		}  
	} 

	static public void LogWarning(object message)  
	{
        if (LogMgr.m_openLevel <= (int)LogLevel.WARNING)
            LogWarning(message,null);  
	}  

	static public void LogWarning(object message, Object context)  
	{
        if (LogMgr.m_openLevel <= (int)LogLevel.WARNING)
        {
            Debug.LogWarning(message,context);  
		}  
	}  
}  

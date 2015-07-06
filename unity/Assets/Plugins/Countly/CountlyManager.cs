/*
 * Copyright (c) 2014 Mario Freitas (imkira@gmail.com)
 *
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so, subject to
 * the following conditions:
 *
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
 * LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
 * WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace Countly
{
  public class Manager : MonoBehaviour
  {
    public string appHost = "https://cloud.count.ly";
    public string appKey;
    public bool allowDebug = false;
    public float updateInterval = 60f;
    public int eventSendThreshold = 10;
    public int queueLimit = 1024;
    public bool queueUsesStorage = true;

    public const string SDK_VERSION = "2.0";

    protected DeviceInfo _deviceInfo = null;

    protected bool _isReady = false;
    protected bool _isRunning = false;
    protected bool _isSuspended = true;
    protected double _sessionLastSentAt = 0.0;
    protected double _unsentSessionLength = 0f;

    protected StringBuilder _connectionStringBuilder = null;
    protected bool _isProcessingConnection = false;
    protected Queue _connectionQueue = null;
    protected Queue ConnectionQueue
    {
      get
      {
        if (_connectionQueue == null)
        {
          _connectionQueue = new Queue(128, queueLimit, queueUsesStorage);
        }
        return _connectionQueue;
      }
    }

    protected StringBuilder _eventStringBuilder = null;
    protected List<Event> _eventQueue = null;
    protected List<Event> EventQueue
    {
      get
      {
        if (_eventQueue == null)
        {
          _eventQueue = new List<Event>(16);
        }
        return _eventQueue;
      }
    }

    public void Init(string appKey)
    {
      if (string.IsNullOrEmpty(appKey) == true)
      {
        return;
      }

      this.appKey = appKey;

      if ((_isRunning == true) ||
          (_isReady == false))
      {
        return;
      }

      Log("Initialize: " + appKey);

      _isRunning = true;
      Resume();
      StartCoroutine(RunTimer());
    }

    public void RecordEvent(Event e)
    {
      bool wasEmpty = (ConnectionQueue.Count <= 0);

      EventQueue.Add(e);
      FlushEvents(eventSendThreshold);

      if (wasEmpty == true)
      {
        ProcessConnectionQueue();
      }
    }

#region Unity Methods
    protected void Start()
    {
     	 _isReady = true;
     	 Init(appKey);
    }

    protected void OnApplicationPause(bool pause)
    {
      if (_isRunning == false)
      {
        return;
      }

      if (pause == true)
      {
        Log("OnApplicationPause -> Background");
        Suspend();
      }
      else
      {
        Log("OnApplicationPause -> Foreground");
        Resume();
      }
    }

    protected void OnApplicationQuit()
    {
      if (_isRunning == false)
      {
        return;
      }

      Log("OnApplicationQuit");
      Suspend();
    }
#endregion

#region Session Methods
    protected void BeginSession()
    {
      DeviceInfo info = GetDeviceInfo();
      StringBuilder builder = InitConnectionDataStringBuilder();

      // compute metrics
      info.JSONSerializeMetrics(builder);
      string metricsString = builder.ToString();

      builder = InitConnectionData(info);

      builder.Append("&sdk_version=");
      AppendConnectionData(builder, SDK_VERSION);

      builder.Append("&begin_session=1");

      builder.Append("&metrics=");
      AppendConnectionData(builder, metricsString);

      ConnectionQueue.Enqueue(builder.ToString());
      ProcessConnectionQueue();
    }

    protected void UpdateSession(long duration)
    {
      DeviceInfo info = GetDeviceInfo();
      StringBuilder builder = InitConnectionData(info);

      builder.Append("&session_duration=");
      AppendConnectionData(builder, duration.ToString());

      ConnectionQueue.Enqueue(builder.ToString());
      ProcessConnectionQueue();
    }

    protected void EndSession(long duration)
    {
      DeviceInfo info = GetDeviceInfo();
      StringBuilder builder = InitConnectionData(info);

      builder.Append("&end_session=1");

      builder.Append("&session_duration=");
      AppendConnectionData(builder, duration.ToString());
			Log ("Requesting session end");
		try {
				WebRequest www = WebRequest.Create(appHost + "/i?" +builder.ToString());
				www.GetResponse().Close();

			}
		catch (System.Exception e) {
				Log (string.Format("Request failed: {0}", e));
			}
    }

    protected void RecordEvents(List<Event> events)
    {
      DeviceInfo info = GetDeviceInfo();
      StringBuilder builder = InitConnectionData(info);

      builder.Append("&events=");
      string eventsString = JSONSerializeEvents(events);
      AppendConnectionData(builder, eventsString);

      ConnectionQueue.Enqueue(builder.ToString());
    }

#endregion

    protected IEnumerator RunTimer()
    {
      while (true)
      {
        yield return new WaitForSeconds(updateInterval);

        if (_isSuspended == true)
        {
          continue;
        }

        // device info may have changed
        UpdateDeviceInfo();

        // record any pending events
        FlushEvents(0);

        long duration = TrackSessionLength();
        UpdateSession(duration);
      }
    }

    protected void Resume()
    {
      // already in unsuspeded state?
      if (_isSuspended == false)
      {
        return;
      }

      Log("Resuming...");

      _isSuspended = false;
      _sessionLastSentAt = Utils.GetCurrentTime();

      // device info may have changed
      UpdateDeviceInfo();

      BeginSession();
    }

    protected void Suspend()
    {
      // already in suspended state?
      if (_isSuspended == true)
      {
        return;
      }

      Log("Suspending...");

      _isSuspended = true;

      // device info may have changed
      UpdateDeviceInfo();

      // record any pending events
      FlushEvents(0);

      long duration = TrackSessionLength();
      EndSession(duration);
    }

#region Utility Methods

	protected void ProcessConnectionQueue()
	{
		if ((_isProcessingConnection == true) ||
		    (ConnectionQueue.Count <= 0))
		{
			return;
		}
			
		_isProcessingConnection = true;
		
		StartCoroutine(_ProcessConnectionQueue());
	}

    protected IEnumerator _ProcessConnectionQueue()
    {
	  int retry = 0;
      while (ConnectionQueue.Count > 0)
      {
        string data = ConnectionQueue.Peek();
        string urlString = appHost + "/i?" + data;

        Log("Request started: " + urlString);

        WWW www = new WWW(urlString)
        {
          threadPriority = ThreadPriority.Low
        };

        yield return www;

        if (string.IsNullOrEmpty(www.error) == false && retry < 5)
        {
          	Log("Request failed: " + www.error);
			retry++;
         	break;
        }
		
        ConnectionQueue.Dequeue();
				if (retry >=5) {
					 retry = 0;
       				 Log("Request failed after 5 retries");
				}
				else {
					retry = 0;
					Log("Request successful");
				}
      }

      _isProcessingConnection = false;
    }

    protected DeviceInfo GetDeviceInfo()
    {
      if (_deviceInfo == null)
      {
        _deviceInfo = new DeviceInfo();
      }
      return _deviceInfo;
    }

    protected DeviceInfo UpdateDeviceInfo()
    {
      DeviceInfo info = GetDeviceInfo();
      info.Update();
      return info;
    }

    protected void FlushEvents(int threshold)
    {
      List<Event> eventQueue = EventQueue;


      // satisfy minimum number of eventQueue
      if ((eventQueue.Count <= 0) ||
          (eventQueue.Count < threshold))
      {
        return;
      }

      RecordEvents(eventQueue);
      eventQueue.Clear();
    }

    protected long TrackSessionLength()
    {
      double now = Utils.GetCurrentTime();

      if (now > _sessionLastSentAt)
      {
        _unsentSessionLength += now - _sessionLastSentAt;
      }

      // duration should be integer
      long duration = (long)_unsentSessionLength;

      // sanity check
      if (duration < 0)
      {
        duration = 0;
      }

      // keep decimal part
      _unsentSessionLength -= duration;

      return duration;
    }

    protected StringBuilder InitConnectionDataStringBuilder()
    {
      if (_connectionStringBuilder == null)
      {
        _connectionStringBuilder = new StringBuilder(1024);
      }
      else
      {
        _connectionStringBuilder.Length = 0;
      }

      return _connectionStringBuilder;
    }

    protected StringBuilder InitConnectionData(DeviceInfo info)
    {
      StringBuilder builder = InitConnectionDataStringBuilder();

      builder.Append("app_key=");
      AppendConnectionData(builder, appKey);
	
      builder.Append("&device_id=");
      AppendConnectionData(builder, info.UDID);

      builder.Append("&timestamp=");

      long timestamp = (long)Utils.GetCurrentTime();
      builder.Append(timestamp);

      return builder;
    }

    protected void AppendConnectionData(StringBuilder builder, string val)
    {
      if (string.IsNullOrEmpty(val) != true)
      {
        builder.Append(Utils.EscapeURL(val));
      }
    }

    protected StringBuilder InitEventStringBuilder()
    {
      if (_eventStringBuilder == null)
      {
        _eventStringBuilder = new StringBuilder(1024);
      }
      else
      {
        _eventStringBuilder.Length = 0;
      }

      return _eventStringBuilder;
    }

    protected string JSONSerializeEvents(List<Event> events)
    {
      StringBuilder builder = InitEventStringBuilder();

      // open array of events
      builder.Append("[");

      bool first = true;

      foreach (Event e in events)
      {
        if (first == true)
        {
          first = false;
        }
        else
        {
          builder.Append(",");
        }

        e.JSONSerialize(builder);
      }

      // close array of events
      builder.Append("]");
			Log(builder.ToString());
      return builder.ToString();
    }

    protected void Log(string str)
    {
      if (allowDebug == true)
      {
        Debug.Log(str);
      }
    }
#endregion
  }
}

public class CountlyManager : Countly.Manager
{
  protected static Countly.Manager _instance = null;
  public static Countly.Manager Instance
  {
    get
    {
      if (_instance == null)
      {
        GameObject singleton = GameObject.Find("CountlyManager");

        if (singleton != null)
        {
          _instance = singleton.GetComponent<Countly.Manager>();
        }
      }

      return _instance;
    }
  }

  protected void Awake()
  {
    if ((_instance != this) && (_instance != null))
    {
      Log("Duplicate manager detected. Destroying...");
      Destroy(gameObject);
      return;
    }

    _instance = this;
    DontDestroyOnLoad(gameObject);
  }

  public static new void Init(string appKey = null)
  {
    Countly.Manager instance = Instance;

    if (instance != null)
    {
      if (appKey == null)
      {
        appKey = instance.appKey;
      }
      instance.Init(appKey);
    }
  }

  public static void Emit(string key, long count)
  {
    Countly.Manager instance = Instance;

    if (instance != null)
    {
      Countly.Event e = new Countly.Event();

      e.Key = key;
      e.Count = count;

      instance.RecordEvent(e);
    }
  }

  public static void Emit(string key, long count, double sum)
  {
    Countly.Manager instance = Instance;

    if (instance != null)
    {
      Countly.Event e = new Countly.Event();

      e.Key = key;
      e.Count = count;
      e.Sum = sum;

      instance.RecordEvent(e);
    }
  }

  public static void Emit(string key, long count,
      Dictionary<string, string> segmentation)
  {
    Countly.Manager instance = Instance;

    if (instance != null)
    {
      Countly.Event e = new Countly.Event();

      e.Key = key;
      e.Count = count;
      e.Segmentation = segmentation;

      instance.RecordEvent(e);
    }
  }

  public static void Emit(string key, long count, double sum,
      Dictionary<string, string> segmentation)
  {
    Countly.Manager instance = Instance;

    if (instance != null)
    {
      Countly.Event e = new Countly.Event();

      e.Key = key;
      e.Count = count;
      e.Sum = sum;
      e.Segmentation = segmentation;

      instance.RecordEvent(e);
    }
  }

  public static void Emit(Countly.Event e)
  {
    Countly.Manager instance = Instance;

    if (instance != null)
    {
      instance.RecordEvent(e);
    }
  }
}

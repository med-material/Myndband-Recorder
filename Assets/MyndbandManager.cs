using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using Jayrock.Json;
using Jayrock.Json.Conversion;

/*
 * To Subscribe to data from the MyndbandManager in your class, use fx :
 * MyndbandManager.UpdateRawdataEvent += OnUpdateRawDataEvent;
 * (for raw EEG data)
 */

public struct MyndbandEvent {
    public DateTime systemTime;
    public string packet;
}

public class StateObject
{
    // Client  socket.
    public Socket workSocket = null;
    // Size of receive buffer.
    public const int BufferSize = 1024;
    // Receive buffer.
    public byte[] buffer = new byte[BufferSize];
    // Received data string.
    public StringBuilder sb = new StringBuilder();
}

public class MyndbandManager : MonoBehaviour
{

	private TcpClient client; 
  	//private Stream stream;
  	private byte[] buffer;

    public enum MyndbandState {
        Disconnected,
        Connecting,
        Connected,
        ReceivingData
    }

    public enum MyndbandSignal {
        NoSignal,
        VeryPoor,
        Poor,
        Medium,
        Good,
        Perfect
    }
	public delegate void UpdateIntValueDelegate(int value);
	public delegate void UpdateFloatValueDelegate(float value);

	public static event UpdateIntValueDelegate UpdatePoorSignalEvent;
	public static event UpdateIntValueDelegate UpdateAttentionEvent;
	public static event UpdateIntValueDelegate UpdateMeditationEvent;
	public static event UpdateIntValueDelegate UpdateRawdataEvent;
	public static event UpdateIntValueDelegate UpdateBlinkEvent;
	
	public static event UpdateFloatValueDelegate UpdateDeltaEvent;
	public static event UpdateFloatValueDelegate UpdateThetaEvent;
	public static event UpdateFloatValueDelegate UpdateLowAlphaEvent;
	public static event UpdateFloatValueDelegate UpdateHighAlphaEvent;
	public static event UpdateFloatValueDelegate UpdateLowBetaEvent;
	public static event UpdateFloatValueDelegate UpdateHighBetaEvent;
	public static event UpdateFloatValueDelegate UpdateLowGammaEvent;
	public static event UpdateFloatValueDelegate UpdateHighGammaEvent;

    private MyndbandState myndbandState;
    private MyndbandSignal myndbandSignal;

    private int signalStrength;
    private int attention1;
    private int meditation1;
	
	private float delta;
    private System.Object rawData = ""; 

    [Serializable]
    public class OnMyndbandStateChanged : UnityEvent<string, string> { }
    private List<MyndbandEvent> packetList;
    private List<MyndbandEvent> rawEegPacketList;
    private List<MyndbandEvent> myndbandPacketList;

    private List<MyndbandEvent> loggedRawEegList;
    private List<MyndbandEvent> loggedmyndbandPacketList;

    public OnMyndbandStateChanged onMyndbandStateChanged;


    private bool isLogging = false;

    private IDictionary rawEegData;
    private IDictionary myndbandData;

    private Socket clientSocket;

    private static MyndbandManager instance;
    private NetworkStream stream;

    // how often should we check for new data (smaller values may cause performance issues)
    public float updateFrequency = 0.5f;

    void Start()
    {
        if (instance == null) {
            instance = this;
        }
        DontDestroyOnLoad(this);
        rawEegPacketList = new List<MyndbandEvent>();
        loggedRawEegList = new List<MyndbandEvent>();
        myndbandPacketList = new List<MyndbandEvent>();
        loggedmyndbandPacketList = new List<MyndbandEvent>();
        myndbandState = MyndbandState.Disconnected;
        onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), "");
        StartCoroutine("ConnectToMyndband");
    }

    public void StartLogging() {
        loggedRawEegList = new List<MyndbandEvent>();
        loggedmyndbandPacketList = new List<MyndbandEvent>();
        isLogging = true;
    }

    public void StopLogging() {
        isLogging = false;
    }
    public Dictionary<string,List<string>> GetLoggedRawEEG() {
        var logCollection = new Dictionary<string, List<string>>();
		logCollection["SystemTime"] = new List<string>();
		logCollection["RawEEG"] = new List<string>();
        var loggedEvents = new MyndbandEvent[loggedRawEegList.Count];
        loggedRawEegList.CopyTo(loggedEvents);
		foreach (MyndbandEvent bandEvent in loggedEvents) {
			var myndbandData = (IDictionary) JsonConvert.Import(typeof(IDictionary), bandEvent.packet);
			if (myndbandData.Contains("rawEeg")) {
				logCollection["RawEEG"].Add(myndbandData["rawEeg"].ToString());
				logCollection["SystemTime"].Add(bandEvent.systemTime.ToString("yyyy-MM-dd HH:mm:ss.ffff"));
			}
        }
        return logCollection;
    }
    public Dictionary<string,List<string>> GetLoggedMyndband() {
        var logCollection = new Dictionary<string, List<string>>();
        logCollection["SystemTime"] = new List<string>();
        logCollection["Delta"] = new List<string>();
        logCollection["Theta"] = new List<string>();
        logCollection["AlphaLow"] = new List<string>();
        logCollection["AlphaHigh"] = new List<string>();
        logCollection["BetaLow"] = new List<string>();
        logCollection["BetaHigh"] = new List<string>();
        logCollection["GammaLow"] = new List<string>();
        logCollection["GammaHigh"] = new List<string>();
        var loggedEvents = new MyndbandEvent[loggedmyndbandPacketList.Count];
        loggedmyndbandPacketList.CopyTo(loggedEvents);
        foreach(MyndbandEvent bandEvent in loggedEvents) {
			var myndbandData = (IDictionary) JsonConvert.Import(typeof(IDictionary), bandEvent.packet);
			if (myndbandData.Contains("eegPower")) {
                IDictionary eegPowers = (IDictionary)myndbandData["eegPower"];
				logCollection["Delta"].Add(eegPowers["delta"] != null ? eegPowers["delta"].ToString() : "NA");
                logCollection["Theta"].Add(eegPowers["theta"].ToString());
                logCollection["AlphaLow"].Add(eegPowers["lowAlpha"].ToString());
                logCollection["AlphaHigh"].Add(eegPowers["highAlpha"].ToString());  
                logCollection["BetaLow"].Add(eegPowers["lowBeta"].ToString());
                logCollection["BetaHigh"].Add(eegPowers["highBeta"].ToString());
                logCollection["GammaLow"].Add(eegPowers["lowGamma"].ToString());
                logCollection["GammaHigh"].Add(eegPowers["highGamma"].ToString());
				logCollection["SystemTime"].Add(bandEvent.systemTime.ToString("yyyy-MM-dd HH:mm:ss.ffff"));
			}
        }
        return logCollection;
    }

    private IEnumerator ConnectToMyndband() {
        clientSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
        while (clientSocket == null || !clientSocket.Connected) {
            myndbandState = MyndbandState.Connecting;
            onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), "Establishing connection to ThinkGear Socket..");     
            yield return new WaitForSeconds(0.5f);
            try {
                clientSocket.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13854));
            } catch(SocketException e)
            {
                UnityEngine.Debug.LogError(e.Message);
                myndbandState = MyndbandState.Disconnected;
                onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), "Could not connect to the ThinkGear Socket..");
            }
            if (myndbandState == MyndbandState.Disconnected) {
                yield return new WaitForSeconds(2f);
            }
        }
        onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), "Waiting for data..Make sure that Myndband is paired with PC.");     
        byte[] myWriteBuffer = Encoding.ASCII.GetBytes(@"{""enableRawOutput"": true, ""format"": ""Json""}");
        SendData(myWriteBuffer);
        stream = new NetworkStream(clientSocket, true);
        StateObject state = new StateObject();
        state.workSocket = clientSocket;
        stream.BeginRead(state.buffer, 0, StateObject.BufferSize, new AsyncCallback(ReceiveCallback), state);
        StartCoroutine("ParseData");
        yield return null;
    }

    private IEnumerator ParseData() {
        while (true) {
            if (rawEegPacketList.Count > 0) {
                if (myndbandState == MyndbandState.Connecting) {
                    myndbandState = MyndbandState.ReceivingData;
                }
                rawEegData = null;
                try {
                    rawEegData = (IDictionary) JsonConvert.Import(typeof(IDictionary), rawEegPacketList.Last().packet);
                } catch (System.Exception e) {
                }
                if (rawEegData != null) {
                        if (myndbandState != MyndbandState.ReceivingData) {
                            myndbandState = MyndbandState.ReceivingData;
                            StartCoroutine("UpdateSignalStrength");   
                        }
                        if (UpdateRawdataEvent != null) {
                            UpdateRawdataEvent(int.Parse(rawEegData["rawEeg"].ToString()));
                        }
                }
            }
            if (myndbandPacketList.Count > 0) {
                myndbandData = null;
                try {
                    myndbandData = (IDictionary) JsonConvert.Import(typeof(IDictionary), myndbandPacketList.Last().packet);
                } catch (System.Exception e) {
                }

                if (myndbandData != null) {
                    if (myndbandData.Contains("status")){
                        if (myndbandState != MyndbandState.Connected && myndbandState != MyndbandState.ReceivingData) {
                            myndbandState = MyndbandState.Connected;
                            string subStatus = "Socket is " + myndbandData["status"].ToString() + "..Make sure that Myndband is paired with PC.";                         
                            onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), subStatus);
                        }
                    }
                    if (myndbandData.Contains("poorSignalLevel")) {
                        signalStrength = int.Parse(myndbandData["poorSignalLevel"].ToString());
                        if (UpdatePoorSignalEvent != null) {
                            UpdatePoorSignalEvent(int.Parse(myndbandData["poorSignalLevel"].ToString()));
                        }
                                
                        if (myndbandData.Contains("eSense")){
                            IDictionary eSense = (IDictionary)myndbandData["eSense"];
                            if(UpdateAttentionEvent != null){ UpdateAttentionEvent(int.Parse(eSense["attention"].ToString())); }		
                            if(UpdateMeditationEvent != null){ UpdateMeditationEvent(int.Parse(eSense["meditation"].ToString())); }
                        }
            
                        if (myndbandData.Contains("eegPower")){
                            IDictionary eegPowers = (IDictionary)myndbandData["eegPower"];
                            if(UpdateDeltaEvent != null){ UpdateDeltaEvent(float.Parse(eegPowers["delta"].ToString()));	}
                            if(UpdateThetaEvent != null){ UpdateThetaEvent(float.Parse(eegPowers["theta"].ToString()));	}
                            if(UpdateLowAlphaEvent != null){ UpdateLowAlphaEvent(float.Parse(eegPowers["lowAlpha"].ToString())); }
                            if(UpdateHighAlphaEvent != null){ UpdateHighAlphaEvent(float.Parse(eegPowers["highAlpha"].ToString())); }
                            if(UpdateLowBetaEvent != null){ UpdateLowBetaEvent(float.Parse(eegPowers["lowBeta"].ToString())); }
                            if(UpdateHighBetaEvent != null){ UpdateHighBetaEvent(float.Parse(eegPowers["highBeta"].ToString())); }
                            if(UpdateLowGammaEvent != null){ UpdateLowGammaEvent(float.Parse(eegPowers["lowGamma"].ToString())); }
                            if(UpdateHighGammaEvent != null){ UpdateHighGammaEvent(float.Parse(eegPowers["highGamma"].ToString())); }
                        }

                    } else if (myndbandData.Contains("blinkStrength") && UpdateBlinkEvent != null) {
                            UpdateBlinkEvent(int.Parse(myndbandData["blinkStrength"].ToString()));
                    }
                }
 
            } 
            yield return new WaitForSeconds(updateFrequency);
        }
    }

    private void ReceiveCallback(IAsyncResult AR) {
        String content = String.Empty;
        StateObject state = (StateObject) AR.AsyncState;
        Socket handler = state.workSocket;

        int bytesRead = stream.EndRead(AR);
        if (bytesRead > 0) {
            string[] packets = Encoding.ASCII.GetString(state.buffer, 0, bytesRead).Split('\r');
            foreach (string packet in packets) {
                if(packet.Length == 0)
                    continue;
                var newEvent = new MyndbandEvent();
                newEvent.systemTime = System.DateTime.Now;
                newEvent.packet = packet;
                if (packet.IndexOf("rawEeg") > -1) {
                    if (isLogging) {
                        loggedRawEegList.Add(newEvent);
                    }
                    rawEegPacketList.Add(newEvent);
                } else {
                    if (isLogging) {
                        loggedmyndbandPacketList.Add(newEvent);
                    }
                    myndbandPacketList.Add(newEvent);
                }
            }
        }

        Thread.Sleep(10);
        stream.BeginRead(state.buffer, 0, StateObject.BufferSize, new AsyncCallback(ReceiveCallback), state);
         
    }
    private void SendData(byte[] data)
    {
        SocketAsyncEventArgs socketAsyncData = new SocketAsyncEventArgs();
        socketAsyncData.SetBuffer(data,0,data.Length);
        clientSocket.SendAsync(socketAsyncData);
    }

    private IEnumerator UpdateSignalStrength() {
        while (myndbandState == MyndbandState.Connected || myndbandState == MyndbandState.ReceivingData) {
            string signalStrengthText = "Signal Strength: " + ParseSignalStrength(signalStrength) + "(" + signalStrength.ToString() + ")";
            onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), signalStrengthText);
            yield return new WaitForSeconds(1.5f);
        }
    }

    // Modified code from Neurosky (with Neurosky's hardcoded value mapping)
	string ParseSignalStrength(int value){
		if(value < 25){
      		myndbandSignal = MyndbandSignal.Perfect;
		} else if(value >= 25 && value < 51){
      		myndbandSignal = MyndbandSignal.Good;
		} else if(value >= 51 && value < 78){
      		myndbandSignal = MyndbandSignal.Medium;
		} else if(value >= 78 && value < 107){
      		myndbandSignal = MyndbandSignal.Poor;
		} else if(value >= 107){
      		myndbandSignal = MyndbandSignal.VeryPoor;
		}
        return Enum.GetName(typeof(MyndbandSignal), myndbandSignal);
	}

	public void Disconnect(){
        StopCoroutine("ParseData");
        if (stream != null) {
            stream.Close();
        }
	}
	void OnApplicationQuit(){
		Disconnect();
	}

}

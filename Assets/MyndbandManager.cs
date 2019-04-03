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
using System.Diagnostics;

public struct MyndbandEvent {
    public DateTime systemTime;
    public TimeSpan stopwatchTime;
    public string packet;
}
public class MyndbandManager : MonoBehaviour
{

	private TcpClient client; 
  	private Stream stream;
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

	public event UpdateIntValueDelegate UpdatePoorSignalEvent;
	public event UpdateIntValueDelegate UpdateAttentionEvent;
	public event UpdateIntValueDelegate UpdateMeditationEvent;
	public event UpdateIntValueDelegate UpdateRawdataEvent;
	public event UpdateIntValueDelegate UpdateBlinkEvent;
	
	public event UpdateFloatValueDelegate UpdateDeltaEvent;
	public event UpdateFloatValueDelegate UpdateThetaEvent;
	public event UpdateFloatValueDelegate UpdateLowAlphaEvent;
	public event UpdateFloatValueDelegate UpdateHighAlphaEvent;
	public event UpdateFloatValueDelegate UpdateLowBetaEvent;
	public event UpdateFloatValueDelegate UpdateHighBetaEvent;
	public event UpdateFloatValueDelegate UpdateLowGammaEvent;
	public event UpdateFloatValueDelegate UpdateHighGammaEvent;

    private MyndbandState myndbandState;
    private MyndbandSignal myndbandSignal;

    private int signalStrength;
    private string socketStatus;
    private int attention1;
    private int meditation1;
	
	private float delta;
    private System.Object rawData = "";

    [Serializable]
    public class OnMyndbandStateChanged : UnityEvent<string, string> { }
    private List<MyndbandEvent> rawEegPacketList;
    private List<MyndbandEvent> myndbandPacketList;

    private List<MyndbandEvent> loggedRawEegList;
    private List<MyndbandEvent> loggedmyndbandPacketList;

    public OnMyndbandStateChanged onMyndbandStateChanged;
    public OnMyndbandStateChanged onMyndbandErrorChanged;

    private TGCConnectionController controller;

    private bool isLogging = false;

    private IDictionary rawEegData;
    private IDictionary myndbandData;

    private Socket clientSocket;

    private static MyndbandManager instance;

    private Stopwatch timer;


    // Start is called before the first frame update
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
        timer.Start();
        isLogging = true;
    }

    public void ResetLogging() {
        timer.Stop();
        timer.Reset();
        isLogging = false;
    }
    public Dictionary<string,List<string>> GetLoggedRawEEG() {
        var logCollection = new Dictionary<string, List<string>>();
		logCollection["SystemTime"] = new List<string>();
        logCollection["StopwatchTime"] = new List<string>();
		logCollection["RawEEG"] = new List<string>();
		foreach (MyndbandEvent bandEvent in loggedRawEegList) {
			var myndbandData = (IDictionary) JsonConvert.Import(typeof(IDictionary), bandEvent.packet);
			if (myndbandData.Contains("rawEeg")) {
				logCollection["RawEEG"].Add(myndbandData["rawEeg"].ToString());
				logCollection["SystemTime"].Add(bandEvent.systemTime.ToString("yyyy-MM-dd HH:mm:ss.ffff"));
                logCollection["StopwatchTime"].Add(bandEvent.stopwatchTime.ToString("G"));
			}
        }
        return logCollection;
    }
    /*public Dictionary<string,List<string>> GetLoggedMyndband() {
        return loggedmyndbandPacketList;
    } */

    private IEnumerator ConnectToMyndband() {
        clientSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
        try {
            clientSocket.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13854));
        } catch(SocketException e)
        {
            UnityEngine.Debug.LogError(e.Message);
            myndbandState = MyndbandState.Disconnected;
            onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), "Could not connect to the ThinkGear Socket..");
        }
        buffer = new byte[1024];
        byte[] myWriteBuffer = Encoding.ASCII.GetBytes(@"{""enableRawOutput"": true, ""format"": ""Json""}");
        SendData(myWriteBuffer);
        clientSocket.BeginReceive(buffer,0,buffer.Length,SocketFlags.None,new AsyncCallback(ReceiveCallBack),null);

        while (true) {
            if (rawEegPacketList.Count > 0) {
                rawEegData = null;
                try {
                    rawEegData = (IDictionary) JsonConvert.Import(typeof(IDictionary), rawEegPacketList.Last().packet);
                } catch (System.Exception e) {
                    //Debug.Log(e);
                }
                if (rawEegData != null) {
                        if (myndbandState != MyndbandState.ReceivingData) {
                            myndbandState = MyndbandState.ReceivingData;
                            socketStatus = "";
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
                    UnityEngine.Debug.Log(myndbandPacketList.Last().packet);
                } catch (System.Exception e) {
                    //Debug.Log(e);
                }

                if (myndbandData != null) {
                    if (myndbandData.Contains("status")){
                        //Debug.Log("status: " + packet);
                        socketStatus = "Socket is: " + myndbandData["status"].ToString();
                        signalStrength = int.Parse(myndbandData["poorSignalLevel"].ToString());
                        if (myndbandState != MyndbandState.Connected) {
                            myndbandState = MyndbandState.Connected;
                            StartCoroutine("UpdateSignalStrength");
                        }
                    } else if (myndbandData.Contains("poorSignalLevel")) {
                        //Debug.Log("poorSignalLevel: " + packet);
                        if (myndbandState != MyndbandState.ReceivingData) {
                            myndbandState = MyndbandState.ReceivingData;
                            socketStatus = "";
                        }
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

            if (myndbandState == MyndbandState.Disconnected) {
                yield return new WaitForSeconds(2f);
                myndbandState = MyndbandState.Connecting;
                onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), "Waiting for ThinkGear Socket to send data..");
                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(0.5f);
        }
        /*
        while (stream == null) {
            myndbandState = MyndbandState.Connecting;
            onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), "");
            yield return new WaitForSeconds(0.25f);
            try {
                client = new TcpClient("127.0.0.1", 13854);
                stream = client.GetStream();
                buffer = new byte[1024];
                byte[] myWriteBuffer = Encoding.ASCII.GetBytes(@"{""enableRawOutput"": true, ""format"": ""Json""}");
                stream.Write(myWriteBuffer, 0, myWriteBuffer.Length);
                StartCoroutine("ParseData"); // InvokeRepeating("ParseData", 0.001f, 0.02f);
            } catch (System.Exception e) {
                Debug.LogError(e);
                myndbandState = MyndbandState.Disconnected;
                onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), "Could not find ThinkGear Socket.");
            }
            yield return new WaitForSeconds(2f);
        }*/
    }

    private void ReceiveCallBack(IAsyncResult AR) {
        //Check how much bytes are recieved and call EndRecieve to finalize handshake
        int recieved = clientSocket.EndReceive(AR);
 
        if(recieved <= 0)
            return;
 
        //Copy the recieved data into new buffer , to avoid null bytes
        byte[] recData = new byte[recieved];
        Buffer.BlockCopy(buffer,0,recData,0,recieved);
 
        //Process data here the way you want , all your bytes will be stored in recData
        string[] packets = Encoding.ASCII.GetString(recData, 0, recData.Length).Split('\r');
        foreach (string packet in packets) {
            if(packet.Length == 0)
                continue;

            var newEvent = new MyndbandEvent();
            newEvent.systemTime = System.DateTime.Now;
            newEvent.stopwatchTime = timer.Elapsed;
            newEvent.packet = packet;
            if (packet.Contains("rawEeg")) {
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
        // Wait 20ms
        //Thread.Sleep(50);
        //Start receiving again
        clientSocket.BeginReceive(buffer,0,buffer.Length,SocketFlags.None,new AsyncCallback(ReceiveCallBack),null);
    }
    private void SendData(byte[] data)
    {
        SocketAsyncEventArgs socketAsyncData = new SocketAsyncEventArgs();
        socketAsyncData.SetBuffer(data,0,data.Length);
        clientSocket.SendAsync(socketAsyncData);
    }

/*	private IEnumerator ParseData(){
        while (true) {
            Debug.Log("run");
            if (stream == null && myndbandState != MyndbandState.Disconnected) {
                Debug.LogError("Stream is null");
                myndbandState = MyndbandState.Disconnected;
                onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), "ThinkGear Socket Stream Not Available.");
                continue;
            }
            if (!stream.CanRead && myndbandState != MyndbandState.Disconnected) {
                Debug.LogError("Stream can't read");
                myndbandState = MyndbandState.Disconnected;
                onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), "Cannot Read the ThinkGear Socket Stream.");
                continue;
            }

            int bytesRead = 0;
            string packet = "";
            if (stream.CanRead) {
                using (StreamReader reader = new StreamReader(stream)) {
                        packet = reader.ReadLine();
                        Debug.Log(packet);
                    //while (packets = reader.ReadBlock(buffer, 0, buffer.Length)) {
                    //    string[] packets = Encoding.ASCII.GetString(buffer, 0, bytesRead).Split('\r');
                    //}
                }
            }
                //int bytesRead = stream.Read(buffer, 0, buffer.Length);
                //stream.Read(buffer, 0, buffer.Length);
            try { 
                string[] packets = Encoding.ASCII.GetString(buffer, 0, bytesRead).Split('\r');
                foreach(string packet in packets) {
                    if(packet.Length == 0)
                        continue;

                  
            yield return new WaitForSeconds(0.02f);
        }
	}// end ParseData
 */

    private IEnumerator UpdateSignalStrength() {
        while (myndbandState == MyndbandState.Connected || myndbandState == MyndbandState.ReceivingData) {
            string signalStrengthText = "Signal Strength: " + ParseSignalStrength(signalStrength) + "(" + signalStrength.ToString() + ")";
            if (!string.IsNullOrEmpty(socketStatus)) {
                onMyndbandStateChanged.Invoke(socketStatus, signalStrengthText);
            } else {
                onMyndbandStateChanged.Invoke(Enum.GetName(typeof(MyndbandState), myndbandState), signalStrengthText);
            }
            yield return new WaitForSeconds(1.5f);
        }
    }

    // Modified code from Neurosky (with Neurosky's hardcoded value mapping)
	string ParseSignalStrength(int value){
		if(value < 25){
      		myndbandSignal = MyndbandSignal.VeryPoor;
		} else if(value >= 25 && value < 51){
      		myndbandSignal = MyndbandSignal.Poor;
		} else if(value >= 51 && value < 78){
      		myndbandSignal = MyndbandSignal.Medium;
		} else if(value >= 78 && value < 107){
      		myndbandSignal = MyndbandSignal.Good;
		} else if(value >= 107){
      		myndbandSignal = MyndbandSignal.Perfect;
		}
        return Enum.GetName(typeof(MyndbandSignal), myndbandSignal);
	}
	void OnUpdateAttention(int value){
		attention1 = value;
	}
	void OnUpdateMeditation(int value){
		meditation1 = value;
	}
	void OnUpdateDelta(float value){
		delta = value;
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

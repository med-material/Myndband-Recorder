using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UI;
using System.Diagnostics;

public class DisplayData : MonoBehaviour
{	
  	[SerializeField]
    private Text myndbandStatus;

  	[SerializeField]
    private Text myndbandSubStatus;

	[SerializeField]
	private Text systemTime;

	[SerializeField]
	private Text rawEEG;

	[SerializeField]
	private Text lowAlpha;
	[SerializeField]
	private Text highAlpha;
	[SerializeField]
	private Text lowBeta;
	[SerializeField]
	private Text highBeta;
	[SerializeField]
	private Text lowGamma;
	[SerializeField]
	private Text highGamma;

	[SerializeField]
	private Text blinkDetected;
	private MyndbandManager controller;

	[SerializeField]
	private Text bigTimer;

	private Stopwatch timer;

    void Start()
    {
		timer = new Stopwatch();
		controller = GameObject.Find("MyndbandManager").GetComponent<MyndbandManager>();	
		controller.UpdateRawdataEvent += OnUpdateRawDataEvent;
		controller.UpdateLowAlphaEvent += OnUpdateLowAlpha;
		controller.UpdateHighAlphaEvent += OnUpdateHighAlpha;
		controller.UpdateLowBetaEvent += OnUpdateLowBeta;
		controller.UpdateHighBetaEvent += OnUpdateHighBeta;
		controller.UpdateLowGammaEvent += OnUpdatelowGamma;
		controller.UpdateHighGammaEvent += OnUpdatehighGamma;
		controller.UpdateBlinkEvent += OnBlinkDetected;
    }

	public void updateMyndbandStatus(string newStatus, string subStatus) {
		myndbandStatus.text = newStatus;
		myndbandSubStatus.text = subStatus;
	}

	void Update() {
		systemTime.text = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff");
		bigTimer.text = timer.Elapsed.ToString("G");
	}

    void OnUpdateRawDataEvent(int value) {
        rawEEG.text = value.ToString();
    }

	void OnUpdateLowAlpha(float value) {
		lowAlpha.text = value.ToString();
	}
	void OnUpdateHighAlpha(float value) {
		highAlpha.text = value.ToString();
	}
	void OnUpdateLowBeta(float value) {
		lowBeta.text = value.ToString();
	}
	void OnUpdateHighBeta(float value) {
		highBeta.text = value.ToString();
	}
	void OnUpdatelowGamma(float value) {
		lowGamma.text = value.ToString();
	}
	void OnUpdatehighGamma(float value) {
		highGamma.text = value.ToString();
	}

	void OnBlinkDetected(int value) {
		blinkDetected.text = "Blink Detected! (" + value.ToString() + ")";
		StopCoroutine("showBlinkDetection");
		StartCoroutine("showBlinkDetection");
	}

	private IEnumerator showBlinkDetection() {
		blinkDetected.gameObject.SetActive(true);
		yield return new WaitForSeconds(1.5f);
		blinkDetected.gameObject.SetActive(false);
	}

	public void StartStopWatch() {
		timer = new Stopwatch();
		timer.Start();
	}
	public void StopStopWatch() {
		timer.Stop();
		timer.Reset();
	}
/*
    void OnGUI()
    {
		GUILayout.BeginHorizontal();
		
		
        if (GUILayout.Button("Connect"))
        {
            controller.Connect();
        }
        if (GUILayout.Button("DisConnect"))
        {
            controller.Disconnect();
			indexSignalIcons = 1;
        }
		
		GUILayout.Space(Screen.width-250);
		GUILayout.Label(signalIcons[indexSignalIcons]);
		
		GUILayout.EndHorizontal();

		
        GUILayout.Label("PoorSignal1:" + poorSignal1);
        GUILayout.Label("Attention1:" + attention1);
        GUILayout.Label("Meditation1:" + meditation1);
		GUILayout.Label("Delta:" + delta);

    }
*/
}

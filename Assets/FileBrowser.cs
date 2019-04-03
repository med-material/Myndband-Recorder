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
using SFB;
using System.Diagnostics;
public class FileBrowser : MonoBehaviour
{

    [Serializable]
    public class OnFilePathReady : UnityEvent<string> { }
    public OnFilePathReady onFilePathReady;

    private string path;
    public string dialogTitle = "Choose CSV File Destination..";
    public string filename = "log";
    public string datatype = "csv";


    // Start is called before the first frame update
    void Start()
    {
        
    }

	public void ShowSaveDialog() {
		path = StandaloneFileBrowser.SaveFilePanel(dialogTitle, "", filename, datatype);
		onFilePathReady.Invoke(path);
    }
}

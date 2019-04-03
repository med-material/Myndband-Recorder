using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
using UnityEditor;
public class LogToDisk : MonoBehaviour
{

	private string customFilepath = "";

    [SerializeField]
    private Text filepathText;

	[SerializeField]

	private StreamWriter writer;

    private string directory = "";

	private string sep = ";";

	[SerializeField]
	private MyndbandManager myndbandManager;

    void Start()
    {
    }

	public void Log(string filepath) {

		if(!Directory.Exists(directory)){
			Directory.CreateDirectory(directory);
		}

        if (string.IsNullOrEmpty(filepath)) {
            filepath = "logs";
        }

		// Overwriting The existing file is disabled for now.
		if (!File.Exists(filepath)) {
			Debug.LogWarning("Overwriting CSV file: " + filepath);
			File.Delete (filepath);
		}
		var logCollection = myndbandManager.GetLoggedRawEEG();
		
		string[] keys = new string[logCollection.Keys.Count];
		logCollection.Keys.CopyTo(keys,0);
		string dbCols = string.Join(sep, keys).Replace("\n",string.Empty);

		using (StreamWriter writer = File.AppendText (filepath)) {
			writer.WriteLine (dbCols);
		}

		List<string> dataString = new List<string>();
		// Create a string with the data
		for(int i = 0; i < logCollection["RawEEG"].Count; i++) {
			List<string> row = new List<string>();
			foreach(string key in logCollection.Keys) {
				row.Add(logCollection[key][i]);
			}
			dataString.Add(string.Join(sep,row.ToArray()) + ";");
		}
		
		foreach (var log in dataString) {
			using (StreamWriter writer = File.AppendText (filepath)) {
				writer.WriteLine (log.Replace("\n",string.Empty));
			}
		}

		Debug.Log("Data logged to: " + filepath);
	}

}

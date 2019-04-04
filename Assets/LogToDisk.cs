using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
using UnityEditor;
public class LogToDisk : MonoBehaviour
{

    [SerializeField]
    private Text filepathText;

	[SerializeField]

	private StreamWriter writer;

	private string sep = ";";

	[SerializeField]
	private MyndbandManager myndbandManager;

	[SerializeField]
	private GameObject savingFile;

    void Start()
    {
    }

	public void Log(string filepath) {
        if (string.IsNullOrEmpty(filepath)) {
            return;
        }

		savingFile.SetActive(true);
		filepathText.gameObject.SetActive(true);
		filepathText.text = filepath;
		filepathText.color = Color.black;

		WriteToCSV(myndbandManager.GetLoggedRawEEG(), System.IO.Path.GetDirectoryName(filepath) + "\\" +  System.IO.Path.GetFileNameWithoutExtension(filepath) + "_512HzRawEEG" + ".csv");
		WriteToCSV(myndbandManager.GetLoggedMyndband(),  System.IO.Path.GetDirectoryName(filepath) + "\\" + System.IO.Path.GetFileNameWithoutExtension(filepath) + "_1HzMyndband" + ".csv");
	}

	private void WriteToCSV(Dictionary<string, List<string>> logCollection, string filepath) {
		try {
		// Overwriting The existing file is disabled for now.
		if (File.Exists(filepath)) {
			Debug.LogWarning("Overwriting CSV file: " + filepath);
			File.Delete (filepath);
		}
		
		string[] keys = new string[logCollection.Keys.Count];
		logCollection.Keys.CopyTo(keys,0);
		for (int i = 0; i < keys.Length; i++) {
			keys[i] = "\"" + keys[i] + "\"";
		}
		string dbCols = string.Join(sep, keys).Replace("\n",string.Empty) + ";";

		using (StreamWriter writer = File.AppendText (filepath)) {
			writer.WriteLine (dbCols);
		}

		List<string> dataString = new List<string>();
		// Create a string with the data
		Debug.LogWarning(logCollection["SystemTime"].Count);
		Debug.LogWarning(filepath);
		for(int i = 0; i < logCollection["SystemTime"].Count; i++) {
			List<string> row = new List<string>();
			foreach(string key in logCollection.Keys) {
				row.Add("\"" + logCollection[key][i] + "\"");
			}
			dataString.Add(string.Join(sep,row.ToArray()) + ";");
		}
		
		foreach (var log in dataString) {
			using (StreamWriter writer = File.AppendText (filepath)) {
				writer.WriteLine (log.Replace("\n",string.Empty));
			}
		}

		Debug.Log("Data logged to: " + filepath);
		savingFile.SetActive(false);
		filepathText.gameObject.SetActive(false);
		} catch (System.Exception e) {
			filepathText.text = e.Message;
			filepathText.color = Color.red;
		}
	}

}

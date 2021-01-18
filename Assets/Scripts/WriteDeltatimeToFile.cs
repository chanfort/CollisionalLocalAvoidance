using System;
using System.IO;
using System.Collections;
using UnityEngine;

public class WriteDeltatimeToFile : MonoBehaviour
{
    public string fileName = "deltatime";
    public float stopAfterTime = 180f;
    public float writeTimeStep = 0.2f;

    void Start()
    {
        StartCoroutine(WriteToFile());
    }

    IEnumerator WriteToFile()
    {
        Overwrite();

        while (true)
        {
            yield return new WaitForSeconds(writeTimeStep);
            Write();
            Debug.Log("Written: " + (Time.time * 100f / stopAfterTime).ToString() + " %");

            if (Time.time > stopAfterTime)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
				Application.Quit();
#endif
            }
        }
    }

    void Write()
    {
        string path = Path.Combine(Application.dataPath, fileName + ".txt");

        if (!File.Exists(path))
        {
            string createText = "# time dt fps" + Environment.NewLine;
            File.WriteAllText(path, createText);
        }

        string appendText = Time.time + " " + Time.deltaTime * 1000 + " " + 1f / Time.deltaTime + Environment.NewLine;
        File.AppendAllText(path, appendText);
    }

    void Overwrite()
    {
        string path = Path.Combine(Application.dataPath, fileName + ".txt");
        string createText = "# time dt fps" + Environment.NewLine;
        File.WriteAllText(path, createText);
    }
}

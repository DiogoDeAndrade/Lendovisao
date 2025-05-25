using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class BeatRecorder : MonoBehaviour
{
    [SerializeField] private AudioSource    audioSource;
    [SerializeField] private string         saveName = "BeatRecording";

    private List<float> beatSamples = new();
    private bool        isRecording = false;

    private void Awake()
    {
        audioSource.Stop();
    }

    void Update()
    {
        // Start playback on first Space press
        if (!isRecording && Input.GetKeyDown(KeyCode.Space))
        {
            audioSource.Play();
            isRecording = true;
            beatSamples.Clear();
            float sample = audioSource.time;
            beatSamples.Add(sample);
            Debug.Log("Recording started...");
        }

        // Record beats on spacebar
        if (isRecording && Input.GetKeyDown(KeyCode.Space))
        {
            float sample = audioSource.time;
            beatSamples.Add(sample);
            Debug.Log($"Recorded beat at sample {sample}");
        }

        // Stop recording and save on Escape
        if (isRecording && Input.GetKeyDown(KeyCode.Escape))
        {
            SaveRecording();
            isRecording = false;
            audioSource.Stop();
            Debug.Log("Recording stopped and saved.");
        }
    }

    void SaveRecording()
    {
#if UNITY_EDITOR
        BeatRecording recording = ScriptableObject.CreateInstance<BeatRecording>();
        recording.audioClip = audioSource.clip;
        recording.beatPositions = new List<float>(beatSamples);

        string folderPath = "Assets/Audio";
        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder("Assets", "Audio");

        string assetPath = $"{folderPath}/{saveName}.asset";
        AssetDatabase.CreateAsset(recording, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Recording saved to {assetPath}");
#else
        Debug.LogWarning("Saving recordings only works in the Unity Editor.");
#endif
    }
}

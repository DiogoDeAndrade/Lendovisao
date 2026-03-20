using NaughtyAttributes;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Beat Recording", fileName = "NewBeatRecording")]
public class BeatRecording : ScriptableObject
{
    [System.Serializable]
    public struct BeatData
    {
        public float beatTime;
    }

    public AudioClip        audioClip;
    public List<BeatData>   beatData = new();
}

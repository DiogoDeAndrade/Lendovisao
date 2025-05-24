using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Beat Recording", fileName = "NewBeatRecording")]
public class BeatRecording : ScriptableObject
{
    public bool         useTime;
    public AudioClip    audioClip;
    public List<float>  beatPositions = new List<float>();
}

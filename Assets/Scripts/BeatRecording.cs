using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Beat Recording", fileName = "NewBeatRecording")]
public class BeatRecording : ScriptableObject
{
    [Flags]
    public enum NoteType 
    {
        None = 0,
        Note1 = 1,
        Note2 = 2,
        Note3 = 4,
        Note4 = 8,
    };

    [System.Serializable]
    public class BeatData
    {
        public float    beatTime;
        public bool     randomNote = true;
        public NoteType definedNote = NoteType.None;
    }

    public AudioClip        audioClip;
    public List<BeatData>   beatData = new();
}

using System.Collections.Generic;
using UnityEngine;

public abstract class BeatSync : MonoBehaviour
{
    [SerializeField] protected int everyN = 4;
    [SerializeField] protected int startIndex = 0;

    protected AudioSource   audioSource;
    protected BeatRecording beatRecording;
    protected List<float>   beatSamples;
    protected int           currentIndex = 0;
    protected float         beatTime = 0.0f;
    protected int           counter;

    protected virtual void Start()
    {
        GameSystem gm = FindAnyObjectByType<GameSystem>();
        if (gm == null)
        {
            Debug.LogWarning($"Game system does not exist, beat sync unavailable for {name}!");
            return;
        }

        audioSource = gm.GetAudioSource();
        beatRecording = gm.GetBeatRecording();

        if (audioSource == null || beatRecording == null || beatRecording.audioClip == null)
        {
            Debug.LogError("BeatSync not properly configured.");
            enabled = false;
            return;
        }

        if (audioSource.clip != beatRecording.audioClip)
        {
            Debug.LogWarning("AudioSource.clip and BeatRecording.audioClip differ. For accurate sync, they should match.");
        }

        beatSamples = beatRecording.beatPositions;
        currentIndex = startIndex;
    }

    protected virtual void Update()
    {
        if (beatRecording == null) return;

        if (!audioSource.isPlaying || currentIndex >= beatSamples.Count)
            return;

        float currentSample = (beatRecording.useTime) ? (audioSource.time) : (audioSource.timeSamples);

        while (currentIndex < beatSamples.Count && currentSample >= beatSamples[currentIndex])
        {
            counter++;
            if (counter >= everyN)
            {
                counter = 0;
                beatTime = Time.time;
                RunBeat();                
            }

            currentIndex++;
        }
    }

    protected abstract void RunBeat();
}

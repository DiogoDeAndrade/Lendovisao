using System.Collections.Generic;
using UnityEngine;
public class HeadBobPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private BeatRecording beatRecording;
    [SerializeField] private AnimationCurve curve;
    [SerializeField] private float bobTime = 0.1f;
    [SerializeField] private int everyN = 4;
    [SerializeField] private float bobAmplitude = 10.0f;
    [SerializeField] private float bobAngle = 10.0f;

    private List<float> beatSamples;
    private int currentIndex = 0;
    private float timer = 0.0f;
    private int counter;
    private Vector3 basePos;
    private float startAngle;

    void Start()
    {
        if (audioSource == null || beatRecording == null || beatRecording.audioClip == null)
        {
            Debug.LogError("HeadBobPlayer not properly configured.");
            enabled = false;
            return;
        }

        if (audioSource.clip != beatRecording.audioClip)
        {
            Debug.LogWarning("AudioSource.clip and BeatRecording.audioClip differ. For accurate sync, they should match.");
        }

        beatSamples = beatRecording.beatPositions;
        currentIndex = 0;

        basePos = transform.localPosition;
        startAngle = transform.localRotation.eulerAngles.z;
    }

    void Update()
    {
        if (!audioSource.isPlaying || currentIndex >= beatSamples.Count)
            return;

        float currentSample = (beatRecording.useTime) ? (audioSource.time) : (audioSource.timeSamples);

        while (currentIndex < beatSamples.Count && currentSample >= beatSamples[currentIndex])
        {
            HeadBob();
            currentIndex++;
        }

        if (timer > 0.0f)
        {
            timer -= Time.deltaTime;
            if (timer <= 0.0f)
            {
                timer = 0.0f;
            }
            float t = 1.0f - timer / bobTime;
            t = curve.Evaluate(t);
            transform.localPosition = basePos + Vector3.left * t * bobAmplitude;
            transform.localRotation = Quaternion.Euler(0.0f, 0.0f, Mathf.Lerp(startAngle, startAngle + bobAngle, t));
        }
    }

    void HeadBob()
    {
        counter++;
        if (counter >= everyN)
        {
            counter = 0;
            timer = bobTime;
        }
    }
}

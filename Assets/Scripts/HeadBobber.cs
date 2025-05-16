using Unity.VisualScripting;
using UnityEngine;

public class HeadBobberDSP : MonoBehaviour
{
    [SerializeField] private AudioSource    audioSource;
    [SerializeField] private float          bpm = 120f;
    [SerializeField] private AnimationCurve curve;
    [SerializeField] private float          bobTime = 0.1f;
    [SerializeField] private int            everyN = 4;
    [SerializeField] private float          bobAmplitude = 10.0f;

    private double beatIntervalDSP;     // In seconds (double precision)
    private double nextBeatDSP;         // DSP time for next beat

    private bool waitingForStart = true;

    private float timer = 0.0f;
    private int   counter;
    private Vector3 basePos;

    void Start()
    {
        if (bpm <= 0f || audioSource == null || audioSource.clip == null)
        {
            Debug.LogError("Invalid BPM or AudioSource/clip not set.");
            enabled = false;
            return;
        }

        beatIntervalDSP = 60.0 / bpm;
        basePos = transform.position;
    }

    void Update()
    {
        if (!audioSource.isPlaying)
        {
            waitingForStart = true;
            return;
        }

        double dspTime = AudioSettings.dspTime;

        // On first playback, sync with the audio's scheduled DSP start time
        if (waitingForStart)
        {
            double audioStartDSP = dspTime - audioSource.time;
            nextBeatDSP = audioStartDSP + Mathf.CeilToInt((float)(audioSource.time / beatIntervalDSP)) * beatIntervalDSP;
            waitingForStart = false;
        }

        while (dspTime >= nextBeatDSP)
        {
            BobHead();
            nextBeatDSP += beatIntervalDSP;
        }

        if (timer > 0.0f)
        {
            timer -= Time.deltaTime;
            if (timer <= 0.0f)
            {
                timer = 0.0f;
            }
            float t = 1.0f - timer / bobTime;
            transform.position = basePos + Vector3.left * curve.Evaluate(t) * bobAmplitude;
        }
    }

    void BobHead()
    {
        counter++;
        if (counter >= everyN)
        {
            counter = 0;
            timer = bobTime;
        }
    }
}

using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UC;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class GameSystem : MonoBehaviour
{
    [System.Serializable]
    struct DualNote
    {
        public string note1;
        public string note2;
    }

    [SerializeField] private bool               cheatMode = false;

    [Header("References")]
    [SerializeField] private AudioSource        audioSource;
    [SerializeField] private AudioSource        booingAudioSource;
    [SerializeField] private BeatRecording      beatRecording;
    [SerializeField] private SubtitleTrack      subtitleTrack;
    [SerializeField] private SpriteRenderer     targetSpriteRenderer;
    [SerializeField] private Light2D[]          lights;
    [SerializeField] private Light2D            globalLight;
    [SerializeField] private Transform          suckOMeter;
    [SerializeField] private SpriteRenderer     arrowSpriteRenderer;
    [SerializeField] private GameObject         gameOverObject;
    [SerializeField] private GameObject         successObject;
    [SerializeField] private TextMeshPro        scoreObject;
    [SerializeField] private CanvasGroup        titleCanvasGroup;
    [SerializeField, Scene] private string      titleScene;
    [SerializeField] private CanvasGroup        subtitleCanvasGroup;
    [SerializeField] private TextMeshProUGUI    subtitleText;

    [Header("Note Prefabs (1 per spawn point)")]
    [SerializeField] private List<Note> notePrefabs;

    [Header("Spawn Points (same order as notePrefabs)")]
    [SerializeField] private List<Transform> spawnPoints;

    [Header("Timing Settings")]
    [SerializeField] private float timeBeforeNote = 1.0f;
    [SerializeField] private float minTimeBetweenNotes = 0.2f;
    [SerializeField] private float delayTime = 2.0f;

    [Header("Colors")]
    [SerializeField] private Color targetDefaultColor = Color.white;
    [SerializeField] private Color targetFailColor = Color.red;
    [SerializeField] private Color targetSuccessColor = Color.green;
    [SerializeField] private Vector2 minMaxLightIntensity = new Vector2(20.0f, 150.0f);

    [Header("Difficulty")]
    [SerializeField] public float dualNoteStartsAt = 0.75f; // Changeable at runtime
    [SerializeField] public float tripleNoteStartsAt = 1.1f; 
    [SerializeField] private float booDecay = 0.025f;
    [SerializeField] private List<DualNote> dualNoteCombos;

    [Header("Controls")]
    [SerializeField] private string[] allButtons;

    private List<int> beatSamples;
    private int currentIndex = 0;
    private float lastSpawnTime = -Mathf.Infinity;

    private float sampleRate;
    private enum AnimTargetState { None, Fail, Success };

    private AnimTargetState             animTargetState = AnimTargetState.None;
    private Tweener.BaseInterpolator    scaleAnim;
    private Tweener.BaseInterpolator    colorAnim;
    private Vector3                     targetOriginalSize;
    private float                       booMeter;
    private float                       starPower = 0.0f;
    private bool                        gameOver;
    private bool                        fadedOut;
    private int                         successNotes = 0;
    private float                       timeSinceEnd = 0.0f;

    public BeatRecording GetBeatRecording() => beatRecording;
    public AudioSource   GetAudioSource() => audioSource;

    void Start()
    {
#if !UNITY_EDITOR
        cheatMode = false;
#endif
        SoundManager.PlayMusic(null);

        if (audioSource == null || beatRecording == null || beatRecording.audioClip == null)
        {
            Debug.LogError("GameSystem is not properly configured.");
            enabled = false;
            return;
        }

        if (notePrefabs.Count != spawnPoints.Count)
        {
            Debug.LogError("notePrefabs and spawnPoints must be the same length.");
            enabled = false;
            return;
        }

        if (audioSource.clip != beatRecording.audioClip)
        {
            Debug.LogWarning("AudioSource.clip and BeatRecording.audioClip differ. For accurate sync, they should match.");
        }

        beatSamples = beatRecording.beatSamplePositions;
        sampleRate = audioSource.clip.frequency;

        currentIndex = 0;
        lastSpawnTime = -minTimeBetweenNotes;

        if (delayTime > 0.0f)
        {
            audioSource.Stop();
            StartCoroutine(StartMusicCR(delayTime));
        }
        else
        {
            audioSource.Play();
        }

        StartCoroutine(StartTitleCR());

        targetOriginalSize = targetSpriteRenderer.transform.localScale;
        if (subtitleText) subtitleText.text = "";
        if (subtitleCanvasGroup) subtitleCanvasGroup.alpha = 0.0f;
    }

    IEnumerator StartTitleCR()
    {
        titleCanvasGroup.alpha = 0.0f;
        yield return new WaitForSeconds(1.0f);
        titleCanvasGroup.FadeIn(1.0f);
        yield return new WaitForSeconds(2.0f);
        titleCanvasGroup.FadeOut(1.0f);
    }

    IEnumerator StartMusicCR(float time)
    {
        yield return new WaitForSeconds(time);

        audioSource.Play();
    }

    void Update()
    {
        if (gameOver)
        {
            timeSinceEnd += Time.deltaTime;
            if ((Input.anyKeyDown) && (timeSinceEnd > 6.0f))
            {
                if (!fadedOut)
                {
                    fadedOut = true;
                    FullscreenFader.FadeOut(1.0f, Color.black, () => SceneManager.LoadScene(titleScene));
                }
            }
            return;
        }
        else if ((subtitleTrack != null) && (subtitleText != null))
        {
            var text = subtitleTrack.GetAtTime(audioSource.time);
            if (text == null)
            {
                if (subtitleText) subtitleText.text = "";
                subtitleCanvasGroup?.FadeOut(0.25f);
            }
            else
            {
                if (subtitleText) subtitleText.text = text.text;
                subtitleCanvasGroup?.FadeIn(0.25f);
            }
        }

        float currentTime = audioSource.time;

        while (currentIndex < beatSamples.Count)
        {
            float beatTime = beatSamples[currentIndex] / sampleRate;
            float spawnTime = beatTime - timeBeforeNote;

            if (currentTime >= spawnTime)
            {
                if (currentTime - lastSpawnTime >= minTimeBetweenNotes)
                {
                    SpawnNotes();
                    lastSpawnTime = currentTime;
                }

                currentIndex++;
            }
            else
            {
                break;
            }
        }

        if (currentIndex >= beatSamples.Count)
        {
            timeSinceEnd += Time.deltaTime;

            if (timeSinceEnd > timeBeforeNote * 1.25f)
            {
                var score = (float)successNotes / (float)beatSamples.Count;
                score = Mathf.Lerp(6, 12, score);
                successObject.SetActive(true);
                scoreObject.text = $"{Mathf.RoundToInt(score)} points!";
                gameOver = true;
                if (subtitleText) subtitleText.text = "";
                subtitleCanvasGroup?.FadeOut(0.25f);
            }
        }

        List<Note> notesInTarget = new();

        var notes = FindObjectsByType<Note>(FindObjectsSortMode.None);
        foreach (var note in notes)
        {
            if ((note.inTarget) && (note.isActive))
            {
                notesInTarget.Add(note);
            }
        }
        if (notesInTarget.Count > 0)
        {
            bool allHit = true;
            foreach (var note in notesInTarget)
            {
                if (!note.isHit) allHit = false;
            }

            bool oneFailHit = false;
            foreach (var button in allButtons)
            {
                if (Input.GetButtonDown(button))
                {
                    bool foundNote = false;
                    foreach (var note in notesInTarget)
                    {
                        if (note.GetButton() == button)
                        {
                            foundNote = true;
                            break;
                        }
                    }
                    if (!foundNote)
                    {
                        oneFailHit = true;
                        break;
                    }

                }
            }
            if (oneFailHit)
            {
                Fail();
                foreach (var note in notesInTarget)
                {
                    note.Fail();
                }
            }
            else if (allHit)
            {
                Success();
                foreach (var note in notesInTarget)
                {
                    note.Success();
                }
            }
        }

        booMeter = Mathf.Clamp01(booMeter - booDecay * Time.deltaTime); 
        booingAudioSource.volume = booMeter;
        audioSource.volume = Mathf.Lerp(0.25f, 1.0f, 1.0f - booMeter);

        if (booMeter < 0.05f)
        {
            starPower = Mathf.Clamp01(starPower + Time.deltaTime * 0.05f);
        }
        else
        {
            starPower = Mathf.Clamp01(starPower - Time.deltaTime * 0.1f);

            if (booMeter >= 1.0f)
            {
                gameOver = true;
                foreach (var light in lights)
                {
                    light.FadeOut(0.25f);
                }
                globalLight.FadeOut(0.25f);
                audioSource.PitchShift(0.5f, 1.0f);
                audioSource.FadeTo(0.0f, 2.0f);
                booingAudioSource.FadeTo(0.5f, 2.0f);
                gameOverObject.SetActive(true);
                if (subtitleText) subtitleText.text = "";
                subtitleCanvasGroup?.FadeOut(0.25f);
            }
        }
        foreach (var light in lights)
        {
            light.intensity = Mathf.Lerp(minMaxLightIntensity.x, minMaxLightIntensity.y, starPower);
        }

        arrowSpriteRenderer.transform.localRotation = Quaternion.Euler(0, 0, 90.0f - 180.0f * booMeter);
    }

    void SpawnNotes()
    {
        DualNote dualNote = dualNoteCombos[0];
        int maxNotesPerSpawn = 1;
        float t = (float)currentIndex / (float)beatSamples.Count;
        if (t > dualNoteStartsAt)
        {
            maxNotesPerSpawn = 2;
            int randIndex = Random.Range(0, dualNoteCombos.Count);
            dualNote = dualNoteCombos[randIndex];
        }
        if (t > tripleNoteStartsAt) maxNotesPerSpawn = 3;

        int totalNotes = Mathf.Min(notePrefabs.Count, maxNotesPerSpawn);
        int notesToSpawn = Random.Range(1, totalNotes + 1);

        // Pick unique indices to spawn
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < notePrefabs.Count; i++)
            availableIndices.Add(i);

        for (int i = 0; i < notesToSpawn; i++)
        {
            int selected = -1;
            if (notesToSpawn == 2)
            {
                if (i == 0) selected = GetNoteIndex(dualNote.note1);
                else if (i == 1) selected = GetNoteIndex(dualNote.note2);
            }
            else
            {
                int randIndex = Random.Range(0, availableIndices.Count);
                selected = availableIndices[randIndex];
                availableIndices.RemoveAt(randIndex);
            }

            Note prefab = notePrefabs[selected];
            Transform spawnPoint = spawnPoints[selected];

            Note noteScript = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

            if (noteScript != null)
            {
                noteScript.moveTime = timeBeforeNote;
                noteScript.audioSource = audioSource;
                noteScript.gameSystem = this;
            }
        }
    }

    private int GetNoteIndex(string button)
    {
        for (int i = 0; i <notePrefabs.Count; i++)
        {
            if (notePrefabs[i].GetButton() == button) return i;
        }

        return -1;
    }

    public void Fail()
    {
        if (animTargetState == AnimTargetState.Fail) return;

        FlashTarget(targetFailColor);

        animTargetState = AnimTargetState.Fail;

        if (!cheatMode) 
            booMeter += 0.1f;
        suckOMeter.transform.localScale = Vector3.one * 1.25f;
        suckOMeter.transform.ScaleTo(Vector3.one, 0.1f);
    }

    void FlashTarget(Color color)
    { 
        if (scaleAnim != null) scaleAnim.Complete(true, true);
        if (colorAnim != null) colorAnim.Complete(true, true);

        targetSpriteRenderer.transform.localScale = new Vector2(targetOriginalSize.x * 1.5f, targetOriginalSize.x * 1.1f);
        scaleAnim = targetSpriteRenderer.transform.ScaleTo(targetOriginalSize, 0.1f).Done(() => targetSpriteRenderer.transform.localScale = targetOriginalSize);

        targetSpriteRenderer.color = color;
        colorAnim = targetSpriteRenderer.FadeTo(targetDefaultColor, 0.1f).Done(() =>
            {
                targetSpriteRenderer.color = targetDefaultColor;
                animTargetState = AnimTargetState.None;
            });
    }

    private void Success()
    {
        if (animTargetState == AnimTargetState.Success) return;

        FlashTarget(targetSuccessColor);

        animTargetState = AnimTargetState.Success;

        if (!cheatMode)
            booMeter -= 0.05f;

        successNotes++;
    }
}

using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] private AudioSource        applauseAudioSource;
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
    [SerializeField] private Vector2 tolerance = new Vector2(-0.1f, 0.2f);
    [SerializeField] private float perfectTolerance = 0.05f;
    [SerializeField] private float booPerFail = 0.1f;
    [SerializeField] private float booPerSuccess = -0.05f;
    [SerializeField] private float booPerPerfectSuccess = -0.1f;

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

    private List<float> beatPos;
    private int currentIndex = 0;
    private float lastSpawnTime = -Mathf.Infinity;

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
    private bool                        applauseOnZero = false;
    private int                         prevNote;
    private float                       prevNoteTime;
    private float                       startTimer;
    private int                         consecutiveFails;
    public BeatRecording GetBeatRecording() => beatRecording;
    public AudioSource   GetAudioSource() => audioSource;

    class NoteInstance
    {
        public List<Note>   notes;
        public List<int>    keysPressed;
        public float        srcX;
        public float        targetTime;
    };
    List<NoteInstance> activeNotes = new();

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

        beatPos = beatRecording.beatPositions;

        currentIndex = 0;

        if (delayTime > 0.0f)
        {
            audioSource.Stop();
            startTimer = delayTime;
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
                    applauseAudioSource.FadeTo(0.0f, 0.5f);
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

        float currentTime = GetCurrentTime();

        while (currentIndex < beatPos.Count)
        {
            float beatTime = beatPos[currentIndex];
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

        if (currentIndex >= beatPos.Count)
        {
            timeSinceEnd += Time.deltaTime;

            if (timeSinceEnd > timeBeforeNote * 1.25f)
            {
                var score = (float)successNotes / (float)beatPos.Count;
                score = Mathf.Lerp(4, 12, score);
                successObject.SetActive(true);
                scoreObject.text = $"{Mathf.FloorToInt(score)} points!";
                gameOver = true;
                if (subtitleText) subtitleText.text = "";
                subtitleCanvasGroup?.FadeOut(0.25f);
                booingAudioSource.FadeTo(0.0f, 0.25f);
                applauseAudioSource.FadeTo(1.0f, 0.5f);
            }
        }

        UpdateNotes();
        UpdateEffects();

        if (startTimer > 0.0f)
        {
            startTimer -= Time.deltaTime;
            if (startTimer <= 0.0f)
            {
                audioSource.Play();
                startTimer = 0.0f;
            }
        }
    }

    float GetCurrentTime()
    {
        if (startTimer > 0.0f) return -startTimer;

        return audioSource.time;
    }

    void UpdateNotes()
    {
        float currentTime = GetCurrentTime();

        // Check inputs
        var currentNote = activeNotes.FirstOrDefault();

        if (currentNote != null)
        {
            // Is it in the window?
            float deltaTime = currentNote.targetTime - currentTime;

            if (deltaTime <= tolerance.y)
            {
                for (int keyId = 0; keyId < allButtons.Length; keyId++)
                {
                    if (Input.GetButtonDown(allButtons[keyId]))
                    {
                        bool need = false;
                        // Check if any note needs this note and if we haven't already pressed it
                        if (currentNote.keysPressed.IndexOf(keyId) == -1)
                        {
                            foreach (var n in currentNote.notes)
                            {
                                if (n.GetButton() == allButtons[keyId])
                                {
                                    need = true;
                                    break;
                                }
                            }
                        }

                        if (need)
                        {
                            // This is a required note
                            currentNote.keysPressed.Add(keyId);

                            if (currentNote.keysPressed.Count == currentNote.notes.Count)
                            {
                                // Correct number of notes pressed
                                bool isPerfect = Mathf.Abs(deltaTime) < perfectTolerance;

                                foreach (var nn in currentNote.notes)
                                {
                                    nn.Success(isPerfect);
                                }
                                currentNote.notes.Clear();
                                currentNote.notes = null;

                                // Run success here
                                Success(isPerfect);
                            }
                        }
                        else
                        {
                            foreach (var nn in currentNote.notes)
                            {
                                nn.Fail();
                            }
                            currentNote.notes.Clear();
                            currentNote.notes = null;

                            // Run fail here
                            Fail();
                        }
                    }
                }
            }
        }

        activeNotes.RemoveAll((n) => n.notes == null);

        // Update positions
        foreach (var n in activeNotes)
        {
            float deltaTime = n.targetTime - currentTime;
            float t = 1.0f - (deltaTime / timeBeforeNote);
            float x = Mathf.LerpUnclamped(n.srcX, targetSpriteRenderer.transform.position.x, t);

            foreach (var nn in n.notes)
            {
                nn.transform.position = nn.transform.position.ChangeX(x);
            }
            
            if (deltaTime < tolerance.x)
            {
                foreach (var nn in n.notes)
                {
                    nn.Fail();

                    // Run fail here
                    Fail();
                }
                n.notes.Clear();
                n.notes = null;
            }
        }
    }

    void UpdateEffects()
    {
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
                booingAudioSource.FadeTo(0.0f, 0.25f);
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
        NoteInstance noteInstance = new();
        noteInstance.notes = new();

        DualNote dualNote = dualNoteCombos[0];
        int maxNotesPerSpawn = 1;
        float t = (float)currentIndex / (float)beatPos.Count;
        if (t > dualNoteStartsAt)
        {
            maxNotesPerSpawn = 2;
            int randIndex = Random.Range(0, dualNoteCombos.Count);
            dualNote = dualNoteCombos[randIndex];
        }
        if (t > tripleNoteStartsAt) maxNotesPerSpawn = 3;

        float timeSinceLastNote = Time.time - prevNoteTime;
        if (timeSinceLastNote < minTimeBetweenNotes * 2.0f)
        {
            maxNotesPerSpawn = 1;
        }

        int totalNotes = Mathf.Min(notePrefabs.Count, maxNotesPerSpawn);
        int notesToSpawn = Random.Range(1, totalNotes + 1);

        // Pick unique indices to spawn
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < notePrefabs.Count; i++)
        {
            if ((timeSinceLastNote < minTimeBetweenNotes * 2.0f) && (prevNote == i)) continue;
            availableIndices.Add(i);
        }

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

            prevNoteTime = Time.time;
            prevNote = selected;

            noteInstance.notes.Add(noteScript);
        }

        noteInstance.keysPressed = new();
        noteInstance.srcX = spawnPoints[0].position.x;
        noteInstance.targetTime = GetCurrentTime() + timeBeforeNote;

        activeNotes.Add(noteInstance);
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
        {
            booMeter += booPerFail;
            applauseAudioSource.FadeTo(0.0f, 0.5f);
            if (booMeter > 0.3f)
            {
                applauseOnZero = true;
            }
        }
        suckOMeter.transform.localScale = Vector3.one * 1.25f;
        suckOMeter.transform.ScaleTo(Vector3.one, 0.1f);

        consecutiveFails++;

        if (consecutiveFails > 5)
        {
            audioSource.pitch = -0.5f;
            audioSource.PitchShift(1.0f, 0.25f).EaseFunction(Ease.Sqr);

            consecutiveFails = 0;
        }
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

    private void Success(bool isPerfect)
    {
        if (animTargetState == AnimTargetState.Success) return;

        FlashTarget(targetSuccessColor);

        animTargetState = AnimTargetState.Success;

        if (booMeter > 0.0f)
        {            
            if (isPerfect) booMeter += booPerPerfectSuccess;
            else booMeter += booPerSuccess;

            if (booMeter <= 0.0f)
            {
                booMeter = 0.0f;
                if (applauseOnZero)
                {
                    applauseAudioSource.FadeTo(1.0f, 0.5f).Done(() =>
                    {
                        applauseAudioSource.FadeTo(0.0f, 0.5f).DelayStart(2.0f);
                    });
                    applauseOnZero = false;
                }
            }
        }

        successNotes++;
        consecutiveFails = 0;
    }
}

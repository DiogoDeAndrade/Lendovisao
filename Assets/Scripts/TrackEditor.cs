using System.Collections.Generic;
using UnityEngine;

public class TrackEditor : MonoBehaviour
{
    [SerializeField] private BeatRecording recordingToEdit;
    [Header("References")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Camera editorCamera;

    [Header("Layout")]
    [SerializeField] private float worldUnitsPerSecond = 5f;
    [SerializeField] private float lineSpacing = 1.5f;
    [SerializeField] private float noteSize = 0.5f;
    [SerializeField] private float lineY = 0f;
    [SerializeField] private float marginSeconds = 1f;

    [Header("Zoom")]
    [SerializeField] private float startingOrthoSize = 8f;
    [SerializeField] private float minOrthoSize = 2f;
    [SerializeField] private float maxOrthoSize = 20f;
    [SerializeField] private float zoomSpeed = 1f;

    [Header("Playhead")]
    [Tooltip("Where on screen the playhead sits during playback (0 = left edge, 1 = right edge)")]
    [SerializeField, Range(0.05f, 0.5f)] private float playheadScreenFraction = 0.15f;

    // -- Colours ------------------------------------------------------
    private readonly Color[] noteColors = new Color[]
    {
        new Color(0.90f, 0.25f, 0.25f, 1f), // Note1
        new Color(0.20f, 0.65f, 0.90f, 1f), // Note2
        new Color(0.25f, 0.85f, 0.35f, 1f), // Note3
        new Color(0.95f, 0.75f, 0.15f, 1f), // Note4
    };

    private static readonly BeatRecording.NoteType[] lineToFlag =
    {
        BeatRecording.NoteType.Note1,
        BeatRecording.NoteType.Note2,
        BeatRecording.NoteType.Note3,
        BeatRecording.NoteType.Note4,
    };

    // -- Runtime objects ----------------------------------------------
    private Transform staveRoot;
    private Transform notesRoot;
    private LineRenderer[] staveLines;
    private LineRenderer playheadLine;
    private Sprite circleSprite;

    private class NoteVisual
    {
        public GameObject go;
        public SpriteRenderer sr;
        public SpriteRenderer randomIndicator;
        public int beatIndex;
        public int line;
    }
    private List<NoteVisual> noteVisuals = new();

    // -- Interaction state --------------------------------------------
    private bool isPanning;
    private Vector3 panStartWorld;
    private Vector3 panStartCamPos;

    private NoteVisual dragNote;
    private float dragTimeOffset;

    private bool isPlaying;
    private Vector3 playheadReturnCamPos;

    // -- Undo / Redo --------------------------------------------------
    private class BeatSnapshot
    {
        public List<BeatRecording.BeatData> beats;
    }
    private Stack<BeatSnapshot> undoStack = new();
    private Stack<BeatSnapshot> redoStack = new();

    // -- Tick marks ---------------------------------------------------
    private Transform tickRoot;

    // ------------------------------------------------------------------
    void Start()
    {
        if (recordingToEdit == null || recordingToEdit.audioClip == null)
        {
            Debug.LogError("[TrackEditor] No BeatRecording or AudioClip assigned.");
            enabled = false;
            return;
        }

        if (editorCamera == null) editorCamera = Camera.main;

        // Apply starting zoom
        editorCamera.orthographicSize = Mathf.Clamp(startingOrthoSize, minOrthoSize, maxOrthoSize);
        // Centre camera on the stave vertically, start of track horizontally (with margin)
        float margin = marginSeconds * worldUnitsPerSecond;
        float camX = -margin + editorCamera.orthographicSize * editorCamera.aspect;
        editorCamera.transform.position = new Vector3(camX, lineY, editorCamera.transform.position.z);

        audioSource.clip = recordingToEdit.audioClip;
        audioSource.Stop();
        ClampCamera();

        circleSprite = CreateCircleSprite(64, Color.white);

        BuildStave();
        RebuildNoteVisuals();
        BuildPlayhead();
    }

    void Update()
    {
        HandleZoom();
        HandleNavigation();
        HandleSpaceBar();
        HandleMouse();
        UpdatePlayhead();
        UpdateTickMarks();
    }

    // -- Keyboard navigation ------------------------------------------
    void HandleNavigation()
    {
        if (isPlaying) return;

        // Left/Right: step one screen-width at a time (GetKey for repeat)
        float camWidth = editorCamera.orthographicSize * editorCamera.aspect * 2f;
        float shiftMultiplier = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 4f : 1f;
        float stepSpeed = camWidth * 1.5f * shiftMultiplier * Time.deltaTime;

        if (Input.GetKey(KeyCode.RightArrow))
        {
            var pos = editorCamera.transform.position;
            pos.x += stepSpeed;
            editorCamera.transform.position = pos;
            ClampCamera();
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            var pos = editorCamera.transform.position;
            pos.x -= stepSpeed;
            editorCamera.transform.position = pos;
            ClampCamera();
        }

        // Home: jump to start
        if (Input.GetKeyDown(KeyCode.Home))
        {
            float margin = marginSeconds * worldUnitsPerSecond;
            float camHalfW = editorCamera.orthographicSize * editorCamera.aspect;
            var pos = editorCamera.transform.position;
            pos.x = -margin + camHalfW;
            editorCamera.transform.position = pos;
            ClampCamera();
        }

        // End: jump to end
        if (Input.GetKeyDown(KeyCode.End))
        {
            float clipLen = recordingToEdit.audioClip.length;
            float margin = marginSeconds * worldUnitsPerSecond;
            float camHalfW = editorCamera.orthographicSize * editorCamera.aspect;
            var pos = editorCamera.transform.position;
            pos.x = clipLen * worldUnitsPerSecond + margin - camHalfW;
            editorCamera.transform.position = pos;
            ClampCamera();
        }
    }

    // -- Undo / Redo --------------------------------------------------
    void PushUndo()
    {
        undoStack.Push(TakeSnapshot());
        redoStack.Clear();
    }

    BeatSnapshot TakeSnapshot()
    {
        var snap = new BeatSnapshot();
        snap.beats = new List<BeatRecording.BeatData>();
        foreach (var bd in recordingToEdit.beatData)
        {
            snap.beats.Add(new BeatRecording.BeatData
            {
                beatTime = bd.beatTime,
                randomNote = bd.randomNote,
                definedNote = bd.definedNote,
            });
        }
        return snap;
    }

    void RestoreSnapshot(BeatSnapshot snap)
    {
        recordingToEdit.beatData.Clear();
        foreach (var bd in snap.beats)
        {
            recordingToEdit.beatData.Add(new BeatRecording.BeatData
            {
                beatTime = bd.beatTime,
                randomNote = bd.randomNote,
                definedNote = bd.definedNote,
            });
        }
        RebuildNoteVisuals();
    }

    /// <summary>
    /// Intercepts keyboard shortcuts in OnGUI so we can call Event.Use()
    /// to prevent them from propagating to the Unity Editor's own undo system.
    /// </summary>
    void OnGUI()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        bool ctrl = e.control || e.command;
        if (!ctrl) return;

        if (e.keyCode == KeyCode.Z)
        {
            if (e.shift)
                Redo();
            else
                Undo();
            e.Use();
        }
        else if (e.keyCode == KeyCode.Y)
        {
            Redo();
            e.Use();
        }
        else if (e.keyCode == KeyCode.S)
        {
            Save();
            e.Use();
        }
    }

    public void Undo()
    {
        if (undoStack.Count == 0) return;
        redoStack.Push(TakeSnapshot());
        RestoreSnapshot(undoStack.Pop());
    }

    public void Redo()
    {
        if (redoStack.Count == 0) return;
        undoStack.Push(TakeSnapshot());
        RestoreSnapshot(redoStack.Pop());
    }

    // -- Save (call from your button) ---------------------------------
    public void Save()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(recordingToEdit);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[TrackEditor] Saved {recordingToEdit.name}");
#endif
    }

    public bool HasUnsavedChanges => undoStack.Count > 0;

    // -- Build stave lines --------------------------------------------
    void BuildStave()
    {
        staveRoot = new GameObject("EditorStave").transform;
        staveLines = new LineRenderer[4];

        float clipLen = recordingToEdit.audioClip.length;
        float margin = marginSeconds * worldUnitsPerSecond;
        float totalWidth = clipLen * worldUnitsPerSecond;

        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject($"Line_{i + 1}");
            go.transform.SetParent(staveRoot);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(1, 1, 1, 0.25f);
            lr.endColor = new Color(1, 1, 1, 0.25f);
            lr.sortingOrder = 0;

            float y = GetLineY(i);
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(-margin, y, 0));
            lr.SetPosition(1, new Vector3(totalWidth + margin, y, 0));

            staveLines[i] = lr;
        }

        // -- Boundary double-lines at t=0 and t=clipLen ---------------
        BuildBoundaryMarker(0f, "BoundaryStart");
        BuildBoundaryMarker(totalWidth, "BoundaryEnd");

        for (int i = 0; i < 4; i++)
        {
            var label = new GameObject($"Label_{i + 1}");
            label.transform.SetParent(staveRoot);
            var tm = label.AddComponent<TextMesh>();
            tm.text = $"Note {i + 1}";
            tm.fontSize = 32;
            tm.characterSize = 0.12f;
            tm.color = noteColors[i];
            tm.anchor = TextAnchor.MiddleRight;
            tm.alignment = TextAlignment.Right;
            label.transform.position = new Vector3(-0.3f, GetLineY(i), 0);
            label.AddComponent<StickToScreenLeft>().Init(editorCamera, GetLineY(i), -0.3f);
        }
    }

    // -- Boundary double-line marker ---------------------------------
    void BuildBoundaryMarker(float worldX, string name)
    {
        float topY = GetLineY(0) + lineSpacing * 0.5f;
        float bottomY = GetLineY(3) - lineSpacing * 0.5f;
        float gap = 0.12f;
        Color col = new Color(1, 1, 1, 0.5f);
        Material mat = new Material(Shader.Find("Sprites/Default"));

        for (int i = 0; i < 2; i++)
        {
            float x = worldX + (i == 0 ? -gap : gap);
            var go = new GameObject($"{name}_{i}");
            go.transform.SetParent(staveRoot);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = 0.06f;
            lr.endWidth = 0.06f;
            lr.material = mat;
            lr.startColor = col;
            lr.endColor = col;
            lr.sortingOrder = 2;
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(x, topY, 0));
            lr.SetPosition(1, new Vector3(x, bottomY, 0));
        }
    }

    // -- Build / rebuild note visuals ---------------------------------
    void RebuildNoteVisuals()
    {
        foreach (var nv in noteVisuals)
            if (nv.go != null) Destroy(nv.go);
        noteVisuals.Clear();

        if (notesRoot != null) Destroy(notesRoot.gameObject);
        notesRoot = new GameObject("EditorNotes").transform;

        var beatData = recordingToEdit.beatData;
        for (int bi = 0; bi < beatData.Count; bi++)
            CreateVisualsForBeat(bi);
    }

    void CreateVisualsForBeat(int bi)
    {
        var bd = recordingToEdit.beatData[bi];
        List<int> lines = GetDisplayLines(bd, bi);

        foreach (int line in lines)
            noteVisuals.Add(SpawnNoteVisual(bi, line, bd.beatTime, bd.randomNote));
    }

    NoteVisual SpawnNoteVisual(int beatIndex, int line, float beatTime, bool isRandom)
    {
        float x = beatTime * worldUnitsPerSecond;
        float y = GetLineY(line);

        var go = new GameObject($"Beat_{beatIndex}_L{line}");
        go.transform.SetParent(notesRoot);
        go.transform.position = new Vector3(x, y, 0);
        go.transform.localScale = Vector3.one * noteSize;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circleSprite;
        sr.color = noteColors[line];
        sr.sortingOrder = 10;

        SpriteRenderer randomInd = null;
        if (isRandom)
        {
            var indGo = new GameObject("RandomIndicator");
            indGo.transform.SetParent(go.transform);
            indGo.transform.localPosition = Vector3.zero;
            indGo.transform.localScale = Vector3.one * 0.35f;
            randomInd = indGo.AddComponent<SpriteRenderer>();
            randomInd.sprite = circleSprite;
            randomInd.color = new Color(1, 1, 1, 0.7f);
            randomInd.sortingOrder = 11;
        }

        return new NoteVisual
        {
            go = go,
            sr = sr,
            randomIndicator = randomInd,
            beatIndex = beatIndex,
            line = line,
        };
    }

    // -- Playhead -----------------------------------------------------
    void BuildPlayhead()
    {
        var go = new GameObject("Playhead");
        playheadLine = go.AddComponent<LineRenderer>();
        playheadLine.useWorldSpace = true;
        playheadLine.startWidth = 0.06f;
        playheadLine.endWidth = 0.06f;
        playheadLine.material = new Material(Shader.Find("Sprites/Default"));
        playheadLine.startColor = new Color(1, 0.3f, 0.3f, 0.9f);
        playheadLine.endColor = new Color(1, 0.3f, 0.3f, 0.9f);
        playheadLine.sortingOrder = 20;
        playheadLine.positionCount = 2;
        playheadLine.enabled = false;
    }

    /// <summary>
    /// Returns the world-X position of the playhead marker on screen.
    /// This is a fixed position near the left edge of the camera view.
    /// </summary>
    float GetPlayheadWorldX()
    {
        float camHalfW = editorCamera.orthographicSize * editorCamera.aspect;
        float camLeft = editorCamera.transform.position.x - camHalfW;
        float camWidth = camHalfW * 2f;
        return camLeft + camWidth * playheadScreenFraction;
    }

    /// <summary>
    /// Returns the audio time that corresponds to the playhead's screen position.
    /// </summary>
    float GetTimeAtPlayhead()
    {
        return GetPlayheadWorldX() / worldUnitsPerSecond;
    }

    /// <summary>
    /// Positions the camera so that the given audio time lines up with the playhead marker.
    /// </summary>
    void SetCameraForTime(float time)
    {
        // We want: camLeft + camWidth * fraction = time * worldUnitsPerSecond
        // camLeft = camPos.x - camHalfW
        // So: camPos.x - camHalfW + camWidth * fraction = time * wups
        //     camPos.x = time * wups + camHalfW - camWidth * fraction
        float camHalfW = editorCamera.orthographicSize * editorCamera.aspect;
        float camWidth = camHalfW * 2f;
        float targetCamX = time * worldUnitsPerSecond + camHalfW - camWidth * playheadScreenFraction;
        var pos = editorCamera.transform.position;
        editorCamera.transform.position = new Vector3(targetCamX, pos.y, pos.z);
    }

    void UpdatePlayhead()
    {
        if (isPlaying && audioSource.isPlaying)
        {
            playheadLine.enabled = true;

            // Position camera so the current audio time sits at the playhead mark
            SetCameraForTime(audioSource.time);
            ClampCamera();

            // Draw the playhead line at its fixed screen position
            float phX = GetPlayheadWorldX();
            float top = GetLineY(0) + lineSpacing;
            float bottom = GetLineY(3) - lineSpacing;
            playheadLine.SetPosition(0, new Vector3(phX, top, 0));
            playheadLine.SetPosition(1, new Vector3(phX, bottom, 0));
        }
        else
        {
            playheadLine.enabled = false;
        }
    }

    // -- Space-bar preview --------------------------------------------
    void HandleSpaceBar()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            playheadReturnCamPos = editorCamera.transform.position;
            float startTime = Mathf.Max(0f, GetTimeAtPlayhead());
            audioSource.time = startTime;
            audioSource.Play();
            isPlaying = true;
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            audioSource.Stop();
            editorCamera.transform.position = playheadReturnCamPos;
            isPlaying = false;
        }
    }

    // -- Zoom ---------------------------------------------------------
    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        Vector3 mouseWorldBefore = editorCamera.ScreenToWorldPoint(Input.mousePosition);

        editorCamera.orthographicSize = Mathf.Clamp(
            editorCamera.orthographicSize - scroll * zoomSpeed,
            minOrthoSize, maxOrthoSize);

        Vector3 mouseWorldAfter = editorCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector3 correction = mouseWorldBefore - mouseWorldAfter;
        correction.y = 0f; // lock vertical position
        editorCamera.transform.position += correction;
        ClampCamera();
    }

    // -- Mouse interactions -------------------------------------------
    void HandleMouse()
    {
        if (isPlaying) return;

        Vector3 mouseWorld = editorCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;

        float clipLen = recordingToEdit.audioClip.length;

        // -- Right-click: create / delete -----------------------------
        if (Input.GetMouseButtonDown(1))
        {
            var hit = FindNoteUnderMouse(mouseWorld);
            if (hit != null)
            {
                PushUndo();
                recordingToEdit.beatData.RemoveAt(hit.beatIndex);
                RebuildNoteVisuals();
            }
            else
            {
                int line = GetNearestLine(mouseWorld.y);
                if (line >= 0)
                {
                    PushUndo();
                    float time = Mathf.Clamp(mouseWorld.x / worldUnitsPerSecond, 0f, clipLen);
                    recordingToEdit.beatData.Add(new BeatRecording.BeatData
                    {
                        beatTime = time,
                        randomNote = false,
                        definedNote = lineToFlag[line],
                    });
                    SortBeats();
                    RebuildNoteVisuals();
                }
            }
        }

        // -- Left-click down: pick note or start pan ------------------
        if (Input.GetMouseButtonDown(0))
        {
            var hit = FindNoteUnderMouse(mouseWorld);
            if (hit != null)
            {
                PushUndo();
                dragNote = hit;
                dragTimeOffset = recordingToEdit.beatData[hit.beatIndex].beatTime
                               - mouseWorld.x / worldUnitsPerSecond;
            }
            else
            {
                isPanning = true;
                panStartWorld = mouseWorld;
                panStartCamPos = editorCamera.transform.position;
            }
        }

        // -- Drag -----------------------------------------------------
        if (Input.GetMouseButton(0))
        {
            if (dragNote != null)
            {
                float newTime = mouseWorld.x / worldUnitsPerSecond + dragTimeOffset;
                newTime = Mathf.Clamp(newTime, 0f, clipLen);

                int newLine = GetNearestLine(mouseWorld.y);
                if (newLine < 0) newLine = dragNote.line;

                var bd = recordingToEdit.beatData[dragNote.beatIndex];
                bd.beatTime = newTime;
                bd.randomNote = false;
                bd.definedNote = lineToFlag[newLine];

                dragNote.go.transform.position = new Vector3(
                    newTime * worldUnitsPerSecond, GetLineY(newLine), 0);
                dragNote.sr.color = noteColors[newLine];
                dragNote.line = newLine;

                if (dragNote.randomIndicator != null)
                {
                    Destroy(dragNote.randomIndicator.gameObject);
                    dragNote.randomIndicator = null;
                }
            }
            else if (isPanning)
            {
                Vector3 currentMouse = editorCamera.ScreenToWorldPoint(Input.mousePosition);
                currentMouse.z = 0;
                float deltaX = panStartWorld.x - currentMouse.x;
                var pos = panStartCamPos;
                pos.x += deltaX;
                editorCamera.transform.position = pos;
                ClampCamera();
                panStartWorld = editorCamera.ScreenToWorldPoint(Input.mousePosition);
                panStartWorld.z = 0;
                panStartCamPos = editorCamera.transform.position;
            }
        }

        // -- Release --------------------------------------------------
        if (Input.GetMouseButtonUp(0))
        {
            if (dragNote != null)
            {
                SortBeats();
                RebuildNoteVisuals();
            }
            dragNote = null;
            isPanning = false;
        }
    }

    NoteVisual FindNoteUnderMouse(Vector3 mouseWorld)
    {
        float bestDist = noteSize * 0.6f;
        NoteVisual best = null;
        foreach (var nv in noteVisuals)
        {
            if (nv.go == null) continue;
            float d = Vector2.Distance(mouseWorld, nv.go.transform.position);
            if (d < bestDist) { bestDist = d; best = nv; }
        }
        return best;
    }

    // -- Tick marks (time ruler) --------------------------------------
    void UpdateTickMarks()
    {
        if (tickRoot != null) Destroy(tickRoot.gameObject);
        tickRoot = new GameObject("Ticks").transform;

        float camHalfW = editorCamera.orthographicSize * editorCamera.aspect;
        float camLeft = editorCamera.transform.position.x - camHalfW;
        float camRight = editorCamera.transform.position.x + camHalfW;

        float viewWidth = camRight - camLeft;
        float step = 1f;
        if (viewWidth > 40) step = 5f;
        else if (viewWidth > 20) step = 2f;
        else if (viewWidth < 5) step = 0.5f;

        float topY = GetLineY(0) + lineSpacing * 0.7f;
        float bottomY = GetLineY(3) - lineSpacing * 0.3f;

        float startSec = Mathf.Floor(camLeft / worldUnitsPerSecond / step) * step;
        float endSec = camRight / worldUnitsPerSecond;

        Material lineMat = new Material(Shader.Find("Sprites/Default"));

        for (float t = startSec; t <= endSec; t += step)
        {
            if (t < 0) continue;
            float x = t * worldUnitsPerSecond;

            var lineGo = new GameObject("Tick");
            lineGo.transform.SetParent(tickRoot);
            var lr = lineGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = 0.02f;
            lr.endWidth = 0.02f;
            lr.material = lineMat;
            lr.startColor = new Color(1, 1, 1, 0.08f);
            lr.endColor = new Color(1, 1, 1, 0.08f);
            lr.sortingOrder = 1;
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(x, topY, 0));
            lr.SetPosition(1, new Vector3(x, bottomY, 0));

            var labelGo = new GameObject("TickLabel");
            labelGo.transform.SetParent(tickRoot);
            labelGo.transform.position = new Vector3(x + 0.05f, topY + 0.15f, 0);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = t >= 60f ? $"{(int)(t / 60)}:{(t % 60):00.0}" : $"{t:F1}s";
            tm.fontSize = 28;
            tm.characterSize = 0.08f;
            tm.color = new Color(1, 1, 1, 0.3f);
            tm.anchor = TextAnchor.LowerLeft;
        }
    }

    // -- Camera clamping ---------------------------------------------
    void ClampCamera()
    {
        float camHalfW = editorCamera.orthographicSize * editorCamera.aspect;
        float clipLen = recordingToEdit.audioClip.length;
        float margin = marginSeconds * worldUnitsPerSecond;
        float minX = -margin + camHalfW;                                     // left edge at margin before t=0
        float maxX = clipLen * worldUnitsPerSecond + margin - camHalfW;       // right edge at margin after clip end
        if (maxX < minX) maxX = minX;

        var pos = editorCamera.transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        editorCamera.transform.position = pos;
    }

    // -- Helpers -------------------------------------------------------
    float GetLineY(int line)
    {
        return lineY + (1.5f - line) * lineSpacing;
    }

    int GetNearestLine(float worldY)
    {
        float best = float.MaxValue;
        int bestLine = -1;
        for (int i = 0; i < 4; i++)
        {
            float d = Mathf.Abs(worldY - GetLineY(i));
            if (d < best) { best = d; bestLine = i; }
        }
        return best < lineSpacing * 0.6f ? bestLine : -1;
    }

    List<int> GetDisplayLines(BeatRecording.BeatData bd, int seedIndex)
    {
        var lines = new List<int>();
        if (bd.randomNote)
        {
            var rng = new System.Random(seedIndex * 7919 + recordingToEdit.GetHashCode());
            lines.Add(rng.Next(0, 4));
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                if ((bd.definedNote & lineToFlag[i]) != 0)
                    lines.Add(i);
            }
            if (lines.Count == 0) lines.Add(0);
        }
        return lines;
    }

    void SortBeats()
    {
        recordingToEdit.beatData.Sort((a, b) => a.beatTime.CompareTo(b.beatTime));
    }

    // -- Procedural circle sprite -------------------------------------
    Sprite CreateCircleSprite(int resolution, Color color)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float centre = resolution * 0.5f;
        float radius = centre - 1;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
                float alpha = Mathf.Clamp01((radius - dist) * 2f);
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution),
                             new Vector2(0.5f, 0.5f), resolution);
    }
}

// -- Helper: keeps a label pinned to the left edge of the camera ------
public class StickToScreenLeft : MonoBehaviour
{
    private Camera cam;
    private float worldY;
    private float offsetX;

    public void Init(Camera camera, float y, float offset)
    {
        cam = camera;
        worldY = y;
        offsetX = offset;
    }

    void LateUpdate()
    {
        if (cam == null) return;
        float leftEdge = cam.transform.position.x - cam.orthographicSize * cam.aspect;
        transform.position = new Vector3(leftEdge + offsetX, worldY, 0);
    }
}
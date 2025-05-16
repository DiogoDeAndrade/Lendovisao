using UC;
using UnityEngine;

public class Note : MonoBehaviour
{
    [HideInInspector] public float          moveTime;                    // Time from spawn to beat
    [HideInInspector] public AudioSource    audioSource;           // Set by spawner
    [HideInInspector] public GameSystem     gameSystem;               // Set by spawner

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteRenderer shadowSpriteRenderer;

    [SerializeField] private float  deltaMove = 1200.0f;
    [SerializeField] private string button;
    
    [SerializeField] private float  tolerance;
    [SerializeField] private float perfectTolerance = 0.025f;
    [SerializeField] private BounceText failText;
    [SerializeField] private BounceText okText;
    [SerializeField] private BounceText perfectText;

    private float spawnTime;                  // Time at which the note was created
    private Vector3 startPosition;
    private bool active = true;
    private bool hit = false;

    public bool inTarget => Mathf.Abs(((audioSource.time - spawnTime) / moveTime) - 1.0f) < tolerance;
    public bool isHit => hit;
    public bool isActive => active;
    public string GetButton() => button;

    void Start()
    {
        if (audioSource == null)
        {
            Debug.LogError("Note has no AudioSource assigned!");
            enabled = false;
            return;
        }

        spawnTime = audioSource.time;
        startPosition = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (!audioSource.isPlaying)
            return;

        float elapsed = audioSource.time - spawnTime;

        // Clamp to [0, moveTime]
        float t = elapsed / moveTime;

        // Move by deltaMove units over moveTime seconds, rightward
        transform.position = startPosition + Vector3.right * (deltaMove * t);

        if (!active) return;

        if (Mathf.Abs(t - 1.0f) < tolerance)
        {
            if (Input.GetButtonDown(button))
            {
                hit = true;
            }
        }

        if (t > (1.0f + tolerance))
        {
            // Fail 2 - disappear the note, warn GameSystem
            Fail();
            gameSystem.Fail();
        }
    }

    public void Success()
    {
        // Success
        transform.ScaleTo(Vector3.one * 4.0f, 0.1f);
        spriteRenderer.FadeTo(spriteRenderer.color.ChangeAlpha(0.0f), 0.1f).Done(() =>
        {
            Destroy(gameObject);
        });
        shadowSpriteRenderer.FadeTo(shadowSpriteRenderer.color.ChangeAlpha(0.0f), 0.1f);
        active = false;

        float elapsed = audioSource.time - spawnTime;

        // Clamp to [0, moveTime]
        float t = elapsed / moveTime;
        if (Mathf.Abs(t - 1.0f) < perfectTolerance)
        {
            Instantiate(perfectText, transform.position, transform.rotation);
        }
        else
        {
            Instantiate(okText, transform.position, transform.rotation);
        }
    }

    public void Fail()
    {
        transform.ScaleTo(Vector3.zero, 0.1f);
        spriteRenderer.FadeTo(spriteRenderer.color.ChangeAlpha(0.0f), 0.1f).Done(() =>
        {
            Destroy(gameObject);
        });
        shadowSpriteRenderer.FadeTo(shadowSpriteRenderer.color.ChangeAlpha(0.0f), 0.1f);
        active = false;

        Instantiate(failText, transform.position, transform.rotation);
    }

}

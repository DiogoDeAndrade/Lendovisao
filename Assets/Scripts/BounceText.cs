using UC;
using UnityEngine;

public class BounceText : MonoBehaviour
{
    [SerializeField] private Vector2 velocityX = Vector2.zero;
    [SerializeField] private float velocityY = 100.0f;
    [SerializeField] private float scale = 1.0f;

    Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        Vector2 velocity = new Vector2(velocityX.Random(), velocityY);
        transform.rotation = Quaternion.LookRotation(Vector3.forward, velocity.normalized);
        rb.linearVelocity = velocity;
        transform.localScale = Vector3.zero;
        transform.ScaleTo(Vector3.one * scale, 0.2f);

        Invoke(nameof(FadeOut), 0.3f);
    }

    void FadeOut()
    {
        var sr = GetComponent<SpriteRenderer>();
        sr.FadeTo(sr.color.ChangeAlpha(0.0f), 0.25f).Done(() => Destroy(gameObject));
    }
}

using UnityEngine;

public class AutoRotate : MonoBehaviour
{
    [SerializeField] private float startAngle = 0f;
    [SerializeField] private float endAngle = 90f;
    [SerializeField] private float duration = 2f; // Time to go from start to end

    private float t = 0f;           // Normalized interpolation factor (0 to 1)
    private bool forward = true;    // Direction of tween

    void Update()
    {
        if (duration <= 0f) return;

        // Update interpolation factor
        float delta = Time.deltaTime / duration;
        t += forward ? delta : -delta;

        // Reverse direction at bounds
        if (t >= 1f)
        {
            t = 1f;
            forward = false;
        }
        else if (t <= 0f)
        {
            t = 0f;
            forward = true;
        }

        // Interpolate with smooth easing
        float easedT = Mathf.SmoothStep(0f, 1f, t);
        float angle = Mathf.Lerp(startAngle, endAngle, easedT);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}

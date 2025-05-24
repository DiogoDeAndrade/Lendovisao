using System.Collections.Generic;
using NaughtyAttributes;
using UC;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundElement : MonoBehaviour
{
    [SerializeField] 
    private List<Sprite>    availableSprites;
    [SerializeField, MinMaxSlider(0.0f, 1.0f)] 
    private Vector2         scaleRange;
    [SerializeField, MinMaxSlider(0.0f, 20.0f)]
    private Vector2         bobAmplitudeRange;
    [SerializeField, MinMaxSlider(0.0f, 360.0f)]
    private Vector2         bobSpeedRange;

    float amplitude;
    float speed;

    float   angle = 0.0f;
    Vector2 originalPos;
    RectTransform rectTransform;

    void Start()
    {
        var img = GetComponent<Image>();
        img.sprite = availableSprites.Random();

        amplitude = bobAmplitudeRange.Random();
        speed = bobSpeedRange.Random();

        angle = Random.Range(0.0f, 360.0f);

        rectTransform = transform as RectTransform;
        originalPos = rectTransform.anchoredPosition;
        rectTransform.localScale = Vector3.one * scaleRange.Random();
    }

    void Update()
    {
        angle += Time.deltaTime * speed;
        while (angle > 360.0f) angle -= 360.0f;

        float s = amplitude * Mathf.Sin(angle * Mathf.Deg2Rad);

        rectTransform.anchoredPosition = originalPos + Vector2.up * s;
    }
}

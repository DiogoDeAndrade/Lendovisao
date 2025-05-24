using NaughtyAttributes;
using UC;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class CharacterSelect : MonoBehaviour
{
    [System.Serializable]
    struct Legend
    {
        public Light2D      light;
        public Material     material;
        [Scene]
        public string       scene;
        public CanvasGroup  titleCanvas;
    }

    [SerializeField] private Legend[]       legends;
    [SerializeField] private float          changeCooldown;
    [SerializeField] private InputControl   moveSelectionInput;
    [SerializeField] private InputControl   selectInput;

    int selection = 0;
    float[] initialLightIntensity;
    float cooldown = 0.0f;

    void Start()
    {
        initialLightIntensity = new float[legends.Length];
        for (int i = 0; i < legends.Length; i++)
        {
            initialLightIntensity[i] = legends[i].light.intensity;
            legends[i].light.intensity = 0.0f;
        }

        UpdateSelection();
    }

    void Update()
    {
        if (cooldown > 0.0f)
        {
            cooldown -= Time.deltaTime;
        }
        else
        {
            if (moveSelectionInput.GetAxis() > 0.1f)
            {
                selection = (selection + 1) % legends.Length;
                UpdateSelection();
                cooldown = changeCooldown;
            }
            else if (moveSelectionInput.GetAxis() < -0.1f)
            {
                selection = (selection + legends.Length - 1) % legends.Length;
                UpdateSelection();
                cooldown = changeCooldown;
            }
            if (selectInput.IsDown())
            {
                SoundManager.PlayMusic(null);
                FullscreenFader.FadeOut(0.5f, Color.black, () =>
                {
                    SceneManager.LoadScene(legends[selection].scene);
                });
            }
        }
    }

    void UpdateSelection()
    {
        for (int i = 0; i < legends.Length; i++)
        {
            if (i == selection)
            {
                legends[i].titleCanvas.FadeIn(0.2f);
                legends[i].light.FadeTo(initialLightIntensity[i], 0.2f);
                legends[i].material.SetFloat("_OutlineEnable", 1.0f);
            }
            else
            {
                legends[i].titleCanvas.FadeOut(0.2f);
                legends[i].light.FadeTo(0.0f, 0.2f);
                legends[i].material.SetFloat("_OutlineEnable", 0.0f);
            }
        }
    }
}

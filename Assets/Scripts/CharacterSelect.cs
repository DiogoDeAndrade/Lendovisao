using NaughtyAttributes;
using UC;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class CharacterSelect : MonoBehaviour
{
    [SerializeField] private Light2D[] selectionLights;
    [SerializeField] private Material[] materials;
    [SerializeField, Scene] private string[] scenes;
    [SerializeField] private InputControl moveSelectionInput;
    [SerializeField] private InputControl selectInput;

    int selection = 0;
    float[] initialLightIntensity;
    float cooldown = 0.0f;

    void Start()
    {
        initialLightIntensity = new float[selectionLights.Length];
        for (int i = 0; i < selectionLights.Length; i++)
        {
            initialLightIntensity[i] = selectionLights[i].intensity;
            selectionLights[i].intensity = 0.0f;
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
                selection = (selection + 1) % selectionLights.Length;
                UpdateSelection();
                cooldown = 0.5f;
            }
            else if (moveSelectionInput.GetAxis() < -0.1f)
            {
                selection = (selection + selectionLights.Length - 1) % selectionLights.Length;
                UpdateSelection();
                cooldown = 0.5f;
            }
            if (selectInput.IsDown())
            {
                FullscreenFader.FadeOut(0.5f, Color.black, () =>
                {
                    SceneManager.LoadScene(scenes[selection]);
                });
            }
        }
    }

    void UpdateSelection()
    {
        for (int i = 0; i < selectionLights.Length; i++)
        {
            if (i == selection)
            {
                selectionLights[i].FadeTo(initialLightIntensity[i], 0.2f);
                materials[i].SetFloat("_OutlineEnable", 1.0f);
            }
            else
            {
                selectionLights[i].FadeTo(0.0f, 0.2f);
                materials[i].SetFloat("_OutlineEnable", 0.0f);
            }
        }
    }
}

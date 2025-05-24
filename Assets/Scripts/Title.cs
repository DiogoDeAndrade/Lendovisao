using NaughtyAttributes;
using UC;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Title : MonoBehaviour
{
    [SerializeField] UIButton startButton;
    [SerializeField] UIButton creditsButton;
    [SerializeField] UIButton quitButton;
    [SerializeField] private BigTextScroll creditsScroll;
    [SerializeField, Scene] string gameScene;
    [SerializeField] private AudioClip defaultMusic;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startButton.onInteract += StartButton_onInteract;
        creditsButton.onInteract += CreditsButton_onInteract;
        quitButton.onInteract += QuitButton_onInteract;

        if (defaultMusic)
            SoundManager.PlayMusic(defaultMusic);
    }

    private void StartButton_onInteract(BaseUIControl control)
    {
        FullscreenFader.FadeOut(1.0f, Color.black, () =>
        {
            SceneManager.LoadScene(gameScene);
        }); 
    }

    private void CreditsButton_onInteract(BaseUIControl control)
    {
        var canvasGroup = creditsScroll.GetComponentInParent<CanvasGroup>();
        canvasGroup.FadeIn(0.5f);

        creditsScroll.Reset();

        creditsScroll.onEndScroll += CreditsScroll_onEndScroll;
    }

    private void CreditsScroll_onEndScroll()
    {
        var canvasGroup = creditsScroll.GetComponentInParent<CanvasGroup>();
        canvasGroup.FadeOut(0.5f);

        creditsScroll.onEndScroll -= CreditsScroll_onEndScroll;
    }

    private void QuitButton_onInteract(BaseUIControl control)
    {
        SoundManager.PlayMusic(null);
        FullscreenFader.FadeOut(1.0f, Color.black, () =>
        {
            Application.Quit();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
        });
    }
}

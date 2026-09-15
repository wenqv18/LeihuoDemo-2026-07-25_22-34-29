using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using System.Collections;

public class VideoTransition : MonoBehaviour
{
    public Animator animator;
    public GameObject videoPanel;
    public VideoPlayer videoPlayer;
    public string nextScene = SceneNames.FirstLevel;

    public void StartTransition()
    {
        StartCoroutine(PlaySequence(continueExistingRun: true));
    }

    public void StartVideoOnly()
    {
        StartCoroutine(PlayVideoOnly(continueExistingRun: false));
    }

    private IEnumerator PlaySequence(bool continueExistingRun)
    {
        if (animator != null)
        {
            animator.Play("GCanimation");
        }

        yield return new WaitForSeconds(1.17f);
        if (videoPanel != null)
        {
            videoPanel.SetActive(true);
        }

        yield return StartCoroutine(PlayVideoAndLoad(continueExistingRun));
    }

    private IEnumerator PlayVideoOnly(bool continueExistingRun)
    {
        if (videoPanel != null)
        {
            videoPanel.SetActive(true);
        }

        yield return StartCoroutine(PlayVideoAndLoad(continueExistingRun));
    }

    private IEnumerator PlayVideoAndLoad(bool continueExistingRun)
    {
        if (videoPlayer == null)
        {
            LoadNextScene(continueExistingRun);
            yield break;
        }

        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        videoPlayer.Play();
        double duration = videoPlayer.frameRate > 0 ? videoPlayer.frameCount / videoPlayer.frameRate : videoPlayer.length;
        if (duration > 0)
        {
            yield return new WaitForSeconds((float)duration);
        }

        LoadNextScene(continueExistingRun);
    }

    private void LoadNextScene(bool continueExistingRun)
    {
        string sceneName;
        if (continueExistingRun && RunSaveService.HasSave)
        {
            sceneName = RunSaveService.GetContinueSceneName();
        }
        else
        {
            RunSaveService.StartNewAttempt(preserveFrozenFloors: true);
            sceneName = string.IsNullOrWhiteSpace(nextScene) ? SceneNames.FirstLevel : nextScene;
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}

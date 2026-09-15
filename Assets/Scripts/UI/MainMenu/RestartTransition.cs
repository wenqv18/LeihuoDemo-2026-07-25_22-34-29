using UnityEngine;
using DG.Tweening;
using System.Collections;

public class RestartTransition : MonoBehaviour
{
    public Animator animator;
    public GameObject confirmPanel;
    public GameObject lastRecordPanel;
    public float panelDelay = 1.0f;
    public float panelDuration = 0.5f;

    public void PlayRestartSequence()
    {
        StartCoroutine(Run());
    }

    public void ResetToMenu()
    {
        confirmPanel.SetActive(false);
        if (animator != null) animator.Play("New State");

        // 重新触发 LastRecordPanel 打字
        var lr = lastRecordPanel?.GetComponent<TypewriterEffect>();
        if (lr != null)
        {
            lr.StopAllCoroutines();
            lr.gameObject.SetActive(false);
            lr.gameObject.SetActive(true);
        }
    }

    private IEnumerator Run()
    {
        animator.Play("RestartAnim");
        yield return new WaitForSeconds(panelDelay);

        confirmPanel.transform.localScale = Vector3.zero;
        confirmPanel.SetActive(true);
        confirmPanel.transform.DOScale(1f, panelDuration).SetEase(Ease.OutBack);
    }
}

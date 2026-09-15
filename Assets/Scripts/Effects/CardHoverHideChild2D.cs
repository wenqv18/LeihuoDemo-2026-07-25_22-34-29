using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class CardHoverHideChild2D : MonoBehaviour
{
    [SerializeField] private string targetChildName = "card_background";
    [SerializeField] private SpriteRenderer targetRenderer;

    private bool originalRendererEnabled;
    private bool hasOriginalState;

    private void Awake()
    {
        ResolveTargetRenderer();
        CacheOriginalState();
    }

    private void OnEnable()
    {
        CacheOriginalState();
    }

    private void OnDisable()
    {
        SetTargetVisible(true);
    }

    private void OnMouseEnter()
    {
        SetTargetVisible(false);
    }

    private void OnMouseExit()
    {
        SetTargetVisible(true);
    }

    private void ResolveTargetRenderer()
    {
        if (targetRenderer != null)
        {
            return;
        }

        Transform target = transform.Find(targetChildName);
        if (target != null)
        {
            targetRenderer = target.GetComponent<SpriteRenderer>();
        }
    }

    private void CacheOriginalState()
    {
        ResolveTargetRenderer();
        if (targetRenderer == null || hasOriginalState)
        {
            return;
        }

        originalRendererEnabled = targetRenderer.enabled;
        hasOriginalState = true;
    }

    private void SetTargetVisible(bool visible)
    {
        ResolveTargetRenderer();
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.enabled = visible ? originalRendererEnabled : false;
    }
}

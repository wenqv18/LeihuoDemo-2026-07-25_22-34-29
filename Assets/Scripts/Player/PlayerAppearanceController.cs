using UnityEngine;

/// <summary>
/// 玩家外观：男生（Player0，默认）/ 女生（Player1）。
/// 读取 PlayerPrefs（Leihuo.Player.Appearance），替换玩家 SpriteRenderer 的美术，
/// 并按碰撞体高度对齐缩放，保证两种外观尺寸一致。
/// </summary>
public sealed class PlayerAppearanceController : MonoBehaviour
{
    public const string PrefsKey = "Leihuo.Player.Appearance";
    public const int MaleIndex = 0;
    public const int FemaleIndex = 1;

    private const string MaleSpritePath = "Player/Player0";
    private const string FemaleSpritePath = "Player/Player1";

    [SerializeField, Tooltip("无碰撞体时的兜底角色高度（有碰撞体时用碰撞体高度）")]
    private float characterHeight = 3.77f;

    private SpriteRenderer[] renderers;

    public static int CurrentIndex => Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, MaleIndex), MaleIndex, FemaleIndex);

    private void Awake()
    {
        Apply(CurrentIndex, true);
    }

    /// <summary>切换外观：持久化并立即应用到场景内所有玩家。</summary>
    public static void SetAppearance(int index)
    {
        int clamped = Mathf.Clamp(index, MaleIndex, FemaleIndex);
        PlayerPrefs.SetInt(PrefsKey, clamped);
        PlayerPrefs.Save();

        PlayerAppearanceController[] controllers = FindObjectsByType<PlayerAppearanceController>(
            FindObjectsInactive.Include);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
            {
                controllers[i].Apply(clamped, false);
            }
        }

        // 同步视频动画（男生用视频，女生用立绘）。
        PlayerVideoAnimator[] animators = FindObjectsByType<PlayerVideoAnimator>(
            FindObjectsInactive.Include);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].RefreshAppearance();
            }
        }
    }

    private void Apply(int index, bool adjustLayout)
    {
        Sprite sprite = Resources.Load<Sprite>(index == MaleIndex ? MaleSpritePath : FemaleSpritePath);
        if (sprite == null)
        {
            return;
        }

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (adjustLayout)
            {
                AlignToCollider(renderer, sprite);
            }

            renderer.sprite = sprite;
        }
    }

    private void AlignToCollider(SpriteRenderer renderer, Sprite sprite)
    {
        if (sprite == null || sprite.bounds.size.y <= 0.001f)
        {
            return;
        }

        Collider2D collider = GetComponent<Collider2D>();
        float targetHeight = collider != null ? collider.bounds.size.y : characterHeight;
        if (targetHeight <= 0.001f)
        {
            targetHeight = characterHeight;
        }

        float scale = targetHeight / sprite.bounds.size.y;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);

        if (collider != null)
        {
            // 角色底部中心对齐碰撞体底部中心（水平居中对齐）。
            Vector2 colliderBottomCenter = collider.bounds.center - new Vector3(0f, collider.bounds.extents.y, 0f);
            Vector2 spriteBottomCenter = renderer.transform.TransformPoint(
                sprite.bounds.center - new Vector3(0f, sprite.bounds.extents.y, 0f));
            renderer.transform.position += (Vector3)(colliderBottomCenter - spriteBottomCenter);
        }
    }
}

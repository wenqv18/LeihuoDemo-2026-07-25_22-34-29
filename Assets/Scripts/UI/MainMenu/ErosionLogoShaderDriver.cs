using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public sealed class ErosionLogoShaderDriver : MonoBehaviour
{
    [Min(0.1f)] public float duration = 4f;
    public bool autoPlay = true;

    private Graphic graphic;
    private Material runtimeMaterial;

    private void OnEnable()
    {
        graphic = GetComponent<Graphic>();
        Apply();
    }

    private void Update()
    {
        if (autoPlay)
        {
            float time = Mathf.Repeat(Time.unscaledTime, duration);
            ApplyAnimation(time);
            if (runtimeMaterial != null)
                runtimeMaterial.SetFloat("_GlitchSeed", Mathf.Floor(time * 30f));
        }
        Apply();
    }

    private void ApplyAnimation(float time)
    {
        if (time < 0.7f)
            SetValues(Mathf.SmoothStep(0.02f, 0.035f, time / 0.7f), 0.02f, 0f);
        else if (time < 1.7f)
            SetValues(Mathf.SmoothStep(0.035f, 0.06f, (time - 0.7f) / 1f), Mathf.Lerp(0.02f, 0.22f, (time - 0.7f) / 1f), 0f);
        else if (time < 2.2f)
            SetValues(0.075f, Mathf.SmoothStep(0.22f, 0.86f, (time - 1.7f) / 0.5f), Mathf.SmoothStep(0f, 0.55f, (time - 1.7f) / 0.5f));
        else if (time < 2.85f)
            SetValues(Mathf.SmoothStep(0.075f, 0.03f, (time - 2.2f) / 0.65f), Mathf.SmoothStep(0.86f, 0.1f, (time - 2.2f) / 0.65f), Mathf.SmoothStep(0.55f, 0f, (time - 2.2f) / 0.65f));
        else
            SetValues(0.02f, 0.04f, 0f);
    }

    private void SetValues(float erosion, float glitch, float flash)
    {
        if (runtimeMaterial == null)
            return;
        runtimeMaterial.SetFloat("_ErosionAmount", erosion);
        runtimeMaterial.SetFloat("_GlitchAmount", glitch);
        runtimeMaterial.SetFloat("_SignalFlash", flash);
    }

    private void Apply()
    {
        if (graphic == null)
            graphic = GetComponent<Graphic>();
        if (graphic == null)
            return;

        if (runtimeMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("UI/Animation/ErosionLogo");
            if (shader == null)
                shader = Shader.Find("UI/ErosionLogo");
            if (shader == null)
                return;
            runtimeMaterial = new Material(shader) { name = "ErosionLogo_Runtime" };
            graphic.material = runtimeMaterial;
        }
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
            return;
        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);
    }
}

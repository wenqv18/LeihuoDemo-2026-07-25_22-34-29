using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VisionCameraReticleView : MonoBehaviour
{
    private const int VisualVersion = 5;
    private const string ErosionFrameResourcePath = "UI/VisionCamera/vision_camera_erosion_frame";

    private sealed class ReticleParticle
    {
        public RectTransform Rect;
        public Image Image;
        public float PathPosition;
        public float Speed;
        public float Lifetime;
        public float Age;
        public float Size;
        public float Phase;
    }

    private sealed class SmokeParticle
    {
        public RectTransform Rect;
        public Image Image;
        public float PathPosition;
        public float Speed;
        public float Age;
        public float Lifetime;
        public float Size;
        public float NormalOffset;
        public float TangentOffset;
        public float Phase;
        public float DriftStrength;
        public Color BaseColor;
    }

    private readonly struct ErosionClump
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly float Strength;
        public readonly float StretchX;
        public readonly float StretchY;

        public ErosionClump(Vector2 center, float radius, float strength, float stretchX, float stretchY)
        {
            Center = center;
            Radius = radius;
            Strength = strength;
            StretchX = stretchX;
            StretchY = stretchY;
        }
    }

    private static readonly Color ProjectTealGreen = new Color(0.12f, 1f, 0.45f, 1f);
    private static readonly Color DarkRed = new Color(0.42f, 0.02f, 0.05f, 1f);

    [SerializeField] private int idleParticleCount = 18;
    [SerializeField] private int holdParticleCount = 28;
    [SerializeField] private int idleSmokeCount = 104;
    [SerializeField] private int holdSmokeCount = 168;
    [SerializeField] private float idleParticleSpeed = 0.035f;
    [SerializeField] private float holdParticleSpeed = 0.09f;
    [SerializeField] private float smokeBandWidth = 26f;
    [SerializeField] private float smokeOutwardDrift = 22f;
    [SerializeField] private float erosionCloudSpread = 42f;
    [SerializeField] private float erosionBorderThickness = 18f;
    [SerializeField] private float jitterPixels = 0.45f;

    private readonly List<Image> frameImages = new List<Image>();
    private readonly List<Vector2> frameBasePositions = new List<Vector2>();
    private readonly List<ReticleParticle> particles = new List<ReticleParticle>();
    private readonly List<SmokeParticle> smokeParticles = new List<SmokeParticle>();

    private RectTransform rectTransform;
    private RectTransform particleRoot;
    private RawImage erosionFrameImage;
    private Sprite solidSprite;
    private Sprite particleSprite;
    private Sprite smokeSprite;
    private Texture2D erosionFrameTexture;
    private Vector2 reticleSize;
    private Color primaryColor = Color.white;
    private bool isHolding;
    private bool configured;
    private bool ownsErosionFrameTexture;
    private int appliedVisualVersion;

    public bool NeedsConfigure => !configured || appliedVisualVersion != VisualVersion || transform.childCount == 0;

    public void Configure(Vector2 size, float cornerLength, float lineThickness, Color color, Sprite lineSprite)
    {
        rectTransform = transform as RectTransform;
        reticleSize = size;
        primaryColor = color;
        solidSprite = lineSprite != null ? lineSprite : CreateSolidSprite();

        if (rectTransform != null)
        {
            rectTransform.sizeDelta = reticleSize;
        }

        ClearChildren();
        frameImages.Clear();
        frameBasePositions.Clear();
        particles.Clear();
        smokeParticles.Clear();

        ReleaseErosionFrameTexture();
        BuildErosionFrame();
        BuildParticles();
        configured = true;
        appliedVisualVersion = VisualVersion;
        SetHoldActive(false);
    }

    public void SetHoldActive(bool active)
    {
        isHolding = active;
        for (int i = 0; i < frameImages.Count; i++)
        {
            Image image = frameImages[i];
            if (image == null)
            {
                continue;
            }

            Color target = Color.Lerp(primaryColor, ProjectTealGreen, active ? 0.08f : 0.02f);
            target.a = active ? 0.92f : 0.78f;
            image.color = target;
        }

        UpdateErosionFrame(0f);
    }

    private void Update()
    {
        if (!configured)
        {
            return;
        }

        UpdateSmokeParticles(Time.unscaledDeltaTime);
        UpdateParticles(Time.unscaledDeltaTime);
        UpdateErosionFrame(Time.unscaledDeltaTime);
        ApplyFrameJitter();
    }

    private void OnDestroy()
    {
        if (particleSprite != null)
        {
            Destroy(particleSprite.texture);
            Destroy(particleSprite);
        }

        if (smokeSprite != null)
        {
            Destroy(smokeSprite.texture);
            Destroy(smokeSprite);
        }

        ReleaseErosionFrameTexture();
    }

    private void BuildFrame(float cornerLength, float lineThickness)
    {
        float visualThickness = Mathf.Clamp(lineThickness * 0.55f, 1f, 2f);
        CreateLine("MainTop", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(reticleSize.x, visualThickness), new Vector2(0f, -visualThickness * 0.5f));
        CreateLine("MainBottom", new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(reticleSize.x, visualThickness), new Vector2(0f, visualThickness * 0.5f));
        CreateLine("MainLeft", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(visualThickness, reticleSize.y), new Vector2(visualThickness * 0.5f, 0f));
        CreateLine("MainRight", new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(visualThickness, reticleSize.y), new Vector2(-visualThickness * 0.5f, 0f));
    }

    private Image CreateLine(string name, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject lineObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lineObject.transform.SetParent(transform, false);

        Image line = lineObject.GetComponent<Image>();
        line.sprite = solidSprite;
        line.raycastTarget = false;

        RectTransform lineRect = line.rectTransform;
        lineRect.anchorMin = anchor;
        lineRect.anchorMax = anchor;
        lineRect.pivot = pivot;
        lineRect.sizeDelta = size;
        lineRect.anchoredPosition = anchoredPosition;

        frameImages.Add(line);
        frameBasePositions.Add(anchoredPosition);
        return line;
    }

    private void BuildErosionFrame()
    {
        GameObject erosionObject = new GameObject("ErosionParticleFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        erosionObject.transform.SetParent(transform, false);
        erosionFrameImage = erosionObject.GetComponent<RawImage>();
        erosionFrameImage.raycastTarget = false;
        Texture2D resourceTexture = Resources.Load<Texture2D>(ErosionFrameResourcePath);
        if (resourceTexture != null)
        {
            erosionFrameTexture = resourceTexture;
            ownsErosionFrameTexture = false;
        }
        else
        {
            erosionFrameTexture = CreateErosionFrameTexture(384, 320, reticleSize, erosionCloudSpread, erosionBorderThickness);
            ownsErosionFrameTexture = true;
        }

        erosionFrameImage.texture = erosionFrameTexture;
        erosionFrameImage.color = Color.white;

        RectTransform rect = erosionFrameImage.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = reticleSize + Vector2.one * erosionCloudSpread * 2f;
    }

    private void UpdateErosionFrame(float deltaTime)
    {
        if (erosionFrameImage == null)
        {
            return;
        }

        RectTransform rect = erosionFrameImage.rectTransform;
        float pulse = Mathf.Sin(Time.unscaledTime * 1.7f) * 0.006f;
        float holdPulse = isHolding ? Mathf.Sin(Time.unscaledTime * 5.5f) * 0.005f : 0f;
        rect.localScale = Vector3.one * (1f + pulse + holdPulse);

        float x = Mathf.Sin(Time.unscaledTime * 0.83f) * (isHolding ? 0.7f : 0.3f);
        float y = Mathf.Sin(Time.unscaledTime * 1.11f + 1.7f) * (isHolding ? 0.6f : 0.25f);
        rect.anchoredPosition = new Vector2(x, y);

        Color color = Color.white;
        color.a = isHolding ? 1f : 0.98f;
        erosionFrameImage.color = color;
    }

    private void BuildParticles()
    {
        GameObject rootObject = new GameObject("ReticleParticles", typeof(RectTransform));
        rootObject.transform.SetParent(transform, false);
        particleRoot = rootObject.GetComponent<RectTransform>();
        particleRoot.anchorMin = Vector2.zero;
        particleRoot.anchorMax = Vector2.one;
        particleRoot.offsetMin = Vector2.zero;
        particleRoot.offsetMax = Vector2.zero;

        particleSprite = CreateParticleSprite();
        smokeSprite = CreateSmokeSprite();
        int count = Mathf.Max(idleParticleCount, holdParticleCount);
        for (int i = 0; i < count; i++)
        {
            GameObject particleObject = new GameObject("ReticleParticle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            particleObject.transform.SetParent(particleRoot, false);
            Image image = particleObject.GetComponent<Image>();
            image.sprite = particleSprite;
            image.raycastTarget = false;

            ReticleParticle particle = new ReticleParticle
            {
                Rect = image.rectTransform,
                Image = image
            };
            RespawnParticle(particle, true);
            particles.Add(particle);
        }

        int smokeCount = Mathf.Max(idleSmokeCount, holdSmokeCount);
        for (int i = 0; i < smokeCount; i++)
        {
            GameObject smokeObject = new GameObject("ReticleSmoke", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            smokeObject.transform.SetParent(particleRoot, false);
            Image image = smokeObject.GetComponent<Image>();
            image.sprite = smokeSprite;
            image.raycastTarget = false;

            SmokeParticle smoke = new SmokeParticle
            {
                Rect = image.rectTransform,
                Image = image
            };
            RespawnSmoke(smoke, true);
            smokeParticles.Add(smoke);
        }
    }

    private void UpdateSmokeParticles(float deltaTime)
    {
        int activeCount = isHolding ? holdSmokeCount : idleSmokeCount;
        float speedScale = isHolding ? 1.35f : 0.72f;
        for (int i = 0; i < smokeParticles.Count; i++)
        {
            SmokeParticle smoke = smokeParticles[i];
            bool active = i < activeCount;
            smoke.Image.enabled = active;
            if (!active)
            {
                continue;
            }

            smoke.Age += deltaTime;
            smoke.PathPosition = Mathf.Repeat(smoke.PathPosition + smoke.Speed * speedScale * deltaTime, 1f);
            if (smoke.Age >= smoke.Lifetime)
            {
                RespawnSmoke(smoke, false);
            }

            float lifeT = Mathf.Clamp01(smoke.Age / Mathf.Max(0.01f, smoke.Lifetime));
            Vector2 normal = EvaluatePerimeterNormal(smoke.PathPosition);
            Vector2 tangent = EvaluatePerimeterTangent(smoke.PathPosition);
            float wobble = Mathf.Sin((Time.unscaledTime + smoke.Phase) * 2.6f) * Mathf.Lerp(1.2f, 3.6f, isHolding ? 1f : 0f);
            float outward = Mathf.SmoothStep(0.18f, 1f, lifeT) * smoke.DriftStrength * smokeOutwardDrift;
            Vector2 position = EvaluatePerimeter(smoke.PathPosition)
                + normal * (smoke.NormalOffset + outward)
                + tangent * (smoke.TangentOffset + wobble);

            smoke.Rect.anchoredPosition = position;
            float sizePulse = 1f + Mathf.Sin((Time.unscaledTime + smoke.Phase) * 3.4f) * 0.12f;
            smoke.Rect.sizeDelta = Vector2.one * smoke.Size * sizePulse;

            Color color = smoke.BaseColor;
            float fadeIn = Mathf.SmoothStep(0f, 0.18f, lifeT);
            float fadeOut = 1f - Mathf.SmoothStep(0.58f, 1f, lifeT);
            float alphaScale = isHolding ? 1.28f : 1f;
            color.a = Mathf.Clamp01(smoke.BaseColor.a * fadeIn * fadeOut * alphaScale);
            smoke.Image.color = color;
        }
    }

    private void UpdateParticles(float deltaTime)
    {
        int activeCount = isHolding ? holdParticleCount : idleParticleCount;
        float speedScale = isHolding ? holdParticleSpeed : idleParticleSpeed;
        for (int i = 0; i < particles.Count; i++)
        {
            ReticleParticle particle = particles[i];
            bool active = i < activeCount;
            particle.Image.enabled = active;
            if (!active)
            {
                continue;
            }

            particle.Age += deltaTime;
            particle.PathPosition = Mathf.Repeat(particle.PathPosition + particle.Speed * speedScale * deltaTime, 1f);
            if (particle.Age >= particle.Lifetime)
            {
                RespawnParticle(particle, false);
            }

            Vector2 position = EvaluatePerimeter(particle.PathPosition);
            float drift = Mathf.Sin((Time.unscaledTime + particle.Phase) * 5.8f) * (isHolding ? 2.4f : 1.2f);
            Vector2 normal = EvaluatePerimeterNormal(particle.PathPosition);
            particle.Rect.anchoredPosition = position + normal * drift;

            float lifeT = Mathf.Clamp01(particle.Age / Mathf.Max(0.01f, particle.Lifetime));
            float blink = 0.65f + Mathf.Sin((Time.unscaledTime + particle.Phase) * (isHolding ? 16f : 9f)) * 0.35f;
            Color color = particle.Image.color;
            color.a = Mathf.Clamp01((1f - Mathf.SmoothStep(0.65f, 1f, lifeT)) * blink * (isHolding ? 0.86f : 0.58f));
            particle.Image.color = color;
        }
    }

    private void RespawnParticle(ReticleParticle particle, bool randomizeAge)
    {
        particle.PathPosition = Random.value;
        particle.Speed = Random.Range(0.65f, 1.55f);
        particle.Lifetime = Random.Range(0.34f, 0.9f);
        particle.Age = randomizeAge ? Random.Range(0f, particle.Lifetime) : 0f;
        particle.Size = Random.Range(2.5f, 6.5f);
        particle.Phase = Random.Range(0f, 100f);
        particle.Rect.sizeDelta = Vector2.one * particle.Size;
        particle.Image.color = PickParticleColor();
    }

    private void RespawnSmoke(SmokeParticle smoke, bool randomizeAge)
    {
        smoke.PathPosition = Random.value;
        smoke.Speed = Random.Range(0.008f, 0.032f);
        smoke.Lifetime = Random.Range(0.85f, 1.9f);
        smoke.Age = randomizeAge ? Random.Range(0f, smoke.Lifetime) : 0f;
        smoke.Size = Random.Range(10f, 32f);
        smoke.NormalOffset = Random.Range(-smokeBandWidth * 0.38f, smokeBandWidth * 1.05f);
        smoke.TangentOffset = Random.Range(-10f, 10f);
        smoke.Phase = Random.Range(0f, 100f);
        smoke.DriftStrength = Random.Range(0.16f, 1f);
        smoke.Rect.sizeDelta = Vector2.one * smoke.Size;
        smoke.BaseColor = PickSmokeColor();
        smoke.Image.color = smoke.BaseColor;
    }

    private Vector2 EvaluatePerimeter(float t)
    {
        float width = reticleSize.x;
        float height = reticleSize.y;
        float perimeter = width * 2f + height * 2f;
        float distance = Mathf.Repeat(t, 1f) * perimeter;
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;

        if (distance < width)
        {
            return new Vector2(-halfW + distance, halfH);
        }

        distance -= width;
        if (distance < height)
        {
            return new Vector2(halfW, halfH - distance);
        }

        distance -= height;
        if (distance < width)
        {
            return new Vector2(halfW - distance, -halfH);
        }

        distance -= width;
        return new Vector2(-halfW, -halfH + distance);
    }

    private Vector2 EvaluatePerimeterNormal(float t)
    {
        float width = reticleSize.x;
        float height = reticleSize.y;
        float perimeter = width * 2f + height * 2f;
        float distance = Mathf.Repeat(t, 1f) * perimeter;

        if (distance < width)
        {
            return Vector2.up;
        }

        distance -= width;
        if (distance < height)
        {
            return Vector2.right;
        }

        distance -= height;
        if (distance < width)
        {
            return Vector2.down;
        }

        return Vector2.left;
    }

    private Vector2 EvaluatePerimeterTangent(float t)
    {
        Vector2 normal = EvaluatePerimeterNormal(t);
        return new Vector2(-normal.y, normal.x);
    }

    private void ApplyFrameJitter()
    {
        float amount = isHolding ? jitterPixels : jitterPixels * 0.35f;
        for (int i = 0; i < frameImages.Count; i++)
        {
            RectTransform rect = frameImages[i] != null ? frameImages[i].rectTransform : null;
            if (rect == null || i >= frameBasePositions.Count)
            {
                continue;
            }

            float x = Mathf.Sin(Time.unscaledTime * 18f + i * 1.91f) * amount;
            float y = Mathf.Sin(Time.unscaledTime * 14f + i * 2.37f) * amount * 0.55f;
            rect.anchoredPosition = frameBasePositions[i] + new Vector2(x, y);
        }
    }

    private static Color PickParticleColor()
    {
        float roll = Random.value;
        if (roll < 0.76f)
        {
            float value = Random.Range(0f, 0.035f);
            return new Color(value, value, value, Random.Range(0.58f, 0.86f));
        }

        if (roll < 0.86f)
        {
            float value = Random.Range(0.05f, 0.12f);
            return new Color(value, value, value, Random.Range(0.32f, 0.56f));
        }

        if (roll < 0.94f)
        {
            return new Color(DarkRed.r, DarkRed.g, DarkRed.b, Random.Range(0.36f, 0.58f));
        }

        return new Color(ProjectTealGreen.r, ProjectTealGreen.g, ProjectTealGreen.b, Random.Range(0.38f, 0.62f));
    }

    private static Color PickSmokeColor()
    {
        float roll = Random.value;
        if (roll < 0.84f)
        {
            float value = Random.Range(0f, 0.018f);
            return new Color(value, value, value, Random.Range(0.52f, 0.86f));
        }

        if (roll < 0.95f)
        {
            float value = Random.Range(0.018f, 0.055f);
            return new Color(value, value, value, Random.Range(0.26f, 0.44f));
        }

        if (roll < 0.988f)
        {
            return new Color(DarkRed.r, DarkRed.g, DarkRed.b, Random.Range(0.18f, 0.34f));
        }

        return new Color(ProjectTealGreen.r, ProjectTealGreen.g, ProjectTealGreen.b, Random.Range(0.14f, 0.24f));
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void ReleaseErosionFrameTexture()
    {
        if (ownsErosionFrameTexture && erosionFrameTexture != null)
        {
            Destroy(erosionFrameTexture);
        }

        erosionFrameTexture = null;
        ownsErosionFrameTexture = false;
    }

    private static Sprite CreateSolidSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateParticleSprite()
    {
        Texture2D texture = new Texture2D(24, 24, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2(11.5f, 11.5f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / 11.5f;
                float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0.1f, 1f, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 24f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateSmokeSprite()
    {
        Texture2D texture = new Texture2D(48, 48, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2(23.5f, 23.5f);
        float seedA = Random.Range(0f, 1000f);
        float seedB = Random.Range(0f, 1000f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / 23.5f;
                float soft = 1f - Mathf.SmoothStep(0.08f, 1f, distance);
                float noise = Mathf.PerlinNoise(x * 0.12f + seedA, y * 0.12f + seedB);
                float holes = Mathf.PerlinNoise(x * 0.28f + seedB, y * 0.28f + seedA);
                float alpha = Mathf.Clamp01(soft * (0.35f + noise * 0.85f) * (holes > 0.28f ? 1f : 0.28f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 48f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Texture2D CreateErosionFrameTexture(int width, int height, Vector2 innerSize, float spread, float borderThickness)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float outerWidth = Mathf.Max(1f, innerSize.x + spread * 2f);
        float outerHeight = Mathf.Max(1f, innerSize.y + spread * 2f);
        Vector2 halfInner = innerSize * 0.5f;
        float seedA = Random.Range(0f, 1000f);
        float seedB = Random.Range(0f, 1000f);
        ErosionClump[] clumps = CreateErosionClumps(halfInner, spread);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float lx = ((x + 0.5f) / width - 0.5f) * outerWidth;
                float ly = ((y + 0.5f) / height - 0.5f) * outerHeight;
                Vector2 p = new Vector2(lx, ly);
                float signedDistance = RectSignedDistance(p, halfInner);
                float distanceFromEdge = Mathf.Abs(signedDistance);
                float outsideDistance = Mathf.Max(0f, signedDistance);
                float borderCore = 1f - Mathf.SmoothStep(borderThickness * 0.25f, borderThickness, distanceFromEdge);
                float closeScatter = 1f - Mathf.SmoothStep(borderThickness, spread, distanceFromEdge);
                float edgeMask = Mathf.Max(borderCore, closeScatter * 0.46f);

                float clumpField = 0f;
                for (int i = 0; i < clumps.Length; i++)
                {
                    ErosionClump clump = clumps[i];
                    Vector2 offset = p - clump.Center;
                    offset.x /= clump.StretchX;
                    offset.y /= clump.StretchY;
                    float blob = 1f - Mathf.SmoothStep(0.18f, 1f, offset.magnitude / clump.Radius);
                    clumpField = Mathf.Max(clumpField, blob * clump.Strength);
                }

                float largeNoise = Mathf.PerlinNoise(lx * 0.015f + seedA, ly * 0.015f + seedB);
                float mediumNoise = Mathf.PerlinNoise(lx * 0.052f + seedB, ly * 0.052f + seedA);
                float fineNoise = Mathf.PerlinNoise(lx * 0.16f + seedA * 0.31f, ly * 0.16f + seedB * 0.47f);
                float holeNoise = Mathf.PerlinNoise(lx * 0.038f + seedB * 0.37f, ly * 0.038f + seedA * 0.29f);
                float cracked = fineNoise > 0.68f ? Mathf.InverseLerp(0.68f, 1f, fineNoise) : 0f;
                float smoke = Mathf.SmoothStep(0.16f, 0.8f, largeNoise) * 0.42f
                    + Mathf.SmoothStep(0.32f, 0.9f, mediumNoise) * 0.34f;
                float brokenGaps = Mathf.Lerp(0.24f, 1f, Mathf.SmoothStep(0.18f, 0.78f, holeNoise));
                float raggedCore = borderCore * (0.62f + clumpField * 0.88f + cracked * 0.42f);
                float scatter = closeScatter * (0.08f + clumpField * 0.42f + smoke + cracked * 0.28f);
                float alpha = (raggedCore + scatter * 0.5f) * brokenGaps;

                if (outsideDistance > spread * 0.72f)
                {
                    alpha = 0f;
                }
                else if (distanceFromEdge > borderThickness * 1.35f)
                {
                    float farFleck = Random.value > 0.975f ? Random.Range(0.16f, 0.52f) : 0f;
                    alpha = Mathf.Max(alpha * 0.42f, farFleck * closeScatter);
                }

                Color color;
                float accentRoll = Random.value;
                if (alpha > 0.08f && (accentRoll > 0.975f || (cracked > 0.72f && accentRoll > 0.93f)))
                {
                    color = ProjectTealGreen;
                    alpha *= 0.86f;
                }
                else if (alpha > 0.1f && accentRoll > 0.955f)
                {
                    color = DarkRed;
                    alpha *= 0.68f;
                }
                else
                {
                    float sootValue = Random.Range(0f, 0.05f);
                    float ashLift = borderCore * Random.Range(0f, 0.08f);
                    color = new Color(sootValue + ashLift, sootValue + ashLift * 0.85f, sootValue + ashLift * 0.72f, 1f);
                }

                color.a = Mathf.Clamp01(alpha * 1.08f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private static ErosionClump[] CreateErosionClumps(Vector2 halfInner, float spread)
    {
        int count = 20;
        ErosionClump[] clumps = new ErosionClump[count];
        for (int i = 0; i < count; i++)
        {
            float t = (i + Random.Range(-0.35f, 0.35f)) / count;
            Vector2 edgePoint = PointOnErosionPerimeter(t, halfInner);
            Vector2 normal = ErosionNormalAt(edgePoint, halfInner);
            float outward = Random.Range(-spread * 0.18f, spread * 0.34f);
            Vector2 center = edgePoint + normal * outward;
            float radius = Random.Range(spread * 0.16f, spread * 0.38f);
            float strength = Random.Range(0.36f, 0.92f);
            float stretchX = Random.Range(0.7f, 1.55f);
            float stretchY = Random.Range(0.7f, 1.55f);
            clumps[i] = new ErosionClump(center, radius, strength, stretchX, stretchY);
        }

        return clumps;
    }

    private static Vector2 PointOnErosionPerimeter(float t, Vector2 halfSize)
    {
        float width = halfSize.x * 2f;
        float height = halfSize.y * 2f;
        float perimeter = width * 2f + height * 2f;
        float distance = Mathf.Repeat(t, 1f) * perimeter;

        if (distance < width)
        {
            return new Vector2(-halfSize.x + distance, halfSize.y);
        }

        distance -= width;
        if (distance < height)
        {
            return new Vector2(halfSize.x, halfSize.y - distance);
        }

        distance -= height;
        if (distance < width)
        {
            return new Vector2(halfSize.x - distance, -halfSize.y);
        }

        distance -= width;
        return new Vector2(-halfSize.x, -halfSize.y + distance);
    }

    private static Vector2 ErosionNormalAt(Vector2 edgePoint, Vector2 halfSize)
    {
        float dx = Mathf.Abs(Mathf.Abs(edgePoint.x) - halfSize.x);
        float dy = Mathf.Abs(Mathf.Abs(edgePoint.y) - halfSize.y);
        if (dx < dy)
        {
            return new Vector2(Mathf.Sign(edgePoint.x), 0f);
        }

        return new Vector2(0f, Mathf.Sign(edgePoint.y));
    }

    private static float RectSignedDistance(Vector2 point, Vector2 halfSize)
    {
        Vector2 q = new Vector2(Mathf.Abs(point.x) - halfSize.x, Mathf.Abs(point.y) - halfSize.y);
        Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
    }
}

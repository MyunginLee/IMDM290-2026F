using UnityEngine;

[DisallowMultipleComponent]
public class CameraMotionFireDemo : MonoBehaviour
{
    [Header("Camera Input")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private int requestedWidth = 640;
    [SerializeField] private int requestedHeight = 480;
    [SerializeField] private int requestedFps = 30;
    [SerializeField] private bool mirrorX = true;

    [Header("Vector Field")]
    [SerializeField, Range(8, 640)] private int sampleColumns = 640;
    [SerializeField, Range(8, 480)] private int sampleRows = 480;
    [SerializeField, Range(2f, 20f)] private float fieldDistance = 8f;
    [SerializeField, Range(0.05f, 2f)] private float vectorLength = 0.55f;
    [SerializeField, Range(0.002f, 0.15f)] private float vectorThickness = 0.03f;
    [SerializeField, Range(0.001f, 0.2f)] private float deltaThreshold = 0.025f;
    [SerializeField, Range(1f, 40f)] private float deltaGain = 18f;
    [SerializeField, Range(1f, 20f)] private float fieldLerpSpeed = 10f;

    [Header("Vector Look")]
    [SerializeField] private Color idleVectorColor = new Color(0.12f, 0.04f, 0.02f, 1f);
    [SerializeField] private Color activeVectorColor = new Color(1f, 0.45f, 0.08f, 1f);

    [Header("Fire")]
    [SerializeField, Range(0f, 1f)] private float fireThreshold = 0.12f;
    [SerializeField, Range(0f, 10f)] private float particlesPerPointPerFrame = 2.5f;
    [SerializeField, Range(0.1f, 6f)] private float fireSpeed = 2.5f;
    [SerializeField, Range(0.1f, 3f)] private float fireLifetime = 0.8f;
    [SerializeField, Range(0.01f, 0.3f)] private float fireSize = 0.06f;

    [Header("Debug")]
    [SerializeField] private bool showDeltaLabel = true;
    [SerializeField, Range(0f, 1f)] private float frameDelta;
    [SerializeField, Range(0f, 1f)] private float smoothedDelta;

    private WebCamTexture webCamTexture;
    private Color32[] webCamPixels;
    private float[] previousLuminance;
    private float[] deltaGrid;
    private float[] smoothedGrid;
    private Vector2[] vectorGrid;
    private Vector3[] pointPositions;
    private Transform[] vectorBars;
    private MeshRenderer[] vectorRenderers;
    private Material[] vectorMaterials;
    private Transform fieldRoot;
    private ParticleSystem fireParticles;
    private Material vectorMaterial;
    private Material particleMaterial;
    private bool hasPreviousFrame;

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0.015f, 0.008f, 0.006f, 1f);
        }

        BuildField();
        StartWebCam();
    }

    private void Update()
    {
        UpdateFieldLayout();
        UpdateMotionField();
        UpdateVectorBars();
        EmitFireFromField();
    }

    private void OnDisable()
    {
        StopWebCam();
    }

    private void OnDestroy()
    {
        StopWebCam();

        if (fieldRoot != null)
        {
            Destroy(fieldRoot.gameObject);
        }

        if (vectorMaterial != null)
        {
            Destroy(vectorMaterial);
        }

        if (vectorMaterials != null)
        {
            for (var i = 0; i < vectorMaterials.Length; i++)
            {
                if (vectorMaterials[i] != null)
                {
                    Destroy(vectorMaterials[i]);
                }
            }
        }

        if (particleMaterial != null)
        {
            Destroy(particleMaterial);
        }
    }

    private void OnGUI()
    {
        if (!showDeltaLabel)
        {
            return;
        }

        GUI.Box(new Rect(16, 16, 320, 72), "Motion Vector Fire");
        GUI.Label(new Rect(32, 44, 280, 20), $"Frame delta: {frameDelta:F4}");
        GUI.Label(new Rect(32, 62, 280, 20), $"Smoothed: {smoothedDelta:F4}");
    }

    private void StartWebCam()
    {
        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogWarning($"{nameof(CameraMotionFireDemo)} could not find a webcam.");
            return;
        }

        var device = WebCamTexture.devices[0];
        webCamTexture = new WebCamTexture(device.name, requestedWidth, requestedHeight, requestedFps);
        webCamTexture.Play();
    }

    private void StopWebCam()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();
        }
    }

    private void BuildField()
    {
        if (targetCamera == null)
        {
            return;
        }

        if (fieldRoot != null)
        {
            Destroy(fieldRoot.gameObject);
        }

        var sampleCount = sampleColumns * sampleRows;

        fieldRoot = new GameObject("Motion Vector Field").transform;
        fieldRoot.SetParent(targetCamera.transform, false);

        previousLuminance = new float[sampleCount];
        deltaGrid = new float[sampleCount];
        smoothedGrid = new float[sampleCount];
        vectorGrid = new Vector2[sampleCount];
        pointPositions = new Vector3[sampleCount];
        vectorBars = new Transform[sampleCount];
        vectorRenderers = new MeshRenderer[sampleCount];
        vectorMaterials = new Material[sampleCount];
        hasPreviousFrame = false;

        vectorMaterial = CreateVectorMaterial();

        for (var i = 0; i < sampleCount; i++)
        {
            var barObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            barObject.name = $"Vector_{i:000}";
            barObject.transform.SetParent(fieldRoot, false);

            var collider = barObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = barObject.GetComponent<MeshRenderer>();
            var materialInstance = new Material(vectorMaterial);
            ApplyMaterialColor(materialInstance, idleVectorColor);
            renderer.material = materialInstance;

            vectorBars[i] = barObject.transform;
            vectorRenderers[i] = renderer;
            vectorMaterials[i] = materialInstance;
        }

        BuildFireParticles();
        UpdateFieldLayout();
    }

    private void BuildFireParticles()
    {
        if (fieldRoot == null)
        {
            return;
        }

        var fireObject = new GameObject("Vector Fire Particles");
        fireObject.transform.SetParent(fieldRoot, false);
        fireObject.transform.localPosition = Vector3.zero;

        fireParticles = fireObject.AddComponent<ParticleSystem>();
        var particleRenderer = fireObject.GetComponent<ParticleSystemRenderer>();
        particleMaterial = CreateParticleMaterial();
        if (particleRenderer != null && particleMaterial != null)
        {
            particleRenderer.sharedMaterial = particleMaterial;
            particleRenderer.trailMaterial = particleMaterial;
        }

        var main = fireParticles.main;
        main.playOnAwake = true;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = fireLifetime;
        main.startSpeed = fireSpeed;
        main.startSize = fireSize;
        main.startColor = new Color(1f, 0.6f, 0.2f, 0.95f);
        main.maxParticles = 12000;

        var emission = fireParticles.emission;
        emission.rateOverTime = 0f;

        var noise = fireParticles.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.8f;
        noise.scrollSpeed = 1f;

        var trails = fireParticles.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.lifetime = 0.15f;
        trails.dieWithParticles = true;
        trails.sizeAffectsWidth = true;

        var colorOverLifetime = fireParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.08f), 0.35f),
                new GradientColorKey(new Color(0.4f, 0.04f, 0.01f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.15f),
                new GradientAlphaKey(0.7f, 0.65f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = gradient;

        fireParticles.Play();
    }

    private void UpdateFieldLayout()
    {
        if (targetCamera == null || pointPositions == null)
        {
            return;
        }

        var fieldHeight = 2f * fieldDistance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        var fieldWidth = fieldHeight * targetCamera.aspect;

        for (var row = 0; row < sampleRows; row++)
        {
            var y = Mathf.Lerp(-fieldHeight * 0.5f, fieldHeight * 0.5f, sampleRows == 1 ? 0.5f : row / (float)(sampleRows - 1));
            for (var column = 0; column < sampleColumns; column++)
            {
                var x = Mathf.Lerp(-fieldWidth * 0.5f, fieldWidth * 0.5f, sampleColumns == 1 ? 0.5f : column / (float)(sampleColumns - 1));
                pointPositions[row * sampleColumns + column] = new Vector3(x, y, fieldDistance);
            }
        }
    }

    private void UpdateMotionField()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying || !webCamTexture.didUpdateThisFrame)
        {
            return;
        }

        var width = webCamTexture.width;
        var height = webCamTexture.height;
        if (width < 16 || height < 16)
        {
            return;
        }

        if (webCamPixels == null || webCamPixels.Length != width * height)
        {
            webCamPixels = new Color32[width * height];
        }

        webCamTexture.GetPixels32(webCamPixels);

        var sampleCount = sampleColumns * sampleRows;
        var totalDelta = 0f;

        for (var row = 0; row < sampleRows; row++)
        {
            var y = sampleRows == 1 ? height / 2 : row * (height - 1) / (sampleRows - 1);

            for (var column = 0; column < sampleColumns; column++)
            {
                var visualColumn = mirrorX ? (sampleColumns - 1 - column) : column;
                var x = sampleColumns == 1 ? width / 2 : visualColumn * (width - 1) / (sampleColumns - 1);
                var pixel = webCamPixels[y * width + x];
                var luminance = (pixel.r + pixel.g + pixel.b) / (255f * 3f);
                var index = row * sampleColumns + column;
                var rawDelta = hasPreviousFrame ? Mathf.Abs(luminance - previousLuminance[index]) : 0f;
                var intensity = Mathf.Clamp01((rawDelta - deltaThreshold) * deltaGain);

                previousLuminance[index] = luminance;
                deltaGrid[index] = intensity;
                smoothedGrid[index] = Mathf.Lerp(smoothedGrid[index], intensity, Time.deltaTime * fieldLerpSpeed);
                totalDelta += rawDelta;
            }
        }

        hasPreviousFrame = true;
        frameDelta = totalDelta / sampleCount;
        smoothedDelta = Mathf.Lerp(smoothedDelta, frameDelta, Time.deltaTime * fieldLerpSpeed);

        for (var row = 0; row < sampleRows; row++)
        {
            for (var column = 0; column < sampleColumns; column++)
            {
                var index = row * sampleColumns + column;
                var left = row * sampleColumns + Mathf.Max(0, column - 1);
                var right = row * sampleColumns + Mathf.Min(sampleColumns - 1, column + 1);
                var down = Mathf.Max(0, row - 1) * sampleColumns + column;
                var up = Mathf.Min(sampleRows - 1, row + 1) * sampleColumns + column;

                var gradientX = deltaGrid[right] - deltaGrid[left];
                var gradientY = deltaGrid[up] - deltaGrid[down];
                var direction = new Vector2(gradientX, gradientY);
                var intensity = smoothedGrid[index];

                if (direction.sqrMagnitude > 0.000001f)
                {
                    direction = direction.normalized * intensity;
                }
                else
                {
                    direction = Vector2.zero;
                }

                vectorGrid[index] = Vector2.Lerp(vectorGrid[index], direction, Time.deltaTime * fieldLerpSpeed);
            }
        }
    }

    private void UpdateVectorBars()
    {
        if (vectorBars == null || pointPositions == null)
        {
            return;
        }

        for (var i = 0; i < vectorBars.Length; i++)
        {
            var bar = vectorBars[i];
            var renderer = vectorRenderers[i];
            var material = vectorMaterials[i];
            if (bar == null || renderer == null || material == null)
            {
                continue;
            }

            var start = pointPositions[i];
            var vector = vectorGrid[i];
            var intensity = smoothedGrid[i];
            var magnitude = vector.magnitude * vectorLength;
            var color = Color.Lerp(idleVectorColor, activeVectorColor, intensity);

            if (magnitude < 0.001f)
            {
                bar.localPosition = start;
                bar.localRotation = Quaternion.identity;
                bar.localScale = new Vector3(0.001f, vectorThickness * 0.2f, 1f);
            }
            else
            {
                var end = start + new Vector3(vector.x, vector.y, 0f) * vectorLength;
                var center = (start + end) * 0.5f;
                var angle = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;

                bar.localPosition = center;
                bar.localRotation = Quaternion.Euler(0f, 0f, angle);
                bar.localScale = new Vector3(magnitude, Mathf.Lerp(vectorThickness * 0.2f, vectorThickness, intensity), 1f);
            }

            ApplyMaterialColor(material, color);
        }
    }

    private void EmitFireFromField()
    {
        if (fireParticles == null || pointPositions == null)
        {
            return;
        }

        var emitParams = new ParticleSystem.EmitParams();

        for (var i = 0; i < pointPositions.Length; i++)
        {
            var intensity = smoothedGrid[i];
            if (intensity < fireThreshold)
            {
                continue;
            }

            var vector = vectorGrid[i];
            var direction = new Vector3(vector.x, vector.y, 0.8f);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }
            direction.Normalize();

            var emitCount = Mathf.Max(1, Mathf.RoundToInt(intensity * particlesPerPointPerFrame));
            emitParams.position = pointPositions[i];
            emitParams.velocity = direction * Mathf.Lerp(fireSpeed * 0.4f, fireSpeed, intensity);
            emitParams.startLifetime = Mathf.Lerp(fireLifetime * 0.5f, fireLifetime, intensity);
            emitParams.startSize = Mathf.Lerp(fireSize * 0.6f, fireSize * 1.8f, intensity);
            emitParams.startColor = Color.Lerp(
                new Color(1f, 0.85f, 0.4f, 0.6f),
                new Color(1f, 0.2f, 0.03f, 1f),
                intensity);
            fireParticles.Emit(emitParams, emitCount);
        }
    }

    private Material CreateVectorMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        var material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        return material;
    }

    private Material CreateParticleMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        }

        var material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        return material;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }
}

using UnityEngine;

[DisallowMultipleComponent]
public class CameraMotionFireDemo : MonoBehaviour
{
    private const string VectorScatterShaderName = "IMDM290/VectorScatterFlare";
    private const string SoftParticleShaderName = "IMDM290/SoftAdditiveParticle";

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

    [Header("Silhouette")]
    [SerializeField, Range(0.005f, 0.25f)] private float silhouetteThreshold = 0.08f;
    [SerializeField, Range(1f, 40f)] private float silhouetteGain = 14f;
    [SerializeField, Range(0.05f, 6f)] private float backgroundAdaptSpeed = 0.6f;
    [SerializeField, Range(0f, 2f)] private float edgeUpwardBias = 0.9f;

    [Header("Fire")]
    [SerializeField, Range(0f, 100f)] private float particlesPerPointPerFrame = 2.5f;
    [SerializeField, Range(0.1f, 20f)] private float fireSpeed = 2.5f;
    [SerializeField, Range(0.1f, 3f)] private float fireLifetime = 0.8f;
    [SerializeField, Range(0.01f, 0.3f)] private float fireSize = 0.06f;
    [SerializeField, Range(0.5f, 8f)] private float fireDensityBoost = 1.8f;
    [SerializeField, Range(0.5f, 30f)] private float motionVectorGain = 12f;
    [SerializeField, Range(0f, 1f)] private float vectorPersistence = 0.92f;
    [SerializeField, Range(0f, 2f)] private float fireRandomness = 0.35f;
    [SerializeField, Range(0f, 0.25f)] private float firePositionJitter = 0.03f;
    [SerializeField, Range(0f, 12f)] private float firePulseSpeed = 5f;
    [SerializeField, Range(0.1f, 1f)] private float fireEmissionJitter = 0.45f;
    [SerializeField, Range(0f, 4f)] private float deltaFireBoost = 2.4f;
    [SerializeField, Range(0f, 40f)] private float deltaImmediateBurst = 12f;
    [SerializeField, Range(0f, 1f)] private float deltaSpawnThreshold = 0.08f;
    [SerializeField, Range(1f, 6f)] private float deltaVelocityBoost = 2.8f;
    [SerializeField, Range(0.1f, 1f)] private float deltaLifetimeFactor = 0.35f;

    [Header("Audio Reactive")]
    [SerializeField, Range(0f, 4f)] private float totalAmplitudeSpawnBoost = 2.4f;
    [SerializeField, Range(0f, 0.08f)] private float totalAmplitudeThresholdReduction = 0.035f;
    [SerializeField, Range(0f, 1f)] private float amplitudeSpawnProbability = 0.85f;
    [SerializeField, Range(0f, 3f)] private float amplitudeIntensityBoost = 1.6f;
    [SerializeField, Range(0f, 4f)] private float bassEmissionBoost = 2.2f;
    [SerializeField, Range(0f, 3f)] private float bassLifetimeBoost = 1.4f;
    [SerializeField, Range(0f, 2f)] private float midSizeBoost = 0.9f;
    [SerializeField, Range(0f, 2f)] private float trebleSpeedBoost = 1.15f;
    [SerializeField, Range(0f, 1.5f)] private float audioBrightnessBoost = 0.85f;
    [SerializeField, Range(0f, 1f)] private float frequencyHueMin = 0.02f;
    [SerializeField, Range(0f, 1f)] private float frequencyHueMax = 0.72f;
    [SerializeField, Range(0f, 8f)] private float liveBandBrightnessBoost = 3.8f;
    [SerializeField, Range(0f, 1f)] private float maxReactiveLifetime = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool showDeltaLabel = true;
    [SerializeField] private bool showDeltaTexture = true;
    [SerializeField, Range(64f, 512f)] private float debugTextureWidth = 240f;
    [SerializeField, Range(0.1f, 1f)] private float debugTextureAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float frameDelta;
    [SerializeField, Range(0f, 1f)] private float smoothedDelta;

    private WebCamTexture webCamTexture;
    private Color32[] webCamPixels;
    private float[] currentLuminance;
    private float[] previousLuminance;
    private float[] backgroundLuminance;
    private float[] deltaGrid;
    private float[] silhouetteGrid;
    private float[] smoothedGrid;
    private float[] edgeGrid;
    private int[] pointSpectrumBins;
    private Vector2[] vectorGrid;
    private Vector2[] immediateVectorGrid;
    private Vector3[] pointPositions;
    private Transform[] vectorBars;
    private MeshRenderer[] vectorRenderers;
    private Material[] vectorMaterials;
    private Transform fieldRoot;
    private ParticleSystem fireParticles;
    private Material vectorMaterial;
    private Material particleMaterial;
    private Texture2D deltaDebugTexture;
    private Color[] deltaDebugPixels;
    private bool hasPreviousFrame;
    private bool hasBackgroundFrame;

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
        UpdateAudioReactiveLook();
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

        if (deltaDebugTexture != null)
        {
            Destroy(deltaDebugTexture);
        }
    }

    private void OnGUI()
    {
        if (!showDeltaLabel && !showDeltaTexture)
        {
            return;
        }

        if (showDeltaLabel)
        {
            GUI.Box(new Rect(16, 16, 320, 108), "Silhouette Fire");
            GUI.Label(new Rect(32, 44, 280, 20), $"Motion delta: {frameDelta:F4}");
            GUI.Label(new Rect(32, 62, 280, 20), $"Silhouette heat: {smoothedDelta:F4}");
            GUI.Label(new Rect(32, 80, 280, 20), $"Amp/Bass/Mid/Treble: {AudioSpectrum.audioAmp:F4} / {AudioSpectrum.bassAmp:F2} / {AudioSpectrum.midAmp:F2} / {AudioSpectrum.trebleAmp:F2}");
        }

        if (showDeltaTexture && deltaDebugTexture != null)
        {
            var aspect = sampleRows <= 0 ? 1f : sampleRows / (float)sampleColumns;
            var width = debugTextureWidth;
            var height = width * aspect;
            var rect = new Rect(16, showDeltaLabel ? 140 : 16, width, height);
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, debugTextureAlpha);
            GUI.DrawTexture(rect, deltaDebugTexture, ScaleMode.StretchToFill, false);
            GUI.color = previousColor;
            GUI.Box(new Rect(rect.x, rect.y - 22f, 140f, 20f), "Delta Grayscale");
        }
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
        currentLuminance = new float[sampleCount];
        backgroundLuminance = new float[sampleCount];
        deltaGrid = new float[sampleCount];
        silhouetteGrid = new float[sampleCount];
        smoothedGrid = new float[sampleCount];
        edgeGrid = new float[sampleCount];
        pointSpectrumBins = new int[sampleCount];
        vectorGrid = new Vector2[sampleCount];
        immediateVectorGrid = new Vector2[sampleCount];
        pointPositions = new Vector3[sampleCount];
        vectorBars = new Transform[sampleCount];
        vectorRenderers = new MeshRenderer[sampleCount];
        vectorMaterials = new Material[sampleCount];
        hasPreviousFrame = false;
        hasBackgroundFrame = false;

        AllocateDeltaDebugTexture();

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
            ApplyMaterialLook(materialInstance, idleVectorColor, 0.08f);
            renderer.material = materialInstance;
            renderer.enabled = false;

            vectorBars[i] = barObject.transform;
            vectorRenderers[i] = renderer;
            vectorMaterials[i] = materialInstance;
            pointSpectrumBins[i] = ChooseSpectrumBinForPoint(i);
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
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.lengthScale = 1f;
            particleRenderer.velocityScale = 0f;
            particleRenderer.cameraVelocityScale = 0f;
            particleRenderer.normalDirection = 1f;
        }

        var main = fireParticles.main;
        main.playOnAwake = true;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = fireLifetime;
        main.startSpeed = fireSpeed;
        main.startSize = fireSize;
        main.startColor = new Color(1.3f, 0.72f, 0.24f, 0.95f);
        main.maxParticles = 60000;

        var emission = fireParticles.emission;
        emission.rateOverTime = 0f;

        var noise = fireParticles.noise;
        noise.enabled = true;
        noise.strength = 0.8f;
        noise.frequency = 1.25f;
        noise.scrollSpeed = 1.8f;

        var trails = fireParticles.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.lifetime = 0.42f;
        trails.dieWithParticles = true;
        trails.sizeAffectsWidth = true;
        trails.inheritParticleColor = true;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.42f);

        var colorOverLifetime = fireParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.45f, 0.08f, 0.02f), 0f),
                new GradientColorKey(new Color(0.95f, 0.28f, 0.04f), 0.32f),
                new GradientColorKey(new Color(1.65f, 0.78f, 0.18f), 0.7f),
                new GradientColorKey(new Color(2.2f, 1.7f, 0.8f), 0.86f),
                new GradientColorKey(new Color(0.18f, 0.03f, 0.01f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.18f, 0.1f),
                new GradientAlphaKey(0.45f, 0.45f),
                new GradientAlphaKey(0.95f, 0.82f),
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
                if (!hasBackgroundFrame)
                {
                    previousLuminance[index] = luminance;
                    currentLuminance[index] = luminance;
                    backgroundLuminance[index] = luminance;
                    deltaGrid[index] = 0f;
                    silhouetteGrid[index] = 0f;
                    smoothedGrid[index] = 0f;
                    edgeGrid[index] = 0f;
                    vectorGrid[index] = Vector2.zero;
                    immediateVectorGrid[index] = Vector2.zero;
                    continue;
                }

                var rawDelta = hasPreviousFrame ? Mathf.Abs(luminance - previousLuminance[index]) : 0f;
                var motionIntensity = Mathf.Clamp01((rawDelta - deltaThreshold) * deltaGain);
                var silhouetteAmount = Mathf.Clamp01((Mathf.Abs(luminance - backgroundLuminance[index]) - silhouetteThreshold) * silhouetteGain);
                var backgroundWeight = Time.deltaTime * backgroundAdaptSpeed * Mathf.Pow(1f - silhouetteAmount, 2f);

                currentLuminance[index] = luminance;
                backgroundLuminance[index] = Mathf.Lerp(backgroundLuminance[index], luminance, backgroundWeight);
                deltaGrid[index] = motionIntensity;
                silhouetteGrid[index] = silhouetteAmount;
                totalDelta += rawDelta;
            }
        }

        if (!hasBackgroundFrame)
        {
            hasBackgroundFrame = true;
            hasPreviousFrame = true;
            frameDelta = 0f;
            smoothedDelta = 0f;
            return;
        }

        hasPreviousFrame = true;
        frameDelta = totalDelta / sampleCount;
        var totalActivation = 0f;

        for (var row = 0; row < sampleRows; row++)
        {
            for (var column = 0; column < sampleColumns; column++)
            {
                var index = row * sampleColumns + column;
                var left = row * sampleColumns + Mathf.Max(0, column - 1);
                var right = row * sampleColumns + Mathf.Min(sampleColumns - 1, column + 1);
                var down = Mathf.Max(0, row - 1) * sampleColumns + column;
                var up = Mathf.Min(sampleRows - 1, row + 1) * sampleColumns + column;

                var silhouette = silhouetteGrid[index];
                smoothedGrid[index] = Mathf.Lerp(smoothedGrid[index], silhouette, Time.deltaTime * fieldLerpSpeed);

                var spatialX = currentLuminance[right] - currentLuminance[left];
                var spatialY = currentLuminance[up] - currentLuminance[down];
                var temporal = currentLuminance[index] - previousLuminance[index];
                var denominator = spatialX * spatialX + spatialY * spatialY + 0.0001f;
                var flow = new Vector2(-temporal * spatialX, -temporal * spatialY) / denominator;
                var flowMagnitude = Mathf.Clamp01(flow.magnitude * motionVectorGain) * Mathf.Clamp01(deltaGrid[index] * 1.2f + silhouette * 0.2f);

                Vector2 targetVector;
                if (flowMagnitude > 0.0001f)
                {
                    targetVector = flow.normalized * flowMagnitude;
                }
                else
                {
                    targetVector = Vector2.zero;
                }

                immediateVectorGrid[index] = targetVector;
                vectorGrid[index] = Vector2.Lerp(vectorGrid[index], targetVector, Time.deltaTime * fieldLerpSpeed * 2f);
                var persistentHeat = Mathf.Lerp(edgeGrid[index], smoothedGrid[index], Time.deltaTime * fieldLerpSpeed);
                var immediateDeltaHeat = Mathf.Clamp01(deltaGrid[index] * deltaFireBoost);
                edgeGrid[index] = Mathf.Max(persistentHeat, immediateDeltaHeat);
                totalActivation += edgeGrid[index];
            }
        }

        for (var i = 0; i < sampleCount; i++)
        {
            previousLuminance[i] = currentLuminance[i];
        }

        smoothedDelta = Mathf.Lerp(smoothedDelta, totalActivation / sampleCount, Time.deltaTime * fieldLerpSpeed);
        UpdateDeltaDebugTexture();
    }

    private void AllocateDeltaDebugTexture()
    {
        if (deltaDebugTexture != null)
        {
            Destroy(deltaDebugTexture);
        }

        deltaDebugTexture = new Texture2D(sampleColumns, sampleRows, TextureFormat.RGBA32, false);
        deltaDebugTexture.wrapMode = TextureWrapMode.Clamp;
        deltaDebugTexture.filterMode = FilterMode.Point;
        deltaDebugPixels = new Color[sampleColumns * sampleRows];
    }

    private void UpdateDeltaDebugTexture()
    {
        if (deltaDebugTexture == null || deltaDebugPixels == null || deltaGrid == null)
        {
            return;
        }

        var sampleCount = sampleColumns * sampleRows;
        for (var i = 0; i < sampleCount; i++)
        {
            var value = deltaGrid[i];
            deltaDebugPixels[i] = new Color(value, value, value, 1f);
        }

        deltaDebugTexture.SetPixels(deltaDebugPixels);
        deltaDebugTexture.Apply(false, false);
    }

    private int ChooseSpectrumBinForPoint(int pointIndex)
    {
        var hash = StableHash01(pointIndex * 37 + 17);
        var warped = Mathf.Pow(hash, 1.55f);
        return Mathf.RoundToInt(Mathf.Lerp(2f, 900f, warped));
    }

    private float SampleSpectrumLevel(int pointIndex)
    {
        if (AudioSpectrum.samples == null || AudioSpectrum.samples.Length == 0 || pointSpectrumBins == null || pointIndex >= pointSpectrumBins.Length)
        {
            return 0f;
        }

        var center = Mathf.Clamp(pointSpectrumBins[pointIndex], 2, Mathf.Min(AudioSpectrum.samples.Length - 1, 900));
        var radius = 4;
        var total = 0f;
        var count = 0;
        for (var i = Mathf.Max(2, center - radius); i <= Mathf.Min(AudioSpectrum.samples.Length - 1, center + radius); i++)
        {
            total += AudioSpectrum.samples[i];
            count++;
        }

        var average = count > 0 ? total / count : 0f;
        return Mathf.Clamp01(average * Mathf.Lerp(650f, 1200f, center / 900f));
    }

    private Color EvaluateFrequencyColor(int pointIndex, float brightness)
    {
        var bin = pointSpectrumBins != null && pointIndex < pointSpectrumBins.Length ? pointSpectrumBins[pointIndex] : 2;
        var normalizedBin = Mathf.InverseLerp(2f, 900f, bin);
        var hue = Mathf.Lerp(frequencyHueMin, frequencyHueMax, normalizedBin);
        return Color.HSVToRGB(hue, 0.85f, Mathf.Lerp(0.8f, 1.35f, brightness));
    }

    private void UpdateAudioReactiveLook()
    {
        if (particleMaterial == null)
        {
            return;
        }

        var bass = Mathf.Clamp01(AudioSpectrum.bassAmp);
        var mid = Mathf.Clamp01(AudioSpectrum.midAmp);
        var treble = Mathf.Clamp01(AudioSpectrum.trebleAmp);
        var bandBrightness = Mathf.Clamp01(bass * 0.45f + mid * 0.75f + treble * 1f);

        var liveTint = new Color(
            1.1f + bass * 1.6f + mid * 0.2f,
            0.35f + bass * 0.5f + mid * 1.2f + treble * 0.4f,
            0.08f + mid * 0.2f + treble * 1.15f,
            1f);

        if (particleMaterial.HasProperty("_BaseColor"))
        {
            particleMaterial.SetColor("_BaseColor", liveTint);
        }
        if (particleMaterial.HasProperty("_Color"))
        {
            particleMaterial.SetColor("_Color", liveTint);
        }
        if (particleMaterial.HasProperty("_Intensity"))
        {
            particleMaterial.SetFloat("_Intensity", 0.8f + bandBrightness * liveBandBrightnessBoost);
        }
    }

    private void EmitFireFromField()
    {
        if (fireParticles == null || pointPositions == null)
        {
            return;
        }

        var totalAmp = Mathf.Clamp01(AudioSpectrum.audioAmp * 220f);
        var bass = Mathf.Clamp01(AudioSpectrum.bassAmp);
        var mid = Mathf.Clamp01(AudioSpectrum.midAmp);
        var treble = Mathf.Clamp01(AudioSpectrum.trebleAmp);
        var audioEmissionFactor = 1f + totalAmp * totalAmplitudeSpawnBoost + bass * bassEmissionBoost;
        var audioSizeFactor = 1f + mid * midSizeBoost + bass * 0.35f;
        var audioSpeedFactor = 1f + treble * trebleSpeedBoost + mid * 0.35f;
        var audioLifetimeFactor = 1f;
        var audioBrightness = Mathf.Clamp01(0.15f + totalAmp * 0.2f + bass * 0.3f + treble * audioBrightnessBoost);
        var spawnThreshold = Mathf.Max(0.005f, deltaSpawnThreshold - totalAmp * totalAmplitudeThresholdReduction);

        var emitParams = new ParticleSystem.EmitParams();

        for (var i = 0; i < pointPositions.Length; i++)
        {
            var directDelta = deltaGrid[i];
            if (directDelta <= spawnThreshold)
            {
                continue;
            }

            var deltaIntensity = Mathf.Clamp01((directDelta - spawnThreshold) / Mathf.Max(0.0001f, 1f - spawnThreshold));
            var spawnProbability = Mathf.Clamp01(amplitudeSpawnProbability * totalAmp * (0.25f + deltaIntensity * 0.75f));
            var spawnRoll = StableHash01(i * 131 + Time.frameCount * 17);
            if (spawnRoll > spawnProbability)
            {
                continue;
            }

            var spectrumLevel = SampleSpectrumLevel(i);
            var intensity = Mathf.Clamp01(deltaIntensity * (0.35f + totalAmp * amplitudeIntensityBoost));
            var vector = immediateVectorGrid[i].sqrMagnitude > 0.0001f ? immediateVectorGrid[i] : vectorGrid[i];
            var baseDirection = vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector2.up;
            if (baseDirection.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            var phase = StableHash01(i * 23 + 11);
            var jitter2D = new Vector2(
                StableHash01(i * 131 + Mathf.RoundToInt(Time.time * 1000f)) - 0.5f,
                StableHash01(i * 197 + Mathf.RoundToInt(Time.time * 1300f)) - 0.5f);

            var direction = new Vector3(
                baseDirection.x + jitter2D.x * fireRandomness,
                baseDirection.y + jitter2D.y * fireRandomness,
                0.8f);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }
            direction.Normalize();

            var emitCount = Mathf.Max(1, Mathf.CeilToInt(intensity * Mathf.Lerp(1f, deltaImmediateBurst, totalAmp) * audioEmissionFactor));
            var frequencyColor = EvaluateFrequencyColor(i, Mathf.Clamp01(totalAmp + spectrumLevel * 0.5f));

            emitParams.position = pointPositions[i] + new Vector3(
                jitter2D.x * firePositionJitter,
                jitter2D.y * firePositionJitter,
                0f);
            emitParams.velocity = direction * Mathf.Lerp(fireSpeed * 0.8f, fireSpeed * deltaVelocityBoost, intensity) * audioSpeedFactor;
            emitParams.startLifetime = Mathf.Min(
                Mathf.Lerp(fireLifetime * 0.08f, fireLifetime * deltaLifetimeFactor, intensity * Mathf.Lerp(0.85f, 1.1f, phase)) * audioLifetimeFactor,
                maxReactiveLifetime);
            emitParams.startSize = Mathf.Lerp(fireSize * 0.08f, fireSize * 0.3f, intensity) * audioSizeFactor;
            emitParams.startColor = Color.Lerp(
                frequencyColor * (0.35f + audioBrightness * 0.35f),
                Color.Lerp(frequencyColor, Color.white, Mathf.Clamp01(audioBrightness + totalAmp * 0.35f)),
                Mathf.Clamp01(0.35f + intensity + spectrumLevel * 0.4f));
            emitParams.randomSeed = (uint)(1 + i * 977 + Mathf.RoundToInt(Time.time * 1000f) + emitCount * 131);
            fireParticles.Emit(emitParams, emitCount);
        }
    }

    private static float StableHash01(int seed)
    {
        var value = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private Material CreateVectorMaterial()
    {
        var shader = Shader.Find(VectorScatterShaderName);
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
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
        if (material.HasProperty("_Intensity"))
        {
            material.SetFloat("_Intensity", 1f);
        }

        return material;
    }

    private Material CreateParticleMaterial()
    {
        var shader = Shader.Find(SoftParticleShaderName);
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }
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
        if (material.HasProperty("_Intensity"))
        {
            material.SetFloat("_Intensity", 1f);
        }

        return material;
    }

    private static void ApplyMaterialLook(Material material, Color color, float intensity)
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
        if (material.HasProperty("_Intensity"))
        {
            material.SetFloat("_Intensity", intensity);
        }
    }
}

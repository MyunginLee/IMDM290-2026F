using UnityEngine;

[DisallowMultipleComponent]
public class GestureDemoAudioVisualInteraction : MonoBehaviour
{
    private const float DryCutoffHz = 22000f;
    private const float MinResonanceQ = 1f;

    [Header("Dependencies")]
    [SerializeField] private MediaPipeBodyTracker tracker;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioLowPassFilter lowPassFilter;
    [SerializeField] private AudioReverbFilter reverbFilter;
    [SerializeField] private AudioClip loopClip;
    [SerializeField] private bool playOnStart = true;

    [Header("Left Hand -> LowPass")]
    [SerializeField, Range(200f, 10000f)] private float minBandwidthHz = 400f;
    [SerializeField, Range(500f, 12000f)] private float maxBandwidthHz = 8000f;
    [SerializeField, Range(0f, 0.2f)] private float minThumbIndexDistance = 0.01f;
    [SerializeField, Range(0.05f, 0.4f)] private float maxThumbIndexDistance = 0.2f;
    [SerializeField, Range(1f, 8f)] private float maxResonanceQ = 4f;

    [Header("Right Hand -> Reverb")]
    [SerializeField, Range(-10000f, -100f)] private float minReverbLevel = -10000f;
    [SerializeField, Range(-4000f, 2000f)] private float maxReverbLevel = -300f;
    [SerializeField, Range(0.1f, 5f)] private float minDecayTime = 0.25f;
    [SerializeField, Range(1f, 20f)] private float maxDecayTime = 8f;

    [Header("Smoothing")]
    [SerializeField, Range(1f, 30f)] private float parameterLerpSpeed = 12f;

    [Header("Visual Feedback (AudioReactive Style)")]
    [SerializeField] private bool showVisualIndicators = true;
    [SerializeField, Range(64, 1200)] private int pointCount = 200;
    [SerializeField] private bool attachPointCloudToMainCamera = false;
    [SerializeField] private Vector3 pointCloudOffset = new Vector3(0f, 0f, 8f);
    [SerializeField, Range(1f, 500f)] private float motionStartRadius = 100f;
    [SerializeField, Range(0.5f, 100f)] private float motionEndRadius = 3f;
    [SerializeField, Range(0.5f, 6f)] private float reverbStretchByAmount = 2.4f;
    [SerializeField, Range(0f, 1f)] private float minSaturationWhenFiltered = 0.06f;
    [SerializeField, Range(20f, 2000f)] private float frequencyBinGain = 600f;
    [SerializeField, Range(0f, 1f)] private float binLerpOffsetStrength = 0.35f;
    [SerializeField, Range(0f, 60f)] private float binRotationBoost = 14f;
    [SerializeField, Range(1f, 40f)] private float binResponseLerpSpeed = 14f;

    [Header("Directional Lights Per Point")]
    [SerializeField] private bool addLightPerPoint = true;
    [SerializeField] private bool syncPointLightColor = true;
    [SerializeField, Range(0f, 3f)] private float pointLightIntensity = 0.2f;
    [SerializeField] private bool pointLightShadows = false;

    [Header("Right Hand Position -> Cloud Movement")]
    [SerializeField] private bool moveCloudWithRightHand = true;
    [SerializeField] private bool mirrorRightHandX = true;
    [SerializeField, Range(0f, 6f)] private float rightHandMoveRangeX = 1.8f;
    [SerializeField, Range(0f, 6f)] private float rightHandMoveRangeY = 1.4f;
    [SerializeField, Range(1f, 30f)] private float rightHandMoveLerpSpeed = 10f;

    [Header("Right Hand Height -> Camera FOV")]
    [SerializeField] private bool controlCameraFovWithRightHand = true;
    [SerializeField] private bool useDualAxisFovZoom = true;
    [SerializeField] private Camera targetCamera;
    [SerializeField, Range(20f, 70f)] private float minFieldOfView = 45f;
    [SerializeField, Range(30f, 170f)] private float maxFieldOfView = 80f;
    [SerializeField, Range(20f, 70f)] private float minHorizontalFieldOfView = 70f;
    [SerializeField, Range(30f, 170f)] private float maxHorizontalFieldOfView = 110f;
    [SerializeField, Range(1f, 30f)] private float fovLerpSpeed = 5f;

    [Header("Right Hand Pinch -> Point LookAt")]
    [SerializeField] private bool pointLookAtRightHandOnPinch = true;
    [SerializeField, Range(1f, 40f)] private float pinchLookAtLerpSpeed = 18f;
    [SerializeField, Range(1f, 20f)] private float pinchTargetRange = 10f;

    private Transform pointCloudRoot;
    private Transform[] points;
    private Material[] pointMaterials;
    private Light[] pointLights;
    private Vector3[] startPositions;
    private Vector3[] endPositions;
    private int[] pointBinIndices;
    private float[] pointBinAmplitudes;
    private float motionTime;
    private bool dualAxisZoomInitialized;
    private bool dualAxisProjectionApplied;
    private float currentVerticalFov;
    private float currentHorizontalFov;
    private bool lastAddLightPerPoint;

    private void Awake()
    {
        if (tracker == null)
        {
            tracker = FindObjectOfType<MediaPipeBodyTracker>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (loopClip != null && audioSource.clip == null)
        {
            audioSource.clip = loopClip;
        }

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (lowPassFilter == null)
        {
            lowPassFilter = GetComponent<AudioLowPassFilter>();
        }
        if (lowPassFilter == null)
        {
            lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
        }

        if (reverbFilter == null)
        {
            reverbFilter = GetComponent<AudioReverbFilter>();
        }
        if (reverbFilter == null)
        {
            reverbFilter = gameObject.AddComponent<AudioReverbFilter>();
        }
        if (GetComponent<AudioSpectrum>() == null)
        {
            gameObject.AddComponent<AudioSpectrum>();
        }

        reverbFilter.reverbPreset = AudioReverbPreset.User;
        reverbFilter.reverbLevel = minReverbLevel;
        reverbFilter.decayTime = minDecayTime;
        lowPassFilter.cutoffFrequency = DryCutoffHz;
        lowPassFilter.lowpassResonanceQ = MinResonanceQ;

        EnsureVisualIndicators();
    }

    private void Start()
    {
        if (playOnStart && audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private void Update()
    {
        if (tracker == null || lowPassFilter == null || reverbFilter == null)
        {
            return;
        }

        var leftTracked = tracker.LeftHandTracked;
        var leftHeight = leftTracked ? Mathf.Clamp01(tracker.LeftHandPosition.y) : 0f;
        var leftDistance = leftTracked ? tracker.LeftThumbIndexDistance : 0f;
        var leftAmount = leftTracked ? Mathf.InverseLerp(minThumbIndexDistance, maxThumbIndexDistance, leftDistance) : 0f;
        var targetBandwidth = Mathf.Lerp(minBandwidthHz, maxBandwidthHz, leftHeight);
        var targetCutoff = Mathf.Lerp(DryCutoffHz, targetBandwidth, leftAmount);
        var targetResonance = Mathf.Lerp(MinResonanceQ, maxResonanceQ, leftHeight);

        lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, targetCutoff, Time.deltaTime * parameterLerpSpeed);
        lowPassFilter.lowpassResonanceQ = Mathf.Lerp(lowPassFilter.lowpassResonanceQ, targetResonance, Time.deltaTime * parameterLerpSpeed);

        var rightTracked = tracker.RightHandTracked;
        var rightHeight = rightTracked ? Mathf.Clamp01(tracker.RightHandPosition.y) : 0f;
        var rightHeightForReverb = rightTracked ? (1f - rightHeight) : 0f;
        var targetReverbLevel = Mathf.Lerp(minReverbLevel, maxReverbLevel, rightHeightForReverb);
        var targetDecayTime = Mathf.Lerp(minDecayTime, maxDecayTime, rightHeightForReverb);
        reverbFilter.reverbLevel = Mathf.Lerp(reverbFilter.reverbLevel, targetReverbLevel, Time.deltaTime * parameterLerpSpeed);
        reverbFilter.decayTime = Mathf.Lerp(reverbFilter.decayTime, targetDecayTime, Time.deltaTime * parameterLerpSpeed);

        UpdateCameraFov(rightTracked, rightHeight);

        var filteredAmount = 1f - Mathf.InverseLerp(minBandwidthHz, DryCutoffHz, lowPassFilter.cutoffFrequency);
        var reverbAmount = Mathf.InverseLerp(minReverbLevel, maxReverbLevel, reverbFilter.reverbLevel);
        UpdateVisualIndicators(Mathf.Clamp01(filteredAmount), Mathf.Clamp01(reverbAmount));
    }

    private void OnDestroy()
    {
        RestoreCameraProjection();

        if (pointMaterials != null)
        {
            for (var i = 0; i < pointMaterials.Length; i++)
            {
                if (pointMaterials[i] != null)
                {
                    Destroy(pointMaterials[i]);
                }
            }
        }

        if (pointCloudRoot != null)
        {
            Destroy(pointCloudRoot.gameObject);
        }
    }

    private void OnDisable()
    {
        RestoreCameraProjection();
    }

    private void EnsureVisualIndicators()
    {
        if (!showVisualIndicators)
        {
            if (pointCloudRoot != null)
            {
                pointCloudRoot.gameObject.SetActive(false);
            }
            return;
        }

        if (points == null || points.Length != pointCount || lastAddLightPerPoint != addLightPerPoint)
        {
            BuildPointCloud();
        }
    }

    private void BuildPointCloud()
    {
        if (pointCloudRoot != null)
        {
            Destroy(pointCloudRoot.gameObject);
        }

        var parent = ResolvePointCloudParent();
        pointCloudRoot = new GameObject("Gesture Point Cloud").transform;
        pointCloudRoot.SetParent(parent, false);
        pointCloudRoot.localPosition = pointCloudOffset;
        pointCloudRoot.localRotation = Quaternion.identity;

        points = new Transform[pointCount];
        pointMaterials = new Material[pointCount];
        pointLights = new Light[pointCount];
        startPositions = new Vector3[pointCount];
        endPositions = new Vector3[pointCount];
        pointBinIndices = new int[pointCount];
        pointBinAmplitudes = new float[pointCount];

        for (var i = 0; i < pointCount; i++)
        {
            startPositions[i] = new Vector3(
                motionStartRadius * Random.Range(-1f, 1f),
                motionStartRadius * Random.Range(-1f, 1f),
                motionStartRadius * Random.Range(-1f, 1f));

            endPositions[i] = new Vector3(
                motionEndRadius * Mathf.Sin(i * 2f * Mathf.PI / pointCount),
                motionEndRadius * Mathf.Cos(i * 2f * Mathf.PI / pointCount),
                0f);

            var point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            point.name = $"Point_{i:000}";
            point.transform.SetParent(pointCloudRoot, false);

            var collider = point.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = point.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var material = renderer.material;

            var hue = (float)i / pointCount;
            point.transform.localPosition = startPositions[i];
            point.transform.localRotation = Quaternion.Euler(
                Random.Range(-180f, 180f),
                Random.Range(-180f, 180f),
                Random.Range(-180f, 180f));
            point.transform.localScale = new Vector3(
                Random.Range(0.3f, 0.5f),
                Random.Range(0.3f, 0.5f),
                Random.Range(0.3f, 0.5f));
            material.color = Color.HSVToRGB(hue, 1f, 1f);

            points[i] = point.transform;
            pointMaterials[i] = material;
            pointBinIndices[i] = (AudioSpectrum.FFTSIZE - 1) * i / Mathf.Max(1, pointCount - 1);
            pointBinAmplitudes[i] = 0f;

            if (addLightPerPoint)
            {
                var lightObject = new GameObject($"DirectionalLight_{i:000}");
                lightObject.transform.SetParent(point.transform, false);
                lightObject.transform.localPosition = Vector3.zero;

                var pointLight = lightObject.AddComponent<Light>();
                pointLight.type = LightType.Directional;
                pointLight.intensity = pointLightIntensity;
                pointLight.color = material.color;
                pointLight.shadows = pointLightShadows ? LightShadows.Soft : LightShadows.None;
                pointLights[i] = pointLight;
            }
        }

        lastAddLightPerPoint = addLightPerPoint;
    }

    private Transform ResolvePointCloudParent()
    {
        if (attachPointCloudToMainCamera && Camera.main != null)
        {
            return Camera.main.transform;
        }

        return transform;
    }

    private void UpdateVisualIndicators(float filteredAmount, float reverbAmount)
    {
        if (!showVisualIndicators)
        {
            if (pointCloudRoot != null)
            {
                pointCloudRoot.gameObject.SetActive(false);
            }
            return;
        }

        EnsureVisualIndicators();
        if (pointCloudRoot == null || points == null)
        {
            return;
        }

        if (!pointCloudRoot.gameObject.activeSelf)
        {
            pointCloudRoot.gameObject.SetActive(true);
        }

        var desiredParent = ResolvePointCloudParent();
        if (pointCloudRoot.parent != desiredParent)
        {
            pointCloudRoot.SetParent(desiredParent, false);
            pointCloudRoot.localPosition = ComputeCloudOffsetFromRightHand();
            pointCloudRoot.localRotation = Quaternion.identity;
        }
        else
        {
            var targetOffset = ComputeCloudOffsetFromRightHand();
            pointCloudRoot.localPosition = Vector3.Lerp(
                pointCloudRoot.localPosition,
                targetOffset,
                Time.deltaTime * rightHandMoveLerpSpeed);
        }

        var audioAmp = AudioSpectrum.audioAmp;
        motionTime += Time.deltaTime * (0.3f+audioAmp * 0.3f);
        var baseLerpFraction = Mathf.Sin(motionTime) * 0.5f + 0.5f;
        var stretch = 1f + reverbAmount * reverbStretchByAmount;
        var rightPinchLookAt = pointLookAtRightHandOnPinch && tracker != null && tracker.RightHandTracked && tracker.RightHandPinch;
        var rightPinchTargetWorld = rightPinchLookAt
            ? pointCloudRoot.TransformPoint(ComputeRightHandPinchTargetLocal())
            : Vector3.zero;
        var samples = AudioSpectrum.samples;
        var sampleCount = samples != null ? samples.Length : 0;
        if (pointBinIndices == null || pointBinIndices.Length != points.Length)
        {
            pointBinIndices = new int[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                pointBinIndices[i] = (AudioSpectrum.FFTSIZE - 1) * i / Mathf.Max(1, points.Length - 1);
            }
        }
        if (pointBinAmplitudes == null || pointBinAmplitudes.Length != points.Length)
        {
            pointBinAmplitudes = new float[points.Length];
        }

        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            var material = pointMaterials[i];

            if (point == null || material == null)
            {
                continue;
            }

            var binAmp = 0f;
            if (sampleCount > 0 && pointBinIndices != null && i < pointBinIndices.Length)
            {
                var sampleIndex = Mathf.Clamp(pointBinIndices[i], 0, sampleCount - 1);
                binAmp = Mathf.Max(0f, samples[sampleIndex]) * frequencyBinGain;
            }
            pointBinAmplitudes[i] = Mathf.Lerp(pointBinAmplitudes[i], binAmp, Time.deltaTime * binResponseLerpSpeed);

            var pointLerpFraction = Mathf.Clamp01(baseLerpFraction + pointBinAmplitudes[i] * binLerpOffsetStrength);
            var position = Vector3.Lerp(startPositions[i], endPositions[i], pointLerpFraction);
            position.y *= stretch;
            point.localPosition = position;

            var scale = (1f + audioAmp) * (1f+tracker.RightHandPosition.y) *10f;
            point.localScale = new Vector3(scale, 1f, 1f);
            if (rightPinchLookAt)
            {
                var toTarget = rightPinchTargetWorld - point.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    var lookRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    point.rotation = Quaternion.Slerp(point.rotation, lookRotation, Time.deltaTime * pinchLookAtLerpSpeed);
                }
            }
            else
            {
                point.Rotate(audioAmp + pointBinAmplitudes[i] * binRotationBoost, 1f, 1f, Space.Self);
            }

            var hue = (float)i / points.Length;
            var saturation = Mathf.Lerp(Mathf.Cos(audioAmp / 10f), minSaturationWhenFiltered, filteredAmount);
            material.color = Color.HSVToRGB(
                Mathf.Abs(hue * Mathf.Cos(motionTime)),
                saturation,
                2f + Mathf.Cos(motionTime));

            if (pointLights != null && i < pointLights.Length && pointLights[i] != null)
            {
                pointLights[i].intensity = pointLightIntensity;
                pointLights[i].shadows = pointLightShadows ? LightShadows.Soft : LightShadows.None;
                if (syncPointLightColor)
                {
                    pointLights[i].color = material.color;
                }
            }
        }
    }

    private Vector3 ComputeCloudOffsetFromRightHand()
    {
        if (!moveCloudWithRightHand || tracker == null || !tracker.RightHandTracked)
        {
            return pointCloudOffset;
        }

        var right = tracker.RightHandPosition;
        var centeredX = Mathf.Clamp01(right.x) * 2f - 1f;
        var centeredY = Mathf.Clamp01(right.y) * 2f - 1f;
        if (mirrorRightHandX)
        {
            centeredX = -centeredX;
        }

        return pointCloudOffset + new Vector3(
            centeredX * rightHandMoveRangeX,
            centeredY * rightHandMoveRangeY,
            0f);
    }

    private Vector3 ComputeRightHandPinchTargetLocal()
    {
        if (tracker == null || !tracker.RightHandTracked)
        {
            return Vector3.zero;
        }

        var right = tracker.RightHandPosition;
        var centeredX = Mathf.Clamp01(right.x) * 2f - 1f;
        var centeredY = Mathf.Clamp01(right.y) * 2f - 1f;
        if (mirrorRightHandX)
        {
            centeredX = -centeredX;
        }

        return new Vector3(
            centeredX * pinchTargetRange,
            centeredY * pinchTargetRange,
            0f);
    }

    private void UpdateCameraFov(bool rightTracked, float rightHeight)
    {
        var cameraToControl = ResolveTargetCamera();
        if (cameraToControl == null)
        {
            return;
        }

        if (!controlCameraFovWithRightHand)
        {
            RestoreCameraProjection();
            return;
        }

        var targetVerticalFov = rightTracked
            ? Mathf.Lerp(minFieldOfView, maxFieldOfView, rightHeight)
            : minFieldOfView;

        if (useDualAxisFovZoom)
        {
            var targetHorizontalFov = rightTracked
                ? Mathf.Lerp(minHorizontalFieldOfView, maxHorizontalFieldOfView, rightHeight)
                : minHorizontalFieldOfView;

            if (!dualAxisZoomInitialized)
            {
                currentVerticalFov = targetVerticalFov;
                currentHorizontalFov = targetHorizontalFov;
                dualAxisZoomInitialized = true;
            }

            currentVerticalFov = Mathf.Lerp(currentVerticalFov, targetVerticalFov, Time.deltaTime * fovLerpSpeed);
            currentHorizontalFov = Mathf.Lerp(currentHorizontalFov, targetHorizontalFov, Time.deltaTime * fovLerpSpeed);

            ApplyDualAxisProjection(cameraToControl, currentHorizontalFov, currentVerticalFov);
            return;
        }

        if (dualAxisProjectionApplied)
        {
            cameraToControl.ResetProjectionMatrix();
            dualAxisProjectionApplied = false;
            dualAxisZoomInitialized = false;
        }

        cameraToControl.fieldOfView = Mathf.Lerp(
            cameraToControl.fieldOfView,
            targetVerticalFov,
            Time.deltaTime * fovLerpSpeed);
    }

    private void ApplyDualAxisProjection(Camera cameraToControl, float horizontalFov, float verticalFov)
    {
        var near = cameraToControl.nearClipPlane;
        var far = cameraToControl.farClipPlane;
        var halfWidth = near * Mathf.Tan(0.5f * Mathf.Deg2Rad * Mathf.Clamp(horizontalFov, 1f, 179f));
        var halfHeight = near * Mathf.Tan(0.5f * Mathf.Deg2Rad * Mathf.Clamp(verticalFov, 1f, 179f));

        cameraToControl.projectionMatrix = Matrix4x4.Frustum(
            -halfWidth,
            halfWidth,
            -halfHeight,
            halfHeight,
            near,
            far);
        cameraToControl.fieldOfView = verticalFov;
        dualAxisProjectionApplied = true;
    }

    private void RestoreCameraProjection()
    {
        var cameraToControl = ResolveTargetCamera();
        if (cameraToControl != null && dualAxisProjectionApplied)
        {
            cameraToControl.ResetProjectionMatrix();
        }

        dualAxisProjectionApplied = false;
        dualAxisZoomInitialized = false;
    }

    private Camera ResolveTargetCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        targetCamera = Camera.main;
        return targetCamera;
    }
}

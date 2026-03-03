using System.Collections;
using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample.Holistic;
using UnityEngine;
using UnityEngine.Rendering;
using Color = UnityEngine.Color;

[DisallowMultipleComponent]
public class MediaPipeBodyTracker : MonoBehaviour
{
    [Header("Graph Source")]
    [SerializeField] private HolisticTrackingGraph graphRunner;

    [Header("Hand Data")]
    [SerializeField] private Vector3 leftHandPosition;
    [SerializeField] private bool leftHandPinch;
    [SerializeField] private Vector3 rightHandPosition;
    [SerializeField] private bool rightHandPinch;

    [Header("Body Data")]
    [SerializeField] private Vector3 torsoPosition;
    [SerializeField] private Vector3 headPosition;

    [Header("Debug / Diagnostics")]
    [SerializeField] private bool verboseLogging = false;
    [SerializeField] private int leftHandPacketCount;
    [SerializeField] private int rightHandPacketCount;
    [SerializeField] private int posePacketCount;

    [Header("Detection Settings")]
    [SerializeField, Range(0.001f, 0.2f)] private float pinchThreshold = 0.035f;

    [Header("Simple Hand Dot Demo")]
    [SerializeField] private bool showHandDots = true;
    [SerializeField] private bool changeDotColorOnPinch = true;
    [SerializeField] private Camera handDotCamera;
    [SerializeField] private bool mirrorX = true;
    [SerializeField] private bool flipY = true;
    [SerializeField, Range(0.2f, 10f)] private float handDotDistance = 2.5f;
    [SerializeField, Range(0.005f, 0.2f)] private float handDotSize = 0.035f;
    [SerializeField] private Color leftHandIdleColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private Color rightHandIdleColor = new Color(1f, 0.5f, 0.2f, 1f);
    [SerializeField] private Color pinchColor = Color.yellow;

    private readonly object dataLock = new object();
    private Vector3 pendingLeftHandPosition;
    private Vector3 pendingRightHandPosition;
    private Vector3 pendingTorsoPosition;
    private Vector3 pendingHeadPosition;
    private bool pendingLeftPinch;
    private bool pendingRightPinch;
    private bool leftHandTracked;
    private bool rightHandTracked;
    private bool poseTracked;
    private bool leftDirty;
    private bool rightDirty;
    private bool poseDirty;
    private bool leftHandVisible;
    private bool rightHandVisible;
    private Coroutine subscribeRoutine;
    private bool hasSubscriptions;
    private HolisticTrackingGraph subscribedRunner;
    private Transform leftHandDotTransform;
    private Transform rightHandDotTransform;
    private Material leftHandDotMaterial;
    private Material rightHandDotMaterial;

    public Vector3 LeftHandPosition => leftHandPosition;
    public bool LeftHandPinch => leftHandPinch;
    public Vector3 RightHandPosition => rightHandPosition;
    public bool RightHandPinch => rightHandPinch;
    public Vector3 TorsoPosition => torsoPosition;
    public Vector3 HeadPosition => headPosition;

    private void OnEnable()
    {
        if (showHandDots)
        {
            EnsureHandDots();
        }

        if (graphRunner == null)
        {
            graphRunner = FindObjectOfType<HolisticTrackingGraph>();
        }

        if (graphRunner == null)
        {
            Debug.LogWarning($"{nameof(MediaPipeBodyTracker)} on {name} could not find a {nameof(HolisticTrackingGraph)}.");
            return;
        }

        if (!TrySubscribe())
        {
            if (verboseLogging)
            {
                Debug.Log($"{nameof(MediaPipeBodyTracker)} waiting for graph runner to initialise...");
            }
            subscribeRoutine = StartCoroutine(SubscribeWhenReady());
        }
    }

    private void OnDisable()
    {
        if (subscribeRoutine != null)
        {
            StopCoroutine(subscribeRoutine);
            subscribeRoutine = null;
        }

        Unsubscribe();
        DisposeHandDots();
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (isActiveAndEnabled)
        {
            if (TrySubscribe())
            {
                subscribeRoutine = null;
                yield break;
            }

            yield return null;
        }
    }

    private bool TrySubscribe()
    {
        if (graphRunner == null || hasSubscriptions)
        {
            return hasSubscriptions;
        }

        try
        {
            graphRunner.OnLeftHandLandmarksOutput += HandleLeftHand;
            graphRunner.OnRightHandLandmarksOutput += HandleRightHand;
            graphRunner.OnPoseWorldLandmarksOutput += HandlePoseWorld;
            subscribedRunner = graphRunner;
            hasSubscriptions = true;

            if (verboseLogging)
            {
                Debug.Log($"{nameof(MediaPipeBodyTracker)} listening to {graphRunner.name}");
            }

            return true;
        }
        catch (System.NullReferenceException)
        {
            return false;
        }
    }

    private void Unsubscribe()
    {
        if (!hasSubscriptions)
        {
            subscribedRunner = null;
            hasSubscriptions = false;
            return;
        }

        var runner = subscribedRunner != null ? subscribedRunner : graphRunner;
        if (runner != null)
        {
            runner.OnLeftHandLandmarksOutput -= HandleLeftHand;
            runner.OnRightHandLandmarksOutput -= HandleRightHand;
            runner.OnPoseWorldLandmarksOutput -= HandlePoseWorld;
        }

        subscribedRunner = null;
        hasSubscriptions = false;
    }

    private void Update()
    {
        lock (dataLock)
        {
            if (leftDirty)
            {
                if (leftHandTracked)
                {
                    leftHandPosition = pendingLeftHandPosition;
                    leftHandPinch = pendingLeftPinch;
                    leftHandVisible = true;
                }
                else
                {
                    leftHandPosition = Vector3.zero;
                    leftHandPinch = false;
                    leftHandVisible = false;
                }

                leftDirty = false;
            }

            if (rightDirty)
            {
                if (rightHandTracked)
                {
                    rightHandPosition = pendingRightHandPosition;
                    rightHandPinch = pendingRightPinch;
                    rightHandVisible = true;
                }
                else
                {
                    rightHandPosition = Vector3.zero;
                    rightHandPinch = false;
                    rightHandVisible = false;
                }

                rightDirty = false;
            }

            if (poseDirty)
            {
                if (poseTracked)
                {
                    torsoPosition = pendingTorsoPosition;
                    headPosition = pendingHeadPosition;
                }
                else
                {
                    torsoPosition = Vector3.zero;
                    headPosition = Vector3.zero;
                }

                poseDirty = false;
            }
        }

        UpdateHandDots();
    }

    private void HandleLeftHand(object sender, OutputStream<NormalizedLandmarkList>.OutputEventArgs eventArgs)
    {
        leftHandPacketCount++;
        if (verboseLogging && (leftHandPacketCount % 30 == 0))
        {
            Debug.Log($"{nameof(MediaPipeBodyTracker)} received {leftHandPacketCount} left-hand packets");
        }

        var landmarkList = ExtractNormalizedLandmarks(eventArgs.packet);

        lock (dataLock)
        {
            if (landmarkList != null && landmarkList.Landmark.Count > 0)
            {
                leftHandTracked = true;
                pendingLeftHandPosition = ToUnityVector(landmarkList.Landmark[0]);
                pendingLeftPinch = IsPinching(landmarkList);
            }
            else
            {
                leftHandTracked = false;
                pendingLeftHandPosition = Vector3.zero;
                pendingLeftPinch = false;
            }

            leftDirty = true;
        }
    }

    private void HandleRightHand(object sender, OutputStream<NormalizedLandmarkList>.OutputEventArgs eventArgs)
    {
        rightHandPacketCount++;
        if (verboseLogging && (rightHandPacketCount % 30 == 0))
        {
            Debug.Log($"{nameof(MediaPipeBodyTracker)} received {rightHandPacketCount} right-hand packets");
        }

        var landmarkList = ExtractNormalizedLandmarks(eventArgs.packet);

        lock (dataLock)
        {
            if (landmarkList != null && landmarkList.Landmark.Count > 0)
            {
                rightHandTracked = true;
                pendingRightHandPosition = ToUnityVector(landmarkList.Landmark[0]);
                pendingRightPinch = IsPinching(landmarkList);
            }
            else
            {
                rightHandTracked = false;
                pendingRightHandPosition = Vector3.zero;
                pendingRightPinch = false;
            }

            rightDirty = true;
        }
    }

    private void HandlePoseWorld(object sender, OutputStream<LandmarkList>.OutputEventArgs eventArgs)
    {
        posePacketCount++;
        if (verboseLogging && (posePacketCount % 30 == 0))
        {
            Debug.Log($"{nameof(MediaPipeBodyTracker)} received {posePacketCount} pose packets");
        }

        var worldLandmarks = ExtractWorldLandmarks(eventArgs.packet);

        lock (dataLock)
        {
            if (worldLandmarks != null && worldLandmarks.Landmark.Count >= 33)
            {
                poseTracked = true;
                pendingHeadPosition = ToUnityVector(worldLandmarks.Landmark[0]);

                var leftShoulder = ToUnityVector(worldLandmarks.Landmark[11]);
                var rightShoulder = ToUnityVector(worldLandmarks.Landmark[12]);
                var leftHip = ToUnityVector(worldLandmarks.Landmark[23]);
                var rightHip = ToUnityVector(worldLandmarks.Landmark[24]);

                var shouldersCenter = (leftShoulder + rightShoulder) * 0.5f;
                var hipsCenter = (leftHip + rightHip) * 0.5f;
                pendingTorsoPosition = (shouldersCenter + hipsCenter) * 0.5f;
            }
            else
            {
                poseTracked = false;
                pendingHeadPosition = Vector3.zero;
                pendingTorsoPosition = Vector3.zero;
            }

            poseDirty = true;
        }
    }

    private NormalizedLandmarkList ExtractNormalizedLandmarks(Packet<NormalizedLandmarkList> packet)
    {
        return packet == null ? null : packet.Get(NormalizedLandmarkList.Parser);
    }

    private LandmarkList ExtractWorldLandmarks(Packet<LandmarkList> packet)
    {
        return packet == null ? null : packet.Get(LandmarkList.Parser);
    }

    private Vector3 ToUnityVector(NormalizedLandmark landmark)
    {
        return new Vector3(landmark.X, landmark.Y, landmark.Z);
    }

    private Vector3 ToUnityVector(Landmark landmark)
    {
        return new Vector3(landmark.X, landmark.Y, landmark.Z);
    }

    private bool IsPinching(NormalizedLandmarkList landmarks)
    {
        if (landmarks.Landmark.Count <= 8)
        {
            return false;
        }

        var thumbTip = landmarks.Landmark[4];
        var indexTip = landmarks.Landmark[8];
        var thumb = new Vector2(thumbTip.X, thumbTip.Y);
        var index = new Vector2(indexTip.X, indexTip.Y);
        var distance = Vector2.Distance(thumb, index);

        if (distance > pinchThreshold)
        {
            return false;
        }

        if (landmarks.Landmark.Count > 12)
        {
            var middleTip = landmarks.Landmark[12];
            var middle = new Vector2(middleTip.X, middleTip.Y);
            var indexToMiddle = Vector2.Distance(index, middle);
            if (distance > indexToMiddle)
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureHandDots()
    {
        if (leftHandDotTransform == null)
        {
            CreateHandDot("Left Hand Dot", out leftHandDotTransform, out leftHandDotMaterial);
        }

        if (rightHandDotTransform == null)
        {
            CreateHandDot("Right Hand Dot", out rightHandDotTransform, out rightHandDotMaterial);
        }
    }

    private void CreateHandDot(string dotName, out Transform dotTransform, out Material dotMaterial)
    {
        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = dotName;
        dot.transform.SetParent(transform, false);

        var collider = dot.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        dotTransform = dot.transform;
        var dotRenderer = dot.GetComponent<Renderer>();
        dotRenderer.shadowCastingMode = ShadowCastingMode.Off;
        dotRenderer.receiveShadows = false;
        dotMaterial = dotRenderer.material;
        dot.SetActive(false);
    }

    private void UpdateHandDots()
    {
        if (!showHandDots)
        {
            SetDotActive(leftHandDotTransform, false);
            SetDotActive(rightHandDotTransform, false);
            return;
        }

        EnsureHandDots();

        var cameraToUse = ResolveHandDotCamera();
        if (cameraToUse == null)
        {
            SetDotActive(leftHandDotTransform, false);
            SetDotActive(rightHandDotTransform, false);
            return;
        }

        UpdateSingleHandDot(
            leftHandDotTransform,
            leftHandDotMaterial,
            leftHandVisible,
            leftHandPosition,
            leftHandPinch,
            leftHandIdleColor,
            cameraToUse);

        UpdateSingleHandDot(
            rightHandDotTransform,
            rightHandDotMaterial,
            rightHandVisible,
            rightHandPosition,
            rightHandPinch,
            rightHandIdleColor,
            cameraToUse);
    }

    private void UpdateSingleHandDot(
        Transform dotTransform,
        Material dotMaterial,
        bool isTracked,
        Vector3 handPosition,
        bool isPinching,
        Color idleColor,
        Camera cameraToUse)
    {
        if (dotTransform == null)
        {
            return;
        }

        SetDotActive(dotTransform, isTracked);
        if (!isTracked)
        {
            return;
        }

        var viewportX = mirrorX ? 1f - handPosition.x : handPosition.x;
        var viewportY = flipY ? 1f - handPosition.y : handPosition.y;
        var viewportPoint = new Vector3(
            Mathf.Clamp01(viewportX),
            Mathf.Clamp01(viewportY),
            Mathf.Max(0.1f, handDotDistance));

        dotTransform.position = cameraToUse.ViewportToWorldPoint(viewportPoint);
        dotTransform.localScale = Vector3.one * handDotSize;

        if (dotMaterial != null)
        {
            dotMaterial.color = (changeDotColorOnPinch && isPinching) ? pinchColor : idleColor;
        }
    }

    private static void SetDotActive(Transform dotTransform, bool active)
    {
        if (dotTransform != null && dotTransform.gameObject.activeSelf != active)
        {
            dotTransform.gameObject.SetActive(active);
        }
    }

    private Camera ResolveHandDotCamera()
    {
        if (handDotCamera != null)
        {
            return handDotCamera;
        }

        handDotCamera = Camera.main;
        return handDotCamera;
    }

    private void DisposeHandDots()
    {
        if (leftHandDotMaterial != null)
        {
            Destroy(leftHandDotMaterial);
            leftHandDotMaterial = null;
        }

        if (rightHandDotMaterial != null)
        {
            Destroy(rightHandDotMaterial);
            rightHandDotMaterial = null;
        }

        if (leftHandDotTransform != null)
        {
            Destroy(leftHandDotTransform.gameObject);
            leftHandDotTransform = null;
        }

        if (rightHandDotTransform != null)
        {
            Destroy(rightHandDotTransform.gameObject);
            rightHandDotTransform = null;
        }
    }
}

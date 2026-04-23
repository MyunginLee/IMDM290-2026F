using UnityEngine;

[DisallowMultipleComponent]
public class CameraFrameDeltaViewer : MonoBehaviour
{
    [Header("Camera Input")]
    [SerializeField] private int requestedWidth = 640;
    [SerializeField] private int requestedHeight = 480;
    [SerializeField] private int requestedFps = 30;
    [SerializeField] private bool mirrorX = true;

    [Header("Delta")]
    [SerializeField, Range(0.001f, 0.2f)] private float deltaThreshold = 0.025f;
    [SerializeField, Range(1f, 40f)] private float deltaGain = 18f;
    [SerializeField, Range(0f, 1f)] private float frameDelta;

    private WebCamTexture webCamTexture;
    private Color32[] webCamPixels;
    private Color32[] deltaPixels;
    private float[] previousLuminance;
    private Texture2D deltaTexture;
    private bool hasPreviousFrame;

    private void Start()
    {
        var targetCamera = Camera.main;
        if (targetCamera != null)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = Color.black;
        }

        StartWebCam();
    }

    private void Update()
    {
        UpdateDeltaTexture();
    }

    private void OnGUI()
    {
        if (deltaTexture == null)
        {
            return;
        }

        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), deltaTexture, ScaleMode.ScaleToFit, false);
    }

    private void OnDisable()
    {
        StopWebCam();
    }

    private void OnDestroy()
    {
        StopWebCam();

        if (deltaTexture != null)
        {
            Destroy(deltaTexture);
            deltaTexture = null;
        }
    }

    private void StartWebCam()
    {
        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogWarning($"{nameof(CameraFrameDeltaViewer)} could not find a webcam.");
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

    private void UpdateDeltaTexture()
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

        var pixelCount = width * height;
        if (webCamPixels == null || webCamPixels.Length != pixelCount)
        {
            webCamPixels = new Color32[pixelCount];
            deltaPixels = new Color32[pixelCount];
            previousLuminance = new float[pixelCount];

            if (deltaTexture != null)
            {
                Destroy(deltaTexture);
            }

            deltaTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            deltaTexture.wrapMode = TextureWrapMode.Clamp;
            deltaTexture.filterMode = FilterMode.Point;
            hasPreviousFrame = false;
        }

        webCamTexture.GetPixels32(webCamPixels);

        if (!hasPreviousFrame)
        {
            InitializePreviousFrame(width, height);
            frameDelta = 0f;
            return;
        }

        var totalDelta = 0f;

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                var sourceX = mirrorX ? (width - 1 - x) : x;
                var sourceIndex = rowOffset + sourceX;
                var outputIndex = rowOffset + x;
                var pixel = webCamPixels[sourceIndex];
                var luminance = (pixel.r + pixel.g + pixel.b) / (255f * 3f);
                var rawDelta = Mathf.Abs(luminance - previousLuminance[outputIndex]);
                var deltaValue = Mathf.Clamp01((rawDelta - deltaThreshold) * deltaGain);
                var byteValue = (byte)Mathf.RoundToInt(deltaValue * 255f);

                deltaPixels[outputIndex] = new Color32(byteValue, byteValue, byteValue, 255);
                previousLuminance[outputIndex] = luminance;
                totalDelta += rawDelta;
            }
        }

        deltaTexture.SetPixels32(deltaPixels);
        deltaTexture.Apply(false, false);
        frameDelta = totalDelta / pixelCount;
    }

    private void InitializePreviousFrame(int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                var sourceX = mirrorX ? (width - 1 - x) : x;
                var sourceIndex = rowOffset + sourceX;
                var outputIndex = rowOffset + x;
                var pixel = webCamPixels[sourceIndex];
                var luminance = (pixel.r + pixel.g + pixel.b) / (255f * 3f);

                previousLuminance[outputIndex] = luminance;
                deltaPixels[outputIndex] = new Color32(0, 0, 0, 255);
            }
        }

        deltaTexture.SetPixels32(deltaPixels);
        deltaTexture.Apply(false, false);
        hasPreviousFrame = true;
    }
}

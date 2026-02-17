// Unity Audio Time-Spectral Plot (log frequency over time)
// IMDM Class Material
using UnityEngine;

public class AudioTimeSpectral : MonoBehaviour
{
    public float scale = 14f;
    public int displayBins = 128;
    public int historyLength = 72;
    public float plotWidth = 16f;
    public float plotDepth = 10f;
    public Vector3 origin = new Vector3(-8f, 0f, -5f);
    public float minLogHz = 20f;
    public float captureRate = 30f;
    public float minHeight = 0.01f;

    GameObject[,] bars;
    Renderer[,] barRenderers;
    float[,] history;
    int[] mappedBins;

    int fftSize;
    float nyquistHz;
    float hzPerBin;
    float xStep;
    float zStep;
    float barWidth;
    float barDepth;
    float captureTimer;
    MaterialPropertyBlock block;

    void Start()
    {
        fftSize = AudioSpectrum.samples != null && AudioSpectrum.samples.Length > 0
            ? AudioSpectrum.samples.Length
            : AudioSpectrum.FFTSIZE;

        nyquistHz = AudioSettings.outputSampleRate * 0.5f;
        hzPerBin = nyquistHz / fftSize;

        displayBins = Mathf.Clamp(displayBins, 16, fftSize);
        historyLength = Mathf.Clamp(historyLength, 16, 256);
        captureRate = Mathf.Max(1f, captureRate);

        xStep = displayBins > 1 ? plotWidth / (displayBins - 1) : 0f;
        zStep = historyLength > 1 ? plotDepth / (historyLength - 1) : 0f;
        barWidth = Mathf.Max(0.01f, xStep * 0.8f);
        barDepth = Mathf.Max(0.01f, zStep * 0.8f);

        bars = new GameObject[historyLength, displayBins];
        barRenderers = new Renderer[historyLength, displayBins];
        history = new float[historyLength, displayBins];
        mappedBins = new int[displayBins];
        block = new MaterialPropertyBlock();

        BuildLogMapping();
        CreateBars();
        RenderHistory();
    }

    void Update()
    {
        float step = 1f / captureRate;
        bool updated = false;
        captureTimer += Time.deltaTime;

        while (captureTimer >= step)
        {
            captureTimer -= step;
            CaptureNewSpectrum();
            updated = true;
        }

        if (updated)
        {
            RenderHistory();
        }
    }

    void BuildLogMapping()
    {
        // Map display x positions to FFT bins on a log-frequency axis.
        float safeMinHz = Mathf.Clamp(minLogHz, hzPerBin, nyquistHz);
        float minLog10 = Mathf.Log10(safeMinHz);
        float maxLog10 = Mathf.Log10(nyquistHz);

        for (int i = 0; i < displayBins; i++)
        {
            float t = displayBins > 1 ? i / (float)(displayBins - 1) : 0f;
            float hz = 0f;

            if (t > 0f)
            {
                float logHz = Mathf.Lerp(minLog10, maxLog10, t);
                hz = Mathf.Pow(10f, logHz);
            }

            mappedBins[i] = Mathf.Clamp(Mathf.RoundToInt(hz / hzPerBin), 0, fftSize - 1);
        }
    }

    void CreateBars()
    {
        for (int t = 0; t < historyLength; t++)
        {
            for (int i = 0; i < displayBins; i++)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"TimeSpectral_{t}_{i}";
                bar.transform.SetParent(transform);
                bar.transform.localScale = new Vector3(barWidth, minHeight, barDepth);
                bar.transform.position = GetBasePosition(t, i) + Vector3.up * (minHeight * 0.5f);

                Collider col = bar.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }

                bars[t, i] = bar;
                barRenderers[t, i] = bar.GetComponent<Renderer>();
            }
        }
    }

    void CaptureNewSpectrum()
    {
        if (AudioSpectrum.samples == null || AudioSpectrum.samples.Length == 0)
        {
            return;
        }

        int safeCount = Mathf.Min(fftSize, AudioSpectrum.samples.Length);

        // Move older frames backward in time.
        for (int t = historyLength - 1; t > 0; t--)
        {
            for (int i = 0; i < displayBins; i++)
            {
                history[t, i] = history[t - 1, i];
            }
        }

        // Insert newest frame at t = 0.
        for (int i = 0; i < displayBins; i++)
        {
            int bin = mappedBins[i];
            if (bin >= safeCount)
            {
                bin = safeCount - 1;
            }
            history[0, i] = AudioSpectrum.samples[bin];
        }
    }

    void RenderHistory()
    {
        float ampScale = scale * scale;

        for (int t = 0; t < historyLength; t++)
        {
            float age = historyLength > 1 ? t / (float)(historyLength - 1) : 0f;

            for (int i = 0; i < displayBins; i++)
            {
                float raw = history[t, i];
                float h = Mathf.Max(raw * ampScale, minHeight);
                Vector3 basePos = GetBasePosition(t, i);

                bars[t, i].transform.localScale = new Vector3(barWidth, h, barDepth);
                bars[t, i].transform.position = basePos + Vector3.up * (h * 0.5f);

                // Heat color from energy (blue -> red), fade with time age.
                float energy = Mathf.Clamp01(Mathf.Log10(1f + raw * 5000f));
                Color heat = Color.HSVToRGB(Mathf.Lerp(0.66f, 0.0f, energy), 1f, 1f);
                Color final = heat * Mathf.Lerp(1f, 0.22f, age);

                block.SetColor("_Color", final);
                block.SetColor("_BaseColor", final);
                barRenderers[t, i].SetPropertyBlock(block);
            }
        }
    }

    Vector3 GetBasePosition(int timeIndex, int freqIndex)
    {
        return origin + new Vector3(freqIndex * xStep, 0f, timeIndex * zStep);
    }
}

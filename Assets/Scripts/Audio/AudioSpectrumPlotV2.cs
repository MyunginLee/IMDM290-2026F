// Unity Audio Spectrum Plot Example (v2)
// IMDM Class Material
using UnityEngine;

public class AudioSpectrumPlotV2 : MonoBehaviour
{
    // Global visual controls for all three plots.
    public float scale = 10f;
    public int displayBins = 192;
    public float plotWidth = 16f;
    public float rowGap = 2.8f;
    public Vector3 origin = new Vector3(-8f, 4f, 0f);

    // Lowest frequency used for the log-scale mapping.
    public float minLogHz = 20f;

    // Number of representative x positions to annotate.
    public int labelCount = 8;

    // Negative value moves labels below the x-axis.
    public float labelYOffset = -0.45f;

    private const int RowCount = 3;
    private const int LinearRow = 0;
    private const int LogRow = 1;
    private const int MelRow = 2;

    // Row index order must match rowNames and rowColors.
    private readonly string[] rowNames = { "1) Linear", "2) Log", "3) Mel" };
    private readonly Color[] rowColors =
    {
        new Color(0.2f, 0.8f, 1.0f, 1f),
        new Color(0.2f, 1.0f, 0.45f, 1f),
        new Color(1.0f, 0.65f, 0.2f, 1f)
    };

    GameObject[,] bars;
    TextMesh[,] labels;
    int[,] mappedBins;
    float[,] mappedHz;
    int[] labelDisplayIndices;
    Font font;

    int fftSize;
    float hzPerBin;
    float nyquist;
    float xStep;

    void Start()
    {
        // FFT settings come from AudioSpectrum (source of spectrum data).
        fftSize = AudioSpectrum.FFTSIZE;

        // Nyquist is the maximum analyzable frequency in digital audio.
        nyquist = AudioSettings.outputSampleRate * 0.5f;
        hzPerBin = nyquist / fftSize;

        // Keep settings in a practical range for the classroom demo.
        displayBins = Mathf.Clamp(displayBins, 16, fftSize);
        labelCount = Mathf.Clamp(labelCount, 2, displayBins);
        xStep = plotWidth / (displayBins - 1);

        // Built-in font so students can run without extra assets.
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        bars = new GameObject[RowCount, displayBins];
        labels = new TextMesh[RowCount, labelCount];
        mappedBins = new int[RowCount, displayBins];
        mappedHz = new float[RowCount, displayBins];
        labelDisplayIndices = BuildRepresentativeIndices(labelCount, displayBins);

        // 1) Map display x positions to FFT bins for each scale.
        BuildMappings();

        // 2) Create visuals once, then only update transforms each frame.
        CreateBars();
        CreateRowTitles();
        CreateLabels();
    }

    void Update()
    {
        if (AudioSpectrum.samples == null) return;

        // Draw three views of the same spectrum: linear, log, mel.
        for (int row = 0; row < RowCount; row++)
        {
            for (int i = 0; i < displayBins; i++)
            {
                int fftBin = mappedBins[row, i];
                float amp = Mathf.Max(AudioSpectrum.samples[fftBin] * scale * scale, 0.001f);
                Vector3 basePos = GetBasePosition(row, i);
                bars[row, i].transform.localScale = new Vector3(0.08f, amp, 0.08f);
                bars[row, i].transform.position = basePos + Vector3.up * (amp * 0.5f);
            }
        }

        // Keep representative labels below each x-axis.
        for (int row = 0; row < RowCount; row++)
        {
            for (int i = 0; i < labelCount; i++)
            {
                int displayIndex = labelDisplayIndices[i];
                Vector3 basePos = GetBasePosition(row, displayIndex);
                labels[row, i].transform.position = basePos + Vector3.up * labelYOffset;
            }
        }
    }

    void BuildMappings()
    {
        // Build each row separately so students can read one scale at a time.
        BuildLinearMapping();
        BuildLogMapping();
        BuildMelMapping();
    }

    void BuildLinearMapping()
    {
        for (int i = 0; i < displayBins; i++)
        {
            // t is x position in [0, 1].
            float t = NormalizedX(i);
            float hz = t * nyquist;
            SetMapping(LinearRow, i, hz);
        }
    }

    void BuildLogMapping()
    {
        float safeMinHz = Mathf.Clamp(minLogHz, hzPerBin, nyquist);

        for (int i = 0; i < displayBins; i++)
        {
            float t = NormalizedX(i);
            float hz = 0f;

            if (t > 0f)
            {
                // Convert [0,1] -> log-frequency range.
                hz = Log01ToHz(t, safeMinHz, nyquist);
            }

            SetMapping(LogRow, i, hz);
        }
    }

    void BuildMelMapping()
    {
        float melMax = HzToMel(nyquist);

        for (int i = 0; i < displayBins; i++)
        {
            float t = NormalizedX(i);
            float hz = 0f;

            if (t > 0f)
            {
                // Interpolate in mel space, then convert mel -> Hz.
                float melValue = Mathf.Lerp(0f, melMax, t);
                hz = MelToHz(melValue);
            }

            SetMapping(MelRow, i, hz);
        }
    }

    float NormalizedX(int index)
    {
        return index / (float)(displayBins - 1);
    }

    float Log01ToHz(float t, float minHz, float maxHz)
    {
        float minLog10 = Mathf.Log10(minHz);
        float maxLog10 = Mathf.Log10(maxHz);
        float logValue = Mathf.Lerp(minLog10, maxLog10, t);
        return Mathf.Pow(10f, logValue);
    }

    void SetMapping(int row, int displayIndex, float hz)
    {
        // Convert continuous frequency (Hz) to a discrete FFT bin index.
        int fftBin = Mathf.Clamp(Mathf.RoundToInt(hz / hzPerBin), 0, fftSize - 1);
        mappedBins[row, displayIndex] = fftBin;

        // Store quantized frequency (actual bin center used in drawing).
        mappedHz[row, displayIndex] = fftBin * hzPerBin;
    }

    void CreateBars()
    {
        // Row 0: Linear, Row 1: Log, Row 2: Mel.
        for (int row = 0; row < RowCount; row++)
        {
            for (int i = 0; i < displayBins; i++)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"V2_{row}_{i}";
                bar.transform.SetParent(transform);
                bar.transform.position = GetBasePosition(row, i);
                bar.transform.localScale = new Vector3(0.08f, 0.001f, 0.08f);
                bar.GetComponent<Renderer>().material.color = rowColors[row];
                bars[row, i] = bar;
            }
        }
    }

    void CreateRowTitles()
    {
        for (int row = 0; row < RowCount; row++)
        {
            GameObject go = new GameObject($"Title_{row}");
            go.transform.SetParent(transform);
            go.transform.position = GetBasePosition(row, 0) + new Vector3(-1.1f, 0.1f, 0f);

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = rowNames[row];
            tm.fontSize = 100;
            tm.characterSize = 0.03f;
            tm.anchor = TextAnchor.MiddleRight;
            tm.alignment = TextAlignment.Right;
            tm.color = rowColors[row];
            tm.GetComponent<MeshRenderer>().material = font.material;
        }
    }

    void CreateLabels()
    {
        // Labels show both frequency and FFT bin index at representative points.
        for (int row = 0; row < RowCount; row++)
        {
            for (int i = 0; i < labelCount; i++)
            {
                int displayIndex = labelDisplayIndices[i];
                int fftBin = mappedBins[row, displayIndex];
                float hz = mappedHz[row, displayIndex];

                GameObject go = new GameObject($"Label_{row}_{i}");
                go.transform.SetParent(transform);
                go.transform.position = GetBasePosition(row, displayIndex) + Vector3.up * labelYOffset;

                TextMesh tm = go.AddComponent<TextMesh>();
                tm.font = font;
                tm.text = $"{hz:0} Hz\nFFT[{fftBin}]";
                tm.fontSize = 70;
                tm.characterSize = 0.025f;
                tm.anchor = TextAnchor.LowerCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = Color.white;
                tm.GetComponent<MeshRenderer>().material = font.material;

                labels[row, i] = tm;
            }
        }
    }

    Vector3 GetBasePosition(int row, int displayIndex)
    {
        // All rows share the same x spacing; each row is shifted downward.
        return origin + new Vector3(displayIndex * xStep, -row * rowGap, 0f);
    }

    static int[] BuildRepresentativeIndices(int count, int size)
    {
        // Evenly distribute label positions across the displayed bins.
        int[] indices = new int[count];
        for (int i = 0; i < count; i++)
        {
            indices[i] = Mathf.RoundToInt(i * (size - 1) / (float)(count - 1));
        }
        return indices;
    }

    static float HzToMel(float hz)
    {
        // Standard mel conversion (Stevens, Volkmann, Newman approximation).
        return 2595f * Mathf.Log10(1f + hz / 700f);
    }

    static float MelToHz(float mel)
    {
        // Inverse of HzToMel.
        return 700f * (Mathf.Pow(10f, mel / 2595f) - 1f);
    }
}

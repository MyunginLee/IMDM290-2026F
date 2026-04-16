// Unity Audio Spectrum data analysis
// IMDM Course Material 
// Author: Myungin Lee
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent (typeof(AudioSource))]

public class AudioSpectrum : MonoBehaviour
{
    AudioSource source;
    public static int FFTSIZE = 4096; // https://en.wikipedia.org/wiki/Fast_Fourier_transform
    public static float[] samples = new float[FFTSIZE];
    public static float audioAmp = 0f;
    public static float bassAmp = 0f;
    public static float midAmp = 0f;
    public static float trebleAmp = 0f;

    void Start()
    {
        source = GetComponent<AudioSource>();

        if (source != null && source.clip != null && source.playOnAwake && !source.isPlaying)
        {
            source.Play();
        }
    }
    void Update()
    {
        if (source == null)
        {
            return;
        }

        // The source (time domain) transforms into samples in frequency domain 
        source.GetSpectrumData(samples, 0, FFTWindow.Hanning);
        // Empty first, and pull down the value.
        audioAmp = 0f;
        for (int i = 0; i < FFTSIZE; i++)
        {
            audioAmp += samples[i];
        }
        audioAmp /= FFTSIZE;

        // Approximate musical bands in FFT bin space for 44.1kHz / 4096 FFT.
        bassAmp = Mathf.Lerp(bassAmp, Mathf.Clamp01(AverageRange(2, 24) * 550f), Time.deltaTime * 14f);
        midAmp = Mathf.Lerp(midAmp, Mathf.Clamp01(AverageRange(25, 160) * 320f), Time.deltaTime * 12f);
        trebleAmp = Mathf.Lerp(trebleAmp, Mathf.Clamp01(AverageRange(161, 700) * 500f), Time.deltaTime * 10f);
    }

    private static float AverageRange(int startInclusive, int endInclusive)
    {
        startInclusive = Mathf.Clamp(startInclusive, 0, FFTSIZE - 1);
        endInclusive = Mathf.Clamp(endInclusive, startInclusive, FFTSIZE - 1);

        var total = 0f;
        var count = 0;
        for (var i = startInclusive; i <= endInclusive; i++)
        {
            total += samples[i];
            count++;
        }

        return count > 0 ? total / count : 0f;
    }
}

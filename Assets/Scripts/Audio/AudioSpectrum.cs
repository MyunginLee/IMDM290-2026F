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
    float percussion, brass;
    void Start()
    {
        source = GetComponent<AudioSource>();       
    }
    void Update()
    {
        // The source (time domain) transforms into samples in frequency domain 
        GetComponent<AudioSource>().GetSpectrumData(samples, 0, FFTWindow.Hanning);
        // Empty first, and pull down the value.
        audioAmp = 0f;
        for (int i = 0; i < FFTSIZE; i++)
        {
            audioAmp += samples[i];
        }
        percussion = 0f;
        brass = 0f;

        // 2~10 : percussion
        for (int i = 2; i < 10; i++)
        {
            percussion += samples[i];
        }

        percussion = percussion / 8f;

        // 25~50 : brass
        for (int i = 25; i < 50; i++)
        {
            brass += samples[i];
        }
        brass = brass / 25f;
    }
}

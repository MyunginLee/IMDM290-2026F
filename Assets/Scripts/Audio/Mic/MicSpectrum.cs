// Unity Microphone Spectrum data analysis
// IMDM Course Material
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MicSpectrum : MonoBehaviour
{
    public static int FFTSIZE = 4096;
    public static float[] samples = new float[FFTSIZE];
    public static float audioAmp = 0f;

    public string selectedDevice = "";
    public FFTWindow fftWindow = FFTWindow.Hanning;

    AudioSource source;
    AudioClip micClip;

    void Start()
    {
        source = GetComponent<AudioSource>();

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone detected.");
            enabled = false;
            return;
        }

        if (string.IsNullOrEmpty(selectedDevice))
        {
            selectedDevice = Microphone.devices[0];
        }

        micClip = Microphone.Start(selectedDevice, true, 1, AudioSettings.outputSampleRate);
        source.clip = micClip;
        source.loop = true;
    }

    void Update()
    {
        if (Microphone.GetPosition(selectedDevice) <= 0)
        {
            return;
        }

        if (!source.isPlaying)
        {
            source.Play();
        }

        source.GetSpectrumData(samples, 0, fftWindow);

        audioAmp = 0f;
        for (int i = 0; i < FFTSIZE; i++)
        {
            audioAmp += samples[i];
        }

        // Mirror data so existing AudioSpectrumPlot scripts can be reused.
        if (AudioSpectrum.samples == null || AudioSpectrum.samples.Length != FFTSIZE)
        {
            AudioSpectrum.samples = new float[FFTSIZE];
        }

        for (int i = 0; i < FFTSIZE; i++)
        {
            AudioSpectrum.samples[i] = samples[i];
        }
        AudioSpectrum.audioAmp = audioAmp;
    }

    void OnDisable()
    {
        StopMicrophone();
    }

    void OnDestroy()
    {
        StopMicrophone();
    }

    void StopMicrophone()
    {
        if (!string.IsNullOrEmpty(selectedDevice) && Microphone.IsRecording(selectedDevice))
        {
            Microphone.End(selectedDevice);
        }
    }
}

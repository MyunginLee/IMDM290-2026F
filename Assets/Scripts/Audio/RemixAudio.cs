using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RemixAudio : MonoBehaviour
{
    [SerializeField] private AudioClip clip;

    [Range(-3f, 3f)]
    public float playbackRate = 1f;

    public bool playOnStart = true;
    public bool loop = true;

    private AudioSource audioSource;
    private float[] samples;
    private int channels;
    private int frameCount;
    private double playhead;
    private bool isPlaying;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        clip = audioSource.clip != null ? audioSource.clip : clip;
        if (clip == null)
        {
            Debug.LogWarning($"{nameof(RemixAudio)} on {name} has no source clip assigned.");
            return;
        }

        channels = clip.channels;
        frameCount = clip.samples;
        samples = new float[frameCount * channels];
        clip.GetData(samples, 0);
        audioSource.clip = clip;
        playhead = 0d;

        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        if (clip == null || audioSource == null)
        {
            return;
        }

        if (audioSource.clip == null)
        {
            audioSource.clip = clip;
        }

        isPlaying = true;

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    public void StopAudio()
    {
        isPlaying = false;
        audioSource.Stop();
    }

    void OnAudioFilterRead(float[] data, int outChannels)
    {
        if (!isPlaying || clip == null || samples == null || outChannels <= 0)
        {
            System.Array.Clear(data, 0, data.Length);
            return;
        }

        if (Mathf.Abs(playbackRate) < 0.001f)
        {
            System.Array.Clear(data, 0, data.Length);
            return;
        }

        int framesToWrite = data.Length / outChannels;

        for (int frame = 0; frame < framesToWrite; frame++)
        {
            int frameIndex = Mathf.FloorToInt((float)playhead);

            if (frameIndex < 0 || frameIndex >= frameCount)
            {
                if (loop)
                {
                    if (frameIndex < 0) playhead = frameCount - 1;
                    if (frameIndex >= frameCount) playhead = 0;
                    frameIndex = Mathf.FloorToInt((float)playhead);
                }
                else
                {
                    isPlaying = false;
                    System.Array.Clear(data, frame * outChannels, data.Length - frame * outChannels);
                    return;
                }
            }

            WriteOutputFrame(data, frame, outChannels, frameIndex);
            playhead += playbackRate;
        }
    }

    private void WriteOutputFrame(float[] data, int frame, int outChannels, int frameIndex)
    {
        var nextFrameIndex = frameIndex + (playbackRate >= 0f ? 1 : -1);
        nextFrameIndex = WrapFrameIndex(nextFrameIndex);
        var interpolation = Mathf.Abs((float)(playhead - frameIndex));

        if (outChannels == 1)
        {
            data[frame] = ReadMixedSample(frameIndex, nextFrameIndex, interpolation);
            return;
        }

        for (var outChannel = 0; outChannel < outChannels; outChannel++)
        {
            var sourceChannel = channels == 1 ? 0 : Mathf.Min(outChannel, channels - 1);
            var currentSample = samples[frameIndex * channels + sourceChannel];
            var nextSample = samples[nextFrameIndex * channels + sourceChannel];
            data[frame * outChannels + outChannel] = Mathf.Lerp(currentSample, nextSample, interpolation);
        }
    }

    private float ReadMixedSample(int frameIndex, int nextFrameIndex, float interpolation)
    {
        var currentSample = 0f;
        var nextSample = 0f;
        for (var ch = 0; ch < channels; ch++)
        {
            currentSample += samples[frameIndex * channels + ch];
            nextSample += samples[nextFrameIndex * channels + ch];
        }

        currentSample /= channels;
        nextSample /= channels;
        return Mathf.Lerp(currentSample, nextSample, interpolation);
    }

    private int WrapFrameIndex(int frameIndex)
    {
        if (frameIndex < 0)
        {
            return loop ? frameCount - 1 : 0;
        }

        if (frameIndex >= frameCount)
        {
            return loop ? 0 : frameCount - 1;
        }

        return frameIndex;
    }
}

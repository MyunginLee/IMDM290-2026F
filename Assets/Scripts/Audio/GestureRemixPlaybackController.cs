using UnityEngine;

[DisallowMultipleComponent]
public class GestureRemixPlaybackController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private MediaPipeBodyTracker tracker;
    [SerializeField] private RemixAudio remixAudio;

    [Header("Left Hand Y -> Playback Rate")]
    [SerializeField, Range(-3f, 3f)] private float minPlaybackRate = -3f;
    [SerializeField, Range(-3f, 3f)] private float maxPlaybackRate = 3f;
    [SerializeField, Range(0f, 1f)] private float minHandY = 0f;
    [SerializeField, Range(0f, 1f)] private float maxHandY = 1f;
    [SerializeField] private bool invertHandY = true;
    [SerializeField, Range(-3f, 3f)] private float untrackedPlaybackRate = 1f;

    [Header("Smoothing")]
    [SerializeField, Range(1f, 30f)] private float playbackLerpSpeed = 10f;
    [SerializeField] private float currentPlaybackRate;

    private void Awake()
    {
        ResolveReferences();

        if (remixAudio != null)
        {
            currentPlaybackRate = remixAudio.playbackRate;
        }
    }

    private void Update()
    {
        ResolveReferences();
        if (tracker == null || remixAudio == null)
        {
            return;
        }

        var targetPlaybackRate = untrackedPlaybackRate;
        if (tracker.LeftHandTracked)
        {
            var leftHandY = Mathf.Clamp01(tracker.LeftHandPosition.y);
            var remappedHandY = invertHandY ? 1f - leftHandY : leftHandY;
            var normalizedY = Mathf.InverseLerp(minHandY, maxHandY, remappedHandY);
            targetPlaybackRate = Mathf.Lerp(minPlaybackRate, maxPlaybackRate, normalizedY);
        }

        currentPlaybackRate = Mathf.Lerp(
            remixAudio.playbackRate,
            targetPlaybackRate,
            Time.deltaTime * playbackLerpSpeed);
        remixAudio.playbackRate = currentPlaybackRate;
    }

    private void ResolveReferences()
    {
        if (tracker == null)
        {
            tracker = FindObjectOfType<MediaPipeBodyTracker>();
        }

        if (remixAudio == null)
        {
            remixAudio = FindObjectOfType<RemixAudio>();
        }
    }
}

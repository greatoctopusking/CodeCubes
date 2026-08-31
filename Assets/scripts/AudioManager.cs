using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SoundId
{
    Boot,
    UiClick,
    ProgramStart,
    RobotBoot,
    ValidationFail,
    LevelEnter,
    BlockConnect,
    BlockDisconnect,
    RobotMove,
    RobotTurn,
    StarCollect,
    LevelComplete,
    LevelFail
}

[DefaultExecutionOrder(-200)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Serializable]
    public class SoundBinding
    {
        public SoundId id;
        public AudioClip clip;
        [Tooltip("Play in world space (robot, stars, blocks). Leave off for UI.")]
        public bool spatial;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Header("One-shots")]
    [Tooltip("Leave a clip empty to skip that event. Spatial sounds play at the robot, star, or block.")]
    public SoundBinding[] sounds;

    [Header("Ambience")]
    public AudioClip garageAmbience;
    [Range(0f, 1f)] public float ambienceVolume = 0.2f;
    public float ambienceFadeSeconds = 0.8f;

    [Header("Star collect")]
    [Tooltip("Pitch rises with each correctly collected star.")]
    public float starPitchStep = 0.08f;
    public int starPitchMaxSteps = 8;

    [Header("3D")]
    public float spatialMinDistance = 1f;
    public float spatialMaxDistance = 18f;

    private readonly Dictionary<SoundId, SoundBinding> lookup = new Dictionary<SoundId, SoundBinding>();
    private AudioSource uiSource;
    private AudioSource spatialSource;
    private AudioSource ambienceSource;
    private bool bootPlayed;
    private Coroutine ambienceFade;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureDefaultEntries();
        RebuildLookup();
        uiSource = CreateSource("UI", spatialBlend: 0f);
        spatialSource = CreateSource("Spatial", spatialBlend: 1f);
        ambienceSource = CreateSource("Ambience", spatialBlend: 0f);
        ambienceSource.loop = true;
        spatialSource.minDistance = spatialMinDistance;
        spatialSource.maxDistance = spatialMaxDistance;
        spatialSource.rolloffMode = AudioRolloffMode.Linear;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    private void Reset() => EnsureDefaultEntries();
    private void OnValidate() => EnsureDefaultEntries();
#endif

    public void Play(SoundId id)
    {
        PlayInternal(id, null, 1f);
    }

    public void Play(SoundId id, Vector3 worldPosition)
    {
        PlayInternal(id, worldPosition, 1f);
    }

    public void PlayStarCollect(int orderIndex, Vector3 worldPosition)
    {
        float pitch = 1f + Mathf.Min(Mathf.Max(orderIndex, 0), starPitchMaxSteps) * starPitchStep;
        PlayInternal(SoundId.StarCollect, worldPosition, pitch);
    }

    public void PlayBootAndAmbience()
    {
        if (!bootPlayed)
        {
            bootPlayed = true;
            StartCoroutine(PlayBootDelayed());
        }

        StartAmbience();
    }

    private IEnumerator PlayBootDelayed()
    {
        yield return new WaitForSeconds(2f);
        Play(SoundId.Boot);
    }

    public void StartAmbience()
    {
        if (garageAmbience == null || ambienceSource == null)
            return;

        if (ambienceSource.clip != garageAmbience)
            ambienceSource.clip = garageAmbience;

        if (ambienceFade != null)
            StopCoroutine(ambienceFade);

        ambienceFade = StartCoroutine(FadeAmbience(ambienceVolume, play: true));
    }

    public void StopAmbience()
    {
        if (ambienceSource == null)
            return;

        if (ambienceFade != null)
            StopCoroutine(ambienceFade);

        ambienceFade = StartCoroutine(FadeAmbience(0f, play: false));
    }

    private void PlayInternal(SoundId id, Vector3? worldPosition, float pitch)
    {
        if (!lookup.TryGetValue(id, out var binding) || binding.clip == null)
            return;

        bool spatial = binding.spatial && worldPosition.HasValue;
        var source = spatial ? spatialSource : uiSource;
        if (source == null)
            return;

        if (spatial)
            source.transform.position = worldPosition.Value;

        source.pitch = pitch;
        source.PlayOneShot(binding.clip, binding.volume);
        source.pitch = 1f;
    }

    private AudioSource CreateSource(string name, float spatialBlend)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = spatialBlend;
        source.dopplerLevel = 0f;
        return source;
    }

    private IEnumerator FadeAmbience(float target, bool play)
    {
        if (play && !ambienceSource.isPlaying)
        {
            ambienceSource.volume = 0f;
            ambienceSource.Play();
        }

        float start = ambienceSource.volume;
        float duration = Mathf.Max(0.01f, ambienceFadeSeconds);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            ambienceSource.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        ambienceSource.volume = target;
        if (!play)
            ambienceSource.Stop();

        ambienceFade = null;
    }

    private void RebuildLookup()
    {
        lookup.Clear();
        if (sounds == null)
            return;

        foreach (var binding in sounds)
        {
            if (binding == null)
                continue;
            lookup[binding.id] = binding;
        }
    }

    private void EnsureDefaultEntries()
    {
        var ids = (SoundId[])Enum.GetValues(typeof(SoundId));
        if (sounds == null)
            sounds = Array.Empty<SoundBinding>();

        var existing = new HashSet<SoundId>();
        foreach (var binding in sounds)
        {
            if (binding != null)
                existing.Add(binding.id);
        }

        if (existing.Count == ids.Length)
            return;

        var list = new List<SoundBinding>(sounds);
        foreach (var id in ids)
        {
            if (existing.Contains(id))
                continue;

            list.Add(new SoundBinding
            {
                id = id,
                volume = DefaultVolume(id),
                spatial = IsSpatialByDefault(id)
            });
        }

        sounds = list.ToArray();
    }

    private static bool IsSpatialByDefault(SoundId id)
    {
        return id == SoundId.RobotBoot
            || id == SoundId.RobotMove
            || id == SoundId.RobotTurn
            || id == SoundId.StarCollect
            || id == SoundId.BlockConnect
            || id == SoundId.BlockDisconnect;
    }

    private static float DefaultVolume(SoundId id)
    {
        return id switch
        {
            SoundId.Boot => 0.7f,
            SoundId.UiClick => 0.5f,
            SoundId.ValidationFail => 0.7f,
            SoundId.RobotMove => 0.45f,
            SoundId.RobotTurn => 0.45f,
            _ => 0.8f
        };
    }
}

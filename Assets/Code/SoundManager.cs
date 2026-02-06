using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip musicClip;
    [Range(0f, 1f)][SerializeField] private float musicVolume = 1f;
    [SerializeField] private bool playMusicOnStart = true;
    [SerializeField] private bool musicLoop = true;

    [Header("SFX Groups")]
    [SerializeField] private List<SfxGroup> sfxGroups = new();

    [Header("SFX Pool")]
    [Min(1)][SerializeField] private int sfxPoolSize = 10;
    [SerializeField] private bool stealSfxSourceIfAllBusy = true;

    [Header("3D SFX Defaults")]
    [SerializeField] private AudioRolloffMode sfx3DRolloff = AudioRolloffMode.Logarithmic;
    [Min(0f)][SerializeField] private float sfx3DMinDistance = 1f;
    [Min(0.01f)][SerializeField] private float sfx3DMaxDistance = 25f;

    [Header("Notes")]
    [Tooltip("Freeform notes shown in the Inspector (not used at runtime).")]
    [TextArea(2, 8)]
    [SerializeField] private string body;

    [Serializable]
    private class SfxGroup
    {
        public string id;
        public bool randomize = true;
        public bool loop;
        [Range(0f, 1f)] public float volume = 1f;
        public Vector2 pitchRange = Vector2.one; // x=min, y=max
        public List<AudioClip> clips = new();

        [NonSerialized] public int nextIndex;
    }

    private readonly Dictionary<string, SfxGroup> groupsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> loopingSfxSourceById = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> loopingSfxIdBySourceIndex = new();
    private AudioSource[] sfxSources = Array.Empty<AudioSource>();
    private float[] sfxEndTimes = Array.Empty<float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureMusicSource();
        BuildSfxPool();
        RebuildGroupLookup();
    }

    private void Start()
    {
        if (playMusicOnStart)
        {
            PlayMusic();
        }
    }

    private void OnValidate()
    {
        if (sfx3DMaxDistance < sfx3DMinDistance)
        {
            sfx3DMaxDistance = sfx3DMinDistance;
        }
    }

    private void EnsureMusicSource()
    {
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }
        }

        musicSource.playOnAwake = false;
        musicSource.loop = musicLoop;
        musicSource.spatialBlend = 0f; // always 2D
        musicSource.volume = musicVolume;
    }

    private void BuildSfxPool()
    {
        if (sfxSources.Length == sfxPoolSize)
        {
            return;
        }

        loopingSfxSourceById.Clear();
        loopingSfxIdBySourceIndex.Clear();

        // Wipe any previous pool children we created (safe if you change pool size in play mode).
        var existingPool = transform.Find("SFX_Pool");
        if (existingPool != null)
        {
            Destroy(existingPool.gameObject);
        }

        var poolRoot = new GameObject("SFX_Pool");
        poolRoot.transform.SetParent(transform, false);

        sfxSources = new AudioSource[sfxPoolSize];
        sfxEndTimes = new float[sfxPoolSize];

        for (var i = 0; i < sfxPoolSize; i++)
        {
            var child = new GameObject($"SFX_{i:00}");
            child.transform.SetParent(poolRoot.transform, false);

            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.dopplerLevel = 0f;

            sfxSources[i] = src;
            sfxEndTimes[i] = 0f;
        }
    }

    private void RebuildGroupLookup()
    {
        groupsById.Clear();

        if (sfxGroups == null)
        {
            return;
        }

        for (var i = 0; i < sfxGroups.Count; i++)
        {
            var group = sfxGroups[i];
            if (group == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(group.id))
            {
                Debug.LogWarning($"SoundManager: SFX group at index {i} has an empty id (ignored).", this);
                continue;
            }

            if (groupsById.ContainsKey(group.id))
            {
                Debug.LogWarning($"SoundManager: duplicate SFX group id '{group.id}' (keeping first, ignoring later).", this);
                continue;
            }

            if (group.clips == null || group.clips.Count == 0)
            {
                Debug.LogWarning($"SoundManager: SFX group '{group.id}' has no clips.", this);
            }

            groupsById.Add(group.id, group);
        }
    }

    public void PlayMusic()
    {
        if (musicClip == null)
        {
            Debug.LogWarning("SoundManager: no musicClip assigned.", this);
            return;
        }

        PlayMusic(musicClip);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("SoundManager: PlayMusic called with null clip.", this);
            return;
        }

        EnsureMusicSource();

        musicSource.loop = musicLoop;
        musicSource.volume = musicVolume;

        if (musicSource.clip != clip)
        {
            musicSource.clip = clip;
        }

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public void SetMusicVolume(float volume01)
    {
        musicVolume = Mathf.Clamp01(volume01);
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
    }

    public void PlaySfx2D(string id, float volumeScale = 1f)
    {
        PlaySfxInternal(id, transform.position, is3D: false, volumeScale);
    }

    public void PlaySfx3D(string id, Vector3 position, float volumeScale = 1f)
    {
        PlaySfxInternal(id, position, is3D: true, volumeScale);
    }

    public void StopSfx(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!loopingSfxSourceById.TryGetValue(id, out var idx))
        {
            return;
        }

        if (sfxSources != null && idx >= 0 && idx < sfxSources.Length)
        {
            var src = sfxSources[idx];
            if (src != null)
            {
                src.Stop();
                src.loop = false;
                src.clip = null;
            }
        }

        loopingSfxSourceById.Remove(id);
        loopingSfxIdBySourceIndex.Remove(idx);
    }

    public void StopAllSfxLoops()
    {
        if (loopingSfxSourceById.Count == 0)
        {
            return;
        }

        // Copy keys because StopSfx mutates the dictionary.
        var ids = new List<string>(loopingSfxSourceById.Keys);
        for (var i = 0; i < ids.Count; i++)
        {
            StopSfx(ids[i]);
        }
    }

    private void PlaySfxInternal(string id, Vector3 position, bool is3D, float volumeScale)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning("SoundManager: PlaySfx called with empty id.", this);
            return;
        }

        if (groupsById.Count == 0)
        {
            // If someone added groups after Awake (in editor), lazily rebuild.
            RebuildGroupLookup();
        }

        if (!groupsById.TryGetValue(id, out var group) || group == null)
        {
            Debug.LogWarning($"SoundManager: unknown SFX id '{id}'.", this);
            return;
        }

        var clip = PickClip(group);
        if (clip == null)
        {
            Debug.LogWarning($"SoundManager: SFX group '{id}' has no valid clip to play.", this);
            return;
        }

        if (group.loop)
        {
            PlayLoopingSfx(id, group, clip, position, is3D, volumeScale);
            return;
        }

        if (!TryGetSfxSource(out var srcIndex, out var src))
        {
            return;
        }

        var pitch = PickPitch(group.pitchRange);
        src.transform.position = position;
        src.pitch = pitch;
        src.volume = Mathf.Clamp01(group.volume) * Mathf.Clamp01(volumeScale);
        src.spatialBlend = is3D ? 1f : 0f;

        if (is3D)
        {
            src.rolloffMode = sfx3DRolloff;
            src.minDistance = sfx3DMinDistance;
            src.maxDistance = sfx3DMaxDistance;
        }

        src.PlayOneShot(clip);
        sfxEndTimes[srcIndex] = Time.unscaledTime + clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
    }

    private void PlayLoopingSfx(string id, SfxGroup group, AudioClip clip, Vector3 position, bool is3D, float volumeScale)
    {
        if (loopingSfxSourceById.TryGetValue(id, out var existingIndex))
        {
            if (sfxSources != null && existingIndex >= 0 && existingIndex < sfxSources.Length)
            {
                var existing = sfxSources[existingIndex];
                if (existing != null)
                {
                    ApplySfxSourceSettings(existing, group, position, is3D, volumeScale);

                    // Ensure the correct clip is playing (in case you changed clips in the inspector during play mode).
                    if (existing.clip != clip)
                    {
                        existing.clip = clip;
                    }

                    existing.loop = true;
                    if (!existing.isPlaying)
                    {
                        existing.Play();
                    }
                    return;
                }
            }

            // Stale entry (pool rebuilt or source destroyed)
            loopingSfxSourceById.Remove(id);
            loopingSfxIdBySourceIndex.Remove(existingIndex);
        }

        if (!TryGetSfxSource(out var srcIndex, out var src))
        {
            return;
        }

        src.Stop();
        src.clip = clip;
        src.loop = true;
        ApplySfxSourceSettings(src, group, position, is3D, volumeScale);
        src.Play();

        loopingSfxSourceById[id] = srcIndex;
        loopingSfxIdBySourceIndex[srcIndex] = id;
    }

    private void ApplySfxSourceSettings(AudioSource src, SfxGroup group, Vector3 position, bool is3D, float volumeScale)
    {
        var pitch = PickPitch(group.pitchRange);
        src.transform.position = position;
        src.pitch = pitch;
        src.volume = Mathf.Clamp01(group.volume) * Mathf.Clamp01(volumeScale);
        src.spatialBlend = is3D ? 1f : 0f;

        if (is3D)
        {
            src.rolloffMode = sfx3DRolloff;
            src.minDistance = sfx3DMinDistance;
            src.maxDistance = sfx3DMaxDistance;
        }
    }

    private AudioClip PickClip(SfxGroup group)
    {
        if (group.clips == null || group.clips.Count == 0)
        {
            return null;
        }

        // Remove any null clips on the fly (helps avoid spammy null ref issues).
        for (var i = group.clips.Count - 1; i >= 0; i--)
        {
            if (group.clips[i] == null)
            {
                group.clips.RemoveAt(i);
            }
        }

        if (group.clips.Count == 0)
        {
            return null;
        }

        if (group.randomize)
        {
            return group.clips[UnityEngine.Random.Range(0, group.clips.Count)];
        }

        var idx = Mathf.Abs(group.nextIndex) % group.clips.Count;
        group.nextIndex = (idx + 1) % group.clips.Count;
        return group.clips[idx];
    }

    private static float PickPitch(Vector2 pitchRange)
    {
        var min = pitchRange.x;
        var max = pitchRange.y;
        if (max < min)
        {
            (min, max) = (max, min);
        }

        // Treat 0 as "use default 1" to avoid accidental silence/invalid pitch.
        if (Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 0f))
        {
            return 1f;
        }

        if (Mathf.Approximately(min, max))
        {
            return Mathf.Clamp(min, -3f, 3f);
        }

        return Mathf.Clamp(UnityEngine.Random.Range(min, max), -3f, 3f);
    }

    private bool TryGetSfxSource(out int index, out AudioSource source)
    {
        if (sfxSources == null || sfxSources.Length != sfxPoolSize)
        {
            BuildSfxPool();
        }

        // Prefer a free source.
        for (var i = 0; i < sfxSources.Length; i++)
        {
            var src = sfxSources[i];
            if (src != null && !src.isPlaying)
            {
                index = i;
                source = src;
                return true;
            }
        }

        if (!stealSfxSourceIfAllBusy)
        {
            index = -1;
            source = null;
            return false;
        }

        // Steal the one that should finish the soonest.
        var bestIdx = 0;
        var bestEndTime = float.MaxValue;
        for (var i = 0; i < sfxSources.Length; i++)
        {
            if (sfxEndTimes[i] < bestEndTime)
            {
                bestEndTime = sfxEndTimes[i];
                bestIdx = i;
            }
        }

        index = bestIdx;
        source = sfxSources[bestIdx];
        if (source != null)
        {
            if (loopingSfxIdBySourceIndex.TryGetValue(bestIdx, out var loopId))
            {
                loopingSfxIdBySourceIndex.Remove(bestIdx);
                loopingSfxSourceById.Remove(loopId);
            }
            source.Stop();
            return true;
        }

        return false;
    }
}



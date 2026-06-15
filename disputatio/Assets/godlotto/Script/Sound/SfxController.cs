using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SfxController : SingletonMonoBehaviour<SfxController>
{
    [System.Obsolete("Use Instance instead.")]
    public static SfxController instance => Instance;

    protected override bool PersistAcrossScenes => true;

    [Header("Settings")]
    [SerializeField] private AudioSource sfxAudioSource;

    [Header("SFX Playlist")]
    public AudioClip[] sfxList;

    [Header("Footstep Settings")]
    public float delayBetweenSteps = 0.3f;

    private readonly Dictionary<int, List<AudioSource>> activeSfxSourcesByIndex = new Dictionary<int, List<AudioSource>>();

    protected override void Awake()
    {
        ResolveAudioSource();
        base.Awake();
    }

    private void ResolveAudioSource()
    {
        if (sfxAudioSource == null)
            sfxAudioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    public void PlaySFX(int index)
    {
        if (sfxList == null || index < 0 || index >= sfxList.Length) return;
        if (sfxList[index] == null) return;

        PlayTrackedSFX(index, loop: false);
    }

    public void PlayLoopingSFX(int index)
    {
        if (sfxList == null || index < 0 || index >= sfxList.Length) return;
        if (sfxList[index] == null) return;

        PlayTrackedSFX(index, loop: true);
    }

    public void StopSFX(int index)
    {
        if (!activeSfxSourcesByIndex.TryGetValue(index, out List<AudioSource> sources))
            return;

        for (int i = sources.Count - 1; i >= 0; i--)
        {
            AudioSource source = sources[i];
            if (source == null)
                continue;

            source.Stop();
            Destroy(source);
        }

        sources.Clear();
        activeSfxSourcesByIndex.Remove(index);
    }

    public void StopAllSFX()
    {
        var indices = new List<int>(activeSfxSourcesByIndex.Keys);
        foreach (int index in indices)
            StopSFX(index);
    }

    public void PlayFootstep(int index)
    {
        if (sfxList == null || index < 0 || index >= sfxList.Length) return;
        if (sfxList[index] == null) return;

        if (sfxAudioSource != null)
            StartCoroutine(FootstepCoroutine(index));
    }

    private void PlayTrackedSFX(int index, bool loop)
    {
        if (sfxAudioSource == null)
            return;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = sfxList[index];
        source.outputAudioMixerGroup = sfxAudioSource.outputAudioMixerGroup;
        source.volume = sfxAudioSource.volume;
        source.pitch = sfxAudioSource.pitch;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = sfxAudioSource.spatialBlend;
        source.panStereo = sfxAudioSource.panStereo;
        source.priority = sfxAudioSource.priority;
        source.Play();

        if (!activeSfxSourcesByIndex.TryGetValue(index, out List<AudioSource> sources))
        {
            sources = new List<AudioSource>();
            activeSfxSourcesByIndex[index] = sources;
        }

        sources.Add(source);

        if (!loop)
            StartCoroutine(ReleaseSfxSourceAfterPlayback(index, source));
    }

    private IEnumerator ReleaseSfxSourceAfterPlayback(int index, AudioSource source)
    {
        yield return new WaitWhile(() => source != null && source.isPlaying);

        if (source != null)
        {
            if (activeSfxSourcesByIndex.TryGetValue(index, out List<AudioSource> sources))
            {
                sources.Remove(source);
                if (sources.Count == 0)
                    activeSfxSourcesByIndex.Remove(index);
            }

            Destroy(source);
        }
    }

    private IEnumerator FootstepCoroutine(int index)
    {
        for (int i = 0; i < 4; i++)
        {
            PlaySFX(index);
            yield return new WaitForSeconds(delayBetweenSteps);
        }
    }
}

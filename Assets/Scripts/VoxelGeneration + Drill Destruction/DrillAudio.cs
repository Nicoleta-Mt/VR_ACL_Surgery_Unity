using UnityEngine;

/// <summary>
/// DrillAudio — handles all drill sound states.
/// Attach this to the drill GameObject alongside (or near) DrillBit.
///
/// Three states:
///   OFF      — drill not active              → silence
///   SPINNING — drill active, no contact      → idle spin hum
///   DRILLING — drill active + touching model → heavy drilling sound
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class DrillAudio : MonoBehaviour
{
    public enum DrillSoundState { Off, Spinning, Drilling }

    [Header("Audio Clips")]
    [Tooltip("Looping hum played while the drill spins but isn't touching anything.")]
    public AudioClip spinClip;

    [Tooltip("Looping sound played while actively chewing through the voxel model.")]
    public AudioClip drillingClip;

    private AudioSource _source;
    private DrillSoundState _currentState = DrillSoundState.Off;

    void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.loop = true;
        _source.playOnAwake = false;
        _source.volume = 1f;
    }

    /// <summary>
    /// Called by DrillBit whenever drilling or contact state changes.
    /// </summary>
    public void SetContactState(bool isDrilling, bool isContacting)
    {
        DrillSoundState desired;

        if (!isDrilling)
            desired = DrillSoundState.Off;
        else if (isContacting)
            desired = DrillSoundState.Drilling;
        else
            desired = DrillSoundState.Spinning;

        if (desired == _currentState) return;

        _currentState = desired;

        switch (desired)
        {
            case DrillSoundState.Off:
                _source.Stop();
                break;

            case DrillSoundState.Spinning:
                PlayClip(spinClip);
                break;

            case DrillSoundState.Drilling:
                PlayClip(drillingClip);
                break;
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        if (_source.clip == clip && _source.isPlaying) return;

        _source.clip = clip;
        _source.volume = 1f;
        _source.Play();
    }
}
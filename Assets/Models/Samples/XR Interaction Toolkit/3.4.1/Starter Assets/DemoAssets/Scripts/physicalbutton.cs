using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PhysicalButton : MonoBehaviour
{
    [Header("Recorder")]
    public SimulationRecorder recorder;
    public bool isStartButton = true;

    [Header("Visuals")]
    public Color normalColor = new Color(0.2f, 0.6f, 1f);
    public Color pressedColor = new Color(0.1f, 1f, 0.3f);
    public float pressDepth = 0.005f;

    [Header("Audio")]
    public AudioClip clickSound;

    // ── private ────────────────────────────────────────────────────────────────

    private XRSimpleInteractable _interactable;
    private Renderer _renderer;
    private AudioSource _audio;
    private Vector3 _originalPosition;
    private bool _pressed = false;

    void Start()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
        _renderer = GetComponent<Renderer>();
        _audio = GetComponent<AudioSource>();
        _originalPosition = transform.localPosition;

        // Set initial color
        if (_renderer != null)
        {
            _renderer.material = new Material(Shader.Find("Standard"));
            _renderer.material.color = normalColor;
        }

        // Hook up events
        if (_interactable != null)
        {
            _interactable.selectEntered.AddListener(OnPressed);
            _interactable.selectExited.AddListener(OnReleased);
        }
    }

    void OnPressed(SelectEnterEventArgs args)
    {
        if (_pressed) return;
        _pressed = true;

        // Visual — press down and change color
        transform.localPosition = _originalPosition - new Vector3(0, pressDepth, 0);
        if (_renderer != null)
            _renderer.material.color = pressedColor;

        // Sound
        if (_audio != null && clickSound != null)
            _audio.PlayOneShot(clickSound);

        // Action
        if (recorder == null)
        {
            Debug.LogWarning("[PhysicalButton] No recorder assigned!");
            return;
        }

        if (isStartButton)
        {
            recorder.StartRecording();
            Debug.Log("[PhysicalButton] Recording started.");
        }
        else
        {
            recorder.StopRecording();
            Debug.Log("[PhysicalButton] Recording stopped.");
        }
    }

    void OnReleased(SelectExitEventArgs args)
    {
        _pressed = false;

        // Visual — pop back up and restore color
        transform.localPosition = _originalPosition;
        if (_renderer != null)
            _renderer.material.color = normalColor;
    }
}
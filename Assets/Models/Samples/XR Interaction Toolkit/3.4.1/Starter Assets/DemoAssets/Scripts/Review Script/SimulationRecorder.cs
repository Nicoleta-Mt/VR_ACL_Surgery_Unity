using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// SimulationRecorder — lightweight version.
/// Attach to the arthroscope GameObject alongside EndoscopePathGuide.
///
/// HOW ACCURACY IS MEASURED:
///   The waypoints form a straight-line path (entry -> ... -> deepest point).
///   Every recordInterval seconds, the script finds the closest point on that
///   line to the arthroscope's current position and measures the distance
///   (the "deviation"). These deviations are averaged across the whole
///   session — a lower average distance from the line means a higher
///   accuracy score. Deviation beyond ~15cm scores 0 accuracy.
///
/// GHOSTS (only appear after Stop is pressed):
///   - Green line  = the ideal path (the waypoints connected in order).
///   - Orange line = the participant's actual trail, but only the points
///     recorded while close to the ideal path (within proximityThreshold).
///
/// LEG MODEL:
///   Optionally assign the knee/leg GameObject to legModel. It is hidden
///   (or destroyed, if destroyLegOnReview is ticked) when Stop is pressed,
///   so the ghost paths are clearly visible during review. It automatically
///   reappears the next time Start is pressed (unless destroyed).
/// </summary>
public class SimulationRecorder : MonoBehaviour
{
    [Header("Tracking Reference")]
    [Tooltip("The point used for accuracy/jitter/trail recording. Should be the arthroscope tip, not the body/grip.")]
    public Transform tip;

    [Header("Waypoints (forms the ideal path line)")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("Participant Info")]
    public string participantName = "Participant";
    public string simulationType = "Arthroscope";

    [Header("Ghost Settings")]
    public Color participantGhostColor = new Color(1f, 0.4f, 0.1f, 1f);
    public Color idealGhostColor = new Color(0.1f, 1f, 0.3f, 1f);
    public float lineWidth = 0.003f;

    [Tooltip("Only record a trail point when within this distance of the path.")]
    public float proximityThreshold = 0.15f;

    [Header("Recording")]
    [Range(0.1f, 1f)]
    [Tooltip("Higher = records less often = better performance.")]
    public float recordInterval = 0.2f;

    [Header("Review Cleanup")]
    [Tooltip("The knee/leg model to hide during review so ghost paths are clearly visible.")]
    public GameObject legModel;

    [Tooltip("If true, destroys the leg permanently on Stop. If false, just hides it (recommended, so it returns on the next Start).")]
    public bool destroyLegOnReview = false;

    [Header("Review UI")]
    public Canvas reviewCanvas;
    public TMP_Text scoreText;
    public TMP_Text statsText;
    public TMP_Text participantText;

    // ── private ────────────────────────────────────────────────────────────────

    private List<Vector3> _trailPoints = new List<Vector3>();
    private bool _isRecording = false;
    private float _recordTimer = 0f;
    private float _sessionStart;

    private float _maxDeviation = 0f;
    private float _totalDeviation = 0f;
    private int _metricFrames = 0;
    private float _maxPathProgress = 0f;
    private float _totalJitter = 0f;
    private Vector3 _lastPosition;

    private GameObject _participantGhost;
    private GameObject _idealGhost;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        if (reviewCanvas != null)
            reviewCanvas.gameObject.SetActive(false);

        ClearGhosts();
    }

    void Update()
    {
        if (!_isRecording) return;

        _recordTimer += Time.deltaTime;
        if (_recordTimer >= recordInterval)
        {
            _recordTimer = 0f;
            TrackMetrics();
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void StartRecording()
    {
        _trailPoints.Clear();
        _isRecording = true;
        _sessionStart = Time.time;
        _maxDeviation = 0f;
        _totalDeviation = 0f;
        _metricFrames = 0;
        _maxPathProgress = 0f;
        _totalJitter = 0f;
        _lastPosition = tip != null ? tip.position : transform.position;

        ClearGhosts();
        if (reviewCanvas != null)
            reviewCanvas.gameObject.SetActive(false);

        // Bring the leg back for the next session (unless it was permanently destroyed)
        if (legModel != null)
            legModel.SetActive(true);

        Debug.Log("[SimulationRecorder] Recording started.");
    }

    public void StopRecording()
    {
        if (!_isRecording) return;
        _isRecording = false;

        SimulationDatabase.SessionResult result = BuildResult();
        SimulationDatabase.SaveSession(result);
        ShowReviewUI(result);
        BuildGhosts();
        HideLeg();

        Debug.Log($"[SimulationRecorder] Stopped. Score: {result.overallScore:F1}");
    }

    void HideLeg()
    {
        if (legModel == null) return;

        if (destroyLegOnReview)
            Destroy(legModel);
        else
            legModel.SetActive(false);
    }

    public void ClearGhosts()
    {
        if (_participantGhost != null) Destroy(_participantGhost);
        if (_idealGhost != null) Destroy(_idealGhost);
    }

    // ── Metrics ────────────────────────────────────────────────────────────────

    void TrackMetrics()
    {
        Vector3 pos = tip != null ? tip.position : transform.position;

        // Jitter: how fast position changed since last sample (smoothness signal).
        float jitter = Vector3.Distance(pos, _lastPosition) / Mathf.Max(recordInterval, 0.0001f);
        _totalJitter += jitter;
        _lastPosition = pos;
        _metricFrames++;

        if (waypoints.Count < 2) return;

        float t = GetClosestT(pos);
        float deviation = Vector3.Distance(pos, GetPathPos(t));

        _totalDeviation += deviation;
        if (deviation > _maxDeviation) _maxDeviation = deviation;
        if (t > _maxPathProgress) _maxPathProgress = t;

        // Only keep trail points close to the ideal line, to keep the ghost
        // readable instead of tracing every bit of stray movement.
        if (deviation <= proximityThreshold)
            _trailPoints.Add(pos);
    }

    // ── Result ─────────────────────────────────────────────────────────────────

    SimulationDatabase.SessionResult BuildResult()
    {
        float duration = Time.time - _sessionStart;
        float avgDeviation = _metricFrames > 0 ? _totalDeviation / _metricFrames : 0f;
        float avgJitter = _metricFrames > 0 ? _totalJitter / _metricFrames : 0f;

        float accuracyScore = Mathf.Clamp01(1f - (avgDeviation / 0.15f)) * 50f;
        float smoothnessScore = Mathf.Clamp01(1f - (avgJitter / 0.5f)) * 20f;
        float completionScore = _maxPathProgress * 30f;

        return new SimulationDatabase.SessionResult
        {
            participantName = participantName,
            simulationType = simulationType,
            totalDuration = duration,
            averagePathDeviation = avgDeviation,
            maxPathDeviation = _maxDeviation,
            completionPercent = _maxPathProgress * 100f,
            averageJitter = avgJitter,
            overallScore = accuracyScore + smoothnessScore + completionScore
        };
    }

    // ── Ghosts (built once, only after Stop) ──────────────────────────────────

    void BuildGhosts()
    {
        if (_trailPoints.Count >= 2)
        {
            _participantGhost = new GameObject("ParticipantGhost");
            LineRenderer lr = _participantGhost.AddComponent<LineRenderer>();
            lr.positionCount = _trailPoints.Count;
            for (int i = 0; i < _trailPoints.Count; i++)
                lr.SetPosition(i, _trailPoints[i]);
            SetupLine(lr, participantGhostColor);
        }

        if (waypoints.Count >= 2)
        {
            _idealGhost = new GameObject("IdealGhost");
            LineRenderer lr = _idealGhost.AddComponent<LineRenderer>();
            lr.positionCount = waypoints.Count;
            for (int i = 0; i < waypoints.Count; i++)
                lr.SetPosition(i, waypoints[i].position);
            SetupLine(lr, idealGhostColor);
        }
    }

    void SetupLine(LineRenderer lr, Color color)
    {
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        Material mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = color;
        lr.material = mat;
    }

    // ── Review UI ──────────────────────────────────────────────────────────────

    void ShowReviewUI(SimulationDatabase.SessionResult result)
    {
        if (reviewCanvas == null) return;
        reviewCanvas.gameObject.SetActive(true);

        if (participantText != null)
            participantText.text = $"Participant: {result.participantName}";

        if (scoreText != null)
            scoreText.text = $"Score: {result.overallScore:F1} / 100";

        if (statsText != null)
            statsText.text =
                $"Duration:      {result.totalDuration:F1}s\n" +
                $"Completion:    {result.completionPercent:F1}%\n" +
                $"Avg Deviation: {result.averagePathDeviation * 100f:F1}cm\n" +
                $"Max Deviation: {result.maxPathDeviation * 100f:F1}cm\n" +
                $"Smoothness:    {Mathf.Clamp01(1f - result.averageJitter / 0.5f) * 100f:F1}%";
    }

    // ── Path helpers ───────────────────────────────────────────────────────────

    Vector3 GetPathPos(float t)
    {
        if (waypoints.Count == 0) return Vector3.zero;
        if (waypoints.Count == 1) return waypoints[0].position;
        float scaled = t * (waypoints.Count - 1);
        int a = Mathf.FloorToInt(scaled);
        int b = Mathf.Min(a + 1, waypoints.Count - 1);
        return Vector3.Lerp(waypoints[a].position, waypoints[b].position, scaled - a);
    }

    float GetClosestT(Vector3 pos)
    {
        if (waypoints.Count < 2) return 0f;
        float bestT = 0f;
        float bestDist = float.MaxValue;
        int steps = (waypoints.Count - 1) * 10;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float dist = Vector3.Distance(pos, GetPathPos(t));
            if (dist < bestDist) { bestDist = dist; bestT = t; }
        }
        return bestT;
    }

    // ── Editor gizmos ──────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (_trailPoints.Count < 2) return;
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        for (int i = 1; i < _trailPoints.Count; i++)
            Gizmos.DrawLine(_trailPoints[i - 1], _trailPoints[i]);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// SimulationRecorder — lightweight version.
/// Attach to the arthroscope GameObject alongside EndoscopePathGuide.
/// </summary>
public class SimulationRecorder : MonoBehaviour
{
    [Header("References")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("Participant Info")]
    public string participantName = "Participant";
    public string simulationType = "Arthroscope";

    [Header("Ghost Settings")]
    public Color participantGhostColor = new Color(1f, 0.4f, 0.1f, 1f);
    public Color idealGhostColor = new Color(0.1f, 1f, 0.3f, 1f);
    public float lineWidth = 0.003f;

    [Header("Recording")]
    [Range(0.1f, 1f)]
    [Tooltip("Higher = records less often = better performance.")]
    public float recordInterval = 0.2f;

    [Header("Review UI")]
    public Canvas reviewCanvas;
    public TMP_Text scoreText;
    public TMP_Text statsText;
    public TMP_Text participantText;

    // ── private ────────────────────────────────────────────────────────────────

    private struct FrameData
    {
        public Vector3 position;
        public float time;
    }

    private List<Vector3> _recorded = new List<Vector3>();
    private bool _isRecording = false;
    private float _recordTimer = 0f;
    private float _sessionStart;

    // Metrics — simple versions only
    private float _maxDeviation = 0f;
    private float _totalDeviation = 0f;
    private int _metricFrames = 0;
    private float _maxPathProgress = 0f;

    private GameObject _participantGhost;
    private GameObject _idealGhost;
    private EndoscopePathGuide _pathGuide;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        _pathGuide = GetComponent<EndoscopePathGuide>();

        if (reviewCanvas != null)
            reviewCanvas.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_isRecording) return;

        _recordTimer += Time.deltaTime;
        if (_recordTimer >= recordInterval)
        {
            _recordTimer = 0f;
            _recorded.Add(transform.position);
            TrackMetrics();
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void StartRecording()
    {
        _recorded.Clear();
        _isRecording = true;
        _sessionStart = Time.time;
        _maxDeviation = 0f;
        _totalDeviation = 0f;
        _metricFrames = 0;
        _maxPathProgress = 0f;

        ClearGhosts();
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

        Debug.Log($"[SimulationRecorder] Stopped. Score: {result.overallScore:F1}");
    }

    public void ClearGhosts()
    {
        if (_participantGhost != null) Destroy(_participantGhost);
        if (_idealGhost != null) Destroy(_idealGhost);
    }

    // ── Metrics ────────────────────────────────────────────────────────────────

    void TrackMetrics()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        float t = GetClosestT(transform.position);
        float deviation = Vector3.Distance(transform.position, GetPathPos(t));

        _totalDeviation += deviation;
        _metricFrames++;

        if (deviation > _maxDeviation) _maxDeviation = deviation;
        if (t > _maxPathProgress) _maxPathProgress = t;
    }

    // ── Result ─────────────────────────────────────────────────────────────────

    SimulationDatabase.SessionResult BuildResult()
    {
        float duration = Time.time - _sessionStart;
        float avgDeviation = _metricFrames > 0 ? _totalDeviation / _metricFrames : 0f;

        float accuracyScore = Mathf.Clamp01(1f - (avgDeviation / 0.15f)) * 70f;
        float completionScore = _maxPathProgress * 30f;

        return new SimulationDatabase.SessionResult
        {
            participantName = participantName,
            simulationType = simulationType,
            totalDuration = duration,
            averagePathDeviation = avgDeviation,
            maxPathDeviation = _maxDeviation,
            completionPercent = _maxPathProgress * 100f,
            overallScore = accuracyScore + completionScore
        };
    }

    // ── Ghosts ─────────────────────────────────────────────────────────────────

    void BuildGhosts()
    {
        // Participant path
        if (_recorded.Count >= 2)
        {
            _participantGhost = new GameObject("ParticipantGhost");
            LineRenderer lr = _participantGhost.AddComponent<LineRenderer>();
            lr.positionCount = _recorded.Count;
            for (int i = 0; i < _recorded.Count; i++)
                lr.SetPosition(i, _recorded[i]);
            SetupLine(lr, participantGhostColor);
        }

        // Ideal path
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
                $"Max Deviation: {result.maxPathDeviation * 100f:F1}cm";
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
        // Reduced steps — only 10 per segment instead of 20
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
        if (_recorded.Count < 2) return;
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        for (int i = 1; i < _recorded.Count; i++)
            Gizmos.DrawLine(_recorded[i - 1], _recorded[i]);
    }
}
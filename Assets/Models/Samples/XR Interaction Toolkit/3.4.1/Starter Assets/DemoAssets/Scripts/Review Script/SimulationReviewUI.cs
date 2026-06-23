using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SimulationReviewUI — attach to a World Space Canvas.
/// Displays all saved sessions from the database.
/// </summary>
public class SimulationReviewUI : MonoBehaviour
{
    [Header("Session List Panel")]
    public Transform sessionListParent;
    public GameObject sessionRowPrefab;
    public TMP_InputField searchField;

    [Header("Detail Panel")]
    public GameObject detailPanel;
    public TMP_Text detailParticipant;
    public TMP_Text detailScore;
    public TMP_Text detailStats;
    public TMP_Text detailTimestamp;
    public TMP_Text detailType;

    [Header("Buttons")]
    public Button refreshButton;
    public Button clearAllButton;
    public Button closeDetailButton;
    public Button replayButton;

    [Header("Ghost Replay (optional)")]
    public SimulationRecorder recorder;

    [Header("Summary")]
    public TMP_Text summaryText;

    // ── private ────────────────────────────────────────────────────────────────

    private List<SimulationDatabase.SessionResult> _sessions = new List<SimulationDatabase.SessionResult>();
    private SimulationDatabase.SessionResult _selected;
    private List<GameObject> _rows = new List<GameObject>();

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        if (detailPanel != null) detailPanel.SetActive(false);
        if (refreshButton != null) refreshButton.onClick.AddListener(Refresh);
        if (clearAllButton != null) clearAllButton.onClick.AddListener(OnClearAll);
        if (closeDetailButton != null) closeDetailButton.onClick.AddListener(CloseDetail);
        if (replayButton != null) replayButton.onClick.AddListener(OnReplay);
        if (searchField != null) searchField.onValueChanged.AddListener(OnSearch);

        Refresh();
    }

    // ── Public ─────────────────────────────────────────────────────────────────

    public void Refresh()
    {
        _sessions = SimulationDatabase.GetAllSessions();
        PopulateList(_sessions);
        UpdateSummary(_sessions);
    }

    // ── Private ────────────────────────────────────────────────────────────────

    void PopulateList(List<SimulationDatabase.SessionResult> sessions)
    {
        foreach (GameObject row in _rows)
            if (row != null) Destroy(row);
        _rows.Clear();

        if (sessionListParent == null || sessionRowPrefab == null) return;

        foreach (var session in sessions)
        {
            GameObject row = Instantiate(sessionRowPrefab, sessionListParent);
            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>();

            if (texts.Length >= 1) texts[0].text = session.participantName;
            if (texts.Length >= 2) texts[1].text = $"{session.overallScore:F1}/100";
            if (texts.Length >= 3) texts[2].text = session.timestamp;
            if (texts.Length >= 4) texts[3].text = session.simulationType;

            Image bg = row.GetComponent<Image>();
            if (bg != null) bg.color = ScoreColor(session.overallScore);

            SimulationDatabase.SessionResult captured = session;
            Button btn = row.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => ShowDetail(captured));

            _rows.Add(row);
        }
    }

    void ShowDetail(SimulationDatabase.SessionResult session)
    {
        _selected = session;
        if (detailPanel != null) detailPanel.SetActive(true);

        if (detailParticipant != null)
            detailParticipant.text = session.participantName;

        if (detailScore != null)
        {
            detailScore.text = $"{session.overallScore:F1} / 100";
            detailScore.color = ScoreColor(session.overallScore);
        }

        if (detailType != null) detailType.text = session.simulationType;
        if (detailTimestamp != null) detailTimestamp.text = session.timestamp;

        if (detailStats != null)
            detailStats.text =
                $"Duration:      {session.totalDuration:F1} s\n" +
                $"Completion:    {session.completionPercent:F1} %\n" +
                $"Avg Deviation: {session.averagePathDeviation * 100f:F1} cm\n" +
                $"Max Deviation: {session.maxPathDeviation * 100f:F1} cm\n" +
                $"\nDatabase path:\n{SimulationDatabase.GetDatabasePath()}";
    }

    void CloseDetail()
    {
        if (detailPanel != null) detailPanel.SetActive(false);
    }

    void OnClearAll()
    {
        SimulationDatabase.ClearAll();
        Refresh();
    }

    void OnSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            PopulateList(_sessions);
            return;
        }

        List<SimulationDatabase.SessionResult> filtered = _sessions.FindAll(s =>
            s.participantName.ToLower().Contains(query.ToLower()) ||
            s.simulationType.ToLower().Contains(query.ToLower()));

        PopulateList(filtered);
    }

    void OnReplay()
    {
        Debug.Log("[SimulationReviewUI] Replay not available in lightweight mode.");
    }

    void UpdateSummary(List<SimulationDatabase.SessionResult> sessions)
    {
        if (summaryText == null) return;

        if (sessions.Count == 0)
        {
            summaryText.text = "No sessions recorded yet.";
            return;
        }

        float totalScore = 0f;
        float bestScore = 0f;
        foreach (var s in sessions)
        {
            totalScore += s.overallScore;
            if (s.overallScore > bestScore) bestScore = s.overallScore;
        }

        summaryText.text =
            $"Total Sessions: {sessions.Count}    " +
            $"Avg Score: {totalScore / sessions.Count:F1}    " +
            $"Best Score: {bestScore:F1}";
    }

    Color ScoreColor(float score)
    {
        if (score >= 80f) return new Color(0.2f, 0.8f, 0.3f, 0.4f);
        if (score >= 50f) return new Color(1f, 0.8f, 0.1f, 0.4f);
        return new Color(0.9f, 0.2f, 0.2f, 0.4f);
    }
}
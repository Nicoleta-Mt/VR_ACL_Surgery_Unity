using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// SimulationDatabase — handles persistent storage of simulation session results.
/// Saves to a JSON file in Application.persistentDataPath.
/// Can be used by both arthroscope and drill simulations.
/// </summary>
public static class SimulationDatabase
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "simulation_results.json");

    // ── Data structures ────────────────────────────────────────────────────────

    [Serializable]
    public class SessionResult
    {
        public string sessionId;
        public string participantName;
        public string simulationType;       // "Arthroscope" or "Drill"
        public string timestamp;

        // Time metrics
        public float totalDuration;         // seconds
        public float timeAtEntry;           // seconds spent near entry point

        // Accuracy metrics
        public float averagePathDeviation;  // average distance from path center (meters)
        public float maxPathDeviation;      // worst deviation
        public float entryAngleAccuracy;    // 0-100%
        public float completionPercent;     // how far along path they reached (0-100%)

        // Smoothness metrics
        public float averageJitter;         // average frame-to-frame position delta
        public int   directionReversals;    // number of times movement direction reversed
        public int   deviationEvents;       // number of times exceeded constrainDistance

        // Score
        public float overallScore;          // 0-100
    }

    [Serializable]
    private class Database
    {
        public List<SessionResult> sessions = new List<SessionResult>();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public static void SaveSession(SessionResult result)
    {
        Database db = Load();
        result.sessionId  = Guid.NewGuid().ToString();
        result.timestamp  = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        db.sessions.Add(result);
        File.WriteAllText(FilePath, JsonUtility.ToJson(db, true));
        Debug.Log($"[SimulationDatabase] Session saved to {FilePath}");
    }

    public static List<SessionResult> GetAllSessions()
    {
        return Load().sessions;
    }

    public static List<SessionResult> GetSessionsByType(string simulationType)
    {
        return Load().sessions.FindAll(s => s.simulationType == simulationType);
    }

    public static List<SessionResult> GetSessionsByParticipant(string name)
    {
        return Load().sessions.FindAll(s =>
            s.participantName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public static void ClearAll()
    {
        File.WriteAllText(FilePath, JsonUtility.ToJson(new Database(), true));
        Debug.Log("[SimulationDatabase] All sessions cleared.");
    }

    public static string GetDatabasePath() => FilePath;

    // ── Private ────────────────────────────────────────────────────────────────

    private static Database Load()
    {
        if (!File.Exists(FilePath))
            return new Database();

        try
        {
            string json = File.ReadAllText(FilePath);
            return JsonUtility.FromJson<Database>(json) ?? new Database();
        }
        catch
        {
            Debug.LogWarning("[SimulationDatabase] Failed to read database, starting fresh.");
            return new Database();
        }
    }
}

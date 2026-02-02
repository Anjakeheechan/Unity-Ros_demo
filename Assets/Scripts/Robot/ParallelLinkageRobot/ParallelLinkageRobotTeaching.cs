using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // New Input System
using System.IO;

/// <summary>
/// Teaching / Playback System for ParallelLinkageRobotController
/// Records motor target angles and plays them back.
/// Does NOT modify ParallelLinkageRobotController or ParallelLinkageRobotSetup.
/// </summary>
public class ParallelLinkageRobotTeaching : MonoBehaviour
{
    [Header("Robot Controller Reference")]
    [Tooltip("Reference to the ParallelLinkageRobotController")]
    public ParallelLinkageRobotController robotController;

    [Header("Playback Settings")]
    public float playbackSpeed = 30f; // Degrees per second for interpolation
    public bool loop = false;

    [Header("Controls (New Input System)")]
    public Key recordKey = Key.N;
    public Key playKey = Key.P;
    public Key clearKey = Key.B;
    public Key stopKey = Key.Escape;
    [Space]
    public Key saveKey = Key.K;
    public Key loadKey = Key.L;

    [Header("Recorded Waypoints")]
    [SerializeField]
    private List<MotorWaypoint> savedWaypoints = new List<MotorWaypoint>();

    private bool isPlaying = false;
    private Coroutine playCoroutine;
    private string saveFilePath;

    [System.Serializable]
    public struct MotorWaypoint
    {
        public float motor1;
        public float motor2;
        public float motor3;

        public MotorWaypoint(float m1, float m2, float m3)
        {
            motor1 = m1;
            motor2 = m2;
            motor3 = m3;
        }

        public MotorWaypoint(Vector3 angles)
        {
            motor1 = angles.x;
            motor2 = angles.y;
            motor3 = angles.z;
        }

        public Vector3 ToVector3() => new Vector3(motor1, motor2, motor3);
    }

    [System.Serializable]
    public class WaypointData
    {
        public List<MotorWaypoint> points;
    }

    void Start()
    {
        saveFilePath = Path.Combine(Application.persistentDataPath, "parallel_robot_waypoints.json");
        Debug.Log($"<color=cyan>[Teaching]</color> Save path: {saveFilePath}");

        if (robotController == null)
        {
            robotController = GetComponent<ParallelLinkageRobotController>();
            if (robotController == null)
            {
                Debug.LogError("[Teaching] ParallelLinkageRobotController not assigned!");
            }
        }

        // Auto-load on start
        LoadWaypoints();
    }

    void Update()
    {
        if (Keyboard.current == null || robotController == null) return;

        // Record
        if (recordKey != Key.None && Keyboard.current[recordKey].wasPressedThisFrame)
        {
            RecordWaypoint();
        }

        // Play
        if (playKey != Key.None && Keyboard.current[playKey].wasPressedThisFrame)
        {
            PlayWaypoints();
        }

        // Clear
        if (clearKey != Key.None && Keyboard.current[clearKey].wasPressedThisFrame)
        {
            ClearWaypoints();
        }

        // Stop
        if (stopKey != Key.None && Keyboard.current[stopKey].wasPressedThisFrame)
        {
            StopPlayback();
        }

        // Save
        if (saveKey != Key.None && Keyboard.current[saveKey].wasPressedThisFrame)
        {
            SaveWaypoints();
        }

        // Load
        if (loadKey != Key.None && Keyboard.current[loadKey].wasPressedThisFrame)
        {
            LoadWaypoints();
        }
    }

    [ContextMenu("Record Waypoint")]
    public void RecordWaypoint()
    {
        Vector3 current = robotController.GetTargetAngles();
        MotorWaypoint wp = new MotorWaypoint(current);
        savedWaypoints.Add(wp);
        Debug.Log($"<color=green>[Teaching]</color> Recorded! ({wp.motor1:F1}, {wp.motor2:F1}, {wp.motor3:F1}) Total: {savedWaypoints.Count}");
    }

    [ContextMenu("Clear Waypoints")]
    public void ClearWaypoints()
    {
        savedWaypoints.Clear();
        StopPlayback();
        Debug.Log("<color=yellow>[Teaching]</color> All waypoints cleared.");
    }

    [ContextMenu("Stop Playback")]
    public void StopPlayback()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
        isPlaying = false;
        Debug.Log("<color=red>[Teaching]</color> Playback stopped.");
    }

    [ContextMenu("Play Waypoints")]
    public void PlayWaypoints()
    {
        if (isPlaying)
        {
            Debug.Log("[Teaching] Already playing!");
            return;
        }

        if (savedWaypoints.Count == 0)
        {
            Debug.LogWarning("[Teaching] No waypoints to play!");
            return;
        }

        if (playCoroutine != null) StopCoroutine(playCoroutine);
        playCoroutine = StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        isPlaying = true;
        Debug.Log("<color=cyan>[Teaching]</color> Playback started...");

        do
        {
            var points = new List<MotorWaypoint>(savedWaypoints);
            for (int i = 0; i < points.Count; i++)
            {
                if (!isPlaying) break;

                MotorWaypoint target = points[i];
                Vector3 targetAngles = target.ToVector3();

                // Move towards target
                while (isPlaying)
                {
                    Vector3 current = robotController.GetTargetAngles();
                    float distance = Vector3.Distance(current, targetAngles);

                    if (distance < 0.5f) break; // Close enough

                    Vector3 newAngles = Vector3.MoveTowards(current, targetAngles, playbackSpeed * Time.deltaTime);
                    robotController.SetTargetAngles(newAngles.x, newAngles.y, newAngles.z);

                    yield return null;
                }

                if (!isPlaying) break;

                // Snap to exact
                robotController.SetTargetAngles(target.motor1, target.motor2, target.motor3);
                
                // Small pause between waypoints
                yield return new WaitForSeconds(0.1f);
            }
        } while (loop && isPlaying);

        isPlaying = false;
        Debug.Log("<color=cyan>[Teaching]</color> Playback finished.");
    }

    [ContextMenu("Save Waypoints")]
    public void SaveWaypoints()
    {
        if (savedWaypoints.Count == 0)
        {
            Debug.LogWarning("[Teaching] No waypoints to save.");
            return;
        }

        WaypointData data = new WaypointData { points = savedWaypoints };
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log($"<color=green>[Teaching]</color> Saved {savedWaypoints.Count} waypoints to: {saveFilePath}");
    }

    [ContextMenu("Load Waypoints")]
    public void LoadWaypoints()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            WaypointData data = JsonUtility.FromJson<WaypointData>(json);
            if (data != null && data.points != null)
            {
                savedWaypoints = data.points;
                Debug.Log($"<color=green>[Teaching]</color> Loaded {savedWaypoints.Count} waypoints.");
            }
        }
        else
        {
            Debug.Log("[Teaching] No save file found. Starting fresh.");
        }
    }

    void OnGUI()
    {
        if (robotController == null || !robotController.showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 300, 400, 150));
        GUI.Box(new Rect(0, 0, 400, 150), "");

        GUILayout.Label("<b>Teaching System</b>");
        GUILayout.Label($"Waypoints: {savedWaypoints.Count} | Playing: {isPlaying} | Loop: {loop}");
        GUILayout.Space(5);
        GUILayout.Label($"N: Record | P: Play | B: Clear | Esc: Stop");
        GUILayout.Label($"K: Save   | L: Load");

        GUILayout.EndArea();
    }
}

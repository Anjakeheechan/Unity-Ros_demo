using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

public class AGVMover : MonoBehaviour
{
    [Header("Way1 시간 설정 (Way1 → Way2 이동)")]
    [SerializeField] private float way1MoveTime = 3.0f;
    [SerializeField] private float way1RotationTime = 1.0f;
    [SerializeField] private float way1WaitTime = 6.0f; // Way1 도착 후 대기 시간

    [Header("Way2 시간 설정 (Way2 → Way1 복귀)")]
    [SerializeField] private float way2MoveTime = 2.0f;
    [SerializeField] private float way2RotationTime = 0.5f;
    [SerializeField] private float way2WaitTime = 6.0f; // Way2 복귀 후 대기 시간

    [Header("상태")]
    [SerializeField] private bool isExecutingRoute = false;
    [SerializeField] private bool isStopRequested = false;
    [SerializeField] private bool isPaused = false;
    [SerializeField] private int currentActionIndex = 0;
    [SerializeField] private string currentStatus = "대기";
    [SerializeField] private string currentWayInfo = "Way1 대기";
    [SerializeField] private int loopCount = 0;

    // Way1: 시작 위치
    private Vector3 way1Position = new Vector3(4.9299f, 0.0683f, 8.5301f);
    // Way2: 도착 위치
    private Vector3 way2Position = new Vector3(4.9299f, 0.0683f, 5.0761f);

    private List<WaypointAction> routeActions = new List<WaypointAction>();

    private int way1ActionCount = 0;
    private int way2ActionCount = 0;

    private float actionElapsedTime = 0f;
    private float currentActionDuration = 0f;
    private Vector3 actionStartPosition;
    private Vector3 actionTargetPosition;
    private float actionStartRotationY;
    private float actionTargetRotationY;
    private bool isRotating = false;
    private bool isMovingToTarget = false;

    private bool wasMovingBeforeStop = false;
    private bool wasRotatingBeforeStop = false;

    public enum ActionType { Move, RotateLeft, RotateRight, Wait }
    public enum WayPhase { Way1, Way2 }

    [System.Serializable]
    public class WaypointAction
    {
        public ActionType actionType;
        public Vector3 targetPosition;
        public float value; // 회전 각도 또는 대기 시간
        public string description;
        public WayPhase phase;
        public int phaseIndex;

        public WaypointAction(ActionType type, Vector3 pos, float val, string desc, WayPhase wayPhase, int index)
        {
            actionType = type;
            targetPosition = pos;
            value = val;
            description = desc;
            phase = wayPhase;
            phaseIndex = index;
        }
    }

    void Start()
    {
        transform.position = way1Position;
       // transform.rotation = Quaternion.Euler(0, 180, 0);
        SetupRoute();
    }

    void SetupRoute()
    {
        routeActions.Clear();
        int way1Index = 0;
        int way2Index = 0;

        // ===== Way1 구간 =====
        routeActions.Add(new WaypointAction(ActionType.Move, way2Position, 0, "Way1 → Way2 이동", WayPhase.Way1, ++way1Index));
        routeActions.Add(new WaypointAction(ActionType.Wait, Vector3.zero, way1WaitTime, "Way2 도착 대기", WayPhase.Way1, ++way1Index));
        way1ActionCount = way1Index;

        // ===== Way2 구간 =====
        routeActions.Add(new WaypointAction(ActionType.RotateLeft, Vector3.zero, 90f, "왼쪽 90도 회전", WayPhase.Way2, ++way2Index));
        routeActions.Add(new WaypointAction(ActionType.Move, new Vector3(6.427f, 0.0683f, 5.0761f), 0, "X: 6.427 이동", WayPhase.Way2, ++way2Index));
        routeActions.Add(new WaypointAction(ActionType.RotateLeft, Vector3.zero, 90f, "왼쪽 90도 회전", WayPhase.Way2, ++way2Index));
        routeActions.Add(new WaypointAction(ActionType.Move, new Vector3(6.427f, 0.0683f, 8.551f), 0, "Z: 8.551 이동", WayPhase.Way2, ++way2Index));
        routeActions.Add(new WaypointAction(ActionType.RotateLeft, Vector3.zero, 90f, "왼쪽 90도 회전", WayPhase.Way2, ++way2Index));
        routeActions.Add(new WaypointAction(ActionType.Move, way1Position, 0, "Way1 복귀", WayPhase.Way2, ++way2Index));
        routeActions.Add(new WaypointAction(ActionType.RotateLeft, Vector3.zero, 90f, "최종 회전", WayPhase.Way2, ++way2Index));
        routeActions.Add(new WaypointAction(ActionType.Wait, Vector3.zero, way2WaitTime, "Way1 복귀 후 대기", WayPhase.Way2, ++way2Index));

        way2ActionCount = way2Index;
        Debug.Log($"경로 설정 완료 - Way1: {way1ActionCount}, Way2: {way2ActionCount}");
    }

    void Update()
    {
        if (DataManager.Instance.stm_stm_yolo_currentstate != "RUNNING")
        {
            return;
        }

        HandleInput();

        if (isExecutingRoute && !isPaused)
        {
            ExecuteCurrentAction();
        }
    }

    void HandleInput()
    {
        // DataManager에서 현재 상태 가져오기
        string currentEspState = DataManager.Instance.modbustcp_esp32_01_state;

        // 1. Start / Resume 로직 (Space 키 OR ESP 상태가 "RUN")
        bool isSpacePressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
        bool isEspRun = (currentEspState == "RUN");

        if (isSpacePressed || isEspRun)
        {
            if (!isExecutingRoute && !isPaused) StartRoute();
            else if (isPaused) ResumeRoute();
        }

        // 2. Stop 로직 (S 키 OR ESP 상태가 "STOP")
        // [수정됨] ESP 상태가 STOP이면 S키를 누른 것과 동일하게 처리
        bool isSPressed = (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame);
        bool isEspStop = (currentEspState == "STOP");

        if (isSPressed || isEspStop)
        {
            // 실행 중이고, 아직 정지 요청이 안 된 상태라면 정지 요청
            if (isExecutingRoute && !isStopRequested)
            {
                RequestStop();
            }
        }

        // 3. Emergency Stop (E 키)
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            EmergencyStop();
        }

        // 4. Reset (R 키)
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            ResetToWay1();
        }
    }

    void PrepareNextAction()
    {
        if (currentActionIndex >= routeActions.Count)
        {
            currentActionIndex = 0;
            loopCount++;
            Debug.Log($"===== 루프 {loopCount}회차 시작 =====");
        }

        WaypointAction action = routeActions[currentActionIndex];
        actionElapsedTime = 0f;
        UpdateWayInfo();

        switch (action.actionType)
        {
            case ActionType.Move:
                actionStartPosition = transform.position;
                actionTargetPosition = action.targetPosition;
                currentActionDuration = (action.phase == WayPhase.Way1) ? way1MoveTime : way2MoveTime;
                isMovingToTarget = true;
                isRotating = false;
                break;
            case ActionType.RotateLeft:
            case ActionType.RotateRight:
                actionStartRotationY = transform.eulerAngles.y;
                float sign = (action.actionType == ActionType.RotateLeft) ? -1f : 1f;
                actionTargetRotationY = actionStartRotationY + (sign * action.value);
                currentActionDuration = (action.phase == WayPhase.Way1) ? way1RotationTime : way2RotationTime;
                isRotating = true;
                isMovingToTarget = false;
                break;
            case ActionType.Wait:
                currentActionDuration = action.value;
                isMovingToTarget = false;
                isRotating = false;
                break;
        }
    }

    void ExecuteCurrentAction()
    {
        actionElapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(actionElapsedTime / currentActionDuration);

        if (isMovingToTarget)
        {
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(actionStartPosition, actionTargetPosition, smoothT);
            if (t >= 1f) { transform.position = actionTargetPosition; isMovingToTarget = false; OnActionComplete(); }
        }
        else if (isRotating)
        {
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float currentY = Mathf.LerpAngle(actionStartRotationY, actionTargetRotationY, smoothT);
            transform.rotation = Quaternion.Euler(0, currentY, 0);
            if (t >= 1f) { transform.rotation = Quaternion.Euler(0, actionTargetRotationY, 0); isRotating = false; OnActionComplete(); }
        }
        else // Wait
        {
            if (t >= 1f) OnActionComplete();
        }
    }

    void OnActionComplete()
    {
        currentActionIndex++;
        if (isStopRequested)
        {
            isExecutingRoute = false;
            isPaused = true;
            isStopRequested = false;
            currentStatus = "일시정지";
            return;
        }
        PrepareNextAction();
    }

    public void StartRoute() { isExecutingRoute = true; isPaused = false; currentActionIndex = 0; loopCount = 1; currentStatus = "실행 중"; PrepareNextAction(); }
    public void RequestStop() { isStopRequested = true; currentStatus = "정지 요청됨"; Debug.Log(">> Stop 신호 수신: 현재 동작 완료 후 정지합니다."); }
    public void ResumeRoute() { isPaused = false; isExecutingRoute = true; currentStatus = "실행 중"; PrepareNextAction(); }
    public void EmergencyStop() { wasMovingBeforeStop = isMovingToTarget; wasRotatingBeforeStop = isRotating; isExecutingRoute = false; isPaused = true; isMovingToTarget = false; isRotating = false; currentStatus = "긴급 정지"; }
    public void ResetToWay1() { isExecutingRoute = false; isPaused = false; transform.position = way1Position; transform.rotation = Quaternion.Euler(0, 180, 0); currentActionIndex = 0; loopCount = 0; currentStatus = "대기"; }
    void UpdateWayInfo() { if (currentActionIndex < routeActions.Count) { WaypointAction action = routeActions[currentActionIndex]; currentWayInfo = $"{action.phase} ({action.phaseIndex})"; } }

    void OnGUI()
    {
        GUIStyle infoStyle = new GUIStyle { fontSize = 18 }; infoStyle.normal.textColor = Color.cyan;
        GUI.Label(new Rect(10, 45, 400, 25), $"상태: {currentStatus}");
        GUI.Label(new Rect(10, 95, 400, 25), $"Loop: {loopCount} | {currentWayInfo}", infoStyle);
        GUI.Label(new Rect(10, 250, 400, 25), $"ESP State: '{DataManager.Instance.modbustcp_esp32_01_state}'", infoStyle);
    }
}
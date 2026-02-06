using UnityEngine;
using System.Collections;

/// <summary>
/// 리니어용 컨베이어 (엘리베이터 연동)
/// - AGV층: Jogging 모드 (Box 감지 시 jogTime 동안 CW 이동)
/// - 1층/2층: 연속 CW 모드 (Box 모두 배출될 때까지) → Dragger CW 텔레포트 → AGV층 복귀
/// </summary>
public class ConveyorForLinear : MonoBehaviour
{
    public enum FloorMode
    {
        AgvFloor,       // AGV층 - Jogging 모드
        Floor1,         // 1층 - 연속 CW 모드
        Floor2          // 2층 - 연속 CW 모드
    }

    [Header("드래거 설정")]
    [SerializeField] private DraggerForLinear[] draggers;
    
    [Header("이동 설정")]
    [SerializeField] private float speed = 1f;
    [Tooltip("AGV층 Box 감지 시 이동 시간 (초)")]
    [SerializeField] private float jogTime = 3f;

    [Header("엘리베이터 연동")]
    [SerializeField] private ElevatorController elevatorController;

    [Header("수동 제어")]
    public bool manualCW = false;
    public bool manualCCW = false;

    [Header("상태 (읽기 전용)")]
    [SerializeField] private FloorMode currentFloorMode = FloorMode.AgvFloor;
    [SerializeField] private bool isRunning = false;
    [SerializeField] private bool jogDisabled = false;

    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;

    private Coroutine currentRoutine = null;

    void Update()
    {
        // 수동 제어 (isRunning 중이 아닐 때만)
        if (!isRunning)
        {
            if (manualCW)
            {
                foreach (var dragger in draggers)
                {
                    if (dragger != null) dragger.Move(true, speed);
                }
            }
            else if (manualCCW)
            {
                foreach (var dragger in draggers)
                {
                    if (dragger != null) dragger.Move(false, speed);
                }
            }
        }

        // AGV층에서만 Box 감지 → Jogging
        if (currentFloorMode == FloorMode.AgvFloor && !isRunning && !jogDisabled)
        {
            CheckForBoxAndJog();
        }
    }

    #region 외부 호출 메서드

    /// <summary>
    /// 엘리베이터가 층에 도착했을 때 호출
    /// </summary>
    public void OnFloorReached(int floor)
    {
        if (showDebugLog)
        {
            Debug.Log($"<color=cyan>[ConveyorForLinear]</color> 층 도착 알림: {floor}층");
        }

        switch (floor)
        {
            case 0:
                currentFloorMode = FloorMode.AgvFloor;
                jogDisabled = false;  // AGV층 도착 시 Jog 활성화
                break;
            case 1:
                currentFloorMode = FloorMode.Floor1;
                StartContinuousCW();
                break;
            case 2:
                currentFloorMode = FloorMode.Floor2;
                StartContinuousCW();
                break;
        }
    }

    #endregion

    #region AGV층 - Jogging 모드

    private void CheckForBoxAndJog()
    {
        foreach (var dragger in draggers)
        {
            if (dragger != null && dragger.HasBox)
            {
                if (showDebugLog)
                {
                    Debug.Log("<color=green>[ConveyorForLinear]</color> AGV층 Box 감지! Jogging 시작!");
                }
                StartJogging();
                return;
            }
        }
    }

    private void StartJogging()
    {
        if (isRunning) return;
        
        StopCurrentRoutine();
        currentRoutine = StartCoroutine(JoggingRoutine());
    }

    private IEnumerator JoggingRoutine()
    {
        isRunning = true;
        float elapsed = 0f;

        if (showDebugLog)
        {
            Debug.Log($"<color=cyan>[ConveyorForLinear]</color> Jogging 시작! 시간: {jogTime}초");
        }

        while (elapsed < jogTime)
        {
            foreach (var dragger in draggers)
            {
                if (dragger != null) dragger.Move(true, speed);  // CW
            }

            // 첫 번째 Dragger CW 도착 체크
            if (draggers.Length > 0 && draggers[0] != null && draggers[0].IsAtCwDestination)
            {
                if (showDebugLog)
                {
                    Debug.Log("<color=red>[ConveyorForLinear]</color> 첫 번째 Dragger CW 도착! Jogging 중지!");
                }
                jogDisabled = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        isRunning = false;

        if (showDebugLog)
        {
            Debug.Log($"<color=yellow>[ConveyorForLinear]</color> Jogging 완료! ({elapsed:F2}초)");
        }
    }

    #endregion

    #region 1층/2층 - 연속 CW 모드

    private void StartContinuousCW()
    {
        StopCurrentRoutine();
        currentRoutine = StartCoroutine(ContinuousCWRoutine());
    }

    private IEnumerator ContinuousCWRoutine()
    {
        isRunning = true;

        if (showDebugLog)
        {
            Debug.Log($"<color=cyan>[ConveyorForLinear]</color> {currentFloorMode} 연속 CW 시작! Box 배출까지 계속 이동");
        }

        // 모든 Dragger에서 자식이 없어질 때까지 CW 이동
        while (HasAnyBoxInDraggers())
        {
            foreach (var dragger in draggers)
            {
                if (dragger != null) dragger.Move(true, speed);  // CW
            }
            yield return null;
        }

        if (showDebugLog)
        {
            Debug.Log("<color=yellow>[ConveyorForLinear]</color> 모든 Box 배출 완료!");
        }

        // 모든 Dragger를 CW Destination으로 텔레포트
        TeleportDraggersToCW();

        yield return new WaitForSeconds(0.5f);

        // AGV층으로 복귀
        ReturnToAgvFloor();

        isRunning = false;
    }

    /// <summary>
    /// 어떤 Dragger에라도 자식(Box)이 있는지 확인
    /// </summary>
    private bool HasAnyBoxInDraggers()
    {
        foreach (var dragger in draggers)
        {
            if (dragger != null && dragger.HasAnyChild)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 모든 Dragger를 CW Destination으로 텔레포트
    /// </summary>
    private void TeleportDraggersToCW()
    {
        foreach (var dragger in draggers)
        {
            if (dragger != null)
            {
                dragger.ResetToCwPosition();
            }
        }

        if (showDebugLog)
        {
            Debug.Log("<color=magenta>[ConveyorForLinear]</color> 모든 Dragger CW 위치로 텔레포트");
        }
    }

    /// <summary>
    /// AGV층으로 엘리베이터 복귀 요청
    /// </summary>
    private void ReturnToAgvFloor()
    {
        if (elevatorController != null)
        {
            elevatorController.SetFloor(0);  // AGV층으로 이동

            if (showDebugLog)
            {
                Debug.Log("<color=blue>[ConveyorForLinear]</color> AGV층으로 복귀 요청");
            }
        }
        else
        {
            Debug.LogWarning("[ConveyorForLinear] ElevatorController가 할당되지 않음!");
        }
    }

    #endregion

    #region 유틸리티

    private void StopCurrentRoutine()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }
        isRunning = false;
    }

    public void StartManualCW()
    {
        manualCW = true;
        manualCCW = false;
    }

    public void StartManualCCW()
    {
        manualCW = false;
        manualCCW = true;
    }

    public void StopManual()
    {
        manualCW = false;
        manualCCW = false;
    }

    [ContextMenu("Reset All Draggers to CCW")]
    public void ResetAllDraggersToCcw()
    {
        StopCurrentRoutine();

        foreach (var dragger in draggers)
        {
            if (dragger != null)
            {
                dragger.DetachBox();
                dragger.SetReturning(false);
                dragger.ResetToCcwPosition();
            }
        }

        manualCW = false;
        manualCCW = false;
        jogDisabled = false;
        currentFloorMode = FloorMode.AgvFloor;

        if (showDebugLog)
        {
            Debug.Log("<color=magenta>[ConveyorForLinear]</color> 모든 드래거 CCW 리셋 + AGV층 모드");
        }
    }

    [ContextMenu("Reset All Draggers to CW")]
    public void ResetAllDraggersToCw()
    {
        StopCurrentRoutine();

        foreach (var dragger in draggers)
        {
            if (dragger != null)
            {
                dragger.DetachBox();
                dragger.SetReturning(false);
                dragger.ResetToCwPosition();
            }
        }

        manualCW = false;
        manualCCW = false;

        if (showDebugLog)
        {
            Debug.Log("<color=magenta>[ConveyorForLinear]</color> 모든 드래거 CW 리셋");
        }
    }

    public void EnableJog()
    {
        jogDisabled = false;
    }

    public void DisableJog()
    {
        jogDisabled = true;
        StopCurrentRoutine();
    }

    public FloorMode GetCurrentFloorMode() => currentFloorMode;
    public bool IsRunning => isRunning;

    #endregion
}

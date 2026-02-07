using UnityEngine;
using System.Collections;

/// <summary>
/// 리니어용 컨베이어 (엘리베이터 연동)
/// - AGV층: Jogging 모드 (Box 감지 시 jogTime 동안 CW 이동)
/// - 1층/2층: 연속 CW 모드 (일정 시간 동안 CW 이동) → AGV층 복귀
/// Dragger는 순회 구조로 자동 텔레포트되므로 별도 리셋 불필요
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
    [Tooltip("1층/2층에서 CW 이동 시간 (초)")]
    [SerializeField] private float floorTransferTime = 5f;

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
                ResetDraggersToInitialPosition();  // Dragger 초기 위치로 리셋
                break;
            case 1:
                currentFloorMode = FloorMode.Floor1;
                StartFloorTransfer();
                break;
            case 2:
                currentFloorMode = FloorMode.Floor2;
                StartFloorTransfer();
                break;
        }
    }

    #endregion

    #region AGV층 - Jogging 모드

    private void CheckForBoxAndJog()
    {
        foreach (var dragger in draggers)
        {
            if (dragger != null && dragger.HasAnyChild)
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

            elapsed += Time.deltaTime;
            yield return null;
        }

        isRunning = false;
        jogDisabled = true;  // Jogging 완료 후 다시는 자동 시작 안함

        if (showDebugLog)
        {
            Debug.Log($"<color=yellow>[ConveyorForLinear]</color> Jogging 완료! ({elapsed:F2}초)");
        }
    }

    #endregion

    #region 1층/2층 - 연속 CW 모드

    private void StartFloorTransfer()
    {
        StopCurrentRoutine();
        currentRoutine = StartCoroutine(FloorTransferRoutine());
    }

    private IEnumerator FloorTransferRoutine()
    {
        isRunning = true;
        float elapsed = 0f;

        if (showDebugLog)
        {
            Debug.Log($"<color=cyan>[ConveyorForLinear]</color> {currentFloorMode} 연속 CW 시작! 시간: {floorTransferTime}초");
        }

        // floorTransferTime 동안 CW 이동 (Dragger가 순회하면서 Box는 자동으로 분리됨)
        while (elapsed < floorTransferTime)
        {
            foreach (var dragger in draggers)
            {
                if (dragger != null) dragger.Move(true, speed);  // CW
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (showDebugLog)
        {
            Debug.Log("<color=yellow>[ConveyorForLinear]</color> 층 이송 완료!");
        }

        yield return new WaitForSeconds(0.5f);

        // AGV층으로 복귀
        ReturnToAgvFloor();

        isRunning = false;
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

    /// <summary>
    /// 모든 Dragger를 초기 위치(CCW)로 리셋
    /// </summary>
    private void ResetDraggersToInitialPosition()
    {
        foreach (var dragger in draggers)
        {
            if (dragger != null)
            {
                dragger.ResetToCcwPosition();
            }
        }

        if (showDebugLog)
        {
            Debug.Log("<color=magenta>[ConveyorForLinear]</color> 모든 Dragger 초기 위치(CCW)로 리셋");
        }
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

    [ContextMenu("Reset All")]
    public void ResetAll()
    {
        StopCurrentRoutine();

        foreach (var dragger in draggers)
        {
            if (dragger != null)
            {
                dragger.ResetToCcwPosition();
            }
        }

        manualCW = false;
        manualCCW = false;
        jogDisabled = false;
        currentFloorMode = FloorMode.AgvFloor;

        if (showDebugLog)
        {
            Debug.Log("<color=magenta>[ConveyorForLinear]</color> 전체 리셋 완료");
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

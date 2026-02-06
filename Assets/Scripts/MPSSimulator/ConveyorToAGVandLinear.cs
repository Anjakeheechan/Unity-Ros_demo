using UnityEngine;
using System.Collections;

/// <summary>
/// AGV와 리니어 사이 물품 이송용 컨베이어
/// 지정된 Y좌표에 AGV가 도달하면 1초 딜레이 후 CCW로 물품 이송
/// </summary>
public class ConveyorToAGVandLinear : MonoBehaviour
{
    [Header("AGV 위치 감지")]
    [Tooltip("AGV Transform (위치 감지용)")]
    [SerializeField] private Transform agvTransform;
    
    [Tooltip("AGV가 도달해야 하는 Z좌표")]
    [SerializeField] private float targetZ = 5.0761f;
    
    [Tooltip("Z좌표 도달 판정 허용 오차")]
    [SerializeField] private float zTolerance = 0.05f;

    [Header("드래거 설정")]
    [SerializeField] private DraggerForLinear[] draggers;
    [SerializeField] private float speed = 1f;

    [Header("타이밍 설정")]
    [Tooltip("AGV 도착 후 이송 시작 딜레이 (초)")]
    [SerializeField] private float startDelay = 1f;
    
    [Tooltip("이송 최대 시간 (초)")]
    [SerializeField] private float transferTime = 5f;
    
    [Tooltip("이송 완료 후 복귀 딜레이 (초)")]
    [SerializeField] private float returnDelay = 0.5f;

    [Header("수동 제어")]
    public bool manualCW = false;
    public bool manualCCW = false;

    [Header("상태")]
    [SerializeField] private ConveyorState currentState = ConveyorState.WaitingForAGV;
    [SerializeField] private bool agvAtPosition = false;
    
    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;

    public enum ConveyorState
    {
        WaitingForAGV,      // AGV 대기 중
        WaitingDelay,       // 딜레이 대기 중
        TransferringCCW,    // CCW 방향 이송 중
        DroppingBox,        // Box 분리 중
        ReturningCW,        // CW로 복귀 중
        ManualControl       // 수동 제어 중
    }

    private Coroutine currentRoutine;
    private bool hasTriggeredThisCycle = false;

    void Update()
    {
        // 수동 제어 체크
        if (manualCW || manualCCW)
        {
            HandleManualControl();
            return;
        }

        switch (currentState)
        {
            case ConveyorState.WaitingForAGV:
                CheckAGVPosition();
                break;
        }
    }

    /// <summary>
    /// AGV 위치 확인
    /// </summary>
    private void CheckAGVPosition()
    {
        if (agvTransform == null) return;

        float currentZ = agvTransform.position.z;
        bool isAtTarget = Mathf.Abs(currentZ - targetZ) <= zTolerance;

        if (isAtTarget && !agvAtPosition && !hasTriggeredThisCycle)
        {
            agvAtPosition = true;
            hasTriggeredThisCycle = true;
            
            if (showDebugLog)
            {
                Debug.Log($"<color=green>[ConveyorToAGVandLinear]</color> AGV 도착! Z={currentZ:F4} (목표: {targetZ})");
            }
            
            // 딜레이 후 이송 시작
            StartTransferWithDelay();
        }
        else if (!isAtTarget)
        {
            agvAtPosition = false;
            // AGV가 위치를 벗어나면 다음 사이클 준비
            if (currentState == ConveyorState.WaitingForAGV)
            {
                hasTriggeredThisCycle = false;
            }
        }
    }

    /// <summary>
    /// 딜레이 후 이송 시작
    /// </summary>
    private void StartTransferWithDelay()
    {
        currentState = ConveyorState.WaitingDelay;
        
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
        }
        currentRoutine = StartCoroutine(TransferRoutine());
    }

    /// <summary>
    /// 이송 코루틴
    /// </summary>
    private IEnumerator TransferRoutine()
    {
        // 딜레이 대기
        if (showDebugLog)
        {
            Debug.Log($"<color=yellow>[ConveyorToAGVandLinear]</color> {startDelay}초 딜레이 시작...");
        }
        
        yield return new WaitForSeconds(startDelay);

        // Box 감지 확인
        bool hasBox = false;
        foreach (var dragger in draggers)
        {
            if (dragger != null && dragger.HasBox)
            {
                hasBox = true;
                break;
            }
        }

        if (!hasBox)
        {
            if (showDebugLog)
            {
                Debug.LogWarning("[ConveyorToAGVandLinear] Box가 없어서 이송 취소");
            }
            currentState = ConveyorState.WaitingForAGV;
            hasTriggeredThisCycle = false;
            yield break;
        }

        // CCW 이송 시작
        currentState = ConveyorState.TransferringCCW;
        
        if (showDebugLog)
        {
            Debug.Log("<color=cyan>[ConveyorToAGVandLinear]</color> CCW 이송 시작!");
        }

        float elapsed = 0f;
        while (elapsed < transferTime)
        {
            // 드래거들을 CCW로 이동
            foreach (var dragger in draggers)
            {
                if (dragger != null)
                {
                    dragger.Move(false, speed); // CCW
                }
            }
            
            elapsed += Time.deltaTime;

            // CCW 목적지 도착 체크
            bool allArrived = true;
            foreach (var dragger in draggers)
            {
                if (dragger != null && !dragger.IsAtCcwDestination)
                {
                    allArrived = false;
                    break;
                }
            }

            if (allArrived)
            {
                if (showDebugLog)
                {
                    Debug.Log("<color=cyan>[ConveyorToAGVandLinear]</color> CCW 목적지 도착!");
                }
                break;
            }

            yield return null;
        }

        // Box 분리
        yield return StartCoroutine(DropBoxRoutine());
    }

    /// <summary>
    /// Box 분리 코루틴
    /// </summary>
    private IEnumerator DropBoxRoutine()
    {
        currentState = ConveyorState.DroppingBox;
        
        if (showDebugLog)
        {
            Debug.Log("<color=yellow>[ConveyorToAGVandLinear]</color> Box 분리 중...");
        }

        foreach (var dragger in draggers)
        {
            if (dragger != null)
            {
                dragger.DetachBox();
            }
        }

        yield return new WaitForSeconds(returnDelay);

        // CW로 복귀
        StartReturnCW();
    }

    /// <summary>
    /// CW 복귀 시작
    /// </summary>
    private void StartReturnCW()
    {
        currentState = ConveyorState.ReturningCW;
        
        // 복귀 모드 설정
        foreach (var dragger in draggers)
        {
            if (dragger != null)
            {
                dragger.SetReturning(true);
            }
        }
        
        if (showDebugLog)
        {
            Debug.Log("<color=blue>[ConveyorToAGVandLinear]</color> CW 복귀 시작");
        }
        
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
        }
        currentRoutine = StartCoroutine(ReturnCWRoutine());
    }

    /// <summary>
    /// CW 복귀 코루틴
    /// </summary>
    private IEnumerator ReturnCWRoutine()
    {
        while (true)
        {
            // 드래거들을 CW로 이동
            foreach (var dragger in draggers)
            {
                if (dragger != null)
                {
                    dragger.Move(true, speed); // CW
                }
            }

            // CW 목적지 도착 체크
            bool allArrived = true;
            foreach (var dragger in draggers)
            {
                if (dragger != null && !dragger.IsAtCwDestination)
                {
                    allArrived = false;
                    break;
                }
            }

            if (allArrived)
            {
                // 복귀 모드 해제
                foreach (var dragger in draggers)
                {
                    if (dragger != null)
                    {
                        dragger.SetReturning(false);
                    }
                }
                
                if (showDebugLog)
                {
                    Debug.Log("<color=green>[ConveyorToAGVandLinear]</color> 복귀 완료! 대기 상태로 전환");
                }
                
                currentState = ConveyorState.WaitingForAGV;
                hasTriggeredThisCycle = false;
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// 수동 제어 처리
    /// </summary>
    private void HandleManualControl()
    {
        currentState = ConveyorState.ManualControl;

        if (manualCW)
        {
            foreach (var dragger in draggers)
            {
                if (dragger != null)
                {
                    dragger.Move(true, speed);
                }
            }
        }
        else if (manualCCW)
        {
            foreach (var dragger in draggers)
            {
                if (dragger != null)
                {
                    dragger.Move(false, speed);
                }
            }
        }

        if (!manualCW && !manualCCW)
        {
            currentState = ConveyorState.WaitingForAGV;
        }
    }

    /// <summary>
    /// 수동으로 이송 시작 (테스트용)
    /// </summary>
    [ContextMenu("Manual Start Transfer")]
    public void ManualStartTransfer()
    {
        if (currentState == ConveyorState.WaitingForAGV)
        {
            StartTransferWithDelay();
        }
    }

    /// <summary>
    /// 리셋
    /// </summary>
    [ContextMenu("Reset All Draggers")]
    public void ResetAllDraggers()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

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
        hasTriggeredThisCycle = false;
        currentState = ConveyorState.WaitingForAGV;
        Debug.Log("<color=magenta>[ConveyorToAGVandLinear]</color> 리셋 완료");
    }

    /// <summary>
    /// 현재 상태 반환
    /// </summary>
    public ConveyorState GetCurrentState()
    {
        return currentState;
    }

    void OnDrawGizmosSelected()
    {
        // 목표 Z좌표 시각화
        Gizmos.color = Color.yellow;
        Vector3 lineStart = transform.position + Vector3.left * 2f;
        lineStart.z = targetZ;
        Vector3 lineEnd = transform.position + Vector3.right * 2f;
        lineEnd.z = targetZ;
        Gizmos.DrawLine(lineStart, lineEnd);
    }
}

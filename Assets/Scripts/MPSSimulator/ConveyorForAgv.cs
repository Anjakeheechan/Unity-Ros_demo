using UnityEngine;
using System.Collections;

/// <summary>
/// AGV용 컨베이어 (통합 버전)
/// 하나의 Dragger 배열로 분배라인/리니어 모두 처리
/// - 분배라인 도착 Z좌표: Box 감지 시 Jogging (CW 방향)
/// - 리니어 도착 Z좌표: CCW 이송 → Box 분리 → CW 복귀
/// </summary>
public class ConveyorForAgv : MonoBehaviour
{
    public enum OperationMode
    {
        Idle,               // 대기 중
        Jogging,            // 분배라인 Jogging 중
        WaitingDelay,       // 리니어 이송 딜레이 대기
        TransferringCCW,    // 리니어 CCW 이송 중
        DroppingBox,        // Box 분리 중
        ReturningCW,        // CW 복귀 중
        ManualControl       // 수동 제어
    }

    [Header("AGV 설정")]
    [Tooltip("AGV Transform (위치 감지용)")]
    [SerializeField] private Transform agvTransform;

    [Header("Z좌표 설정")]
    [Tooltip("분배라인 도착 Z좌표 (Jogging 시작)")]
    [SerializeField] private float distributionLineZ = 3.0f;
    
    [Tooltip("리니어 도착 Z좌표 (CCW 이송 시작)")]
    [SerializeField] private float linearZ = 5.0761f;
    
    [Tooltip("Z좌표 도달 판정 허용 오차")]
    [SerializeField] private float zTolerance = 0.1f;

    [Header("드래거 설정")]
    [Tooltip("분배라인 & 리니어 공용 Dragger")]
    [SerializeField] private DraggerForAgv[] draggers;

    [Header("이동 설정")]
    [SerializeField] private float speed = 1f;
    
    [Tooltip("분배라인 Jogging 시간 (초)")]
    [SerializeField] private float jogTime = 3f;
    
    [Tooltip("리니어 이송 시작 딜레이 (초)")]
    [SerializeField] private float linearStartDelay = 1f;
    
    [Tooltip("리니어 이송 최대 시간 (초)")]
    [SerializeField] private float linearTransferTime = 5f;
    
    [Tooltip("이송 완료 후 복귀 딜레이 (초)")]
    [SerializeField] private float returnDelay = 0.5f;

    [Header("수동 제어")]
    public bool manualCW = false;
    public bool manualCCW = false;

    [Header("상태 (읽기 전용)")]
    [SerializeField] private OperationMode currentMode = OperationMode.Idle;
    [SerializeField] private bool isAtDistributionLine = false;
    [SerializeField] private bool isAtLinearPosition = false;
    [SerializeField] private bool jogDisabledForDistribution = false;

    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;

    private Coroutine currentRoutine = null;
    private bool hasTriggeredLinearThisCycle = false;

    void Start()
    {
        // Dragger 이벤트 구독
        foreach (var dragger in draggers)
        {
            if (dragger != null)
            {
                dragger.OnBoxDetected += OnBoxDetected;
                dragger.OnReachedCwDestination += OnDraggerReachedCw;
            }
        }
    }

    void OnDestroy()
    {
        foreach (var dragger in draggers)
        {
            if (dragger != null)
            {
                dragger.OnBoxDetected -= OnBoxDetected;
                dragger.OnReachedCwDestination -= OnDraggerReachedCw;
            }
        }
    }

    void Update()
    {
        // 수동 제어 체크
        if (manualCW || manualCCW)
        {
            HandleManualControl();
            return;
        }

        // AGV 위치에 따른 동작 결정
        CheckAGVPosition();
    }

    /// <summary>
    /// AGV 위치 확인 및 동작 모드 결정
    /// </summary>
    private void CheckAGVPosition()
    {
        if (agvTransform == null) return;
        if (currentMode != OperationMode.Idle) return;

        float currentZ = agvTransform.position.z;
        
        // 분배라인 Z좌표 체크
        bool atDistribution = Mathf.Abs(currentZ - distributionLineZ) <= zTolerance;
        
        // 리니어 Z좌표 체크
        bool atLinear = Mathf.Abs(currentZ - linearZ) <= zTolerance;

        // 분배라인 도착 상태 업데이트
        if (atDistribution && !isAtDistributionLine)
        {
            isAtDistributionLine = true;
            jogDisabledForDistribution = false;
            
            if (showDebugLog)
            {
                Debug.Log($"<color=green>[ConveyorForAgv]</color> 분배라인 도착! Z={currentZ:F4}");
            }
        }
        else if (!atDistribution)
        {
            isAtDistributionLine = false;
        }

        // 리니어 도착 체크
        if (atLinear && !isAtLinearPosition && !hasTriggeredLinearThisCycle)
        {
            isAtLinearPosition = true;
            hasTriggeredLinearThisCycle = true;
            
            if (showDebugLog)
            {
                Debug.Log($"<color=cyan>[ConveyorForAgv]</color> 리니어 위치 도착! Z={currentZ:F4} → CCW 이송 시작");
            }
            
            StartLinearTransfer();
        }
        else if (!atLinear)
        {
            isAtLinearPosition = false;
            if (currentMode == OperationMode.Idle)
            {
                hasTriggeredLinearThisCycle = false;
            }
        }
    }

    #region 분배라인 Jogging

    /// <summary>
    /// Box 감지 시 호출 (분배라인에서만 동작)
    /// </summary>
    private void OnBoxDetected()
    {
        if (!isAtDistributionLine) return;
        if (jogDisabledForDistribution) return;
        if (currentMode != OperationMode.Idle) return;

        if (showDebugLog)
        {
            Debug.Log("<color=green>[ConveyorForAgv]</color> 분배라인 Box 감지! 전체 Dragger Jogging 시작!");
        }

        StartDistributionJogging();
    }

    /// <summary>
    /// Dragger가 CW에 도착하면 Jogging 중지
    /// </summary>
    private void OnDraggerReachedCw()
    {
        if (currentMode != OperationMode.Jogging) return;
        
        jogDisabledForDistribution = true;
        StopCurrentRoutine();
        currentMode = OperationMode.Idle;
        
        if (showDebugLog)
        {
            Debug.Log("<color=red>[ConveyorForAgv]</color> Dragger CW 도착! Jogging 중지!");
        }
    }

    private void StartDistributionJogging()
    {
        StopCurrentRoutine();
        currentRoutine = StartCoroutine(DistributionJoggingRoutine());
    }

    private IEnumerator DistributionJoggingRoutine()
    {
        currentMode = OperationMode.Jogging;
        float elapsed = 0f;

        if (showDebugLog)
        {
            Debug.Log($"<color=cyan>[ConveyorForAgv]</color> 분배라인 Jogging 시작! 시간: {jogTime}초");
        }

        while (elapsed < jogTime && !jogDisabledForDistribution)
        {
            foreach (var dragger in draggers)
            {
                if (dragger != null)
                {
                    dragger.Move(true, speed);  // CW
                }
            }

            if (draggers.Length > 0 && draggers[0] != null)
            {
                if (draggers[0].IsAtCwDestination)
                {
                    jogDisabledForDistribution = true;
                    break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        currentMode = OperationMode.Idle;

        if (showDebugLog)
        {
            Debug.Log($"<color=yellow>[ConveyorForAgv]</color> 분배라인 Jogging 완료! (이동 시간: {elapsed:F2}초)");
        }
    }

    #endregion

    #region 리니어 이송

    private void StartLinearTransfer()
    {
        StopCurrentRoutine();
        currentRoutine = StartCoroutine(LinearTransferRoutine());
    }

    private IEnumerator LinearTransferRoutine()
    {
        currentMode = OperationMode.WaitingDelay;

        if (showDebugLog)
        {
            Debug.Log($"<color=yellow>[ConveyorForAgv]</color> 리니어 이송 {linearStartDelay}초 딜레이...");
        }

        yield return new WaitForSeconds(linearStartDelay);

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
                Debug.LogWarning("[ConveyorForAgv] Box가 없어서 리니어 이송 취소");
            }
            currentMode = OperationMode.Idle;
            hasTriggeredLinearThisCycle = false;
            yield break;
        }

        // CCW 이송 시작
        currentMode = OperationMode.TransferringCCW;

        if (showDebugLog)
        {
            Debug.Log("<color=cyan>[ConveyorForAgv]</color> 리니어 CCW 이송 시작!");
        }

        float elapsed = 0f;
        while (elapsed < linearTransferTime)
        {
            foreach (var dragger in draggers)
            {
                if (dragger != null)
                {
                    dragger.Move(false, speed);  // CCW
                }
            }

            elapsed += Time.deltaTime;

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
                    Debug.Log("<color=cyan>[ConveyorForAgv]</color> 리니어 CCW 목적지 도착!");
                }
                break;
            }

            yield return null;
        }

        // Box 분리
        yield return StartCoroutine(LinearDropBoxRoutine());
    }

    private IEnumerator LinearDropBoxRoutine()
    {
        currentMode = OperationMode.DroppingBox;

        if (showDebugLog)
        {
            Debug.Log("<color=yellow>[ConveyorForAgv]</color> 리니어 Box 분리 중...");
        }

        foreach (var dragger in draggers)
        {
            if (dragger != null)
            {
                dragger.DetachBox();
            }
        }

        yield return new WaitForSeconds(returnDelay);

        // CW 복귀
        StartLinearReturnCW();
    }

    private void StartLinearReturnCW()
    {
        currentMode = OperationMode.ReturningCW;

        foreach (var dragger in draggers)
        {
            if (dragger != null)
            {
                dragger.SetReturning(true);
            }
        }

        if (showDebugLog)
        {
            Debug.Log("<color=blue>[ConveyorForAgv]</color> 리니어 CW 복귀 시작");
        }

        StopCurrentRoutine();
        currentRoutine = StartCoroutine(LinearReturnCWRoutine());
    }

    private IEnumerator LinearReturnCWRoutine()
    {
        while (true)
        {
            foreach (var dragger in draggers)
            {
                if (dragger != null)
                {
                    dragger.Move(true, speed);  // CW
                }
            }

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
                foreach (var dragger in draggers)
                {
                    if (dragger != null)
                    {
                        dragger.SetReturning(false);
                    }
                }

                if (showDebugLog)
                {
                    Debug.Log("<color=green>[ConveyorForAgv]</color> 리니어 CW 복귀 완료!");
                }

                currentMode = OperationMode.Idle;
                hasTriggeredLinearThisCycle = false;
                yield break;
            }

            yield return null;
        }
    }

    #endregion

    #region 수동 제어

    private void HandleManualControl()
    {
        currentMode = OperationMode.ManualControl;

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

        if (!manualCW && !manualCCW)
        {
            currentMode = OperationMode.Idle;
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

    #endregion

    #region 유틸리티

    private void StopCurrentRoutine()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }
    }

    [ContextMenu("Reset All")]
    public void ResetAll()
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
        currentMode = OperationMode.Idle;
        jogDisabledForDistribution = false;
        hasTriggeredLinearThisCycle = false;

        if (showDebugLog)
        {
            Debug.Log("<color=magenta>[ConveyorForAgv]</color> 전체 리셋 완료!");
        }
    }

    public OperationMode GetCurrentMode()
    {
        return currentMode;
    }

    void OnDrawGizmosSelected()
    {
        // 분배라인 Z좌표 (녹색)
        Gizmos.color = Color.green;
        Vector3 distStart = transform.position + Vector3.left * 2f;
        distStart.z = distributionLineZ;
        Vector3 distEnd = transform.position + Vector3.right * 2f;
        distEnd.z = distributionLineZ;
        Gizmos.DrawLine(distStart, distEnd);

        // 리니어 Z좌표 (노란색)
        Gizmos.color = Color.yellow;
        Vector3 linearStart = transform.position + Vector3.left * 2f;
        linearStart.z = linearZ;
        Vector3 linearEnd = transform.position + Vector3.right * 2f;
        linearEnd.z = linearZ;
        Gizmos.DrawLine(linearStart, linearEnd);
    }

    #endregion
}

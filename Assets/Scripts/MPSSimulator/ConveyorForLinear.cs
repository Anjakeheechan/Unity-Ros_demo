using UnityEngine;
using System.Collections;

/// <summary>
/// 리니어용 컨베이어 (Jogging 모드)
/// 어떤 Dragger든 Box를 감지하면 모든 Dragger가 함께 jogTime 동안 이동
/// 첫 번째 Dragger가 CW Destination에 도착하면 Jogging 중지
/// </summary>
public class ConveyorForLinear : MonoBehaviour
{
    [Header("드래거 설정")]
    [SerializeField] private DraggerForLinear[] draggers;
    
    [Header("Jogging 설정")]
    [SerializeField] private float speed = 1f;
    [Tooltip("Box 감지 시 이동 시간 (초)")]
    [SerializeField] private float jogTime = 3f;
    [Tooltip("이동 방향 (true=CW, false=CCW)")]
    [SerializeField] private bool moveDirectionCW = true;

    [Header("수동 제어")]
    [Tooltip("수동으로 CW 이동")]
    public bool manualCW = false;
    [Tooltip("수동으로 CCW 이동")]
    public bool manualCCW = false;

    [Header("상태 (읽기 전용)")]
    [SerializeField] private bool isJogging = false;
    [SerializeField] private bool jogDisabled = false;

    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;

    private Coroutine jogCoroutine = null;

    void Update()
    {
        // 수동 제어 (Jogging 중이 아닐 때만)
        if (!isJogging)
        {
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
        }

        // Box 감지 체크 (Jogging 중이 아닐 때만)
        if (!isJogging && !jogDisabled)
        {
            CheckForBox();
        }
    }

    /// <summary>
    /// Box 감지 확인
    /// </summary>
    private void CheckForBox()
    {
        foreach (var dragger in draggers)
        {
            if (dragger != null && dragger.HasBox)
            {
                if (showDebugLog)
                {
                    Debug.Log("<color=green>[ConveyorForLinear]</color> Box 감지! 전체 Dragger Jogging 시작!");
                }
                StartJogging();
                return;
            }
        }
    }

    /// <summary>
    /// 전체 Dragger Jogging 시작
    /// </summary>
    public void StartJogging()
    {
        if (jogDisabled || isJogging) return;
        
        if (jogCoroutine != null)
        {
            StopCoroutine(jogCoroutine);
        }
        jogCoroutine = StartCoroutine(JoggingRoutine());
    }

    /// <summary>
    /// Jogging 코루틴 - 모든 Dragger를 jogTime 동안 함께 이동
    /// </summary>
    private IEnumerator JoggingRoutine()
    {
        isJogging = true;
        float elapsed = 0f;
        
        string dir = moveDirectionCW ? "CW" : "CCW";
        if (showDebugLog)
        {
            Debug.Log($"<color=cyan>[ConveyorForLinear]</color> 전체 Jogging 시작! 방향: {dir}, 시간: {jogTime}초");
        }

        while (elapsed < jogTime && !jogDisabled)
        {
            // 모든 Dragger를 함께 이동
            foreach (var dragger in draggers)
            {
                if (dragger != null)
                {
                    dragger.Move(moveDirectionCW, speed);
                }
            }
            
            // 첫 번째 Dragger가 CW 도착했는지 체크
            if (moveDirectionCW && draggers.Length > 0 && draggers[0] != null)
            {
                if (draggers[0].IsAtCwDestination)
                {
                    if (showDebugLog)
                    {
                        Debug.Log("<color=red>[ConveyorForLinear]</color> 첫 번째 Dragger CW 도착! Jogging 중지!");
                    }
                    jogDisabled = true;
                    break;
                }
            }
            // CCW 방향일 경우 CCW 도착 체크
            else if (!moveDirectionCW && draggers.Length > 0 && draggers[0] != null)
            {
                if (draggers[0].IsAtCcwDestination)
                {
                    if (showDebugLog)
                    {
                        Debug.Log("<color=red>[ConveyorForLinear]</color> 첫 번째 Dragger CCW 도착! Jogging 중지!");
                    }
                    jogDisabled = true;
                    break;
                }
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        isJogging = false;
        
        if (showDebugLog)
        {
            Debug.Log($"<color=yellow>[ConveyorForLinear]</color> Jogging 완료! (이동 시간: {elapsed:F2}초)");
        }
    }

    /// <summary>
    /// Jogging 강제 중지
    /// </summary>
    public void StopJogging()
    {
        if (jogCoroutine != null)
        {
            StopCoroutine(jogCoroutine);
            jogCoroutine = null;
        }
        isJogging = false;
    }

    /// <summary>
    /// 수동으로 CW 시작
    /// </summary>
    public void StartManualCW()
    {
        manualCW = true;
        manualCCW = false;
    }

    /// <summary>
    /// 수동으로 CCW 시작
    /// </summary>
    public void StartManualCCW()
    {
        manualCW = false;
        manualCCW = true;
    }

    /// <summary>
    /// 수동 제어 정지
    /// </summary>
    public void StopManual()
    {
        manualCW = false;
        manualCCW = false;
    }

    /// <summary>
    /// 모든 드래거 리셋 (CCW 위치로) + Jog 다시 활성화
    /// </summary>
    [ContextMenu("Reset All Draggers to CCW")]
    public void ResetAllDraggersToCcw()
    {
        StopJogging();
        
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
        
        if (showDebugLog)
        {
            Debug.Log("<color=magenta>[ConveyorForLinear]</color> 모든 드래거 CCW 위치로 리셋 + Jog 활성화");
        }
    }

    /// <summary>
    /// 모든 드래거 리셋 (CW 위치로)
    /// </summary>
    [ContextMenu("Reset All Draggers to CW")]
    public void ResetAllDraggersToCw()
    {
        StopJogging();
        
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
            Debug.Log("<color=magenta>[ConveyorForLinear]</color> 모든 드래거 CW 위치로 리셋");
        }
    }

    /// <summary>
    /// Jog 기능 다시 활성화
    /// </summary>
    public void EnableJog()
    {
        jogDisabled = false;
        
        if (showDebugLog)
        {
            Debug.Log("<color=green>[ConveyorForLinear]</color> Jog 기능 활성화");
        }
    }

    /// <summary>
    /// Jog 기능 비활성화
    /// </summary>
    public void DisableJog()
    {
        jogDisabled = true;
        StopJogging();
        
        if (showDebugLog)
        {
            Debug.Log("<color=red>[ConveyorForLinear]</color> Jog 기능 비활성화");
        }
    }
}

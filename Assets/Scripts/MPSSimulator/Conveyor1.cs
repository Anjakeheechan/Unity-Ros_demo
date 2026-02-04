using UnityEngine;

/// <summary>
/// PLC 없이 동작하는 컨베이어
/// 직접 cwSignal/ccwSignal을 제어하거나 외부에서 Start/Stop 호출
/// </summary>
public class Conveyor1 : MonoBehaviour
{
    [Header("컨베이어 신호")]
    [Tooltip("정방향(CW) 회전 신호")]
    public bool cwSignal = false;
    
    [Tooltip("역방향(CCW) 회전 신호")]
    public bool ccwSignal = false;

    [Header("컨베이어 설정")]
    [SerializeField] private Dragger[] draggers;
    [SerializeField] private float speed = 1f;

    [Header("상태")]
    [SerializeField] private bool isStopped = false;
    
    // 정지 전 신호 상태 저장
    private bool savedCwSignal = false;
    private bool savedCcwSignal = false;

    void Update()
    {
        if (isStopped) return;

        if (cwSignal)
        {
            foreach (var dragger in draggers)
            {
                if (dragger != null)
                {
                    dragger.Move(true, speed);
                }
            }
        }

        if (ccwSignal)
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

    /// <summary>
    /// 컨베이어 정지 (현재 신호 상태 저장)
    /// </summary>
    public void StopConveyor()
    {
        if (!isStopped)
        {
            savedCwSignal = cwSignal;
            savedCcwSignal = ccwSignal;
            isStopped = true;
            Debug.Log("<color=red>[Conveyor1]</color> 컨베이어 정지");
        }
    }

    /// <summary>
    /// 컨베이어 재시작 (저장된 신호 상태 복원)
    /// </summary>
    public void StartConveyor()
    {
        if (isStopped)
        {
            isStopped = false;
            cwSignal = savedCwSignal;
            ccwSignal = savedCcwSignal;
            Debug.Log("<color=green>[Conveyor1]</color> 컨베이어 재시작");
        }
    }

    /// <summary>
    /// 정방향 회전 시작
    /// </summary>
    public void StartCW()
    {
        cwSignal = true;
        ccwSignal = false;
        isStopped = false;
    }

    /// <summary>
    /// 역방향 회전 시작
    /// </summary>
    public void StartCCW()
    {
        cwSignal = false;
        ccwSignal = true;
        isStopped = false;
    }

    /// <summary>
    /// 완전 정지 (신호 리셋)
    /// </summary>
    public void FullStop()
    {
        cwSignal = false;
        ccwSignal = false;
        isStopped = true;
        Debug.Log("<color=red>[Conveyor1]</color> 완전 정지");
    }

    /// <summary>
    /// 현재 정지 상태 확인
    /// </summary>
    public bool IsStopped => isStopped;
}

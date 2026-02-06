using UnityEngine;
using System;

/// <summary>
/// 리니어용 드래거
/// DraggerForAgv와 동일한 구조로 Box 부착/분리 처리
/// Box_A 태그 물체가 영역에 들어오면 자식으로 설정
/// </summary>
public class DraggerForLinear : MonoBehaviour
{
    [Header("목적지 설정")]
    [SerializeField] private Transform cwDestination;
    [SerializeField] private Transform ccwDestination;
    
    [Header("도착 감지")]
    [Tooltip("목적지 도착 판정 거리")]
    [SerializeField] private float arrivalThreshold = 0.1f;

    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;

    [Header("상태 (읽기 전용)")]
    [SerializeField] private bool isReturning = false;  // 복귀 중 플래그

    // 현재 자식으로 있는 Box
    private GameObject attachedBox = null;
    
    // Box 감지 이벤트 (ConveyorForLinear에서 구독 가능)
    public event Action OnBoxDetected;
    
    // CW 도착 이벤트
    public event Action OnReachedCwDestination;
    
    // 프로퍼티
    public bool HasBox => attachedBox != null;
    public bool IsReturning => isReturning;
    public bool IsAtCwDestination => cwDestination != null && 
        Vector3.Distance(transform.position, cwDestination.position) < arrivalThreshold;
    public bool IsAtCcwDestination => ccwDestination != null && 
        Vector3.Distance(transform.position, ccwDestination.position) < arrivalThreshold;
    
    // 자식 오브젝트가 하나라도 있는지 확인 (Box 태그 체크 없이)
    public bool HasAnyChild => transform.childCount > 0;

    /// <summary>
    /// 이동 (Conveyor에서 호출)
    /// </summary>
    public void Move(bool isCW, float speed)
    {
        Transform targetDest = isCW ? cwDestination : ccwDestination;
        if (targetDest == null) return;

        Vector3 direction = targetDest.position - transform.position;
        float distance = direction.magnitude;

        if (distance < arrivalThreshold)
        {
            // CW 목적지 도착 시 이벤트 발생
            if (isCW && IsAtCwDestination)
            {
                OnReachedCwDestination?.Invoke();
            }
            return;
        }

        transform.position += direction.normalized * speed * Time.deltaTime;
    }

    /// <summary>
    /// 복귀 모드 설정 - 복귀 중에는 Box를 자식으로 설정하지 않음
    /// </summary>
    public void SetReturning(bool returning)
    {
        isReturning = returning;
        if (showDebugLog)
        {
            Debug.Log($"<color=blue>[DraggerForLinear]</color> {gameObject.name} 복귀 모드: {returning}");
        }
    }

    /// <summary>
    /// 즉시 CCW 위치로 이동 (리셋용)
    /// </summary>
    public void ResetToCcwPosition()
    {
        if (ccwDestination != null)
        {
            transform.position = ccwDestination.position;
            isReturning = false;
            if (showDebugLog)
            {
                Debug.Log($"<color=gray>[DraggerForLinear]</color> {gameObject.name} CCW 위치로 리셋");
            }
        }
    }

    /// <summary>
    /// 즉시 CW 위치로 이동
    /// </summary>
    public void ResetToCwPosition()
    {
        if (cwDestination != null)
        {
            transform.position = cwDestination.position;
            isReturning = false;
            if (showDebugLog)
            {
                Debug.Log($"<color=gray>[DraggerForLinear]</color> {gameObject.name} CW 위치로 리셋");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAttachBox(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryAttachBox(collision.gameObject);
    }

    /// <summary>
    /// Box 부착 시도 - 성공 시 이벤트 발생
    /// </summary>
    private void TryAttachBox(GameObject obj)
    {
        // 복귀 중이면 무시
        if (isReturning) return;
        
        // Box_A 태그 또는 Box가 포함된 태그, 또는 이름에 Box가 포함된 경우
        bool isBox = obj.CompareTag("Box_A") || 
                     obj.tag.Contains("Box") || 
                     obj.name.Contains("Box");

        if (isBox && attachedBox == null)
        {
            attachedBox = obj;
            obj.transform.SetParent(this.transform);
            
            if (showDebugLog)
            {
                Debug.Log($"<color=green>[DraggerForLinear]</color> {gameObject.name} Box 부착: {obj.name}");
            }
            
            // ConveyorForLinear에 Box 감지 알림
            OnBoxDetected?.Invoke();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (isReturning) return;
        
        if (attachedBox == null)
        {
            TryAttachBox(other.gameObject);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (isReturning) return;
        
        if (attachedBox == null)
        {
            TryAttachBox(collision.gameObject);
        }
    }

    /// <summary>
    /// Box 분리
    /// </summary>
    public void DetachBox()
    {
        if (attachedBox != null)
        {
            attachedBox.transform.SetParent(null);
            
            if (showDebugLog)
            {
                Debug.Log($"<color=yellow>[DraggerForLinear]</color> {gameObject.name} Box 분리: {attachedBox.name}");
            }
            
            attachedBox = null;
        }
        
        // 자식 중 Box 태그 물체가 있으면 모두 분리
        foreach (Transform child in transform)
        {
            if (child.CompareTag("Box_A") || child.tag.Contains("Box"))
            {
                if (showDebugLog)
                {
                    Debug.Log($"<color=yellow>[DraggerForLinear]</color> {gameObject.name} 자식 Box 분리: {child.name}");
                }
                child.SetParent(null);
            }
        }
    }

    /// <summary>
    /// 현재 부착된 Box 반환
    /// </summary>
    public GameObject GetAttachedBox()
    {
        return attachedBox;
    }

    void OnDrawGizmosSelected()
    {
        if (cwDestination != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(cwDestination.position, 0.1f);
            Gizmos.DrawLine(transform.position, cwDestination.position);
        }
        if (ccwDestination != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(ccwDestination.position, 0.1f);
        }
    }
}

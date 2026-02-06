using UnityEngine;

/// <summary>
/// 리니어용 드래거
/// Box_A 태그 물체가 영역에 들어오면 자식으로 설정
/// DraggerForAgv와 유사하지만 리니어 측 컨베이어용
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

    [Header("상태")]
    [SerializeField] private bool isReturning = false;
    [SerializeField] private bool isEnabled = true; // 드래거 활성화 상태

    // 현재 자식으로 있는 Box
    private GameObject attachedBox = null;
    
    // 이동 방향
    private Vector3 direction;
    
    // 프로퍼티
    public bool HasBox => attachedBox != null;
    public bool IsReturning => isReturning;
    public bool IsEnabled => isEnabled;
    public bool IsAtCwDestination => cwDestination != null && 
        Vector3.Distance(transform.position, cwDestination.position) < arrivalThreshold;
    public bool IsAtCcwDestination => ccwDestination != null && 
        Vector3.Distance(transform.position, ccwDestination.position) < arrivalThreshold;

    private void Start()
    {
        if (cwDestination != null)
        {
            direction = cwDestination.position;
        }
    }

    /// <summary>
    /// 이동 (Conveyor에서 호출)
    /// </summary>
    public void Move(bool isCW, float speed)
    {
        Transform targetDest = isCW ? cwDestination : ccwDestination;
        if (targetDest == null) return;

        direction = targetDest.position - transform.position;
        float distance = direction.magnitude;

        if (distance < arrivalThreshold)
        {
            return;
        }

        transform.position += direction.normalized * speed * Time.deltaTime;
    }

    /// <summary>
    /// 드래거 활성화/비활성화
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        if (showDebugLog)
        {
            Debug.Log($"<color=cyan>[DraggerForLinear]</color> 활성화: {enabled}");
        }
    }

    /// <summary>
    /// 복귀 모드 설정
    /// </summary>
    public void SetReturning(bool returning)
    {
        isReturning = returning;
        if (showDebugLog)
        {
            Debug.Log($"<color=blue>[DraggerForLinear]</color> 복귀 모드: {returning}");
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
                Debug.Log($"<color=gray>[DraggerForLinear]</color> CCW 위치로 리셋");
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
                Debug.Log($"<color=gray>[DraggerForLinear]</color> CW 위치로 리셋");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isEnabled) return;
        if (isReturning) return;

        if (other.CompareTag("Box_A") || other.tag.Contains("Box"))
        {
            if (attachedBox == null)
            {
                attachedBox = other.gameObject;
                other.transform.SetParent(this.transform);
                
                if (showDebugLog)
                {
                    Debug.Log($"<color=green>[DraggerForLinear]</color> Box 부착: {other.gameObject.name}");
                }
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isEnabled) return;
        if (isReturning) return;

        if (attachedBox == null && (other.CompareTag("Box_A") || other.tag.Contains("Box")))
        {
            attachedBox = other.gameObject;
            other.transform.SetParent(this.transform);
            
            if (showDebugLog)
            {
                Debug.Log($"<color=green>[DraggerForLinear]</color> Box 부착 (Stay): {other.gameObject.name}");
            }
        }
    }

    /// <summary>
    /// Box 분리 (강제)
    /// </summary>
    public void DetachBox()
    {
        if (attachedBox != null)
        {
            attachedBox.transform.SetParent(null);
            
            if (showDebugLog)
            {
                Debug.Log($"<color=yellow>[DraggerForLinear]</color> Box 분리: {attachedBox.name}");
            }
            
            attachedBox = null;
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

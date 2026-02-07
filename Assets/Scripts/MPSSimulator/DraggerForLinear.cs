using UnityEngine;

/// <summary>
/// 리니어용 드래거 (원본 Dragger.cs 참고)
/// - CW 이동 시 CW 도착하면 CCW로 텔레포트 (순회)
/// - CCW 이동 시 CCW 도착하면 CW로 텔레포트 (순회)
/// - OnTriggerEnter/Stay: Box 자식으로 설정
/// - 목적지 도착 시 Box 분리 (텔레포트 직전)
/// </summary>
public class DraggerForLinear : MonoBehaviour
{
    [Header("목적지 설정")]
    [SerializeField] private Transform cwDestination;
    [SerializeField] private Transform ccwDestination;
    
    [Header("도착 감지")]
    [Tooltip("목적지 도착 판정 거리 (스케일에 맞게 조정)")]
    [SerializeField] private float arrivalThreshold = 0.001f;

    [Header("디버그")]
    [SerializeField] private bool showDebugLog = false;

    private GameObject attachedBox = null;
    
    // 프로퍼티
    public bool HasBox => attachedBox != null;
    public bool HasAnyChild => transform.childCount > 0;
    public bool IsAtCwDestination => cwDestination != null && 
        Vector3.Distance(transform.position, cwDestination.position) < arrivalThreshold;
    public bool IsAtCcwDestination => ccwDestination != null && 
        Vector3.Distance(transform.position, ccwDestination.position) < arrivalThreshold;

    /// <summary>
    /// 이동 (Conveyor에서 호출) - 수정된 순회 로직
    /// </summary>
    public void Move(bool isCW, float speed)
    {
        Vector3 targetPos;
        Vector3 teleportPos;
        
        if (isCW)
        {
            targetPos = cwDestination.position;
            teleportPos = ccwDestination.position;
        }
        else
        {
            targetPos = ccwDestination.position;
            teleportPos = cwDestination.position;
        }
        
        // 현재 위치에서 목적지까지의 거리 계산
        Vector3 direction = targetPos - transform.position;
        float distance = direction.magnitude;
        
        // 목적지 도착 체크 (현재 거리 기준!)
        if (distance < arrivalThreshold)
        {
            DetachAllBoxes();  // 먼저 Box 분리
            transform.position = teleportPos;  // 텔레포트
            
            if (showDebugLog)
            {
                string fromTo = isCW ? "CW → CCW" : "CCW → CW";
                Debug.Log($"<color=cyan>[DraggerForLinear]</color> {gameObject.name} {fromTo} 텔레포트");
            }
            return;  // 이번 프레임은 이동 안함
        }
        
        // 이동
        transform.position += direction.normalized * speed * Time.deltaTime;
    }

    /// <summary>
    /// Box가 Dragger 영역에 진입하면 자식으로 설정
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        TryAttachBox(other.gameObject);
    }

    /// <summary>
    /// Box가 Dragger 영역에 머물면 자식으로 유지
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (attachedBox == null)
        {
            TryAttachBox(other.gameObject);
        }
    }

    /// <summary>
    /// 물체 부착 시도 - 영역에 들어온 모든 물체를 자식으로 설정
    /// </summary>
    private void TryAttachBox(GameObject obj)
    {
        if (attachedBox != null) return;
        
        attachedBox = obj;
        obj.transform.SetParent(this.transform);
        
        if (showDebugLog)
        {
            Debug.Log($"<color=green>[DraggerForLinear]</color> {gameObject.name} 물체 부착: {obj.name}");
        }
    }

    /// <summary>
    /// 모든 자식 Box 분리 (텔레포트 전 호출)
    /// </summary>
    private void DetachAllBoxes()
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
        
        // 혹시 남아있는 자식 Box도 모두 분리
        foreach (Transform child in transform)
        {
            if (child.tag.Contains("Box") || child.CompareTag("Box_A") || child.name.Contains("Box"))
            {
                child.SetParent(null);
            }
        }
    }

    /// <summary>
    /// 즉시 CCW 위치로 이동 (초기 위치 리셋용)
    /// </summary>
    public void ResetToCcwPosition()
    {
        if (ccwDestination != null)
        {
            transform.position = ccwDestination.position;
            
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
        }
    }

    void OnDrawGizmosSelected()
    {
        if (cwDestination != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(cwDestination.position, 0.01f);
            Gizmos.DrawLine(transform.position, cwDestination.position);
        }
        if (ccwDestination != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(ccwDestination.position, 0.01f);
        }
    }
}

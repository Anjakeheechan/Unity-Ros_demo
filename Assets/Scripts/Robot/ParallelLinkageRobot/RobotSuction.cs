using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 로봇 End Effector의 석션(Suction) 기능
/// I 키: 석션 활성화 - Box_A 태그 물체를 자식으로 부착
/// O 키: 석션 해제 - 물체를 월드로 분리
/// </summary>
public class RobotSuction : MonoBehaviour
{
    [Header("석션 영역")]
    [Tooltip("석션이 작동하는 Trigger Collider 영역")]
    [SerializeField] private Collider suctionZone;

    [Header("입력 키 설정 (New Input System)")]
    [SerializeField] private Key suctionKey = Key.I;
    [SerializeField] private Key releaseKey = Key.O;

    [Header("대상 태그")]
    [SerializeField] private string targetTag = "Box_A";

    [Header("근거리 검색 설정")]
    [Tooltip("영역 내 물체가 없을 때 근처에서 찾을 검색 반경")]
    [SerializeField] private float searchRadius = 0.5f;
    [Tooltip("근거리 검색 사용 여부")]
    [SerializeField] private bool useNearbySearch = true;

    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;

    [Header("상태")]
    public bool isSuctionActive = false;

    // 현재 석션 영역 내에 있는 물체들
    private List<GameObject> objectsInZone = new List<GameObject>();
    
    // 현재 석션으로 부착된 물체
    private GameObject attachedObject = null;
    private Vector3 attachedLocalPosition;
    private Quaternion attachedLocalRotation;

    void Start()
    {
        if (suctionZone == null)
        {
            // 자신이 Trigger Collider를 가지고 있으면 사용
            suctionZone = GetComponent<Collider>();
        }

        if (suctionZone != null && !suctionZone.isTrigger)
        {
            Debug.LogWarning("[RobotSuction] Suction Zone은 Trigger Collider여야 합니다!");
        }

        if (showDebugLog)
        {
            Debug.Log($"<color=cyan>[RobotSuction]</color> 초기화 완료. 검색 반경: {searchRadius}m");
        }
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // I 키: 석션 활성화
        if (Keyboard.current[suctionKey].wasPressedThisFrame)
        {
            ActivateSuction();
        }

        // O 키: 석션 해제
        if (Keyboard.current[releaseKey].wasPressedThisFrame)
        {
            DeactivateSuction();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            if (!objectsInZone.Contains(other.gameObject))
            {
                objectsInZone.Add(other.gameObject);
                if (showDebugLog)
                {
                    Debug.Log($"<color=cyan>[RobotSuction]</color> 영역 진입: {other.gameObject.name}");
                }
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Stay 중에도 리스트에 추가 (놓친 경우 대비)
        if (other.CompareTag(targetTag) && !objectsInZone.Contains(other.gameObject))
        {
            objectsInZone.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            objectsInZone.Remove(other.gameObject);
            if (showDebugLog)
            {
                Debug.Log($"<color=gray>[RobotSuction]</color> 영역 이탈: {other.gameObject.name}");
            }
        }
    }

    /// <summary>
    /// 석션 활성화 - 영역 내 첫 번째 물체를 자식으로 부착
    /// 영역 내에 없으면 근처에서 검색
    /// </summary>
    public void ActivateSuction()
    {
        if (isSuctionActive && attachedObject != null)
        {
            if (showDebugLog)
            {
                Debug.Log("[RobotSuction] 이미 석션이 활성화되어 있습니다.");
            }
            return;
        }

        // 영역 내에 물체가 있는지 확인
        CleanupNullObjects();

        GameObject targetObject = null;

        if (objectsInZone.Count > 0)
        {
            // 영역 내 물체 사용
            targetObject = objectsInZone[0];
            if (showDebugLog)
            {
                Debug.Log($"<color=green>[RobotSuction]</color> 영역 내 물체 발견: {targetObject.name}");
            }
        }
        else if (useNearbySearch)
        {
            // 근거리 검색
            targetObject = FindNearestBoxA();
            if (targetObject != null && showDebugLog)
            {
                Debug.Log($"<color=yellow>[RobotSuction]</color> 근거리 검색으로 물체 발견: {targetObject.name}");
            }
        }

        if (targetObject != null)
        {
            AttachObject(targetObject);
        }
        else
        {
            if (showDebugLog)
            {
                Debug.LogWarning($"[RobotSuction] 석션 영역({searchRadius}m 반경) 내에 Box_A 물체가 없습니다.");
            }
        }
    }

    /// <summary>
    /// 근처에서 가장 가까운 Box_A 태그 물체 찾기
    /// </summary>
    private GameObject FindNearestBoxA()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, searchRadius);
        
        GameObject nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var col in colliders)
        {
            if (col.CompareTag(targetTag))
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = col.gameObject;
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// 물체를 석션에 부착
    /// </summary>
    private void AttachObject(GameObject obj)
    {
        attachedObject = obj;
        
        // 물체를 자식으로 설정
        Rigidbody rb = attachedObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // 물리 영향 비활성화
            rb.tag = "Untagged";
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 현재 상대 위치/회전 저장
        attachedLocalPosition = transform.InverseTransformPoint(attachedObject.transform.position);
        attachedLocalRotation = Quaternion.Inverse(transform.rotation) * attachedObject.transform.rotation;

        attachedObject.transform.SetParent(this.transform);
        isSuctionActive = true;

        Debug.Log($"<color=green>[RobotSuction]</color> 석션 활성화: {attachedObject.name}");
    }

    /// <summary>
    /// 석션 해제 - 부착된 물체를 월드로 분리
    /// </summary>
    public void DeactivateSuction()
    {
        if (!isSuctionActive || attachedObject == null)
        {
            if (showDebugLog)
            {
                Debug.Log("[RobotSuction] 석션이 활성화되어 있지 않습니다.");
            }
            isSuctionActive = false;
            return;
        }

        // 물체를 월드로 분리
        attachedObject.transform.SetParent(null);

        // 물리 영향 다시 활성화
        Rigidbody rb = attachedObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.tag = "Box_A";
        }

        Debug.Log($"<color=yellow>[RobotSuction]</color> 석션 해제: {attachedObject.name}");

        attachedObject = null;
        isSuctionActive = false;
    }

    /// <summary>
    /// 석션 상태 설정 (티칭 시스템 재생용)
    /// </summary>
    public void SetSuctionState(bool active)
    {
        if (showDebugLog)
        {
            Debug.Log($"<color=magenta>[RobotSuction]</color> SetSuctionState 호출: {active} (현재: {isSuctionActive})");
        }

        if (active && !isSuctionActive)
        {
            ActivateSuction();
        }
        else if (!active && isSuctionActive)
        {
            DeactivateSuction();
        }
    }

    /// <summary>
    /// 현재 석션 상태 반환 (티칭 시스템 기록용)
    /// </summary>
    public bool GetSuctionState()
    {
        return isSuctionActive;
    }

    private void CleanupNullObjects()
    {
        objectsInZone.RemoveAll(obj => obj == null);
    }

    /// <summary>
    /// 현재 부착된 물체 반환
    /// </summary>
    public GameObject GetAttachedObject()
    {
        return attachedObject;
    }

    void OnDrawGizmosSelected()
    {
        // 검색 반경 시각화
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }
}

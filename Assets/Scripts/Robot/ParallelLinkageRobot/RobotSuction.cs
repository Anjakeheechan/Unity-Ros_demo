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
                Debug.Log($"<color=cyan>[RobotSuction]</color> 영역 진입: {other.gameObject.name}");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            objectsInZone.Remove(other.gameObject);
            Debug.Log($"<color=gray>[RobotSuction]</color> 영역 이탈: {other.gameObject.name}");
        }
    }

    /// <summary>
    /// 석션 활성화 - 영역 내 첫 번째 물체를 자식으로 부착
    /// </summary>
    public void ActivateSuction()
    {
        if (isSuctionActive)
        {
            Debug.Log("[RobotSuction] 이미 석션이 활성화되어 있습니다.");
            return;
        }

        // 영역 내에 물체가 있는지 확인
        CleanupNullObjects();

        if (objectsInZone.Count > 0)
        {
            attachedObject = objectsInZone[0];
            
            // 물체를 자식으로 설정
            Rigidbody rb = attachedObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // 물리 영향 비활성화
            }

            // 현재 상대 위치/회전 저장
            attachedLocalPosition = transform.InverseTransformPoint(attachedObject.transform.position);
            attachedLocalRotation = Quaternion.Inverse(transform.rotation) * attachedObject.transform.rotation;

            attachedObject.transform.SetParent(this.transform);
            isSuctionActive = true;

            Debug.Log($"<color=green>[RobotSuction]</color> 석션 활성화: {attachedObject.name}");
        }
        else
        {
            Debug.LogWarning("[RobotSuction] 석션 영역에 물체가 없습니다.");
        }
    }

    /// <summary>
    /// 석션 해제 - 부착된 물체를 월드로 분리
    /// </summary>
    public void DeactivateSuction()
    {
        if (!isSuctionActive || attachedObject == null)
        {
            Debug.Log("[RobotSuction] 석션이 활성화되어 있지 않습니다.");
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
}

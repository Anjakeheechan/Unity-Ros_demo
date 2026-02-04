using UnityEngine;

/// <summary>
/// PLC 없이 동작하는 센서
/// Box_A 태그 물체 감지 시 Conveyor1 정지 및 로봇 티칭 재생
/// </summary>
public class Sensor1 : MonoBehaviour
{
    [Header("연결할 컴포넌트")]
    [Tooltip("감지 시 정지시킬 컨베이어")]
    [SerializeField] private Conveyor1 targetConveyor;
    
    [Tooltip("감지 시 재생할 로봇 티칭 시스템")]
    [SerializeField] private ParallelLinkageRobotTeaching robotTeaching;

    [Header("센서 설정")]
    [SerializeField] private string targetTag = "Box_A";
    [Tooltip("태그 대신 이름에 특정 문자열 포함 여부로 체크")]
    [SerializeField] private bool useNameContains = false;
    [SerializeField] private string targetNameContains = "Box";
    
    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;
    
    [Header("상태")]
    public bool isObjectDetected = false;

    private MeshRenderer meshRenderer;
    private Collider sensorCollider;
    private Color originalColor = new Color(0, 0, 0, 0.7f);
    private Color detectedColor = new Color(1, 0, 0, 0.7f);

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.color = originalColor;
        }

        // Collider 확인
        sensorCollider = GetComponent<Collider>();
        if (sensorCollider == null)
        {
            Debug.LogError("[Sensor1] Collider가 없습니다! Collider를 추가해주세요.");
        }
        else if (!sensorCollider.isTrigger)
        {
            Debug.LogWarning("[Sensor1] Collider가 Trigger가 아닙니다! 'Is Trigger'를 체크해주세요.");
        }
        else if (showDebugLog)
        {
            Debug.Log($"<color=green>[Sensor1]</color> 센서 준비 완료. 대상 태그: {targetTag}");
        }
    }

    private bool IsTargetObject(Collider other)
    {
        if (useNameContains)
        {
            return other.gameObject.name.Contains(targetNameContains);
        }
        else
        {
            return other.CompareTag(targetTag);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (showDebugLog)
        {
            Debug.Log($"<color=blue>[Sensor1]</color> OnTriggerEnter: {other.gameObject.name} (태그: {other.tag})");
        }

        if (IsTargetObject(other))
        {
            isObjectDetected = true;
            
            // 센서 색상 변경
            if (meshRenderer != null)
            {
                meshRenderer.material.color = detectedColor;
            }

            // 컨베이어 정지
            if (targetConveyor != null)
            {
                targetConveyor.StopConveyor();
                Debug.Log("<color=yellow>[Sensor1]</color> Box_A 감지 -> 컨베이어 정지");
            }
            else if (showDebugLog)
            {
                Debug.LogWarning("[Sensor1] Target Conveyor가 연결되지 않았습니다.");
            }

            // 로봇 티칭 재생
            if (robotTeaching != null)
            {
                robotTeaching.PlayWaypoints();
                Debug.Log("<color=cyan>[Sensor1]</color> Box_A 감지 -> 로봇 티칭 재생 시작");
            }
            else if (showDebugLog)
            {
                Debug.LogWarning("[Sensor1] Robot Teaching이 연결되지 않았습니다.");
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Stay 중에도 감지 상태 유지
        if (IsTargetObject(other) && !isObjectDetected)
        {
            OnTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (showDebugLog)
        {
            Debug.Log($"<color=gray>[Sensor1]</color> OnTriggerExit: {other.gameObject.name}");
        }

        if (IsTargetObject(other))
        {
            isObjectDetected = false;
            
            // 센서 색상 복원
            if (meshRenderer != null)
            {
                meshRenderer.material.color = originalColor;
            }

            Debug.Log("<color=green>[Sensor1]</color> Box_A 이탈");
        }
    }

    /// <summary>
    /// 외부에서 컨베이어 재시작 호출
    /// </summary>
    public void ResumeConveyor()
    {
        if (targetConveyor != null && !isObjectDetected)
        {
            targetConveyor.StartConveyor();
            Debug.Log("<color=green>[Sensor1]</color> 컨베이어 재시작");
        }
    }

    void OnDrawGizmos()
    {
        // Scene 뷰에서 센서 영역 시각화
        Gizmos.color = isObjectDetected ? Color.red : Color.green;
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}

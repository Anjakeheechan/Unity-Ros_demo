using UnityEngine;

public class ElevatorController : MonoBehaviour
{
    [Header("Elevator Settings")]
    [SerializeField] private float moveSpeed = 50f;
    [SerializeField] private float floor1Y = 50f;
    [SerializeField] private float floor2Y = 160f;
    [SerializeField] private float floorAgvY = 0f; // AGV 물품 수령 위치

    [Header("Sensor References")]
    [SerializeField] private Transform sensor1; // 센서1 (아래쪽)
    [SerializeField] private Transform sensor2; // 센서2 (위쪽)
    [SerializeField] private string limitBarTag = "Limitbar";

    private float targetY;
    private bool isEmergencyReturning = false;

    private void Start()
    {
        // Initial target is 1st floor
        targetY = floorAgvY;
    }

    private void Update()
    {
        if (isEmergencyReturning)
        {
            ReturnToSensor();
        }
        else
        {
            MoveToTarget();
        }

        // Input examples for testing (User can replace this with actual triggers)
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetFloor(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetFloor(2);
        if (Input.GetKeyDown(KeyCode.Alpha0)) SetFloor(0); // AGV 층
    }

    private void MoveToTarget()
    {
        Vector3 currentPos = transform.localPosition;
        float newY = Mathf.MoveTowards(currentPos.y, targetY, moveSpeed * Time.deltaTime);
        transform.localPosition = new Vector3(currentPos.x, newY, currentPos.z);
    }

    private void ReturnToSensor()
    {
        if (sensor1 == null) return;

        // Move towards sensor1's local position (assuming it's relative to the same parent or world)
        // Adjust based on object hierarchy
        Vector3 currentPos = transform.localPosition;
        Vector3 targetPos = sensor1.localPosition;

        transform.localPosition = Vector3.MoveTowards(currentPos, targetPos, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.localPosition, targetPos) < 0.1f)
        {
            isEmergencyReturning = false;
            targetY = transform.localPosition.y; // Stay at current position
            Debug.Log("Reset to Sensor (1) completed.");
        }
    }

    public void SetFloor(int floor)
    {
        if (isEmergencyReturning) return;

        if (floor == 0) targetY = floorAgvY; // AGV 층
        else if (floor == 1) targetY = floor1Y;
        else if (floor == 2) targetY = floor2Y;
        
        Debug.Log($"Target set to Floor {floor}: Y = {targetY}");
    }

    /// <summary>
    /// 센서2를 사용하여 복귀 (위쪽 센서)
    /// </summary>
    public void ReturnToSensor2()
    {
        if (sensor2 == null)
        {
            Debug.LogWarning("Sensor2 is not assigned!");
            return;
        }
        targetY = sensor2.localPosition.y;
        Debug.Log($"Returning to Sensor2 position: Y = {targetY}");
    }

    /// <summary>
    /// 현재 사용 중인 센서 가져오기 (sensor1 또는 sensor2)
    /// </summary>
    public Transform GetSensor1() => sensor1;
    public Transform GetSensor2() => sensor2;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(limitBarTag))
        {
            Debug.LogWarning("Limitbar detected! Emergency return initiated.");
            isEmergencyReturning = true;
        }
    }

    // For OnCollisionEnter if the sensor uses physics collision instead of triggers
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(limitBarTag))
        {
            Debug.LogWarning("Limitbar collided! Emergency return initiated.");
            isEmergencyReturning = true;
        }
    }
}

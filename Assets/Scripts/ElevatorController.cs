using UnityEngine;

public class ElevatorController : MonoBehaviour
{
    [Header("Elevator Settings")]
    [SerializeField] private float moveSpeed = 50f;
    [SerializeField] private float floor1Y = 50f;
    [SerializeField] private float floor2Y = 160f;

    [Header("Sensor References")]
    [SerializeField] private Transform sensor1; // Return position (Origin)
    [SerializeField] private string limitBarTag = "Limitbar";

    private float targetY;
    private bool isEmergencyReturning = false;

    private void Start()
    {
        // Initial target is 1st floor
        targetY = floor1Y;
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

        if (floor == 1) targetY = floor1Y;
        else if (floor == 2) targetY = floor2Y;
        
        Debug.Log($"Target set to Floor {floor}: Y = {targetY}");
    }

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

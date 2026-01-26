using UnityEngine;
using System.Collections.Generic;

public class RobotArmController : MonoBehaviour
{
    public enum DriverType
    {
        Base,
        Shoulder,
        Elbow,
        EndEffector
    }

    [System.Serializable]
    public class RobotJoint
    {
        public Transform jointTransform;
        public Vector3 rotationAxis = Vector3.up; // Default to Y
        public float minAngle = -90f;
        public float maxAngle = 90f;
        
        [Range(-180f, 180f)]
        public float inputAngle; // Inspector input

        [HideInInspector]
        public Quaternion initialRotation;
        [HideInInspector]
        public float effectiveAngle; // Actually applied angle
    }

    [System.Serializable]
    public class PassiveJoint
    {
        public string name; // For Inspector readability
        public Transform targetTransform;
        public Vector3 rotationAxis = Vector3.right; // Default to X
        public DriverType driver;
        public float multiplier = 1.0f;
        public float offset = 0f;

        [HideInInspector]
        public Quaternion initialRotation;
    }

    [Header("Main Joints (3-DOF + Parallel Linkage)")]
    public RobotJoint baseJoint;
    public RobotJoint shoulderJoint;
    public RobotJoint elbowJoint;

    [Header("End Effector (Passive - Horizontal Compensation)")]
    [Tooltip("엔드 이펙터는 패시브 조인트로, Shoulder+Elbow 각도를 상쇄하여 수평 유지")]
    public RobotJoint endEffectorJoint;

    [Header("Coupling Settings")]
    public bool coupleElbowToShoulder = true;
    public float elbowCouplingRatio = 1.0f;

    [Header("Parallel Linkage Settings")]
    [Tooltip("활성화 시 엔드 이펙터가 자동으로 수평 유지 (Shoulder + Elbow 보정)")]
    public bool enableParallelLinkage = true;
    [Tooltip("추가 오프셋 각도 (미세 조정용)")]
    public float endEffectorOffset = 0f;

    [Header("Passive/Parallel Joints")]
    public List<PassiveJoint> passiveJoints = new List<PassiveJoint>();

    void Start()
    {
        InitializeMainJoint(baseJoint);
        InitializeMainJoint(shoulderJoint);
        InitializeMainJoint(elbowJoint);
        InitializeMainJoint(endEffectorJoint);

        foreach (var pj in passiveJoints)
        {
            if (pj.targetTransform != null)
            {
                pj.initialRotation = pj.targetTransform.localRotation;
                // Normalize axis
                if (pj.rotationAxis == Vector3.zero) pj.rotationAxis = Vector3.up;
                pj.rotationAxis.Normalize();
            }
        }
    }

    void InitializeMainJoint(RobotJoint joint)
    {
        if (joint.jointTransform != null)
        {
            joint.initialRotation = joint.jointTransform.localRotation;
            // Normalize axis
            if (joint.rotationAxis == Vector3.zero) joint.rotationAxis = Vector3.up;
            joint.rotationAxis.Normalize();
        }
    }

    void Update()
    {
        // 1. Base (수평 회전)
        UpdateBase();

        // 2. Shoulder (팔 확장/수축)
        UpdateShoulder();

        // 3. Elbow (depends on Shoulder if coupled)
        UpdateElbow();

        // 4. EndEffector (패시브 - 평행 링키지로 수평 유지)
        UpdateEndEffector();

        // 5. Passive Joints (depend on their drivers)
        UpdatePassiveJoints();
    }

    void UpdateBase()
    {
        if (baseJoint.jointTransform == null) return;

        // Clamp & Calculate
        float angle = Mathf.Clamp(baseJoint.inputAngle, baseJoint.minAngle, baseJoint.maxAngle);
        baseJoint.effectiveAngle = angle;

        // Apply
        ApplyRotation(baseJoint.jointTransform, baseJoint.initialRotation, baseJoint.rotationAxis, baseJoint.effectiveAngle);
    }

    void UpdateShoulder()
    {
        if (shoulderJoint.jointTransform == null) return;

        // Clamp & Calculate
        float angle = Mathf.Clamp(shoulderJoint.inputAngle, shoulderJoint.minAngle, shoulderJoint.maxAngle);
        shoulderJoint.effectiveAngle = angle;

        // Apply
        ApplyRotation(shoulderJoint.jointTransform, shoulderJoint.initialRotation, shoulderJoint.rotationAxis, shoulderJoint.effectiveAngle);
    }

    void UpdateElbow()
    {
        if (elbowJoint.jointTransform == null) return;

        // Calculate coupling offset
        float couplingOffset = 0f;
        if (coupleElbowToShoulder)
        {
            couplingOffset = shoulderJoint.effectiveAngle * elbowCouplingRatio;
        }

        // Target angle is Input + Offset
        // We clamp the RESULTING effective angle (or should we clamp the input? 
        // User asked: "elbow clamp is applied to effective". OK.)
        float rawTarget = elbowJoint.inputAngle + couplingOffset;
        
        float angle = Mathf.Clamp(rawTarget, elbowJoint.minAngle, elbowJoint.maxAngle);
        elbowJoint.effectiveAngle = angle;

        // Apply
        ApplyRotation(elbowJoint.jointTransform, elbowJoint.initialRotation, elbowJoint.rotationAxis, elbowJoint.effectiveAngle);
    }

    /// <summary>
    /// 엔드 이펙터 업데이트 - Four-bar 링키지로 수평 유지
    /// Shoulder + Elbow 각도를 상쇄하여 항상 수평 유지
    /// </summary>
    void UpdateEndEffector()
    {
        if (endEffectorJoint.jointTransform == null) return;

        float angle;
        
        if (enableParallelLinkage)
        {
            // 평행 링키지: Shoulder + Elbow의 역방향으로 보정하여 수평 유지
            // 공식: EndEffector = -(Shoulder + Elbow) + offset
            float compensationAngle = -(shoulderJoint.effectiveAngle + elbowJoint.effectiveAngle);
            angle = compensationAngle + endEffectorOffset;
        }
        else
        {
            // 평행 링키지 비활성화 시 수동 입력 사용
            angle = Mathf.Clamp(endEffectorJoint.inputAngle, endEffectorJoint.minAngle, endEffectorJoint.maxAngle);
        }
        
        endEffectorJoint.effectiveAngle = angle;

        // Apply
        ApplyRotation(endEffectorJoint.jointTransform, endEffectorJoint.initialRotation, endEffectorJoint.rotationAxis, endEffectorJoint.effectiveAngle);
    }

    void UpdatePassiveJoints()
    {
        foreach (var pj in passiveJoints)
        {
            if (pj.targetTransform == null) continue;

            float driverAngle = 0f;
            switch (pj.driver)
            {
                case DriverType.Base:
                    driverAngle = baseJoint.effectiveAngle;
                    break;
                case DriverType.Shoulder:
                    driverAngle = shoulderJoint.effectiveAngle;
                    break;
                case DriverType.Elbow:
                    driverAngle = elbowJoint.effectiveAngle;
                    break;
                case DriverType.EndEffector:
                    driverAngle = endEffectorJoint.effectiveAngle;
                    break;
            }

            // Calculate passive angle
            float finalAngle = (driverAngle * pj.multiplier) + pj.offset;

            // Apply
            ApplyRotation(pj.targetTransform, pj.initialRotation, pj.rotationAxis, finalAngle);
        }
    }

    void ApplyRotation(Transform t, Quaternion initialRot, Vector3 axis, float angle)
    {
        // Use AngleAxis for stability as requested
        t.localRotation = initialRot * Quaternion.AngleAxis(angle, axis);
    }

    void OnValidate()
    {
        // Optional: Warn if limits are zero (which means it won't move)
        // CheckMainJoint("Base", baseJoint);
        // CheckMainJoint("Shoulder", shoulderJoint);
        // CheckMainJoint("Elbow", elbowJoint);
    }
}

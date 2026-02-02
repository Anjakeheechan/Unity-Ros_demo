using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Parallel Linkage Robot Hierarchy 재구성 도구
/// 3가지 구조 중 선택하여 자동 설정
/// </summary>
public class ParallelLinkageRestructureTool : MonoBehaviour
{
    public enum StructureType
    {
        [Tooltip("ArticulationBody + Kinematic 동기화 (권장 - 간단)")]
        A_ArticulationKinematic,
        
        [Tooltip("ArticulationBody + ConfigurableJoint 하이브리드")]
        B_Hybrid,
        
        [Tooltip("전체 Rigidbody + HingeJoint (폐루프 물리)")]
        C_RigidbodyOnly
    }

    [Header("=== 구조 선택 ===")]
    public StructureType selectedStructure = StructureType.A_ArticulationKinematic;

    [Header("=== 현재 Transform 참조 ===")]
    public Transform robotRoot;
    public Transform t_Base;
    public Transform t_Waist;
    public Transform t_BigArm;
    public Transform t_TriangleBracket;
    public Transform t_Forearm;
    public Transform t_Wrist;
    public Transform t_ParallelArm;
    public Transform t_DriveLink;
    public Transform t_ParallelLinkBig;
    public Transform t_ParallelLinkForearm;

    [Header("=== 물리 설정 ===")]
    public float baseMass = 10f;
    public float linkMass = 1f;
    public bool useGravity = true;
    
    [Header("=== 모터 설정 ===")]
    public float motorForce = 1000f;
    public float motorDamping = 100f;

    [Header("=== 링크 길이 (자동 계산됨) ===")]
    [SerializeField] private float len_BigArm;
    [SerializeField] private float len_Forearm;
    [SerializeField] private float len_ParallelArm;
    [SerializeField] private float len_ParallelBig;

    // 원본 부모 저장 (복원용)
    private Dictionary<Transform, Transform> originalParents = new Dictionary<Transform, Transform>();
    private Dictionary<Transform, Vector3> originalLocalPos = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Quaternion> originalLocalRot = new Dictionary<Transform, Quaternion>();

    [ContextMenu("1. Find All Transforms")]
    public void FindAllTransforms()
    {
        if (robotRoot == null) robotRoot = transform;

        var all = robotRoot.GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            string n = t.name.ToLower();
            if (n.Contains("10_base") || n.Contains("base")) t_Base = t;
            else if (n.Contains("09_waist") || n.Contains("waist")) t_Waist = t;
            else if (n.Contains("01_bigarm") || n.Contains("bigarm")) t_BigArm = t;
            else if (n.Contains("07") || n.Contains("triangle")) t_TriangleBracket = t;
            else if (n.Contains("02_forearm")) t_Forearm = t;
            else if (n.Contains("08_wrist")) t_Wrist = t;
            else if (n.Contains("03_parallelarm")) t_ParallelArm = t;
            else if (n.Contains("04_drivelink")) t_DriveLink = t;
            else if (n.Contains("05_parallellink_big")) t_ParallelLinkBig = t;
            else if (n.Contains("06_parallellink_forearm")) t_ParallelLinkForearm = t;
        }

        SaveOriginalHierarchy();
        CalculateLinkLengths();
        Debug.Log("Transform 검색 및 원본 저장 완료!");
    }

    void SaveOriginalHierarchy()
    {
        originalParents.Clear();
        originalLocalPos.Clear();
        originalLocalRot.Clear();

        Transform[] all = { t_Base, t_Waist, t_BigArm, t_TriangleBracket, t_Forearm, 
                           t_Wrist, t_ParallelArm, t_DriveLink, t_ParallelLinkBig, t_ParallelLinkForearm };

        foreach (var t in all)
        {
            if (t == null) continue;
            originalParents[t] = t.parent;
            originalLocalPos[t] = t.localPosition;
            originalLocalRot[t] = t.localRotation;
        }
    }

    void CalculateLinkLengths()
    {
        if (t_BigArm && t_Forearm)
            len_BigArm = Vector3.Distance(t_BigArm.position, t_Forearm.position);
        if (t_Forearm && t_Wrist)
            len_Forearm = Vector3.Distance(t_Forearm.position, t_Wrist.position);
        if (t_ParallelArm && t_DriveLink)
            len_ParallelArm = Vector3.Distance(t_ParallelArm.position, t_DriveLink.position);
        if (t_Waist && t_ParallelLinkBig)
            len_ParallelBig = Vector3.Distance(t_Waist.position, t_ParallelLinkBig.position);

        Debug.Log($"링크 길이 - BigArm: {len_BigArm:F3}, Forearm: {len_Forearm:F3}, " +
                  $"ParallelArm: {len_ParallelArm:F3}, ParallelBig: {len_ParallelBig:F3}");
    }

    [ContextMenu("2. Apply Selected Structure")]
    public void ApplySelectedStructure()
    {
        // 기존 컴포넌트 제거
        ClearAllPhysicsComponents();

        switch (selectedStructure)
        {
            case StructureType.A_ArticulationKinematic:
                ApplyStructureA();
                break;
            case StructureType.B_Hybrid:
                ApplyStructureB();
                break;
            case StructureType.C_RigidbodyOnly:
                ApplyStructureC();
                break;
        }

        Debug.Log($"구조 {selectedStructure} 적용 완료!");
    }

    void ClearAllPhysicsComponents()
    {
        Transform[] all = { t_Base, t_Waist, t_BigArm, t_TriangleBracket, t_Forearm, 
                           t_Wrist, t_ParallelArm, t_DriveLink, t_ParallelLinkBig, t_ParallelLinkForearm };

        foreach (var t in all)
        {
            if (t == null) continue;
            
            // Joint 먼저 제거
            foreach (var j in t.GetComponents<Joint>()) DestroyImmediate(j);
            
            // ArticulationBody 제거
            var ab = t.GetComponent<ArticulationBody>();
            if (ab) DestroyImmediate(ab);
            
            // Rigidbody 제거
            var rb = t.GetComponent<Rigidbody>();
            if (rb) DestroyImmediate(rb);
        }
    }

    #region Structure A: ArticulationBody + Kinematic
    void ApplyStructureA()
    {
        Debug.Log("=== 구조 A: ArticulationBody + Kinematic 동기화 ===");

        // Hierarchy 재구성 (필요시)
        // 메인 체인: Base → Waist → BigArm → Forearm → Wrist
        // ParallelArm은 Waist 아래
        // Parallel Links는 별도 분리

        // === ArticulationBody 설정 ===
        
        // Base (Root)
        var ab_Base = AddArticulationBody(t_Base);
        ab_Base.jointType = ArticulationJointType.FixedJoint;
        ab_Base.immovable = true;
        ab_Base.mass = baseMass;

        // Motor 1: Waist
        SetupArticulationMotor(t_Waist, new Vector2(-180, 180), linkMass * 2);

        // Motor 2: BigArm  
        SetupArticulationMotor(t_BigArm, new Vector2(-45, 90), linkMass * 1.5f);

        // Triangle Bracket - Fixed to BigArm
        var ab_Tri = AddArticulationBody(t_TriangleBracket);
        if (ab_Tri != null)
        {
            ab_Tri.jointType = ArticulationJointType.FixedJoint;
            ab_Tri.mass = linkMass * 0.3f;
        }

        // Forearm - Passive (동기화됨)
        SetupArticulationPassive(t_Forearm, linkMass);

        // Wrist - Passive
        SetupArticulationPassive(t_Wrist, linkMass * 0.5f);

        // Motor 3: ParallelArm
        SetupArticulationMotor(t_ParallelArm, new Vector2(-45, 90), linkMass);

        // === Parallel Links - ArticulationBody 없이 Transform만 ===
        // 이들은 스크립트로 위치/회전 동기화
        
        // DriveLink, ParallelLinkBig, ParallelLinkForearm은 
        // ArticulationBody 체인에서 제외하고 별도 처리
        
        Debug.Log("구조 A: Parallel Links는 컨트롤러에서 kinematic 동기화됩니다.");
    }

    ArticulationBody AddArticulationBody(Transform t)
    {
        if (t == null) return null;
        var ab = t.GetComponent<ArticulationBody>();
        if (ab == null) ab = t.gameObject.AddComponent<ArticulationBody>();
        ab.useGravity = useGravity;
        return ab;
    }

    void SetupArticulationMotor(Transform t, Vector2 limits, float mass)
    {
        var ab = AddArticulationBody(t);
        if (ab == null) return;

        ab.jointType = ArticulationJointType.RevoluteJoint;
        ab.twistLock = ArticulationDofLock.LimitedMotion;
        ab.anchorRotation = Quaternion.identity;
        ab.mass = mass;

        var drive = ab.xDrive;
        drive.lowerLimit = limits.x;
        drive.upperLimit = limits.y;
        drive.stiffness = 100000f;
        drive.damping = 10000f;
        drive.forceLimit = motorForce;
        ab.xDrive = drive;
    }

    void SetupArticulationPassive(Transform t, float mass)
    {
        var ab = AddArticulationBody(t);
        if (ab == null) return;

        ab.jointType = ArticulationJointType.RevoluteJoint;
        ab.twistLock = ArticulationDofLock.FreeMotion;
        ab.anchorRotation = Quaternion.identity;
        ab.mass = mass;

        var drive = ab.xDrive;
        drive.stiffness = 50000f;
        drive.damping = 5000f;
        drive.forceLimit = motorForce * 0.5f;
        ab.xDrive = drive;
    }
    #endregion

    #region Structure B: Hybrid
    void ApplyStructureB()
    {
        Debug.Log("=== 구조 B: ArticulationBody + ConfigurableJoint 하이브리드 ===");

        // 메인 체인은 ArticulationBody
        ApplyStructureA(); // 기본 설정 재사용

        // Parallel Links는 Rigidbody + ConfigurableJoint
        SetupParallelLinksWithJoints();
    }

    void SetupParallelLinksWithJoints()
    {
        // ParallelLinkBig: Waist에 Hinge 연결
        var rb_PLB = AddRigidbody(t_ParallelLinkBig, linkMass);
        if (rb_PLB != null && t_Waist != null)
        {
            var hinge = t_ParallelLinkBig.gameObject.AddComponent<HingeJoint>();
            hinge.connectedBody = t_Waist.GetComponent<Rigidbody>(); // Waist에 RB 필요
            hinge.axis = Vector3.forward;
            hinge.anchor = Vector3.zero;
        }

        // DriveLink: ParallelArm 끝에 연결
        var rb_DL = AddRigidbody(t_DriveLink, linkMass * 0.5f);
        if (rb_DL != null && t_ParallelArm != null)
        {
            var hinge = t_DriveLink.gameObject.AddComponent<HingeJoint>();
            hinge.connectedBody = t_ParallelArm.GetComponent<Rigidbody>();
            hinge.axis = Vector3.forward;
        }

        // ParallelLinkForearm: Forearm에 연결 + ParallelLinkBig에 연결 (폐루프)
        var rb_PLF = AddRigidbody(t_ParallelLinkForearm, linkMass * 0.5f);
        if (rb_PLF != null)
        {
            // Forearm 연결
            if (t_Forearm != null)
            {
                var hinge1 = t_ParallelLinkForearm.gameObject.AddComponent<HingeJoint>();
                hinge1.connectedBody = t_Forearm.GetComponent<Rigidbody>();
                hinge1.axis = Vector3.forward;
            }

            // ParallelLinkBig 연결 (폐루프 형성)
            if (t_ParallelLinkBig != null)
            {
                var hinge2 = t_ParallelLinkForearm.gameObject.AddComponent<HingeJoint>();
                hinge2.connectedBody = rb_PLB;
                hinge2.axis = Vector3.forward;
                // anchor 위치는 링크 끝점으로 설정 필요
            }
        }

        Debug.Log("구조 B: Parallel Links에 HingeJoint 연결 완료. Anchor 위치 조정 필요!");
    }

    Rigidbody AddRigidbody(Transform t, float mass)
    {
        if (t == null) return null;
        var rb = t.GetComponent<Rigidbody>();
        if (rb == null) rb = t.gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = useGravity;
        return rb;
    }
    #endregion

    #region Structure C: Full Rigidbody
    void ApplyStructureC()
    {
        Debug.Log("=== 구조 C: 전체 Rigidbody + HingeJoint ===");

        // Base - Kinematic
        var rb_Base = AddRigidbody(t_Base, baseMass);
        rb_Base.isKinematic = true;

        // Waist - Motor 1
        var rb_Waist = AddRigidbody(t_Waist, linkMass * 2);
        SetupHingeMotor(t_Waist, rb_Base, true);

        // BigArm - Motor 2
        var rb_BigArm = AddRigidbody(t_BigArm, linkMass * 1.5f);
        SetupHingeMotor(t_BigArm, rb_Waist, true);

        // TriangleBracket - Fixed to BigArm
        var rb_Tri = AddRigidbody(t_TriangleBracket, linkMass * 0.3f);
        if (t_TriangleBracket != null)
        {
            var fj = t_TriangleBracket.gameObject.AddComponent<FixedJoint>();
            fj.connectedBody = rb_BigArm;
        }

        // Forearm
        var rb_Forearm = AddRigidbody(t_Forearm, linkMass);
        SetupHingeMotor(t_Forearm, rb_Tri, false);

        // Wrist
        var rb_Wrist = AddRigidbody(t_Wrist, linkMass * 0.5f);
        SetupHingeMotor(t_Wrist, rb_Forearm, false);

        // ParallelArm - Motor 3
        var rb_ParallelArm = AddRigidbody(t_ParallelArm, linkMass);
        SetupHingeMotor(t_ParallelArm, rb_Waist, true);

        // Parallel Links with closed loop
        SetupParallelLinksWithJoints();

        Debug.Log("구조 C 완료. HingeJoint.motor로 제어하세요.");
    }

    void SetupHingeMotor(Transform t, Rigidbody connectedRB, bool useMotor)
    {
        if (t == null) return;

        var hinge = t.gameObject.AddComponent<HingeJoint>();
        hinge.connectedBody = connectedRB;
        hinge.axis = Vector3.forward; // 로컬 Z축
        hinge.anchor = Vector3.zero;

        if (useMotor)
        {
            hinge.useMotor = true;
            var motor = hinge.motor;
            motor.force = motorForce;
            motor.targetVelocity = 0;
            motor.freeSpin = false;
            hinge.motor = motor;
        }
    }
    #endregion

    [ContextMenu("3. Attach Appropriate Controller")]
    public void AttachController()
    {
        switch (selectedStructure)
        {
            case StructureType.A_ArticulationKinematic:
            case StructureType.B_Hybrid:
                AttachArticulationController();
                break;
            case StructureType.C_RigidbodyOnly:
                AttachRigidbodyController();
                break;
        }
    }

    void AttachArticulationController()
    {
        var ctrl = robotRoot.GetComponent<ParallelLinkageRobotController>();
        if (ctrl == null)
            ctrl = robotRoot.gameObject.AddComponent<ParallelLinkageRobotController>();

        // Motor joints
        ctrl.motor1_Waist = t_Waist?.GetComponent<ArticulationBody>();
        ctrl.motor2_BigArm = t_BigArm?.GetComponent<ArticulationBody>();
        ctrl.motor3_ParallelArm = t_ParallelLinkBig?.GetComponent<ArticulationBody>();

        // Passive joints
        ctrl.parallelLinkBig = t_ParallelArm?.GetComponent<ArticulationBody>();
        ctrl.triangleBracket = t_TriangleBracket?.GetComponent<ArticulationBody>();
        ctrl.forearm = t_Forearm?.GetComponent<ArticulationBody>();
        ctrl.wrist = t_Wrist?.GetComponent<ArticulationBody>();
        ctrl.driveLink = t_DriveLink?.GetComponent<ArticulationBody>();
        ctrl.parallelLinkForearm = t_ParallelLinkForearm?.GetComponent<ArticulationBody>();

        // Kinematic 동기화용 Transform도 연결
        var sync = robotRoot.GetComponent<ParallelLinkageKinematicSync>();
        if (sync == null)
            sync = robotRoot.gameObject.AddComponent<ParallelLinkageKinematicSync>();

        sync.t_BigArm = t_BigArm;
        sync.t_Forearm = t_Forearm;
        sync.t_ParallelArm = t_ParallelArm;
        sync.t_DriveLink = t_DriveLink;
        sync.t_ParallelLinkBig = t_ParallelLinkBig;
        sync.t_ParallelLinkForearm = t_ParallelLinkForearm;
        sync.t_TriangleBracket = t_TriangleBracket;

        Debug.Log("ArticulationBody Controller 연결 완료!");
    }

    void AttachRigidbodyController()
    {
        // 구조 C는 HingeJoint.motor로 직접 제어
        // 필요시 별도 컨트롤러 작성
        Debug.Log("구조 C: HingeJoint는 스크립트에서 motor.targetVelocity로 제어하세요.");
    }

    [ContextMenu("Restore Original Hierarchy")]
    public void RestoreOriginalHierarchy()
    {
        foreach (var kvp in originalParents)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Key.SetParent(kvp.Value);
                kvp.Key.localPosition = originalLocalPos[kvp.Key];
                kvp.Key.localRotation = originalLocalRot[kvp.Key];
            }
        }
        Debug.Log("원본 Hierarchy 복원 완료!");
    }

    [ContextMenu("Run All Steps")]
    public void RunAll()
    {
        FindAllTransforms();
        ApplySelectedStructure();
        AttachController();
        Debug.Log($"=== 전체 설정 완료! (구조: {selectedStructure}) ===");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ParallelLinkageRestructureTool))]
public class ParallelLinkageRestructureToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tool = (ParallelLinkageRestructureTool)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("=== Setup Steps ===", EditorStyles.boldLabel);

        if (GUILayout.Button("1. Find All Transforms", GUILayout.Height(28)))
            tool.FindAllTransforms();

        EditorGUILayout.Space(5);
        
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("2. Apply Selected Structure", GUILayout.Height(28)))
            tool.ApplySelectedStructure();
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("3. Attach Controller", GUILayout.Height(28)))
            tool.AttachController();

        EditorGUILayout.Space(10);

        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("▶ Run All Steps", GUILayout.Height(35)))
            tool.RunAll();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("↩ Restore Original Hierarchy", GUILayout.Height(25)))
            tool.RestoreOriginalHierarchy();
        GUI.backgroundColor = Color.white;
    }
}
#endif
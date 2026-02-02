using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ParallelLinkageRobotSetup : MonoBehaviour
{
    [Header("=== Auto Setup ===")]
    [Tooltip("플레이 시작할 때 자동으로 설정 실행")]
    public bool runOnStart = true;

    [Header("=== Robot Root ===")]
    public Transform robotRoot;

    [Header("=== Found Transforms ===")]
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

    [Header("=== Physics Settings ===")]
    public float baseMass = 10f;
    public float linkMass = 1f;
    public bool useGravity = true;

    public enum Axis { X, Y, Z }

    void Start()
    {
        if (runOnStart)
        {
            RunAll();
            Debug.Log("플레이 시작 - 자동 설정 완료!");
        }
    }

    // ========== STEP 1 ==========
    [ContextMenu("Step 1: Find Transforms")]
    public void FindTransforms()
    {
        if (robotRoot == null)
            robotRoot = transform;

        var all = robotRoot.GetComponentsInChildren<Transform>(true);

        foreach (var t in all)
        {
            string n = t.name;

            if (n.Contains("10_Base") || n == "10_Base") t_Base = t;
            else if (n.Contains("09_Waist")) t_Waist = t;
            else if (n.Contains("01_BigArm")) t_BigArm = t;
            else if (n.Contains("07") && n.Contains("Triangle")) t_TriangleBracket = t;
            else if (n.Contains("02_Forearm")) t_Forearm = t;
            else if (n.Contains("08_Wrist")) t_Wrist = t;
            else if (n.Contains("03_ParallelArm")) t_ParallelArm = t;
            else if (n.Contains("04_DriveLink")) t_DriveLink = t;
            else if (n.Contains("05_ParallelLink_Big")) t_ParallelLinkBig = t;
            else if (n.Contains("06_ParallelLink_Forearm")) t_ParallelLinkForearm = t;
        }

        Debug.Log("Step 1 완료: Transform 검색 완료!");
    }

    // ========== STEP 2 ==========
    [ContextMenu("Step 2: Setup ArticulationBodies")]
    public void SetupArticulationBodies()
    {
        // 기존 ArticulationBody 전부 삭제
        RemoveAllArticulationBodies();

        // Base (Root - Immovable)
        var ab_Base = AddAB(t_Base);
        if (ab_Base != null)
        {
            ab_Base.jointType = ArticulationJointType.FixedJoint;
            ab_Base.immovable = true;
            ab_Base.mass = baseMass;
        }

        // Motor 1: Waist (Y축 회전)
        SetupMotorJoint(t_Waist, new Vector2(-180, 180), linkMass * 2f, Axis.Y);

        // Motor 2: BigArm (Z축 회전)
        SetupMotorJoint(t_BigArm, new Vector2(-45, 90), linkMass * 1.5f, Axis.Z);

        // Triangle Bracket (Passive, Z축) - ParallelLink_Big과 연동
        SetupPassiveJoint(t_TriangleBracket, linkMass * 0.5f, Axis.Z);

        // Forearm (Passive, Z축)
        SetupPassiveJoint(t_Forearm, linkMass, Axis.Z);

        // Wrist (Passive, Z축)
        SetupPassiveJoint(t_Wrist, linkMass * 0.5f, Axis.Z);

        // Motor 3: ParallelLink_Big (Z축 회전) - Forearm 각도 조절
        SetupMotorJoint(t_ParallelLinkBig, new Vector2(-45, 90), linkMass, Axis.Z);

        // ParallelArm (Passive, Z축) - BigArm과 평행 연동
        SetupPassiveJoint(t_ParallelArm, linkMass, Axis.Z);

        // DriveLink (Passive, Z축)
        SetupPassiveJoint(t_DriveLink, linkMass * 0.5f, Axis.Z);

        // ParallelLink_Forearm (Passive, Z축)
        SetupPassiveJoint(t_ParallelLinkForearm, linkMass * 0.5f, Axis.Z);

        Debug.Log("Step 2 완료: ArticulationBody 설정 완료!");
    }

    void RemoveAllArticulationBodies()
    {
        Transform[] parts = { t_ParallelLinkForearm, t_ParallelLinkBig, t_DriveLink,
                              t_ParallelArm, t_Wrist, t_Forearm, t_TriangleBracket,
                              t_BigArm, t_Waist, t_Base };

        foreach (var t in parts)
        {
            if (t == null) continue;
            var ab = t.GetComponent<ArticulationBody>();
            if (ab != null)
                DestroyImmediate(ab);
        }

        Debug.Log("기존 ArticulationBody 전부 삭제 완료");
    }

    ArticulationBody AddAB(Transform t)
    {
        if (t == null) return null;
        var ab = t.gameObject.AddComponent<ArticulationBody>();
        ab.useGravity = useGravity;
        return ab;
    }

    Quaternion GetAnchorRotation(Axis axis)
    {
        switch (axis)
        {
            case Axis.Y: return Quaternion.Euler(0, 0, 90);
            case Axis.Z: return Quaternion.Euler(0, -90, 0);
            default: return Quaternion.identity;
        }
    }

    void SetupMotorJoint(Transform t, Vector2 limits, float mass, Axis axis)
    {
        var ab = AddAB(t);
        if (ab == null) return;

        ab.jointType = ArticulationJointType.RevoluteJoint;
        ab.twistLock = ArticulationDofLock.LimitedMotion;
        ab.anchorRotation = GetAnchorRotation(axis);
        ab.mass = mass;

        var drive = ab.xDrive;
        drive.lowerLimit = limits.x;
        drive.upperLimit = limits.y;
        drive.stiffness = 100000f;
        drive.damping = 10000f;
        drive.forceLimit = 1000f;
        ab.xDrive = drive;
    }

    void SetupPassiveJoint(Transform t, float mass, Axis axis)
    {
        var ab = AddAB(t);
        if (ab == null) return;

        ab.jointType = ArticulationJointType.RevoluteJoint;
        ab.twistLock = ArticulationDofLock.FreeMotion;
        ab.anchorRotation = GetAnchorRotation(axis);
        ab.mass = mass;

        var drive = ab.xDrive;
        drive.stiffness = 50000f;
        drive.damping = 5000f;
        drive.forceLimit = 500f;
        ab.xDrive = drive;
    }

    // ========== STEP 3 ==========
    [ContextMenu("Step 3: Attach Controller")]
    public void AttachController()
    {
        var ctrl = robotRoot.GetComponent<ParallelLinkageRobotController>();
        if (ctrl == null)
            ctrl = robotRoot.gameObject.AddComponent<ParallelLinkageRobotController>();

        // Motor joints
        ctrl.motor1_Waist = t_Waist?.GetComponent<ArticulationBody>();
        ctrl.motor2_BigArm = t_BigArm?.GetComponent<ArticulationBody>();
        ctrl.motor3_ParallelArm = t_ParallelArm?.GetComponent<ArticulationBody>();

        // Passive joints
        ctrl.driveLink = t_DriveLink?.GetComponent<ArticulationBody>();
        ctrl.parallelLinkBig = t_ParallelLinkBig?.GetComponent<ArticulationBody>();
        ctrl.triangleBracket = t_TriangleBracket?.GetComponent<ArticulationBody>();
        ctrl.forearm = t_Forearm?.GetComponent<ArticulationBody>();
        ctrl.wrist = t_Wrist?.GetComponent<ArticulationBody>();
        ctrl.parallelLinkForearm = t_ParallelLinkForearm?.GetComponent<ArticulationBody>();

        Debug.Log("Step 3 완료: Controller 연결 완료!");

#if UNITY_EDITOR
        EditorUtility.SetDirty(ctrl);
#endif
    }

    // ========== RUN ALL ==========
    [ContextMenu("Run All Steps")]
    public void RunAll()
    {
        FindTransforms();
        SetupArticulationBodies();
        AttachController();
        Debug.Log("=== 전체 설정 완료! ===");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ParallelLinkageRobotSetup))]
public class ParallelLinkageRobotSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var setup = (ParallelLinkageRobotSetup)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("=== Setup Steps ===", EditorStyles.boldLabel);

        if (GUILayout.Button("1. Find Transforms", GUILayout.Height(30)))
            setup.FindTransforms();

        if (GUILayout.Button("2. Setup ArticulationBodies", GUILayout.Height(30)))
            setup.SetupArticulationBodies();

        if (GUILayout.Button("3. Attach Controller", GUILayout.Height(30)))
            setup.AttachController();

        EditorGUILayout.Space(10);

        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("Run All Steps", GUILayout.Height(40)))
            setup.RunAll();
        GUI.backgroundColor = Color.white;
    }
}
#endif
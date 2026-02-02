using UnityEngine;

/// <summary>
/// Parallel Linkage Robot Arm Controller (3 DOF)
/// 
/// Motor 1 (Waist): 전체 Y축 회전
/// Motor 2 (BigArm): 팔 전체 상하, ParallelArm 평행 연동
/// Motor 3 (ParallelLink_Big): Forearm 각도 조절
/// </summary>
public class ParallelLinkageRobotController : MonoBehaviour
{
    [Header("=== Motor Joints (능동) ===")]
    [Tooltip("09_Waist - Motor 1: 베이스 회전 (Y축)")]
    public ArticulationBody motor1_Waist;
    
    [Tooltip("01_BigArm - Motor 2: 팔 전체 상하 (Z축)")]
    public ArticulationBody motor2_BigArm;
    
    [Tooltip("03_ParallelArm - Motor 3: Forearm 각도 조절 (Z축)")]
    public ArticulationBody motor3_ParallelArm;

    [Header("=== Passive Joints (수동 - Linkage) ===")]
    [Tooltip("04_DriveLink - ParallelArm 따라감")]
    public ArticulationBody driveLink;
    
    [Tooltip("05_ParallelLink_Big - 고정")]
    public ArticulationBody parallelLinkBig;
    
    [Tooltip("07_Triangle Bracket")]
    public ArticulationBody triangleBracket;
    
    [Tooltip("02_Forearm")]
    public ArticulationBody forearm;
    
    [Tooltip("06_ParallelLink_Forearm")]
    public ArticulationBody parallelLinkForearm;
    
    [Tooltip("08_Wrist")]
    public ArticulationBody wrist;

    [Header("=== Target Angles (Degrees) ===")]
    [Range(-180f, 180f)] public float targetMotor1 = 0f;  // Waist
    [Range(-90f, 90f)] public float targetMotor2 = 0f;    // BigArm
    [Range(-90f, 90f)] public float targetMotor3 = 0f;    // ParallelLink_Big

    [Header("=== Joint Limits (Degrees) ===")]
    public Vector2 motor1Limits = new Vector2(-180f, 180f);
    public Vector2 motor2Limits = new Vector2(-45f, 90f);
    public Vector2 motor3Limits = new Vector2(-45f, 90f);

    [Header("=== Drive Settings ===")]
    public float stiffness = 100000f;
    public float damping = 10000f;
    public float forceLimit = 1000f;
    
    [Header("=== Passive Joint Settings ===")]
    public float passiveStiffness = 50000f;
    public float passiveDamping = 5000f;

    [Header("=== Debug ===")]
    public bool showDebugInfo = true;
    public bool enableManualControl = true;

    // 현재 각도
    private float cur1, cur2, cur3;

    void FixedUpdate()
    {
        if (enableManualControl)
            HandleInput();

        ApplyMotorTargets();
        SynchronizeLinkage();
        ReadCurrentAngles();
    }

    void HandleInput()
    {
        float speed = 45f * Time.fixedDeltaTime;

        // Motor 1 (Waist): Q/E
        if (Input.GetKey(KeyCode.Q)) targetMotor1 -= speed;
        if (Input.GetKey(KeyCode.E)) targetMotor1 += speed;

        // Motor 2 (BigArm): W/S
        if (Input.GetKey(KeyCode.W)) targetMotor2 += speed;
        if (Input.GetKey(KeyCode.S)) targetMotor2 -= speed;

        // Motor 3 (ParallelLink_Big): A/D
        if (Input.GetKey(KeyCode.A)) targetMotor3 += speed;
        if (Input.GetKey(KeyCode.D)) targetMotor3 -= speed;

        // Clamp
        targetMotor1 = Mathf.Clamp(targetMotor1, motor1Limits.x, motor1Limits.y);
        targetMotor2 = Mathf.Clamp(targetMotor2, motor2Limits.x, motor2Limits.y);
        targetMotor3 = Mathf.Clamp(targetMotor3, motor3Limits.x, motor3Limits.y);

        // Home: H
        if (Input.GetKeyDown(KeyCode.H))
        {
            targetMotor1 = 0f;
            targetMotor2 = 0f;
            targetMotor3 = 0f;
        }
    }

    void ApplyMotorTargets()
    {
        SetDriveTarget(motor1_Waist, targetMotor1);
        SetDriveTarget(motor2_BigArm, targetMotor2);
        SetDriveTarget(motor3_ParallelArm, targetMotor3);
    }

    void SetDriveTarget(ArticulationBody ab, float targetDeg)
    {
        if (ab == null) return;
        var drive = ab.xDrive;
        drive.target = targetDeg;
        ab.xDrive = drive;
    }

    /// <summary>
    /// 4-bar linkage 동기화
    /// 
    /// 구조:
    /// - BigArm(Motor2) → TriangleBracket → Forearm → Wrist
    ///                                    → ParallelLink_Forearm (Forearm과 평행!)
    /// - Waist → ParallelArm(Motor3) → DriveLink
    ///         → ParallelLink_Big (BigArm과 평행!)
    /// 
    /// 핵심:
    /// - ParallelLink_Big은 BigArm과 평행 (같은 방향 회전)
    /// - ParallelLink_Forearm은 Forearm과 평행
    /// - DriveLink는 두 체인을 연결
    /// </summary>
    void SynchronizeLinkage()
    {
        float theta2 = GetJointAngleDeg(motor2_BigArm);        // BigArm 각도
        float theta3 = GetJointAngleDeg(motor3_ParallelArm);   // ParallelArm 각도

        // === ParallelLink_Big: Waist 자식, BigArm과 평행! (같은 방향) ===
        float parallelLinkBigAngle = theta2;  // BigArm과 같은 방향!
        
        // === DriveLink: ParallelArm 자식, ParallelLink_Big과 연결 ===
        // ParallelArm(theta3) 회전 상쇄 + ParallelLink_Big(theta2) 방향
        float driveLinkAngle = -theta3 + theta2;
        
        // === Triangle Bracket: BigArm 자식, ParallelArm에 의해 회전 ===
        float triangleBracketAngle = theta3;
        
        // === Forearm: Triangle Bracket 자식, 추가 회전 없음 ===
        float forearmAngle = 0f;
        
        // === ParallelLink_Forearm: Triangle Bracket 자식, Forearm과 평행! ===
        float parallelLinkForearmAngle = 0f;
        
        // === Wrist: Forearm 자식, 수평 유지 ===
        float wristAngle = -(theta2 + theta3);

        // 각도 적용
        SetPassiveTarget(parallelLinkBig, parallelLinkBigAngle);
        SetPassiveTarget(driveLink, driveLinkAngle);
        SetPassiveTarget(triangleBracket, triangleBracketAngle);
        SetPassiveTarget(forearm, forearmAngle);
        SetPassiveTarget(parallelLinkForearm, parallelLinkForearmAngle);
        SetPassiveTarget(wrist, wristAngle);
    }

    void SetPassiveTarget(ArticulationBody ab, float targetDeg)
    {
        if (ab == null)
        {
            Debug.LogWarning("ArticulationBody is null!");
            return;
        }
        
        // FixedJoint면 회전 불가
        if (ab.jointType == ArticulationJointType.FixedJoint)
        {
            Debug.LogWarning($"{ab.name} is FixedJoint - cannot rotate!");
            return;
        }
        
        var drive = ab.xDrive;
        drive.target = targetDeg;
        drive.stiffness = passiveStiffness;
        drive.damping = passiveDamping;
        ab.xDrive = drive;
    }

    float GetJointAngleDeg(ArticulationBody ab)
    {
        if (ab == null || ab.dofCount == 0) return 0f;
        return ab.jointPosition[0] * Mathf.Rad2Deg;
    }

    void ReadCurrentAngles()
    {
        cur1 = GetJointAngleDeg(motor1_Waist);
        cur2 = GetJointAngleDeg(motor2_BigArm);
        cur3 = GetJointAngleDeg(motor3_ParallelArm);
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 450, 280));
        GUI.Box(new Rect(0, 0, 450, 280), "");
        
        GUILayout.Label("<b>Parallel Linkage Robot (3 DOF)</b>");
        GUILayout.Space(5);
        
        GUILayout.Label($"Motor 1 - Waist (Q/E):       {cur1,7:F1}° → {targetMotor1,7:F1}°");
        GUILayout.Label($"Motor 2 - BigArm (W/S):      {cur2,7:F1}° → {targetMotor2,7:F1}°");
        GUILayout.Label($"Motor 3 - ParallelArm (A/D): {cur3,7:F1}° → {targetMotor3,7:F1}°");
        
        GUILayout.Space(10);
        GUILayout.Label("=== Passive Joints ===");
        
        string plfStatus = "NULL";
        if (parallelLinkForearm != null)
        {
            float angle = GetJointAngleDeg(parallelLinkForearm);
            plfStatus = $"{parallelLinkForearm.jointType} | Angle: {angle:F1}°";
        }
        GUILayout.Label($"ParallelLink_Forearm: {plfStatus}");
        
        GUILayout.Space(10);
        GUILayout.Label("Q/E: Waist | W/S: BigArm | A/D: Forearm | H: Home");
        
        GUILayout.EndArea();
    }

    // === Public API ===
    public void SetTargetAngles(float m1, float m2, float m3)
    {
        targetMotor1 = Mathf.Clamp(m1, motor1Limits.x, motor1Limits.y);
        targetMotor2 = Mathf.Clamp(m2, motor2Limits.x, motor2Limits.y);
        targetMotor3 = Mathf.Clamp(m3, motor3Limits.x, motor3Limits.y);
    }

    public Vector3 GetCurrentAngles() => new Vector3(cur1, cur2, cur3);
    public Vector3 GetTargetAngles() => new Vector3(targetMotor1, targetMotor2, targetMotor3);
}
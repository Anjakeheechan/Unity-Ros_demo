using UnityEngine;

/// <summary>
/// Parallel Linkage Kinematic Synchronization
/// 구조 A에서 Parallel Link들의 위치/회전을 기구학적으로 동기화
/// 
/// 4-bar linkage 폐루프를 kinematic으로 해결
/// </summary>
public class ParallelLinkageKinematicSync : MonoBehaviour
{
    [Header("=== Main Chain Transforms ===")]
    public Transform t_Waist;
    public Transform t_BigArm;
    public Transform t_TriangleBracket;
    public Transform t_Forearm;

    [Header("=== Parallel Chain Transforms ===")]
    public Transform t_ParallelArm;
    public Transform t_DriveLink;
    public Transform t_ParallelLinkBig;
    public Transform t_ParallelLinkForearm;

    [Header("=== Pivot Points (로컬 좌표) ===")]
    [Tooltip("BigArm 시작점 (Waist 기준)")]
    public Vector3 pivot_BigArm = Vector3.zero;
    
    [Tooltip("ParallelArm 시작점 (Waist 기준)")]
    public Vector3 pivot_ParallelArm = new Vector3(0.1f, 0, 0);
    
    [Tooltip("ParallelLinkBig 시작점 (Waist 기준)")]
    public Vector3 pivot_ParallelLinkBig = new Vector3(0.1f, 0, 0);
    
    [Tooltip("DriveLink-ParallelLinkBig 연결점")]
    public Vector3 pivot_DriveLinkEnd = new Vector3(0, 0.1f, 0);

    [Header("=== Link Lengths ===")]
    public float len_BigArm = 0.1f;
    public float len_Forearm = 0.1f;
    public float len_ParallelArm = 0.08f;
    public float len_DriveLink = 0.05f;
    public float len_ParallelLinkBig = 0.15f;
    public float len_ParallelLinkForearm = 0.1f;

    [Header("=== Sync Mode ===")]
    public bool useTransformSync = true;  // Transform 직접 조작
    public bool useArticulationSync = false; // ArticulationBody target 설정

    [Header("=== Debug ===")]
    public bool showGizmos = true;
    public Color gizmoColor = Color.yellow;

    void LateUpdate()
    {
        if (useTransformSync)
            SyncParallelLinksTransform();
    }

    void FixedUpdate()
    {
        if (useArticulationSync)
            SyncParallelLinksArticulation();
    }

    /// <summary>
    /// Transform을 직접 조작하여 동기화 (간단하고 안정적)
    /// </summary>
    void SyncParallelLinksTransform()
    {
        if (t_BigArm == null || t_ParallelArm == null) return;

        // 현재 모터 각도 읽기 (로컬 Z축 회전)
        float theta_BigArm = GetLocalZAngle(t_BigArm);
        float theta_ParallelArm = GetLocalZAngle(t_ParallelArm);

        // === ParallelLinkBig 동기화 ===
        // ParallelArm과 동일한 각도로 회전 (같은 축에서 시작하므로)
        if (t_ParallelLinkBig != null)
        {
            // ParallelLinkBig는 ParallelArm과 연동
            SetLocalZAngle(t_ParallelLinkBig, theta_ParallelArm);
        }

        // === DriveLink 동기화 ===
        // ParallelArm 끝점에서 ParallelLinkBig와 만나는 각도 계산
        if (t_DriveLink != null && t_ParallelArm != null)
        {
            // DriveLink는 ParallelArm의 끝에 붙어있고,
            // ParallelLinkBig의 특정 지점과 연결됨
            float driveLinkAngle = CalculateDriveLinkAngle(theta_BigArm, theta_ParallelArm);
            SetLocalZAngle(t_DriveLink, driveLinkAngle);
        }

        // === Forearm 동기화 (평행 유지) ===
        // 4-bar 평행사변형에서 Forearm은 항상 수평 유지
        if (t_Forearm != null)
        {
            // Forearm = -BigArm (수평 유지 조건)
            float forearmAngle = -theta_BigArm;
            SetLocalZAngle(t_Forearm, forearmAngle);
        }

        // === ParallelLinkForearm 동기화 ===
        if (t_ParallelLinkForearm != null)
        {
            // Forearm에서 시작하여 ParallelLinkBig/DriveLink와 만남
            float plfAngle = CalculateParallelLinkForearmAngle(theta_BigArm, theta_ParallelArm);
            SetLocalZAngle(t_ParallelLinkForearm, plfAngle);
        }
    }

    /// <summary>
    /// ArticulationBody의 target을 설정하여 동기화
    /// </summary>
    void SyncParallelLinksArticulation()
    {
        if (t_BigArm == null || t_ParallelArm == null) return;

        float theta_BigArm = GetArticulationAngle(t_BigArm);
        float theta_ParallelArm = GetArticulationAngle(t_ParallelArm);

        // Forearm
        SetArticulationTarget(t_Forearm, -theta_BigArm);

        // ParallelLinkBig
        SetArticulationTarget(t_ParallelLinkBig, theta_ParallelArm);

        // DriveLink
        float driveLinkAngle = CalculateDriveLinkAngle(theta_BigArm, theta_ParallelArm);
        SetArticulationTarget(t_DriveLink, driveLinkAngle);

        // ParallelLinkForearm
        float plfAngle = CalculateParallelLinkForearmAngle(theta_BigArm, theta_ParallelArm);
        SetArticulationTarget(t_ParallelLinkForearm, plfAngle);
    }

    /// <summary>
    /// DriveLink 각도 계산 (4-bar 기구학)
    /// </summary>
    float CalculateDriveLinkAngle(float thetaBigArm, float thetaParallelArm)
    {
        // 간단한 근사: DriveLink는 ParallelArm과 BigArm의 차이에 비례
        // 실제로는 링크 길이에 따른 정확한 기구학 계산 필요
        return thetaBigArm - thetaParallelArm;
    }

    /// <summary>
    /// ParallelLinkForearm 각도 계산
    /// </summary>
    float CalculateParallelLinkForearmAngle(float thetaBigArm, float thetaParallelArm)
    {
        // Forearm에서 시작하므로 Forearm 각도 기준
        // ParallelLinkBig와 만나야 하므로 조정
        return thetaParallelArm - thetaBigArm;
    }

    #region Utility Methods
    float GetLocalZAngle(Transform t)
    {
        if (t == null) return 0;
        return t.localEulerAngles.z;
    }

    void SetLocalZAngle(Transform t, float angle)
    {
        if (t == null) return;
        var euler = t.localEulerAngles;
        euler.z = angle;
        t.localEulerAngles = euler;
    }

    float GetArticulationAngle(Transform t)
    {
        if (t == null) return 0;
        var ab = t.GetComponent<ArticulationBody>();
        if (ab == null || ab.dofCount == 0) return 0;
        return ab.jointPosition[0] * Mathf.Rad2Deg;
    }

    void SetArticulationTarget(Transform t, float angleDeg)
    {
        if (t == null) return;
        var ab = t.GetComponent<ArticulationBody>();
        if (ab == null) return;

        var drive = ab.xDrive;
        drive.target = angleDeg;
        ab.xDrive = drive;
    }
    #endregion

    #region Auto Calculate Link Lengths
    [ContextMenu("Auto Calculate Link Lengths")]
    public void AutoCalculateLinkLengths()
    {
        if (t_BigArm != null && t_TriangleBracket != null)
            len_BigArm = Vector3.Distance(t_BigArm.position, t_TriangleBracket.position);

        if (t_TriangleBracket != null && t_Forearm != null)
        {
            // Forearm은 TriangleBracket 다음
        }

        if (t_ParallelArm != null && t_DriveLink != null)
            len_ParallelArm = Vector3.Distance(t_ParallelArm.position, t_DriveLink.position);

        Debug.Log($"링크 길이 계산 완료: BigArm={len_BigArm:F3}, ParallelArm={len_ParallelArm:F3}");
    }
    #endregion

    #region Gizmos
    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = gizmoColor;

        // 각 링크 연결선 표시
        DrawLinkGizmo(t_Waist, t_BigArm, "BigArm");
        DrawLinkGizmo(t_BigArm, t_TriangleBracket, "Triangle");
        DrawLinkGizmo(t_TriangleBracket, t_Forearm, "Forearm");
        DrawLinkGizmo(t_Waist, t_ParallelArm, "ParallelArm");
        DrawLinkGizmo(t_ParallelArm, t_DriveLink, "DriveLink");
        DrawLinkGizmo(t_Waist, t_ParallelLinkBig, "PLBig");

        // 폐루프 연결 (점선으로 표시)
        Gizmos.color = Color.red;
        if (t_DriveLink != null && t_ParallelLinkBig != null)
            Gizmos.DrawLine(t_DriveLink.position, t_ParallelLinkBig.position);
    }

    void DrawLinkGizmo(Transform from, Transform to, string label)
    {
        if (from == null || to == null) return;
        Gizmos.DrawLine(from.position, to.position);
        Gizmos.DrawWireSphere(to.position, 0.005f);
    }
    #endregion
}
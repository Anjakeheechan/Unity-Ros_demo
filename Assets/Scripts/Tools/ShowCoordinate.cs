using UnityEngine;
#if UNITY_EDITOR
using UnityEditor; // Handles 사용을 위해 필수
#endif

/*public class ShowCoordinate : MonoBehaviour
{
    [Header("좌표계 설정")]
    [Tooltip("좌표축 선의 길이")]
    public float axisLength = 1.0f;

    [Tooltip("선의 두께 (픽셀 단위)")]
    [Range(2f, 10f)] // 두께 조절 슬라이더
    public float lineThickness = 4.0f;

    [Tooltip("매시를 뚫고 항상 위에 보일지 여부")]
    public bool alwaysShowOnTop = true;

    [Header("텍스트 설정")]
    public bool showName = true;
    public bool showPosition = true;
    public Color textColor = Color.white;
    public Vector3 textOffset = new Vector3(0, 0.5f, 0);

    // 에디터에서만 작동하도록 전처리기 추가
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 1. 기존 핸들 설정 저장 (다른 기즈모에 영향 안 주게)
        Color defaultColor = Handles.color;
        var defaultZTest = Handles.zTest;

        // 2. '항상 위에 그리기' 설정
        if (alwaysShowOnTop)
        {
            // 이 설정이 있으면 매시 뒤에 있어도 투과해서 보입니다.
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        }

        Vector3 origin = transform.position;

        // 3. 두께가 적용된 선 그리기 (DrawAAPolyLine 사용)
        
        // X축 (Red)
        Handles.color = Color.red;
        Handles.DrawAAPolyLine(lineThickness, origin, origin + transform.right * axisLength);
        // 끝점 장식 (선택사항)
        Handles.DotHandleCap(0, origin + transform.right * axisLength, transform.rotation, axisLength * 0.05f, EventType.Repaint);

        // Y축 (Green)
        Handles.color = Color.green;
        Handles.DrawAAPolyLine(lineThickness, origin, origin + transform.up * axisLength);
        Handles.DotHandleCap(0, origin + transform.up * axisLength, transform.rotation, axisLength * 0.05f, EventType.Repaint);

        // Z축 (Blue)
        Handles.color = Color.blue;
        Handles.DrawAAPolyLine(lineThickness, origin, origin + transform.forward * axisLength);
        Handles.DotHandleCap(0, origin + transform.forward * axisLength, transform.rotation, axisLength * 0.05f, EventType.Repaint);

        // 4. 원점 표시 (작은 흰색 점)
        Handles.color = Color.white;
        Handles.DotHandleCap(0, origin, transform.rotation, axisLength * 0.02f, EventType.Repaint);

        // 5. 텍스트 표시
        if (showName || showPosition)
        {
            string labelText = "";
            if (showName) labelText += gameObject.name;
            if (showName && showPosition) labelText += "\n";
            if (showPosition) labelText += transform.position.ToString("F1");

            GUIStyle style = new GUIStyle();
            style.normal.textColor = textColor;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;

            // 텍스트는 원래 항상 위에 보이지만, 명시적으로 위치 잡기
            Handles.Label(origin + textOffset, labelText, style);
        }

        // 6. 설정 복구 (필수)
        Handles.color = defaultColor;
        Handles.zTest = defaultZTest;
    }
#endif
}*/
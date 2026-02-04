using UnityEngine;

/// <summary>
/// PLC의 Y00(정방향회전), Y01(역방향회전) 신호를 받아, Dragger를 특정속도로 이동시킨다.
/// 속성: Dragger들, 속도
/// </summary>
public class Conveyor : MonoBehaviour
{
    [Header("PLC 신호")]
    public bool cwSignal;
    public bool ccwSignal;

    [Header("컨베이어 설정")]
    [SerializeField] Dragger[] draggers;
    [SerializeField] float speed;

    // Update is called once per frame
    void Update()
    {
        if (cwSignal)
        {
            foreach (var dragger in draggers)
            {
                dragger.Move(true, speed);
            }
        }

        if (ccwSignal)
        {
            foreach (var dragger in draggers)
            {
                dragger.Move(false, speed);
            }
        }
    }
}

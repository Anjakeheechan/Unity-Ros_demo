using System.Collections;
using UnityEngine;

/// <summary>
/// LOADER 버튼을 누를 때 마다 금속, 플라스틱 순서로 프리팹을 인스턴싱한다.
/// 속성: 금속 프리팹, 플라스틱 프리팹, loadSignal, count;
/// </summary>
public class Loader : MonoBehaviour
{
    [Header("PLC 신호")]
    public bool loadSignal;
    public int count;

    [Header("장비 설정")]
    [SerializeField] GameObject Box_A;
    [SerializeField] GameObject Box_B;
    [SerializeField] GameObject Box_C;
    bool isLoaded = false;

    // Update is called once per frame
    void Update()
    {
        if(loadSignal && !isLoaded)
        {
            isLoaded = true;

            if(count % 2 == 0)
            {
                GameObject obj = Instantiate(Box_A, this.transform);
                obj.transform.localPosition = Vector3.zero;
            }
            else
            {
                GameObject obj = Instantiate(Box_B, this.transform);
                obj.transform.localPosition = Vector3.zero;
            }

            count++;
        }

        if(!loadSignal)
        {
            isLoaded = false;
        }
    }
}

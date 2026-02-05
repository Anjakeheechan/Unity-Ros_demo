using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Drawing;
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

    [Header("생성 위치")]
    [SerializeField] GameObject LocationObj;

    bool isLoaded = false;
    private string lastTime = string.Empty;
    // Update is called once per frame
    void Update()
    {
        JObject boxObj = DataManager.Instance.stm_stm_yolo_boxcreated;
        if (boxObj == null) return;

        int size = boxObj["size"].Value<int>();
        //f (size == "999") return;

        string nowTime = boxObj["timestamp"]?.ToString();
        if (string.IsNullOrEmpty(nowTime)) return;


        if (lastTime == nowTime) return;
        lastTime = nowTime;

        SpawnBox(size);
    }

    void SpawnBox(int size)
    {
        GameObject prefab = (size % 2 == 0) ? Box_A : Box_B;

        if (size == 999) prefab = Box_C;        // 테스트용
        GameObject obj = Instantiate(prefab, transform);

        obj.transform.position = LocationObj.transform.position;
    }
}

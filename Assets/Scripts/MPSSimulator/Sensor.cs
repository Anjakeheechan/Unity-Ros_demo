using UnityEngine;

namespace MPSSimulator
{
    /// <summary>
    /// 광전센서(근접센서), 유도형센서(금속센서)에서 신호를 받으면 PLC로 상태를 전달한다.
    /// * 참고: 실제로는 하드웨어 센서의 인풋이 PLC로 들어가고 -> 시뮬레이터로 반영
    /// 속성: 센서의 타입, PLC전달신호, MeshRenderer
    /// </summary>
    public class Sensor : MonoBehaviour
    {
        [Header("PLC 신호")]
        public bool sensorSignal;
        enum 센서타입
        {
            광전센서,
            유도형센서,
            용량형센서
        }
        [Header("센서 설정")]
        [SerializeField] 센서타입 type = 센서타입.광전센서;
        MeshRenderer meshRenderer;  // 센서에 물체가 닿으면 색상을 변경

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.material.color = new Color(0, 0, 0, 0.7f);
        }

        private void OnTriggerEnter(Collider other)
        {
            switch(type)
            {
                case 센서타입.광전센서:

                    sensorSignal = true;
                    meshRenderer.material.color = new Color(1, 0, 0, 0.7f);

                    break;
                case 센서타입.유도형센서:

                    if(other.tag == "Box_A")
                    {
                        sensorSignal = true;
                        meshRenderer.material.color = new Color(1, 0, 0, 0.7f);
                    }

                    break;
                case 센서타입.용량형센서:
                    break;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if(sensorSignal)
            {
                sensorSignal = false;
                meshRenderer.material.color = new Color(0, 0, 0, 0.7f);
            }
        }
    }
}

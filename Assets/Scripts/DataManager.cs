using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class DataManager
{
    public float modbustcp_esp32_01_posx { get; set; }
    public float modbustcp_esp32_01_posy { get; set; }
    public float modbustcp_esp32_01_postheta { get; set; }
    public bool modbustcp_esp32_01_targeta { get; set; }
    public string modbustcp_esp32_01_state { get; set; }
    public bool stm_stm_yolo_agvloadarrived { get; set; }
    public bool stm_stm_yolo_agvloaddeparted { get; set; }
    public bool stm_stm_yolo_agvsortarrived { get; set; }
    public bool stm_stm_yolo_agvsortdeparted { get; set; }
    public long stm_stm_yolo_currentfloor { get; set; }
    public long stm_stm_yolo_currentspeedload { get; set; }
    public long stm_stm_yolo_currentspeedmain { get; set; }
    public long stm_stm_yolo_currentspeedsort { get; set; }
    public string stm_stm_yolo_currentstate { get; set; } //  입력값 float => 0: 부팅, 1: 대기, 2: running, 3: stop, 4: emergency robot(미사용), 5: emergency stop
    public bool stm_stm_yolo_isliftmoving { get; set; }
    public bool stm_stm_yolo_isrobotdone { get; set; }
    public bool stm_stm_yolo_isrobotworking { get; set; }

    public JObject stm_stm_yolo_boxcreated { get; set; }

    /// <summary>
    /// singleton 설정
    /// 사용하려면 DataManager.Instance 로 접근하면 됨
    /// </summary>
    private static readonly Lazy<DataManager> _instance = new Lazy<DataManager>(() => new DataManager());
    public static DataManager Instance = _instance.Value;

    private DataManager()
    {

    }


    public int SetDataAsync(JObject obj)
    {
        try
        {

            // ESP32 데이터 설정
            modbustcp_esp32_01_posx = Convert.ToSingle(obj["modbustcp_esp32_01_posx"]);
            modbustcp_esp32_01_posy = Convert.ToSingle(obj["modbustcp_esp32_01_posy"]);
            modbustcp_esp32_01_postheta = Convert.ToSingle(obj["modbustcp_esp32_01_postheta"]);
            modbustcp_esp32_01_targeta = Convert.ToBoolean(obj["modbustcp_esp32_01_targeta"]);
            modbustcp_esp32_01_state = Convert.ToString(obj["modbustcp_esp32_01_state"]);

            // STM 데이터 설정
            stm_stm_yolo_agvloadarrived = Convert.ToBoolean(obj["stm_stm_yolo_agvloadarrived"]);
            stm_stm_yolo_agvloaddeparted = Convert.ToBoolean(obj["stm_stm_yolo_agvloaddeparted"]);
            stm_stm_yolo_agvsortarrived = Convert.ToBoolean(obj["stm_stm_yolo_agvsortarrived"]);
            stm_stm_yolo_agvsortdeparted = Convert.ToBoolean(obj["stm_stm_yolo_agvsortdeparted"]);
            stm_stm_yolo_currentfloor = Convert.ToInt64(obj["stm_stm_yolo_currentfloor"]);
            stm_stm_yolo_currentspeedload = Convert.ToInt64(obj["stm_stm_yolo_currentspeedload"]);
            stm_stm_yolo_currentspeedmain = Convert.ToInt64(obj["stm_stm_yolo_currentspeedmain"]);
            stm_stm_yolo_currentspeedsort = Convert.ToInt64(obj["stm_stm_yolo_currentspeedsort"]);
            stm_stm_yolo_isliftmoving = Convert.ToBoolean(obj["stm_stm_yolo_isliftmoving"]);
            stm_stm_yolo_isrobotdone = Convert.ToBoolean(obj["stm_stm_yolo_isrobotdone"]);
            stm_stm_yolo_isrobotworking = Convert.ToBoolean(obj["stm_stm_yolo_isrobotworking"]);

            // 현재 상태 변환
            switch (Convert.ToInt16(obj["stm_stm_yolo_currentstate"]))
            {
                case 0:
                case 1:
                    stm_stm_yolo_currentstate = "IDLE";
                    break;
                case 2:
                    stm_stm_yolo_currentstate = "RUNNING";
                    break;
                case 3:
                    stm_stm_yolo_currentstate = "STOP";
                    break;
                case 4:
                case 5:
                    stm_stm_yolo_currentstate = "EMERGENCYSTOP";
                    break;
            };

            // json 변환
            stm_stm_yolo_boxcreated = JsonConvert.DeserializeObject<JObject>(Convert.ToString(obj["stm_stm_yolo_boxcreated"]));

            return 1;
        }
        catch (Exception e)
        {
            Debug.LogError("[DataManager] OPC Data 적용 중 오류 발생! : " + e.Message);
            Debug.LogError("[DataManager] 스택 트레이스: " + e.StackTrace);
            return 0;
        }
    }


    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== DataManager 현재 상태 ===");
        sb.AppendLine();

        // ESP32 데이터
        sb.AppendLine("[ ESP32 AGV 데이터 ]");
        sb.AppendLine($"  위치 X: {modbustcp_esp32_01_posx}");
        sb.AppendLine($"  위치 Y: {modbustcp_esp32_01_posy}");
        sb.AppendLine($"  위치 Theta: {modbustcp_esp32_01_postheta}");
        sb.AppendLine($"  목표 A: {modbustcp_esp32_01_targeta}");
        sb.AppendLine($"  상태: {modbustcp_esp32_01_state}");
        sb.AppendLine();

        // STM 시스템 상태
        sb.AppendLine("[ STM 시스템 상태 ]");
        sb.AppendLine($"  현재 상태: {stm_stm_yolo_currentstate}");
        sb.AppendLine($"  현재 층: {stm_stm_yolo_currentfloor}");
        sb.AppendLine($"  리프트 이동 중: {stm_stm_yolo_isliftmoving}");
        sb.AppendLine($"  로봇 작업 중: {stm_stm_yolo_isrobotworking}");
        sb.AppendLine($"  로봇 작업 완료: {stm_stm_yolo_isrobotdone}");
        sb.AppendLine();

        // 컨베이어 속도
        sb.AppendLine("[ 컨베이어 속도 ]");
        sb.AppendLine($"  Load 컨베이어: {stm_stm_yolo_currentspeedload} m/s");
        sb.AppendLine($"  Main 컨베이어: {stm_stm_yolo_currentspeedmain} m/s");
        sb.AppendLine($"  Sort 컨베이어: {stm_stm_yolo_currentspeedsort} m/s");
        sb.AppendLine();

        // AGV 상태
        sb.AppendLine("[ AGV 도착/출발 상태 ]");
        sb.AppendLine($"  AGV Load 도착: {stm_stm_yolo_agvloadarrived}");
        sb.AppendLine($"  AGV Load 출발: {stm_stm_yolo_agvloaddeparted}");
        sb.AppendLine($"  AGV Sort 도착: {stm_stm_yolo_agvsortarrived}");
        sb.AppendLine($"  AGV Sort 출발: {stm_stm_yolo_agvsortdeparted}");
        sb.AppendLine();

        // box 생성 여부 확인
        sb.AppendLine("[ BOX 생성 여부 ]");
        sb.AppendLine($"  BOX 생성 여부: {stm_stm_yolo_boxcreated.ToString(Formatting.Indented)}");

        sb.AppendLine("================================");

        Debug.Log(sb.ToString());

        return sb.ToString();
    }

}

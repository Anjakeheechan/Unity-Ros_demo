using System;
using Newtonsoft.Json;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class DataManager : MonoBehaviour
{
    public string modbus_esp_PosX { get; set; }
    public string modbus_esp_PosY { get; set; }
    public string modbus_esp_PosTheta { get; set; }
    public string modbus_esp_TargetA { get; set; }
    public string modbus_esp_State { get; set; } 
    public string stm_stm_AgvLoadArrived { get; set; }
    public string stm_stm_AgvLoadDeparted { get; set; }
    public string stm_stm_AgvSortArrived { get; set; }
    public string stm_stm_AgvSortDeparted { get; set; }
    public string stm_stm_CurrentFloor { get; set; }
    public string stm_stm_CurrentSpeedLoad { get; set; }
    public string stm_stm_CurrentSpeedMain { get; set; }
    public string stm_stm_CurrentSpeedSort { get; set; }
    public string stm_stm_CurrentState { get; set; }
    public string stm_stm_IsLiftMoving { get; set; }
    public string stm_stm_IsRobotDone { get; set; }
    public string stm_stm_IsRobotWorking { get; set; }
    public string modbus_esp_Control { get; set; }
    public string stm_stm_TargetState { get; set; }
    public string stm_stm_TargetSpeedSort { get; set; }
    public string stm_stm_TargetSpeedMain { get; set; }
    public string stm_stm_TargetSpeedLoad { get; set; }

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
            modbus_esp_PosX = Convert.ToString(obj["modbus_esp_PosX"]);
            modbus_esp_PosY = Convert.ToString(obj["modbus_esp_PosY"]);
            modbus_esp_PosTheta = Convert.ToString(obj["modbus_esp_PosTheta"]);
            modbus_esp_TargetA = Convert.ToString(obj["modbus_esp_TargetA"]);
            modbus_esp_State = Convert.ToString(obj["modbus_esp_State"]);
            stm_stm_AgvLoadArrived = Convert.ToString(obj["stm_stm_AgvLoadArrived"]);
            stm_stm_AgvLoadDeparted = Convert.ToString(obj["stm_stm_AgvLoadDeparted"]);
            stm_stm_AgvSortArrived = Convert.ToString(obj["stm_stm_AgvSortArrived"]);
            stm_stm_AgvSortDeparted = Convert.ToString(obj["stm_stm_AgvSortDeparted"]);
            stm_stm_CurrentFloor = Convert.ToString(obj["stm_stm_CurrentFloor"]);
            stm_stm_CurrentSpeedLoad = Convert.ToString(obj["stm_stm_CurrentSpeedLoad"]);
            stm_stm_CurrentSpeedMain = Convert.ToString(obj["stm_stm_CurrentSpeedMain"]);
            stm_stm_CurrentSpeedSort = Convert.ToString(obj["stm_stm_CurrentSpeedSort"]);
            stm_stm_CurrentState = Convert.ToString(obj["stm_stm_CurrentState"]);
            stm_stm_IsLiftMoving = Convert.ToString(obj["stm_stm_IsLiftMoving"]);
            stm_stm_IsRobotDone = Convert.ToString(obj["stm_stm_IsRobotDone"]);
            stm_stm_IsRobotWorking = Convert.ToString(obj["stm_stm_IsRobotWorking"]);
            modbus_esp_Control = Convert.ToString(obj["modbus_esp_Control"]);
            stm_stm_TargetState = Convert.ToString(obj["stm_stm_TargetState"]);
            stm_stm_TargetSpeedSort = Convert.ToString(obj["stm_stm_TargetSpeedSort"]);
            stm_stm_TargetSpeedMain = Convert.ToString(obj["stm_stm_TargetSpeedMain"]);
            stm_stm_TargetSpeedLoad = Convert.ToString(obj["stm_stm_TargetSpeedLoad"]);

            return 1;
        } catch(Exception e)
        {
            Debug.LogError("OPC Data 적용 중 오류 발생! : " + e.Message);
            return 0;
        }
    }

}

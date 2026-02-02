using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine;
using UnityEngine.Networking;

public class DataReceiver : MonoBehaviour
{
    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;

    // 서버 주소 설정
    private string serverUrl = "ws://localhost:5178/ws/opc?type=unity";

    async void Start()
    {
        await ConnectAndReceive();
    }

    async Task ConnectAndReceive()
    {
        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        try
        {
            // 연결
            await _ws.ConnectAsync(new Uri(serverUrl), _cts.Token);

            // 수신 루프
            var buffer = new byte[1024 * 4];
            while (_ws.State == WebSocketState.Open)
            {
                var result = await _ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    _cts.Token);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessData(json);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OPC] 오류: {ex.Message}");
        }
    }

    void ProcessData(string json)
    {
        try
        {

            // JSON 파싱
            var data = JObject.Parse(json);

            // DataManager에 데이터 설정
            int result = DataManager.Instance.SetDataAsync(data);

            if (result == 1)
            {
                DataManager.Instance.ToString();
            }
            else
            {
                Debug.LogWarning("[OPC] DataManager 업데이트 실패");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OPC] 데이터 처리 오류: {ex.Message}");
        }
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        _ws?.Dispose();
    }
}

using Assets.Scripts;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

public class RTCConnector : MonoBehaviour
{
    [Header("RTC 변수")]

    [Tooltip("송출카메라")]
    public Camera _sourceCamera;

    [Tooltip("해상도[가로]")]
    public int _width = 1280;

    [Tooltip("해상도[세로]")]
    public int _height = 720;

    [Tooltip("송출 프레임")]
    public int _fps = 30;

    [Tooltip("STUN서버")]
    public string[] _stunUrl = new[] { "stun:stun.l.google.com:19302" };


    #region 내부 변수
    private ClientWebSocket _socket = null;          // websocket 객체
    private CancellationTokenSource _cts = null;

    private readonly string _roomId = "UNITY-1";     // roomId/broadcaster 구분용 아이디
    private string _serverUri = string.Empty;        // 서버 uri

    private readonly ConcurrentDictionary<string, RTCPeerConnection> _pcs = new();  // client별 peerconnection 저장
    private readonly ConcurrentDictionary<string, List<IceDTO>> _iceByClient = new();  // client 별 ice 
    private readonly ConcurrentDictionary<string, IceDTO> _remoteIceByClient = new();  // offer 처리 전 오는 ice처리용 버퍼

    private readonly ConcurrentDictionary<string, VideoStreamTrack> _videoTracks = new();
    private readonly ConcurrentDictionary<string, RenderTexture> _renderTextures = new();
    private readonly ConcurrentDictionary<string, Camera> _cameras = new();
    private readonly ConcurrentDictionary<string, string> _clientCameraMap = new();
    #endregion

    private async void Start()
    {
        _cts = new CancellationTokenSource();
        StartCoroutine(WebRTC.Update());

        await ConnectSocket(_cts.Token);
    }

    private async Task ConnectSocket(CancellationToken ct)
    {
        Uri uri = new Uri($"ws://127.0.0.1:5178/ws/rtc?type=Broadcaster&roomId={_roomId}");


        try
        {
            _socket = new ClientWebSocket();
            await _socket.ConnectAsync(uri, ct);

            // 연결 성공 시 broadcaster 등록 처리
            await SendMessage("Join", "Server", string.Empty, ct);

            // _ => 실행만 시키고 바로 다음 줄 실행(fire and forget)

            _ = ReceiveMessages(ct);

        }
        catch (Exception ex)
        {
            Debug.LogError("소켓 연결 중 오류 : " + ex.Message);
        }

    }

    private async Task ReceiveMessages(CancellationToken ct)
    {
        var buffer = new byte[1024 * 8];

        while (true)
        {

            if (_socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(
                                             new ArraySegment<byte>(buffer),
                                             ct);

                // 종료 처리
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "",
                        ct
                        );

                    Debug.Log("소켓 연결 종료");
                    break;
                }

                // 메시지 수신
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var received = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    try
                    {
                        var message = JsonConvert.DeserializeObject<WebSocketMessage>(received);

                        switch (message.Type)
                        {
                            case "offer":
                                await HandleOfferAsync(message, ct);
                                break;
                            case "ice":
                                await HandleRemoteIce(message);
                                break;
                            case "cameraChange":
                                await HandleCameraChangeAsync(message, ct);
                                break;
                            case "System":
                                Debug.Log("[ServerMessage] " + message.Payload);
                                break;
                        }

                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("JSON 파싱 중 오류 : " + ex.Message);
                    }
                }
            }

        }
    }




    #region message 처리부분
    async Task HandleOfferAsync(WebSocketMessage message, CancellationToken ct)
    {
        var clientId = message.SenderId;

        // 클라이언트가 볼 카메라 이름 결정
        if (!_clientCameraMap.TryGetValue(clientId, out var cameraName))
        {
            Camera defaultCam = null;

            if (_sourceCamera != null)
            {
                defaultCam = _sourceCamera;
            } else if (Camera.main != null)
            {
                defaultCam = Camera.main;
            } else
            {
                Camera.allCameras.FirstOrDefault();
            }
            if (defaultCam == null)
            {
                Debug.LogWarning($"[WebRTC] ({clientId}) 사용할 카메라가 없습니다.");
                return;
            }

            cameraName = defaultCam.name;
            _clientCameraMap[clientId] = cameraName;
        }

        // VideoTrack 생성/공유
        var videoTrack = await GetOrCreateVideoTrackForCameraAsync(cameraName);
        if (videoTrack == null)
        {
            Debug.LogError($"[WebRTC] ({clientId}) VideoTrack 생성 실패 (camera: {cameraName})");
            return;
        }

        var offerDto = JsonConvert.DeserializeObject<SdpDTO>(message.Payload);

        // 기존 peer 정보 있을 경우 초기화
        if (_pcs.TryGetValue(clientId, out var peer) && peer != null)
        {
            peer.Close();
            peer.Dispose();
        }
        _pcs.TryRemove(clientId, out _);


        // peerConnection 생성
        var pc = CreatePeerConnection(clientId, cameraName, ct);
        _pcs[clientId] = pc;

        var offer = new RTCSessionDescription
        {
            type = RTCSdpType.Offer,
            sdp = offerDto.sdp
        };

        // SDP 정보 저장
        var opRemote = pc.SetRemoteDescription(ref offer);

        while (!opRemote.IsDone) await Task.Yield();
        if (opRemote.IsError)
        {
            Debug.LogError($"[WebRTC] ({clientId}) SetRemoteDescription failed: {opRemote.Error.message}");
            ClosePeerForClient(clientId);
            return;
        }

        PendingRemoteIce(clientId);

        // Answer 생성
        var opAnswer = pc.CreateAnswer();
        while (!opAnswer.IsDone) await Task.Yield();
        if (opAnswer.IsError)
        {
            Debug.LogError($"[WebRTC] ({clientId}) CreateAnswer failed: {opAnswer.Error.message}");
            ClosePeerForClient(clientId);
            return;
        }

        var answerDesc = opAnswer.Desc;

        // Local 설정에 Answer 등록
        var opLocal = pc.SetLocalDescription(ref answerDesc);
        while (!opLocal.IsDone) await Task.Yield();
        if (opLocal.IsError)
        {
            Debug.LogError($"[WebRTC] ({clientId}) SetLocalDescription failed: {opLocal.Error.message}");
            ClosePeerForClient(clientId);
            return;
        }

        var answerDTO = new SdpDTO
        {
            type = "answer",
            sdp = answerDesc.sdp,
        };

        // answer 정보 전송
        await SendMessage("Answer", clientId, JsonConvert.SerializeObject(answerDTO), ct);

        Debug.Log("[SocketRTC] Answer sent");
    }


    private RTCPeerConnection CreatePeerConnection(string clientId, string cameraName, CancellationToken ct)
    {

        // stun 서버 설정 (임시로 하나만 설정)
        var iceServerList = new List<RTCIceServer>();
        iceServerList.Add(new RTCIceServer { urls = _stunUrl });

        var config = new RTCConfiguration
        {
            iceServers = iceServerList.ToArray()
        };


        RTCPeerConnection pc = new RTCPeerConnection(ref config);

        // video트랙 가져오기
        if (!_videoTracks.TryGetValue(cameraName, out var videoTrack) || videoTrack == null)
        {
            Debug.LogError($"[WebRTC] ({clientId}) VideoTrack이 없습니다. camera: {cameraName}");
            return pc;
        }
        var sender = pc.AddTrack(videoTrack);

        var transceiver = pc.GetTransceivers().FirstOrDefault(t => t.Sender == sender);
        if (transceiver != null)
        {
            transceiver.Direction = RTCRtpTransceiverDirection.SendOnly;
        }

        pc.OnIceCandidate = async (candidate) =>
        {
            if (candidate == null) return;

            if (_socket.State == WebSocketState.Open)
            {
                var client = clientId; // offer 에서 들어온 SenderId
                var dto = new IceDTO
                {
                    candidate = candidate.Candidate,
                    sdpMid = candidate.SdpMid,
                    sdpMLineIndex = candidate.SdpMLineIndex
                };
                await SendMessage("ice", clientId, JsonConvert.SerializeObject(dto), ct);
            }
        };

        return pc;
    }

    // messageType 이 ice 일 때의 ICE처리
    private async Task HandleRemoteIce(WebSocketMessage message)
    {
        var clientId = message.SenderId;
        var ice = JsonConvert.DeserializeObject<IceDTO>(message.Payload);

        if (!_pcs.TryGetValue(clientId, out var pc) || pc == null)
        {
            if (!_iceByClient.TryGetValue(clientId, out var list))
            {
                list = new List<IceDTO>();
                _iceByClient[clientId] = list;
            }
            list.Add(ice);
            return;
        }

        AddRemoteIceToPC(clientId, pc, ice);
    }


    #endregion


    #region 카메라 처리 메서드
    private async Task HandleCameraChangeAsync(WebSocketMessage message, CancellationToken ct)
    {
        try
        {
            var clientId = message.SenderId;
            var cameraName = message.Payload;

            _clientCameraMap[clientId] = cameraName;

            var track = await GetOrCreateVideoTrackForCameraAsync(cameraName);
            if (track == null)
                return;

            await RecreatePeerConnectionAsync(clientId, cameraName, ct);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SocketRTC] 카메라 변경 중 오류: {ex.Message}");
        }
    }

    async Task RecreatePeerConnectionAsync(string clientId, string cameraName, CancellationToken ct)
    {
        // 기존 PC 정리
        if (_pcs.TryGetValue(clientId, out var oldPc) && oldPc != null)
        {
            try
            {
                oldPc.Close();
                oldPc.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SocketRTC] 기존 PeerConnection 정리 중 오류: {ex.Message}");
            }
        }
        _pcs.TryRemove(clientId, out _);

        // 새 PC는 다음 Offer가 올 때 `HandleOfferAsync`에서 다시 만들어도 되고,
        // 여기서 바로 만들어서 사용하는 패턴도 가능
        // (현재 구조에서는 Offer가 다시 오면 자연스럽게 새 PC를 만들도록 두는 게 단순합니다)
    }


    private async Task<VideoStreamTrack> GetOrCreateVideoTrackForCameraAsync(string cameraName)
    {
        // 이미 있으면 그대로 공유
        if (_videoTracks.TryGetValue(cameraName, out var existingTrack) && existingTrack != null)
            return existingTrack;

        // 카메라 찾기
        var camera = Camera.allCameras.FirstOrDefault(c => c.name == cameraName);
        if (camera == null)
        {
            Debug.LogError($"[SocketRTC] 카메라를 찾을 수 없습니다: {cameraName}");
            return null;
        }

        _cameras[cameraName] = camera;

        // RenderTexture 생성/재사용
        if (_renderTextures.TryGetValue(cameraName, out var oldRt) && oldRt != null)
        {
            oldRt.Release();
            Destroy(oldRt);
        }

        var rt = new RenderTexture(_width, _height, 0, RenderTextureFormat.ARGB32);
        rt.Create();
        _renderTextures[cameraName] = rt;

        // 카메라에 한 번만 targetTexture 설정
        camera.targetTexture = rt;

        // 하나의 VideoTrack 생성해서 공유
        var track = camera.CaptureStreamTrack(_width, _height);
        _videoTracks[cameraName] = track;

        Debug.Log($"[SocketRTC] VideoTrack 생성: {cameraName} ({_width}x{_height}@{_fps})");

        return track;
    }
    #endregion


    // peerConnection 객체 정리
    void ClosePeerForClient(string clientId)
    {
        if (_pcs.TryGetValue(clientId, out var pc) && pc != null)
        {
            try
            {
                pc.Close();
                pc.Dispose();
            }
            catch { /*ignore*/}
        }

        _pcs.Remove(clientId, out _);

        // 어떤 카메라를 쓰고 있었는지 조회
        if (_clientCameraMap.TryRemove(clientId, out var cameraName))
        {
            // 이 카메라를 쓰는 다른 클라이언트가 남아있는지 확인
            bool anyOther = _clientCameraMap.Values.Any(name => name == cameraName);
            if (!anyOther)
            {
                // 아무도 안 쓰면 비디오 리소스 정리
                if (_videoTracks.TryRemove(cameraName, out var vt) && vt != null)
                    vt.Dispose();

                if (_renderTextures.TryRemove(cameraName, out var rt) && rt != null)
                {
                    rt.Release();
                    UnityEngine.Object.Destroy(rt);
                }

                _cameras.TryRemove(cameraName, out _);
            }
        }

        _iceByClient.Remove(clientId, out _);
        _remoteIceByClient.Remove(clientId, out _);
    }

    #region remote ICE 처리
    private void PendingRemoteIce(string clientId)
    {
        if (!_iceByClient.TryGetValue(clientId, out var list) || list.Count == 0) return;
        if (!_pcs.TryGetValue(clientId, out var pc) || pc == null) return;

        foreach (var ice in list)
            AddRemoteIceToPC(clientId, pc, ice);

        list.Clear();
    }

    private void AddRemoteIceToPC(string clientId, RTCPeerConnection pc, IceDTO ice)
    {
        var init = new RTCIceCandidateInit
        {
            candidate = ice.candidate,
            sdpMid = ice.sdpMid,
            sdpMLineIndex = ice.sdpMLineIndex,
        };

        pc.AddIceCandidate(new RTCIceCandidate(init));
    }
    #endregion

    // 메시지 전송
    private async Task SendMessage(string type, string receiver, string message, CancellationToken ct)
    {
        if (_socket == null || _socket.State != WebSocketState.Open)
        {
            Debug.LogWarning("[SocketRTC] 소켓이 열려있지 않습니다.");
            return;
        }

        var buffer = new WebSocketMessage
        {
            Type = type,
            Payload = message,
            SenderId = _roomId,
            SenderType = "Broadcaster",
            ReceiverId = receiver,
            Timestamp = DateTimeOffset.UtcNow
        };

        var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(buffer));

        await _socket.SendAsync(
               new ArraySegment<byte>(bytes),
               WebSocketMessageType.Text,
               true,
               ct
            );
    }


    // 종료시 객체 정리
    private async void OnDestroy()
    {
        _cts.Cancel();
        if (_socket.State == WebSocketState.Open)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }
        _socket.Dispose();

        foreach (var key in _pcs.Keys.ToList())
        {
            ClosePeerForClient(key);
        }

        if (_sourceCamera != null)
        {
            _sourceCamera.targetTexture = null;
        }

    }
}

public class SdpDTO
{
    public string type { get; set; }
    public string sdp { get; set; }
}

public class IceDTO
{
    public string candidate { get; set; }
    public string sdpMid { get; set; }
    public int? sdpMLineIndex { get; set; }
}

using System.Text.Json;
using Microsoft.MixedReality.WebRTC;
using WebSocketSharp;

namespace Test
{
    internal class SigRegisterResponse
    {
        public string type { get; set; }
        public string connectionId { get; set; }
        public bool isExistClient { get; set; }
        public bool isExistUser { get; set; }
        public bool isInitiator { get; set; }
        public IceServer[] iceServers { get; set; }

        public SigRegisterResponse(
            string type, string connectionId,
            bool isExistClient, bool isExistUser, bool isInitiator,
            IceServer[] iceServers)
        {
            this.type = type;
            this.connectionId = connectionId;
            this.isExistClient = isExistClient;
            this.isExistUser = isExistUser;
            this.isInitiator = isInitiator;
            this.iceServers = iceServers;
        }
    };

    internal class IceServer
    {
        public string credential { get; set; }
        public string username { get; set; }
        public string[] urls { get; set; }

        public IceServer(string credential, string username, string[] urls)
        {
            this.credential = credential;
            this.username = username;
            this.urls = urls;
        }
    };

    internal class SigContext
    {
        public string Url = "";
        public string SigKey = "";
        public string RoomId = "";
    }

    internal class SigClient
    {
        protected WebSocketSharp.WebSocket _webSocket;

        public delegate void SigRegisterResponseReceivedDelegate(SigRegisterResponse response);
        public delegate void SdpMessageReceivedDelegate(SdpMessage message);
        public delegate void IceCandidateReceivedDelegate(IceCandidate candidate);

        public WebRTCController.ErrorDelegate? SigError;
        public SigRegisterResponseReceivedDelegate? SigRegisterResponseReceived;
        public SdpMessageReceivedDelegate? SdpMessageReceived;
        public IceCandidateReceivedDelegate? IceCandidateReceived;

        private SigContext _context;

        public SigClient(SigContext context)
        {
            _context = context;

            _webSocket = new WebSocketSharp.WebSocket(_context.Url);
            _webSocket.SslConfiguration.EnabledSslProtocols =
                System.Security.Authentication.SslProtocols.Tls13;

            _webSocket.OnOpen += (sender, e) =>
            {
                Logger.Trace("websocket open");
            };

            _webSocket.OnClose += (sender, e) =>
            {
                Logger.Trace("websocket close");

                ErrorHandle("切断されました");
            };

            _webSocket.OnError += (sender, e) =>
            {
                Logger.Trace("*** websocket error");

                ErrorHandle("WebSocket Error");
            };

            _webSocket.OnMessage += (sender, e) =>
            {
                Logger.Trace("websocket receive");

                if (!e.IsText)
                {
                    return;
                }

                Logger.Trace($"{e.Data}");

                MessageHandle(e.Data);
            };
        }

        public bool Connect()
        {
            _webSocket.Connect();

            return _webSocket.ReadyState == WebSocketState.Open;
        }

        protected void MessageHandle(string message)
        {
            try
            {
                JsonDocument doc = JsonDocument.Parse(message);
                JsonElement root = doc.RootElement;

                string? type = root.GetProperty("type").GetString();

                if (type == null)
                {
                    ErrorHandle("Protocol Error (type)");
                }
                else if (type == "candidate")
                {
                    string iceStr = root.GetProperty("ice").ToString();
                    JsonDocument iceDoc = JsonDocument.Parse(iceStr);
                    JsonElement iceRoot = iceDoc.RootElement;

                    string sdpMid = "0";
                    JsonElement sdpMidElement;

                    if (iceRoot.TryGetProperty("sdpMid", out sdpMidElement))
                    {
                        sdpMid = sdpMidElement.ToString();
                    }

                    int sdpMlineIndex = 0;
                    JsonElement sdpMlineIndexElement;

                    if (iceRoot.TryGetProperty("sdpMLineIndex", out sdpMlineIndexElement))
                    {
                        sdpMlineIndex = sdpMlineIndexElement.GetInt32();
                    }

                    string? content = iceRoot.GetProperty("candidate").GetString();

                    if (content != null)
                    {
                        IceCandidate candidate = new IceCandidate()
                        {
                            SdpMid = sdpMid,
                            SdpMlineIndex = sdpMlineIndex,
                            Content = content
                        };

                        IceCandidateReceived?.Invoke(candidate);
                    }
                    else
                    {
                        ErrorHandle("Protocol Error (candidate)");
                    }
                }
                else if (type == "offer" || type == "answer")
                {
                    string sdpStr = root.GetProperty("sdp").ToString();
                    string content = sdpStr.Replace("\\r\\n", "\r\n");

                    SdpMessage sdp = new SdpMessage()
                    {
                        Type = SdpMessage.StringToType(type),
                        Content = sdpStr
                    };

                    SdpMessageReceived?.Invoke(sdp);
                }
                else if (type == "accept")
                {
                    SigRegisterResponse? response =
                        JsonSerializer.Deserialize<SigRegisterResponse>(message);

                    if (response != null)
                    {
                        SigRegisterResponseReceived?.Invoke(response);
                    }
                    else
                    {
                        ErrorHandle("Protocol Error (accept)");
                    }
                }
                else if (type == "ping")
                {
                    _webSocket.Send("{\"type\":\"pong\"}");
                }
                else
                {
                    ErrorHandle($"Protocol Error (unknown type:{type})");
                }
            }
            catch (Exception e)
            {
                ErrorHandle($"Protocol Error (Parse:{e.Message})");
            }
        }

        private void ErrorHandle(string message)
        {
            SigError?.Invoke(message);
        }

        public void Close()
        {
            SigError = null;
            _webSocket.Close();
        }

        public bool SendRegister()
        {
            var random = new Random();
            int cliendId = random.Next();

            string jsonStr = "{";
            jsonStr += "\"type\":\"register\"";
            jsonStr += ",";
            jsonStr += $"\"roomId\":\"{_context.RoomId}\"";
            jsonStr += ",";
            jsonStr += $"\"clientId\":\"{cliendId}\"";
            jsonStr += ",";
            jsonStr += $"\"key\":\"{_context.SigKey}\"";
            jsonStr += ",";
            jsonStr += "\"authnMetadata\":{}";
            jsonStr += "}";

            Logger.Trace($"websocket send (reg)");
            Logger.Trace(jsonStr);

            try
            {
                _webSocket.Send(jsonStr);
            }
            catch (Exception e)
            {
                Logger.Trace($"*** Exception(SendRegister): {e.Message}");

                return false;
            }

            return true;
        }

        public bool SendIceCanditate(IceCandidate candidate)
        {
            string ice = "{";
            ice += $"\"candidate\":\"{candidate.Content}\"";
            ice += ",";
            ice += $"\"sdpMid\":\"{candidate.SdpMid}\"";
            ice += ",";
            ice += $"\"sdpMLineIndex\":{candidate.SdpMlineIndex}";
            ice += "}";

            string jsonStr = $"{{\"type\":\"candidate\",\"ice\":{ice}}}";

            Logger.Trace($"websocket send (ice)");
            Logger.Trace(jsonStr);

            try
            {
                _webSocket.Send(jsonStr);
            }
            catch (Exception e)
            {
                Logger.Trace($"*** Exception(SendIceCanditate): {e.Message}");

                return false;
            }

            return true;
        }

        public bool SendSdpMessage(SdpMessage message)
        {
            string type = SdpMessage.TypeToString(message.Type);
            string content = message.Content.Replace("\r\n", "\\r\\n");
            string jsonStr = $"{{\"type\":\"{type}\",\"sdp\":\"{content}\"}}";

            Logger.Trace($"websocket send (sdp)");
            Logger.Trace(jsonStr);

            try
            {
                _webSocket.Send(jsonStr);
            }
            catch (Exception e)
            {
                Logger.Trace($"*** Exception(SendSdpMessage): {e.Message}");

                return false;
            }

            return true;
        }
    }
}

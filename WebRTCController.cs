using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.MixedReality.WebRTC;

namespace Test
{
    internal class WebRTCController
    {
        private SigClient? _sigClient;
        private PeerConnection? _peerConnection;
        private VideoTrackSource? _videoSource;
        private LocalVideoTrack? _localVideoTrack;

        public BitmapSource? _localImage;
        public BitmapSource? _remoteImage;

        private readonly object _lockLocalImage = new object();
        private readonly object _lockRemoteImage = new object();

        private Transceiver.Direction _direction;

        public delegate void ConnectedDelegate();
        public delegate void ErrorDelegate(string message);

        public ConnectedDelegate? Connected;
        public ErrorDelegate? Error;

        public bool ConnectSigServer(bool send, bool receive, SigContext context)
        {
            _sigClient = new SigClient(context);
            _sigClient.SigError = Error;
            _sigClient.IceCandidateReceived = OnIceCandidateReceived;
            _sigClient.SdpMessageReceived = OnSdpMessageReceived;
            _sigClient.SigRegisterResponseReceived = OnSigRegisterResponseReceived;

            _direction = Transceiver.DirectionFromSendRecv(send, receive);

            if (!_sigClient.Connect())
            {
                Logger.Trace("*** Failed to connect sig server");

                return false;
            }

            if (!_sigClient.SendRegister())
            {
                Logger.Trace("*** Failed to send register");

                return false;
            }

            return true;
        }

        public async Task<bool> CreateLocalVideoDevice()
        {
            try
            {
                if (_videoSource != null)
                {
                    return true;
                }

                _videoSource = await DeviceVideoTrackSource.CreateAsync();

                if (_videoSource == null)
                {
                    return false;
                }

                var videoTrackConfig = new LocalVideoTrackInitConfig
                {
                    trackName = "webcam_track"
                };

                _localVideoTrack =
                    LocalVideoTrack.CreateFromSource(_videoSource, videoTrackConfig);

                if (_localVideoTrack == null)
                {
                    _videoSource.Dispose();
                    _videoSource = null;

                    return false;
                }

                _videoSource.Argb32VideoFrameReady += OnLocalVideoFrameReady;
            }
            catch (Exception e)
            {
                Logger.Trace($"*** Local Camera Error: {e.Message}");

                return false;
            }

            return true;
        }

        public void Destroy()
        {
            _sigClient?.Close();
            _localVideoTrack?.Dispose();
            _videoSource?.Dispose();
            _peerConnection?.Dispose();

            _sigClient = null;
            _videoSource = null;
            _localVideoTrack = null;
            _peerConnection = null;

            _localImage = null;
            _remoteImage = null;
        }

        private void ErrorHandle(string message)
        {
            Error?.Invoke(message);
        }

        private async void OnSigRegisterResponseReceived(SigRegisterResponse resopnse)
        {
            if (resopnse.iceServers.Length == 0)
            {
                ErrorHandle("Protocol Error (OnSigRegisterResponseReceived)");

                return;
            }

            IceServer server = resopnse.iceServers[0];

            PeerConnectionConfiguration configuration = new PeerConnectionConfiguration
            {
                IceServers = new List<Microsoft.MixedReality.WebRTC.IceServer> {
                    new Microsoft.MixedReality.WebRTC.IceServer{
                        Urls = new List<string>{ },
                        TurnUserName = server.username,
                        TurnPassword = server.credential
                    }
                }
            };
            configuration.IceServers[0].Urls.AddRange(server.urls);

            _peerConnection = new PeerConnection();

            _peerConnection.IceCandidateReadytoSend += OnIceCandidateReadytoSend;
            _peerConnection.LocalSdpReadytoSend += OnLocalSdpReadytoSend;
            _peerConnection.Connected += OnPeerConnected;
            _peerConnection.VideoTrackAdded += OnRemoteVideoTrackAdded;
            _peerConnection.AudioTrackAdded += OnRemoteAudioTrackAdded;
            _peerConnection.IceStateChanged += OnIceStateChanged;
            _peerConnection.IceGatheringStateChanged += OnIceGatheringStateChanged;
            _peerConnection.RenegotiationNeeded += OnRenegotiationNeeded;

            await _peerConnection.InitializeAsync(configuration);

            var videoTransceiver = _peerConnection.AddTransceiver(MediaKind.Video);
            videoTransceiver.DesiredDirection = _direction;

            if (_direction != Transceiver.Direction.ReceiveOnly)
            {
                videoTransceiver.LocalVideoTrack = _localVideoTrack;
            }

            if (resopnse.isExistClient)
            {
                _peerConnection.CreateOffer();
            }
        }

        private void OnIceCandidateReadytoSend(IceCandidate candidate)
        {
            Logger.Trace($"OnIceCandidateReadytoSend({candidate.Content})");

            _sigClient?.SendIceCanditate(candidate);
        }

        private void OnLocalSdpReadytoSend(SdpMessage message)
        {
            Logger.Trace($"OnLocalSdpReadytoSend({message.Content})");

            _sigClient?.SendSdpMessage(message);
        }

        private void OnIceCandidateReceived(IceCandidate candidate)
        {
            Logger.Trace($"OnIceCandidateReceived({candidate.Content})");

            _peerConnection?.AddIceCandidate(candidate);
        }

        private async void OnSdpMessageReceived(SdpMessage message)
        {
            Logger.Trace($"OnSdpMessageReceived([{message.Type.ToString()}] {message.Content})");

            try
            {
                await _peerConnection!.SetRemoteDescriptionAsync(message);

                if (message.Type == SdpMessageType.Offer)
                {
                    _peerConnection.CreateAnswer();
                }
            }
            catch (Exception e)
            {
                Logger.Trace($"*** Exception(OnSdpMessageReceived): {e.Message}");

                ErrorHandle("Protocol Error (OnSdpMessageReceived)");

                return;
            }
        }

        private void OnPeerConnected()
        {
            Logger.Trace("OnPeerConnected");

            Connected?.Invoke();
        }

        private void OnIceStateChanged(IceConnectionState newState)
        {
            Logger.Trace($"OnIceStateChanged({newState})");
        }

        private void OnIceGatheringStateChanged(IceGatheringState newState)
        {
            Logger.Trace($"OnIceGatheringStateChanged({newState})");
        }

        private void OnRemoteVideoTrackAdded(RemoteVideoTrack track)
        {
            track.Argb32VideoFrameReady += OnRemoteVideoFrameReady;
        }

        private void OnRemoteAudioTrackAdded(RemoteAudioTrack track)
        {
            track.OutputToDevice(false);
        }

        private void OnRenegotiationNeeded()
        {
            Logger.Trace($"OnRenegotiationNeeded");
            
            // _peerConnection.CreateOffer();
        }

        private BitmapSource VideoFrameToBitmap(Argb32VideoFrame frame)
        {
            return BitmapSource.Create(
                (int)frame.width, (int)frame.height, 96, 96,
                PixelFormats.Bgra32, null, frame.data,
                (int)(frame.stride * frame.height), frame.stride);
        }

        private void OnLocalVideoFrameReady(Argb32VideoFrame frame)
        {
            lock (_lockLocalImage)
            {
                _localImage = VideoFrameToBitmap(frame);
                _localImage?.Freeze();
            }
        }

        private void OnRemoteVideoFrameReady(Argb32VideoFrame frame)
        {
            lock (_lockRemoteImage)
            {
                _remoteImage = VideoFrameToBitmap(frame);
                _remoteImage?.Freeze();
            }
        }

        public BitmapSource? GetLocalImage()
        {
            BitmapSource? image;

            lock (_lockLocalImage)
            {
                image = _localImage;
                _localImage = null;
            }

            return image;
        }
        public BitmapSource? GetRemoteImage()
        {
            BitmapSource? image;

            lock (_lockRemoteImage)
            {
                image = _remoteImage;
                _remoteImage = null;
            }

            return image;
        }
    }
}

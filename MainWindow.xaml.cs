using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;
using System.Configuration;

namespace Test
{
    public partial class MainWindow : Window
    {
        private readonly string AppTitle = "テストアプリ";

        private SigContext _sigContext;

        private WebRTCController _webrtcController;
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _fpsTimer;

        private uint _localFpsCounter;
        private uint _remoteFpsCounter;

        private bool _isRunning;

        public MainWindow()
        {
            InitializeComponent();

            this.Closing += new CancelEventHandler(OnWindowClose);

            _webrtcController = new WebRTCController();
            _webrtcController.Connected = OnPeerConnected;
            _webrtcController.Error = OnWebRTCError;

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = new TimeSpan(0, 0, 0, 0, 10); // 10 msec
            _refreshTimer.Tick += new EventHandler(OnRefreshTimer);

            _fpsTimer = new DispatcherTimer();
            _fpsTimer.Interval = new TimeSpan(0, 0, 0, 1); // 1sec
            _fpsTimer.Tick += new EventHandler(OnFpsTimer);

            this.BothButton.IsChecked = true;

            _sigContext = new SigContext();

            try
            {
                var exeFileMap =
                    new ExeConfigurationFileMap { ExeConfigFilename = "./test.config" };
                var config =
                    ConfigurationManager.OpenMappedExeConfiguration(exeFileMap, ConfigurationUserLevel.None);

                _sigContext.Url = config.AppSettings.Settings["Url"]?.Value ?? "";
                _sigContext.SigKey = config.AppSettings.Settings["SigKey"]?.Value ?? "";
                _sigContext.RoomId = config.AppSettings.Settings["RoomId"]?.Value ?? "";
            }
            catch (Exception e)
            {
                Logger.Trace($"*** Exception(Config): {e.Message}");
            }

            if (_sigContext.Url == "" || _sigContext.SigKey == "" || _sigContext.RoomId == "")
            {
                MessageBox.Show(
                    "設定ファイルの読み込みに失敗しました",
                    AppTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);

                Application.Current.Shutdown();

                return;
            }

            SetStatus("");
        }

        private async Task<bool> Start(bool send, bool receive)
        {
            if (_isRunning)
            {
                return false;
            }

            this.StartButton.IsEnabled = false;

            if (send)
            {
                if (!await _webrtcController.CreateLocalVideoDevice())
                {
                    MessageBox.Show(
                        "カメラに接続できませんでした",
                        AppTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error);

                    this.StartButton.IsEnabled = true;

                    return false;
                }
            }

            if (!_webrtcController.ConnectSigServer(send, receive, _sigContext))
            {
                MessageBox.Show(
                    "シグナリングサーバーに接続できませんでした",
                    AppTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);

                this.StartButton.IsEnabled = true;

                return false;
            }

            _refreshTimer.Start();
            _fpsTimer.Start();

            this.StartButton.Content = "停止";
            this.DirectionButtons.IsEnabled = false;
            this.StartButton.IsEnabled = true;

            SetStatus("(待機中)");

            _isRunning = true;

            return true;
        }

        private void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            SetStatus("(停止処理中 ･･･)");

            _webrtcController.Destroy();

            _refreshTimer.Stop();
            _fpsTimer.Stop();

            this.LocalFps.Text = "FPS 0";
            this.RemoteFps.Text = "FPS 0";

            this.LocalImage.Source = null;
            this.RemoteImage.Source = null;

            _remoteFpsCounter = 0;
            _localFpsCounter = 0;

            this.StartButton.Content = "開始";
            this.DirectionButtons.IsEnabled = true;

            SetStatus("");

            _isRunning = false;
        }

        private void SetStatus(string message)
        {
            this.Title = AppTitle + " " + message;
        }

        private void ErrorHandle(string message)
        {
            MessageBox.Show(
                message,
                AppTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);

            Stop();
        }

        private void OnRefreshTimer(object? sender, EventArgs e)
        {
            var localImage = _webrtcController.GetLocalImage();

            if (localImage != null)
            {
                this.LocalImage.Source = localImage;
                _localFpsCounter++;
            }

            var remoteImage = _webrtcController.GetRemoteImage();

            if (remoteImage != null)
            {
                if (this.EffectCheck.IsChecked ?? false)
                {
                    remoteImage = Misc.ApplyEffect(remoteImage);
                }
 
                this.RemoteImage.Source = remoteImage;
                _remoteFpsCounter++;
            }
        }

        private void OnFpsTimer(object? sender, EventArgs e)
        {
            this.LocalFps.Text = "FPS " + _localFpsCounter.ToString();
            _localFpsCounter = 0;

            this.RemoteFps.Text = "FPS " + _remoteFpsCounter.ToString();
            _remoteFpsCounter = 0;
        }

        private void OnWindowClose(object? sender, CancelEventArgs e)
        {
            Logger.Trace("OnWindowClose");

            Stop();
        }

        private void OnWebRTCError(string message)
        {
            Dispatcher.BeginInvoke(new Action(() => { ErrorHandle(message); }));
        }

        private void OnPeerConnected()
        {
            Dispatcher.BeginInvoke(new Action(() => { SetStatus("稼働中"); }));
        }

        private async void OnStartButtonClick(object? sender, RoutedEventArgs e)
        {
            if (!_isRunning)
            {
                bool bothChecked = this.BothButton.IsChecked ?? false;
                bool sendChecked = this.SendButton.IsChecked ?? false;
                bool receiveChecked = this.ReceiveButton.IsChecked ?? false;

                bool send = false;
                bool receive = false;

                if (bothChecked)
                {
                    send = true;
                    receive = true;
                }
                else if (sendChecked)
                {
                    send = true;
                }
                else if (receiveChecked)
                {
                    receive = true;
                }
                else
                {
                    return;
                }

                if (!await Start(send, receive))
                {
                    Stop();
                }
            }
            else
            {
                Stop();
            }
        }
    }
}

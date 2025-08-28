using Microsoft.Maui.Devices.Sensors;
using System.ComponentModel;
using System.Timers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace SensorManager
{
    public partial class MainPage : ContentPage, INotifyPropertyChanged
    {
        private double _xAngle;
        private double _yAngle;
        private string _statusMessage;
        private string _timerTitle;
        private string _timerValue;
        private bool _isMonitoring;
        private bool _isTimerVisible;
        private int _delaySeconds;
        private int _stableTimeSeconds;
        private double _threshold;
        private bool _vibrationEnabled;
        private System.Timers.Timer _countdownTimer;
        private int _remainingSeconds;
        private bool _isStablePeriod;
        private List<TiltRecord> _tiltRecords;
        private DateTime _monitoringStartTime;
        private bool _isTilted;
        private bool _hasVibrationPermission;
        private bool _hasSensorsPermission;
        private string _displayXAngle;
        private string _displayYAngle;
        private string _tiltStatus;
        private DateTime _recordingStartTime;
        private List<TiltEvent> _tiltEvents;
        private Color _originalTimerColor;
        private bool _isBlinking;
        private System.Timers.Timer _blinkTimer;
        private Color _timerTextColor;
        private bool _radarVibrationEnabled;
        private int _vibrationIntensity;
        private System.Timers.Timer _vibrationTimer;
        private double _currentTiltAmount;
        private DateTime _lastVibrationTime;
        private double _baselineXAngle;
        private double _baselineYAngle;
        private bool _isRelativeBaseline;


        public string TiltStatus
        {
            get => _tiltStatus;
            set
            {
                if (_tiltStatus != value)
                {
                    _tiltStatus = value;
                    OnPropertyChanged(nameof(TiltStatus));
                }
            }
        }

        public class TiltEvent
        {
            public DateTime AbsoluteTime { get; set; }
            public TimeSpan RelativeTime { get; set; }
            public TimeSpan StableTimeOffset { get; set; }
            public double XAngle { get; set; }
            public double YAngle { get; set; }
            public string EventType { get; set; }
            public bool IsRelative { get; set; }
        }

        public string DisplayXAngle
        {
            get => _displayXAngle;
            set
            {
                if (_displayXAngle != value)
                {
                    _displayXAngle = value;
                    OnPropertyChanged(nameof(DisplayXAngle));
                }
            }
        }

        public string DisplayYAngle
        {
            get => _displayYAngle;
            set
            {
                if (_displayYAngle != value)
                {
                    _displayYAngle = value;
                    OnPropertyChanged(nameof(DisplayYAngle));
                }
            }
        }

        public Color TimerTextColor
        {
            get => _timerTextColor;
            set
            {
                if (_timerTextColor != value)
                {
                    _timerTextColor = value;
                    OnPropertyChanged(nameof(TimerTextColor));
                }
            }
        }

        public MainPage()
        {
            InitializeComponent();

            BindingContext = this;
            SetLandscapeOrientation();

            // 初始状态
            _isMonitoring = false;
            IsTimerVisible = true;
            TimerTitle = "稳定时间";
            TimerValue = "00:00:00";
            TiltStatus = "水平";
            _originalTimerColor = Colors.Black;
            _timerTextColor = Colors.Black;
            UpdateButtonState();

            _tiltRecords = new List<TiltRecord>();
            _tiltEvents = new List<TiltEvent>();
            LoadSettings();

            UpdateStableTimeDisplay();
            DisplayXAngle = "0.0°";
            DisplayYAngle = "0.0°";
            StatusMessage = "准备就绪\n点击开始按钮开始记录";

            MessagingCenter.Subscribe<SettingsPage>(this, "SettingsPageDisappearing", (sender) =>
            {
                System.Diagnostics.Debug.WriteLine("收到设置页面消失消息，重启传感器");
                RestartSensor();
            });

            RequestPermissions();
        }

        private void RestartSensor()
        {
            if (_hasSensorsPermission)
            {
                StopAccelerometer();
                StartAccelerometer();
                System.Diagnostics.Debug.WriteLine("传感器已重启");
            }
        }

        private async void RequestPermissions()
        {
            try
            {
                var sensorsStatus = await Permissions.RequestAsync<Permissions.Sensors>();
                _hasSensorsPermission = sensorsStatus == PermissionStatus.Granted;

                var vibrateStatus = await Permissions.RequestAsync<Permissions.Vibrate>();
                _hasVibrationPermission = vibrateStatus == PermissionStatus.Granted;

                if (_hasSensorsPermission)
                {
                    StartAccelerometer();
                }
                else
                {
                    StatusMessage = "需要传感器权限才能监测倾斜状态";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"权限请求失败: {ex.Message}";
            }
        }

        private void SetLandscapeOrientation()
        {
#if ANDROID
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Landscape;
            activity.Window?.SetFlags(
                Android.Views.WindowManagerFlags.Fullscreen,
                Android.Views.WindowManagerFlags.Fullscreen);
#endif
        }

        private void LoadSettings()
        {
            _delaySeconds = Preferences.Get("DelaySeconds", 0);
            int stableHours = Preferences.Get("StableHours", 0);
            int stableMinutes = Preferences.Get("StableMinutes", 0);
            int stableSeconds = Preferences.Get("StableSeconds", 0);

            _stableTimeSeconds = stableHours * 3600 + stableMinutes * 60 + stableSeconds;
            _threshold = Preferences.Get("Threshold", 3.0);
            _vibrationEnabled = Preferences.Get("VibrationEnabled", true);
            _radarVibrationEnabled = Preferences.Get("RadarVibrationEnabled", false);
            _vibrationIntensity = Preferences.Get("VibrationIntensity", 500);
            _isRelativeBaseline = Preferences.Get("IsRelativeBaseline", false);

            UpdateStableTimeDisplay();
        }

        private void UpdateStableTimeDisplay()
        {
            var timeSpan = TimeSpan.FromSeconds(_stableTimeSeconds);
            TimerValue = timeSpan.ToString(@"hh\:mm\:ss");
        }

        public double XAngle
        {
            get => _xAngle;
            set
            {
                if (Math.Abs(_xAngle - value) > 0.01)
                {
                    _xAngle = value;
                    DisplayXAngle = $"{value:F1}°";
                    OnPropertyChanged(nameof(XAngle));

                    UpdateStatusMessage();
                    UpdateBubblePosition();
                    UpdateTiltStatus();
                    UpdateBubbleColor(); // 调用无参数版本
                    CheckTiltStatus();
                }
            }
        }

        public double YAngle
        {
            get => _yAngle;
            set
            {
                if (Math.Abs(_yAngle - value) > 0.01)
                {
                    _yAngle = value;
                    DisplayYAngle = $"{value:F1}°";
                    OnPropertyChanged(nameof(YAngle));

                    UpdateStatusMessage();
                    UpdateBubblePosition();
                    UpdateTiltStatus();
                    UpdateBubbleColor(); // 调用无参数版本
                    CheckTiltStatus();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                }
            }
        }

        public string TimerTitle
        {
            get => _timerTitle;
            set
            {
                if (_timerTitle != value)
                {
                    _timerTitle = value;
                    OnPropertyChanged(nameof(TimerTitle));
                }
            }
        }

        public string TimerValue
        {
            get => _timerValue;
            set
            {
                if (_timerValue != value)
                {
                    _timerValue = value;
                    OnPropertyChanged(nameof(TimerValue));
                }
            }
        }

        public bool IsTimerVisible
        {
            get => _isTimerVisible;
            set
            {
                if (_isTimerVisible != value)
                {
                    _isTimerVisible = value;
                    OnPropertyChanged(nameof(IsTimerVisible));
                }
            }
        }

        private async void OnStartPauseClicked(object sender, EventArgs e)
        {
            if (!_hasSensorsPermission)
            {
                await DisplayAlert("权限不足", "需要传感器权限才能开始记录", "确定");
                RequestPermissions();
                return;
            }

            if (_isMonitoring)
            {
                StopRecording();
                _isMonitoring = false;
                StatusMessage = $"监测中（未记录）\n{TiltStatus}\n纵向角度: {DisplayXAngle}\n横向角度: {DisplayYAngle}";
            }
            else
            {
                StartRecording();
                _isMonitoring = true;
            }
            UpdateButtonState();
        }

        private void OnStopClicked(object sender, EventArgs e)
        {
            if (_isMonitoring)
            {
                StopRecording();
                _isMonitoring = false;
                UpdateButtonState();
            }

            SaveRecords();
            StatusMessage = $"监测中（未记录）\n纵向角度: {DisplayXAngle}\n横向角度: {DisplayYAngle}";

            TimerTitle = "稳定时间";
            UpdateStableTimeDisplay();
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            if (_isMonitoring)
            {
                StopRecording();
                _isMonitoring = false;
                UpdateButtonState();
            }

            SaveRecords();
            StatusMessage = $"监测中（未记录）\n当前状态: {TiltStatus}\n纵向角度: {DisplayXAngle}\n横向角度: {DisplayYAngle}";

            TimerTitle = "稳定时间";
            UpdateStableTimeDisplay();

            await Navigation.PushAsync(new SettingsPage());
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            if (Handler == null)
            {
                MessagingCenter.Unsubscribe<SettingsPage>(this, "SettingsPageDisappearing");
            }
        }

        private void UpdateButtonState()
        {
            if (StartPauseButton != null)
            {
                StartPauseButton.Text = _isMonitoring ? "暂停记录" : "开始记录";
                StartPauseButton.BackgroundColor = _isMonitoring ?
                    Color.FromArgb("#28A745") : Color.FromArgb("#007BFF");
            }
        }

        private void StartRecording()
        {
            LoadSettings();
            _tiltRecords.Clear();
            _tiltEvents.Clear();
            _monitoringStartTime = DateTime.Now;
            _recordingStartTime = DateTime.Now;

            // 捕获当前角度作为基准（如果启用相对基准模式）
            if (_isRelativeBaseline)
            {
                _baselineXAngle = XAngle;
                _baselineYAngle = YAngle;
                System.Diagnostics.Debug.WriteLine($"设置相对基准: X={_baselineXAngle:F1}°, Y={_baselineYAngle:F1}°");
            }
            else
            {
                _baselineXAngle = 0;
                _baselineYAngle = 0;
                System.Diagnostics.Debug.WriteLine("使用绝对水平基准");
            }

            if (_delaySeconds > 0)
            {
                TimerTitle = "倒计时";
                StartCountdown(_delaySeconds, () => StartStablePeriod());
            }
            else
            {
                StartStablePeriod();
            }

            StatusMessage = $"记录中...\n纵向角度: {DisplayXAngle}\n横向角度: {DisplayYAngle}";
        }

        private (double relativeX, double relativeY) CalculateRelativeAngles(double currentX, double currentY)
        {
            if (!_isRelativeBaseline)
                return (currentX, currentY);

            return (currentX - _baselineXAngle, currentY - _baselineYAngle);
        }


        private void StopRecording()
        {
            _countdownTimer?.Stop();
            _countdownTimer?.Dispose();
            _isStablePeriod = false;
            IsTimerVisible = true;
            TimerTitle = "稳定时间";
            StopBlinking();
            StopRadarVibration();
            UpdateStableTimeDisplay();
        }

        private void StartStablePeriod()
        {
            _isStablePeriod = true;
            if (_stableTimeSeconds > 0)
            {
                TimerTitle = "稳定时间";
                StartCountdown(_stableTimeSeconds, () => StopRecording());
            }
            else
            {
                TimerTitle = "记录中";
                TimerValue = "∞";
                StatusMessage = $"记录中（无限时）\n纵向角度: {DisplayXAngle}\n横向角度: {DisplayYAngle}";
            }
        }

        private void StartCountdown(int seconds, Action onComplete)
        {
            _remainingSeconds = seconds;

            _countdownTimer = new System.Timers.Timer(1000);
            _countdownTimer.Elapsed += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _remainingSeconds--;

                    var timeSpan = TimeSpan.FromSeconds(_remainingSeconds);
                    TimerValue = timeSpan.ToString(@"hh\:mm\:ss");

                    if (_remainingSeconds <= 0)
                    {
                        _countdownTimer.Stop();
                        _countdownTimer.Dispose();

                        TriggerStableTimeEndAlert();
                        StartBlinking();

                        onComplete?.Invoke();
                    }
                });
            };
            _countdownTimer.Start();

            var initialTimeSpan = TimeSpan.FromSeconds(_remainingSeconds);
            TimerValue = initialTimeSpan.ToString(@"hh\:mm\:ss");
        }

        private async void TriggerStableTimeEndAlert()
        {
            if (_vibrationEnabled && _hasVibrationPermission)
            {
                try
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(1000));
                }
                catch
                {
                    // 忽略振动异常
                }
            }
            else
            {
                await DisplayAlert("提示", "稳定时间已结束！", "确定");
            }
        }

        private void StartBlinking()
        {
            _isBlinking = true;
            _blinkTimer = new System.Timers.Timer(500);
            _blinkTimer.Elapsed += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TimerTextColor = TimerTextColor == Colors.Red ? Colors.Black : Colors.Red;
                });
            };
            _blinkTimer.Start();
        }

        private void StopBlinking()
        {
            _isBlinking = false;
            _blinkTimer?.Stop();
            _blinkTimer?.Dispose();
            TimerTextColor = Colors.Black;
        }

        private void StartAccelerometer()
        {
            if (Accelerometer.IsSupported && _hasSensorsPermission)
            {
                if (!Accelerometer.IsMonitoring)
                {
                    Accelerometer.ReadingChanged += Accelerometer_ReadingChanged;
                    Accelerometer.Start(SensorSpeed.UI);
                    StatusMessage = $"监测中（未记录）\n纵向角度: {DisplayXAngle}\n横向角度: {DisplayYAngle}";
                }
            }
            else if (!_hasSensorsPermission)
            {
                StatusMessage = "请授予传感器权限";
            }
            else
            {
                StatusMessage = "当前设备不支持加速度计";
            }
        }

        private void StopAccelerometer()
        {
            if (Accelerometer.IsSupported && Accelerometer.IsMonitoring)
            {
                Accelerometer.Stop();
                Accelerometer.ReadingChanged -= Accelerometer_ReadingChanged;
            }
        }

        private void Accelerometer_ReadingChanged(object sender, AccelerometerChangedEventArgs e)
        {
            var reading = e.Reading;

            var xAngle = reading.Acceleration.X * 90;
            var yAngle = reading.Acceleration.Y * 90;

            xAngle = Math.Max(Math.Min(xAngle, 90), -90);
            yAngle = Math.Max(Math.Min(yAngle, 90), -90);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                XAngle = Math.Round(xAngle, 1);
                YAngle = Math.Round(yAngle, 1);
            });
        }

        private void UpdateBubblePosition()
        {
            if (Bubble == null) return;

            var maxMovement = 120;
            var xPos = (XAngle / 90) * maxMovement;
            var yPos = (YAngle / 90) * maxMovement;

            xPos = Math.Max(Math.Min(xPos, maxMovement), -maxMovement);
            yPos = Math.Max(Math.Min(yPos, maxMovement), -maxMovement);

            Bubble.TranslationX = yPos;
            Bubble.TranslationY = xPos;
        }

        private void UpdateStatusMessage()
        {
            string statusPrefix = _isMonitoring ? "记录中" : "监测中（未记录）";
            var (relativeX, relativeY) = CalculateRelativeAngles(XAngle, YAngle);

            string angleInfo = _isRelativeBaseline ?
                $"相对角度: X={relativeX:F1}°, Y={relativeY:F1}°" :
                $"绝对角度: X={DisplayXAngle}, Y={DisplayYAngle}";

            StatusMessage = $"{statusPrefix}\n{angleInfo}\n状态: {TiltStatus}";
        }

        private void CheckTiltStatus()
        {
            if (!_isStablePeriod) return;

            // 计算相对于基准的角度
            var (relativeX, relativeY) = CalculateRelativeAngles(XAngle, YAngle);

            bool isCurrentlyTilted = Math.Abs(relativeX) >= _threshold || Math.Abs(relativeY) >= _threshold;
            _currentTiltAmount = CalculateTiltAmount(relativeX, relativeY);

            // 更新颜色 - 使用相对角度
            UpdateBubbleColor(relativeX, relativeY);

            if (isCurrentlyTilted && !_isTilted)
            {
                OnTiltStarted(relativeX, relativeY);
                StartRadarVibration();
                System.Diagnostics.Debug.WriteLine($"倾斜开始: 相对角度 X={relativeX:F1}°, Y={relativeY:F1}°");
            }
            else if (!isCurrentlyTilted && _isTilted)
            {
                OnTiltEnded(relativeX, relativeY);
                StopRadarVibration();
                System.Diagnostics.Debug.WriteLine("回到基准状态");
            }
            else if (isCurrentlyTilted && _isTilted)
            {
                UpdateRadarVibration();
                // 持续倾斜中也需要更新颜色
                UpdateBubbleColor(relativeX, relativeY);
            }

            _isTilted = isCurrentlyTilted;
        }



        private double CalculateTiltAmount(double xAngle, double yAngle)
        {
            double tiltAmount = Math.Sqrt(xAngle * xAngle + yAngle * yAngle);
            return Math.Max(0, tiltAmount - (_threshold * 0.2));
        }



        private void StopRadarVibration()
        {
            _vibrationTimer?.Stop();
            _vibrationTimer?.Dispose();
            _vibrationTimer = null;
        }

        private void UpdateRadarVibration()
        {
            if (_vibrationTimer == null || !_radarVibrationEnabled)
                return;

            double interval = CalculateVibrationInterval();
            _vibrationTimer.Interval = interval;
        }

        private double CalculateVibrationInterval()
        {
            if (_currentTiltAmount <= 0)
                return 1500; // 无倾斜时1.5秒震动一次

            // 倾斜0.5度时1秒震动一次，倾斜5度时0.1秒震动一次
            double interval = 1050 - (_currentTiltAmount * 190);
            return Math.Max(50, Math.Min(1000, interval)); // 最小间隔50ms，最大1000ms
        }

        private void OnVibrationTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // 检查当前是否仍然处于倾斜状态
            bool isStillTilted = Math.Abs(XAngle) >= _threshold || Math.Abs(YAngle) >= _threshold;
            if (!isStillTilted)
            {
                System.Diagnostics.Debug.WriteLine("震动定时器检测到已回到水平，停止震动");
                StopRadarVibration();
                return;
            }

            if ((DateTime.Now - _lastVibrationTime).TotalMilliseconds < _vibrationTimer.Interval)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // 再次确认当前状态
                    bool isCurrentlyTilted = Math.Abs(XAngle) >= _threshold || Math.Abs(YAngle) >= _threshold;
                    if (isCurrentlyTilted)
                    {
                        double intensityFactor = Math.Min(1.0, _currentTiltAmount / 10.0);
                        int vibrationDuration = (int)(_vibrationIntensity * intensityFactor / 10.0);
                        vibrationDuration = Math.Max(50, Math.Min(1000, vibrationDuration));

                        Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(vibrationDuration));
                        _lastVibrationTime = DateTime.Now;

                        System.Diagnostics.Debug.WriteLine($"震动触发: 间隔={_vibrationTimer.Interval}ms, 持续时间={vibrationDuration}ms");
                    }
                }
                catch
                {
                    // 忽略振动异常
                }
            });
        }

        private void StartRadarVibration()
        {
            if (!_radarVibrationEnabled || !_vibrationEnabled || !_hasVibrationPermission)
            {
                System.Diagnostics.Debug.WriteLine("震动功能未启用或无权限");
                return;
            }

            _vibrationTimer = new System.Timers.Timer(100);
            _vibrationTimer.Elapsed += OnVibrationTimerElapsed;
            _vibrationTimer.Start();

            System.Diagnostics.Debug.WriteLine("雷达震动启动");

            // 立即震动一次
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(_vibrationIntensity / 10.0));
                    _lastVibrationTime = DateTime.Now;
                    System.Diagnostics.Debug.WriteLine("初始震动触发");
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("震动失败");
                }
            });
        }



        private void UpdateBubbleColor(double xAngle, double yAngle)
        {
            if (Bubble == null) return;

            var gradient = new RadialGradientBrush();
            bool shouldBeRed = Math.Abs(xAngle) >= _threshold || Math.Abs(yAngle) >= _threshold;

            if (shouldBeRed)
            {
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb("#FF4444"), 0.0f));
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb("#FF0000"), 1.0f));
            }
            else
            {
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb("#44FF44"), 0.0f));
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb("#00FF00"), 1.0f));
            }

            Bubble.Fill = gradient;
        }
        private void UpdateBubbleColor()
        {
            var (relativeX, relativeY) = CalculateRelativeAngles(XAngle, YAngle);
            UpdateBubbleColor(relativeX, relativeY);
        }

        private void UpdateTiltStatus()
        {
            var (relativeX, relativeY) = CalculateRelativeAngles(XAngle, YAngle);

            if (Math.Abs(relativeX) < _threshold && Math.Abs(relativeY) < _threshold)
            {
                TiltStatus = _isRelativeBaseline ? "基准状态" : "水平";
            }
            else
            {
                TiltStatus = "倾斜";
            }
        }

        private async void TriggerAlerts()
        {
            if (_vibrationEnabled && _hasVibrationPermission && !_radarVibrationEnabled)
            {
                try
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(500));
                }
                catch
                {
                    // 忽略振动异常
                }
            }
        }

        private void OnTiltStarted(double relativeX, double relativeY)
        {
            var record = new TiltRecord
            {
                StartTime = DateTime.Now,
                RelativeTime = DateTime.Now - _monitoringStartTime,
                XAngle = relativeX,
                YAngle = relativeY,
                IsRelative = _isRelativeBaseline
            };
            _tiltRecords.Add(record);

            var tiltEvent = new TiltEvent
            {
                AbsoluteTime = DateTime.Now,
                RelativeTime = DateTime.Now - _recordingStartTime,
                StableTimeOffset = DateTime.Now - _monitoringStartTime,
                XAngle = relativeX,
                YAngle = relativeY,
                EventType = "开始倾斜",
                IsRelative = _isRelativeBaseline
            };
            _tiltEvents.Add(tiltEvent);

            TriggerAlerts();
        }

        private void OnTiltEnded(double relativeX, double relativeY)
        {
            if (_tiltRecords.Count > 0)
            {
                _tiltRecords[^1].EndTime = DateTime.Now;
                _tiltRecords[^1].Duration = DateTime.Now - _tiltRecords[^1].StartTime;

                var tiltEvent = new TiltEvent
                {
                    AbsoluteTime = DateTime.Now,
                    RelativeTime = DateTime.Now - _recordingStartTime,
                    StableTimeOffset = DateTime.Now - _monitoringStartTime,
                    XAngle = relativeX,
                    YAngle = relativeY,
                    EventType = "结束倾斜",
                    IsRelative = _isRelativeBaseline
                };
                _tiltEvents.Add(tiltEvent);
            }

            StopRadarVibration();
        }

        private void SaveRecords()
        {
            int totalRecords = Preferences.Get("RecordCount", 0) + _tiltEvents.Count;
            Preferences.Set("RecordCount", totalRecords);

            var eventsJson = System.Text.Json.JsonSerializer.Serialize(_tiltEvents);
            Preferences.Set("LastTiltEvents", eventsJson);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadSettings();

            if (_hasSensorsPermission && !Accelerometer.IsMonitoring)
            {
                RestartSensor();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (!Navigation.NavigationStack.Contains(this))
            {
                StopAccelerometer();
            }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            if (width < height)
            {
                SetLandscapeOrientation();
            }
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        protected new void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TiltRecord
    {
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan RelativeTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public double XAngle { get; set; }
        public double YAngle { get; set; }
        public bool IsRelative { get; set; }
    }



}
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Maui.Controls;

namespace SensorManager
{
    public partial class SettingsPage : ContentPage, INotifyPropertyChanged
    {
        private bool _isCustomThresholdVisible;
        private string _recordsCountText;
        private bool _isRadarVibrationEnabled;
        private bool _isRelativeBaseline; // 添加这个字段


        public SettingsPage()
        {
            InitializeComponent();
            BindingContext = this;

            SetPortraitOrientation();
            LoadSettingsData();
        }

        public List<int> HoursList { get; } = Enumerable.Range(0, 25).ToList();
        public List<int> MinutesList { get; } = Enumerable.Range(0, 61).ToList();

        public bool IsCustomThresholdVisible
        {
            get => _isCustomThresholdVisible;
            set
            {
                _isCustomThresholdVisible = value;
                OnPropertyChanged(nameof(IsCustomThresholdVisible));
            }
        }

        public string RecordsCountText
        {
            get => _recordsCountText;
            set
            {
                _recordsCountText = value;
                OnPropertyChanged(nameof(RecordsCountText));
            }
        }

        public bool IsRadarVibrationEnabled
        {
            get => _isRadarVibrationEnabled;
            set
            {
                _isRadarVibrationEnabled = value;
                OnPropertyChanged(nameof(IsRadarVibrationEnabled));
            }
        }

        // 添加相对基准属性
        public bool IsRelativeBaseline
        {
            get => _isRelativeBaseline;
            set
            {
                _isRelativeBaseline = value;
                OnPropertyChanged(nameof(IsRelativeBaseline));
            }
        }

        private void SetPortraitOrientation()
        {
#if ANDROID
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;

            activity.Window?.ClearFlags(Android.Views.WindowManagerFlags.Fullscreen);

            var decorView = activity.Window?.DecorView;
            if (decorView != null)
            {
                var uiOptions = (int)decorView.SystemUiVisibility;
                uiOptions &= ~(int)Android.Views.SystemUiFlags.Fullscreen;
                uiOptions &= ~(int)Android.Views.SystemUiFlags.HideNavigation;
                uiOptions &= ~(int)Android.Views.SystemUiFlags.ImmersiveSticky;
                decorView.SystemUiVisibility = (Android.Views.StatusBarVisibility)uiOptions;
            }

            activity.Window?.SetStatusBarColor(Android.Graphics.Color.ParseColor("#FFFFFFFF"));
#endif
        }

        private void LoadSettingsData()
        {
            var stableHours = Preferences.Get("StableHours", 0);
            var stableMinutes = Preferences.Get("StableMinutes", 0);
            var delaySeconds = Preferences.Get("DelaySeconds", 0);
            var threshold = Preferences.Get("Threshold", 3.0);
            var vibrationEnabled = Preferences.Get("VibrationEnabled", true);
            var radarVibrationEnabled = Preferences.Get("RadarVibrationEnabled", false);
            var vibrationIntensity = Preferences.Get("VibrationIntensity", 500);
            var isRelativeBaseline = Preferences.Get("IsRelativeBaseline", false);

            if (HoursPicker != null) HoursPicker.SelectedItem = stableHours;
            if (MinutesPicker != null) MinutesPicker.SelectedItem = stableMinutes;
            if (DelayEntry != null) DelayEntry.Text = delaySeconds.ToString();
            if (VibrationSwitch != null) VibrationSwitch.IsToggled = vibrationEnabled;
            if (RadarVibrationSwitch != null) RadarVibrationSwitch.IsToggled = radarVibrationEnabled;

            // 安全地设置相对基准开关
            var relativeBaselineSwitch = this.FindByName<Switch>("RelativeBaselineSwitch");
            if (relativeBaselineSwitch != null)
            {
                relativeBaselineSwitch.IsToggled = isRelativeBaseline;
            }

            if (VibrationIntensitySlider != null) VibrationIntensitySlider.Value = vibrationIntensity;

            IsRadarVibrationEnabled = radarVibrationEnabled;
            IsRelativeBaseline = isRelativeBaseline;
            SetThresholdSelection(threshold);
            UpdateRecordsCount();
        }

        private void SetThresholdSelection(double threshold)
        {
            IsCustomThresholdVisible = false;

            // 找到包含单选按钮的布局容器
            var radioLayout = this.FindByName<VerticalStackLayout>("ThresholdRadioLayout");
            if (radioLayout == null) return;

            foreach (var child in radioLayout.Children)
            {
                if (child is RadioButton radioButton)
                {
                    if (radioButton.Value is string stringValue && double.TryParse(stringValue, out double radioValue))
                    {
                        if (Math.Abs(threshold - radioValue) < 0.1)
                        {
                            radioButton.IsChecked = true;
                            if (radioValue == 0) // 自定义选项
                            {
                                IsCustomThresholdVisible = true;
                            }
                            break;
                        }
                    }
                }
            }

            // 如果是自定义值，需要额外处理
            if (IsCustomThresholdVisible && CustomThresholdEntry != null)
            {
                CustomThresholdEntry.Text = threshold.ToString("F1");
            }
        }

        private void UpdateRecordsCount()
        {
            var records = Preferences.Get("RecordCount", 0);
            RecordsCountText = records == 0 ? "暂无记录" : $"当前共有 {records} 条记录";
        }

        private void OnThresholdRadioChanged(object sender, CheckedChangedEventArgs e)
        {
            if (sender is RadioButton radioButton && e.Value && radioButton.Value != null)
            {
                var value = radioButton.Value.ToString();
                if (value == "0") // 自定义
                {
                    IsCustomThresholdVisible = true;
                }
                else
                {
                    IsCustomThresholdVisible = false;
                    if (double.TryParse(value, out double threshold))
                    {
                        Preferences.Set("Threshold", threshold);
                        System.Diagnostics.Debug.WriteLine($"阈值设置为: {threshold}°");
                    }
                }
            }
        }

        private void OnCustomThresholdCompleted(object sender, EventArgs e)
        {
            if (sender is Entry entry && double.TryParse(entry.Text, out double value))
            {
                if (value >= 0.1 && value <= 10.0)
                {
                    Preferences.Set("Threshold", Math.Round(value, 1));

                    // 选中自定义单选按钮
                    var customRadio = this.FindByName<RadioButton>("CustomRadio");
                    if (customRadio != null)
                        customRadio.IsChecked = true;

                    System.Diagnostics.Debug.WriteLine($"自定义阈值设置为: {Math.Round(value, 1)}°");
                }
                else
                {
                    DisplayAlert("错误", "阈值必须在 0.1° 到 10.0° 之间", "确定");
                    entry.Text = "3.0";
                }
            }
            else if (sender is Entry entry2)
            {
                DisplayAlert("错误", "请输入有效的数字", "确定");
                entry2.Text = "3.0";
            }
        }

        private void OnDelayEntryCompleted(object sender, EventArgs e)
        {
            if (sender is Entry entry && int.TryParse(entry.Text, out int delay) && delay >= 0)
            {
                Preferences.Set("DelaySeconds", delay);
            }
            else if (sender is Entry entry2)
            {
                DisplayAlert("错误", "请输入有效的秒数", "确定");
                entry2.Text = "0";
            }
        }

        private void OnVibrationToggled(object sender, ToggledEventArgs e)
        {
            Preferences.Set("VibrationEnabled", e.Value);
        }

        private void OnRadarVibrationToggled(object sender, ToggledEventArgs e)
        {
            IsRadarVibrationEnabled = e.Value;
            Preferences.Set("RadarVibrationEnabled", e.Value);
        }

        // 添加相对基准开关事件处理
        private void OnRelativeBaselineToggled(object sender, ToggledEventArgs e)
        {
            IsRelativeBaseline = e.Value;
            Preferences.Set("IsRelativeBaseline", e.Value);
        }

        private void OnVibrationIntensityChanged(object sender, ValueChangedEventArgs e)
        {
            Preferences.Set("VibrationIntensity", (int)e.NewValue);
        }

        private async void OnViewRecordsClicked(object sender, EventArgs e)
        {
            var eventsJson = Preferences.Get("LastTiltEvents", "");
            if (string.IsNullOrEmpty(eventsJson))
            {
                await DisplayAlert("查看记录", "当前还没有记录", "确定");
                return;
            }

            try
            {
                var tiltEvents = JsonSerializer.Deserialize<List<MainPage.TiltEvent>>(eventsJson);
                if (tiltEvents == null || tiltEvents.Count == 0)
                {
                    await DisplayAlert("查看记录", "当前还没有记录", "确定");
                    return;
                }
                int tiltStartCount = tiltEvents.Count(evt => evt.EventType == "开始倾斜");
                var recordDetails = new System.Text.StringBuilder();
                recordDetails.AppendLine($"总共 {tiltEvents.Count} 个倾斜事件\n");
                recordDetails.AppendLine($"\n总共倾斜次数: {tiltStartCount} 次");

                foreach (var evt in tiltEvents)
                {
                    recordDetails.AppendLine($"时间: {evt.AbsoluteTime:HH:mm:ss}");
                    //recordDetails.AppendLine($"相对记录时间: {evt.RelativeTime:hh\\:mm\\:ss}");
                    recordDetails.AppendLine($"相对稳定时间: {evt.StableTimeOffset:hh\\:mm\\:ss}");
                    recordDetails.AppendLine($"事件: {evt.EventType}");
                    recordDetails.AppendLine($"角度: X={evt.XAngle:F1}°, Y={evt.YAngle:F1}°");
                    //recordDetails.AppendLine($"基准模式: {(evt.IsRelative ? "相对基准" : "绝对水平")}");
                    recordDetails.AppendLine("────────────────────");
                }

                await DisplayAlert("本次记录详情", recordDetails.ToString(), "确定");
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"读取记录失败: {ex.Message}", "确定");
            }
        }

        private async void OnClearRecordsClicked(object sender, EventArgs e)
        {
            var confirm = await DisplayAlert("确认", "确定要清空所有记录吗？此操作不可恢复。", "确定", "取消");
            if (confirm)
            {
                Preferences.Set("RecordCount", 0);
                UpdateRecordsCount();
                await DisplayAlert("成功", "所有记录已清空", "确定");
            }
        }

        private async void OnSaveSettingsButtonClicked(object sender, EventArgs e)
        {
            if (HoursPicker?.SelectedItem is int hours)
                Preferences.Set("StableHours", hours);
            if (MinutesPicker?.SelectedItem is int minutes)
                Preferences.Set("StableMinutes", minutes);

            if (DelayEntry != null && int.TryParse(DelayEntry.Text, out int delay))
                Preferences.Set("DelaySeconds", delay);

            if (VibrationSwitch != null)
                Preferences.Set("VibrationEnabled", VibrationSwitch.IsToggled);
            if (RadarVibrationSwitch != null)
                Preferences.Set("RadarVibrationEnabled", RadarVibrationSwitch.IsToggled);

            // 安全地保存相对基准设置
            var relativeBaselineSwitch = this.FindByName<Switch>("RelativeBaselineSwitch");
            if (relativeBaselineSwitch != null)
            {
                Preferences.Set("IsRelativeBaseline", relativeBaselineSwitch.IsToggled);
            }

            if (VibrationIntensitySlider != null)
                Preferences.Set("VibrationIntensity", (int)VibrationIntensitySlider.Value);

            await DisplayAlert("成功", "设置已保存", "确定");
            MessagingCenter.Send(this, "SettingsPageDisappearing");
            await Navigation.PopAsync();
        }

        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            MessagingCenter.Send(this, "SettingsPageDisappearing");
            await Navigation.PopAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
#if ANDROID
            var mainActivity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            mainActivity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Landscape;
            mainActivity.Window?.SetFlags(
                Android.Views.WindowManagerFlags.Fullscreen,
                Android.Views.WindowManagerFlags.Fullscreen);   
#endif
            MessagingCenter.Send(this, "SettingsPageDisappearing");
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        protected new void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
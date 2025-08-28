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
        private bool _isRelativeBaseline; // �������ֶ�


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

        // �����Ի�׼����
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

            // ��ȫ��������Ի�׼����
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

            // �ҵ�������ѡ��ť�Ĳ�������
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
                            if (radioValue == 0) // �Զ���ѡ��
                            {
                                IsCustomThresholdVisible = true;
                            }
                            break;
                        }
                    }
                }
            }

            // ������Զ���ֵ����Ҫ���⴦��
            if (IsCustomThresholdVisible && CustomThresholdEntry != null)
            {
                CustomThresholdEntry.Text = threshold.ToString("F1");
            }
        }

        private void UpdateRecordsCount()
        {
            var records = Preferences.Get("RecordCount", 0);
            RecordsCountText = records == 0 ? "���޼�¼" : $"��ǰ���� {records} ����¼";
        }

        private void OnThresholdRadioChanged(object sender, CheckedChangedEventArgs e)
        {
            if (sender is RadioButton radioButton && e.Value && radioButton.Value != null)
            {
                var value = radioButton.Value.ToString();
                if (value == "0") // �Զ���
                {
                    IsCustomThresholdVisible = true;
                }
                else
                {
                    IsCustomThresholdVisible = false;
                    if (double.TryParse(value, out double threshold))
                    {
                        Preferences.Set("Threshold", threshold);
                        System.Diagnostics.Debug.WriteLine($"��ֵ����Ϊ: {threshold}��");
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

                    // ѡ���Զ��嵥ѡ��ť
                    var customRadio = this.FindByName<RadioButton>("CustomRadio");
                    if (customRadio != null)
                        customRadio.IsChecked = true;

                    System.Diagnostics.Debug.WriteLine($"�Զ�����ֵ����Ϊ: {Math.Round(value, 1)}��");
                }
                else
                {
                    DisplayAlert("����", "��ֵ������ 0.1�� �� 10.0�� ֮��", "ȷ��");
                    entry.Text = "3.0";
                }
            }
            else if (sender is Entry entry2)
            {
                DisplayAlert("����", "��������Ч������", "ȷ��");
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
                DisplayAlert("����", "��������Ч������", "ȷ��");
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

        // �����Ի�׼�����¼�����
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
                await DisplayAlert("�鿴��¼", "��ǰ��û�м�¼", "ȷ��");
                return;
            }

            try
            {
                var tiltEvents = JsonSerializer.Deserialize<List<MainPage.TiltEvent>>(eventsJson);
                if (tiltEvents == null || tiltEvents.Count == 0)
                {
                    await DisplayAlert("�鿴��¼", "��ǰ��û�м�¼", "ȷ��");
                    return;
                }
                int tiltStartCount = tiltEvents.Count(evt => evt.EventType == "��ʼ��б");
                var recordDetails = new System.Text.StringBuilder();
                recordDetails.AppendLine($"�ܹ� {tiltEvents.Count} ����б�¼�\n");
                recordDetails.AppendLine($"\n�ܹ���б����: {tiltStartCount} ��");

                foreach (var evt in tiltEvents)
                {
                    recordDetails.AppendLine($"ʱ��: {evt.AbsoluteTime:HH:mm:ss}");
                    //recordDetails.AppendLine($"��Լ�¼ʱ��: {evt.RelativeTime:hh\\:mm\\:ss}");
                    recordDetails.AppendLine($"����ȶ�ʱ��: {evt.StableTimeOffset:hh\\:mm\\:ss}");
                    recordDetails.AppendLine($"�¼�: {evt.EventType}");
                    recordDetails.AppendLine($"�Ƕ�: X={evt.XAngle:F1}��, Y={evt.YAngle:F1}��");
                    //recordDetails.AppendLine($"��׼ģʽ: {(evt.IsRelative ? "��Ի�׼" : "����ˮƽ")}");
                    recordDetails.AppendLine("����������������������������������������");
                }

                await DisplayAlert("���μ�¼����", recordDetails.ToString(), "ȷ��");
            }
            catch (Exception ex)
            {
                await DisplayAlert("����", $"��ȡ��¼ʧ��: {ex.Message}", "ȷ��");
            }
        }

        private async void OnClearRecordsClicked(object sender, EventArgs e)
        {
            var confirm = await DisplayAlert("ȷ��", "ȷ��Ҫ������м�¼�𣿴˲������ɻָ���", "ȷ��", "ȡ��");
            if (confirm)
            {
                Preferences.Set("RecordCount", 0);
                UpdateRecordsCount();
                await DisplayAlert("�ɹ�", "���м�¼�����", "ȷ��");
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

            // ��ȫ�ر�����Ի�׼����
            var relativeBaselineSwitch = this.FindByName<Switch>("RelativeBaselineSwitch");
            if (relativeBaselineSwitch != null)
            {
                Preferences.Set("IsRelativeBaseline", relativeBaselineSwitch.IsToggled);
            }

            if (VibrationIntensitySlider != null)
                Preferences.Set("VibrationIntensity", (int)VibrationIntensitySlider.Value);

            await DisplayAlert("�ɹ�", "�����ѱ���", "ȷ��");
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System;
using System.Windows.Forms;
using System.Windows.Media;

namespace ReMux2
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly FfmpegService _ffmpegService;
        private readonly FileLogger _fileLogger;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            _ffmpegService = new FfmpegService();
            _fileLogger = new FileLogger("ReMux2.log");

            _ffmpegService.Logger = Log;
            _ffmpegService.ProgressUpdater = UpdateProgress;
            _ffmpegService.EtaSetter = SetEta;
            _ffmpegService.UiStateSetter = SetUiRunningState;
            _ffmpegService.ProcessCompleted = OnProcessCompleted;
        }

        private void OnProcessCompleted()
        {
            Dispatcher.Invoke(() =>
            {
                _viewModel.Progress = 100;
                Log("Process finished.\n");
            });
        }

        private void Log(string message)
        {
            var existingLines = _viewModel.LogText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            var newLines = string.Join(Environment.NewLine, existingLines.Skip(Math.Max(0, existingLines.Length - 9)));
            _viewModel.LogText = newLines + Environment.NewLine + message;
            _fileLogger.Log(message);
        }

        public void UpdateProgress(double percentage)
        {
            _viewModel.Progress = percentage;
        }

        public void SetEta(string eta)
        {
            _viewModel.Eta = eta;
        }

        private void SetUiRunningState(bool isRunning)
        {
            _viewModel.IsEncoding = isRunning;

            if (!isRunning)
            {
                _viewModel.IsPaused = false;
                PauseResumeButton.Content = "Pause";
                _viewModel.Progress = 0;
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load settings
                _viewModel.VideoPath = AppServices.Settings.Get("VideoInputPath", string.Empty);
                _viewModel.AudioPath = AppServices.Settings.Get("AudioInputPath", string.Empty);
                _viewModel.ModeSelectedIndex = AppServices.Settings.Get("Mode", 0);
                _viewModel.PresetSelectedIndex = AppServices.Settings.Get("Preset", 0);
                _viewModel.EncoderSelectedIndex = AppServices.Settings.Get("Encoder", 0);
                _viewModel.ContainerSelectedIndex = AppServices.Settings.Get("Container", 0);
                _viewModel.PrioritySelectedIndex = AppServices.Settings.Get("Priority", 0);

                var ffmpegPath = _ffmpegService.ResolveFfmpegPath(_viewModel.FfmpegPath);
                if (!string.IsNullOrEmpty(ffmpegPath))
                {
                    await _ffmpegService.EnsureEncoderProbe(ffmpegPath, Log);
                }

                UpdateUiForMode((OperationMode)_viewModel.ModeSelectedIndex);
            }
            catch (Exception ex)
            {
                Log($"Error on startup: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_viewModel.IsEncoding)
            {
                MessageBoxResult result = System.Windows.MessageBox.Show("Encoding is in progress. Are you sure you want to exit?", "Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                else
                {
                    _ffmpegService.StopProcess();
                }
            }

            SaveSettings();
        }

        private void SaveSettings()
        {
            AppServices.Settings.Set("FFmpegPath", _viewModel.FfmpegPath);
            AppServices.Settings.Set("VideoPath", _viewModel.VideoPath);
            AppServices.Settings.Set("AudioPath", _viewModel.AudioPath);
            AppServices.Settings.Set("Encoder", _viewModel.EncoderSelectedIndex);
            AppServices.Settings.Set("Mode", _viewModel.ModeSelectedIndex);
            AppServices.Settings.Set("Container", _viewModel.ContainerSelectedIndex);
            AppServices.Settings.Set("Priority", _viewModel.PrioritySelectedIndex);
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsEncoding) return;

            var ffmpegPath = _ffmpegService.ResolveFfmpegPath(_viewModel.FfmpegPath);
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                Log("FFmpeg not found. Please set the path in settings.");
                return;
            }

            if (string.IsNullOrEmpty(_viewModel.VideoPath) || !File.Exists(_viewModel.VideoPath))
            {
                Log("Please select a valid video file.");
                return;
            }

            var operationMode = (OperationMode)_viewModel.ModeSelectedIndex;
            string outputPath;
            string arguments;

            switch (operationMode)
            {
                case OperationMode.ExtractAudio:
                    var audioCodec = (AudioCodecSelector.SelectedItem as ComboBoxItem)?.Content as string ?? "wav";
                    outputPath = _ffmpegService.GetAudioOutputPath(_viewModel.VideoPath, audioCodec);
                    (arguments, _) = _ffmpegService.BuildExtractAudioArgs(_viewModel.VideoPath, outputPath, audioCodec);
                    break;
                case OperationMode.RemuxAudio:
                    if (string.IsNullOrEmpty(_viewModel.AudioPath) || !File.Exists(_viewModel.AudioPath))
                    {
                        Log("Please select a valid audio file for remuxing.");
                        return;
                    }
                    outputPath = _ffmpegService.GetOutputPath(_viewModel.VideoPath, (ContainerOption)_viewModel.ContainerSelectedIndex);
                    arguments = _ffmpegService.BuildRemuxArgs(_viewModel.VideoPath, _viewModel.AudioPath, outputPath);
                    break;
                case OperationMode.YouTubeOptimize:
                case OperationMode.YifyReencode:
                case OperationMode.CustomEncode:
                    outputPath = _ffmpegService.GetOutputPath(_viewModel.VideoPath, (ContainerOption)_viewModel.ContainerSelectedIndex);
                    EncodePreset preset;
                    if (operationMode == OperationMode.CustomEncode)
                    {
                        preset = (EncodePreset)_viewModel.PresetSelectedIndex;
                    }
                    else
                    {
                        preset = EncodePreset.Medium; // Default preset for non-custom modes
                    }

                    arguments = _ffmpegService.BuildEncodingArgs(
                        _viewModel.VideoPath,
                        _viewModel.AudioPath, // Can be null
                        outputPath,
                        (VideoEncoderOption)_viewModel.EncoderSelectedIndex,
                        preset,
                        operationMode == OperationMode.YouTubeOptimize,
                        operationMode == OperationMode.YifyReencode
                    );
                    break;
                default:
                    Log("Invalid operation mode selected.");
                    return;
            }

            await RunFfmpegWithProgress(ffmpegPath, arguments, outputPath);
        }

        private async System.Threading.Tasks.Task RunFfmpegWithProgress(string ffmpegPath, string arguments, string outputPath)
        {
            _viewModel.LogText = string.Empty;
            _viewModel.Progress = 0;

            var duration = await _ffmpegService.GetVideoDuration(ffmpegPath, _viewModel.VideoPath, Log);
            if (duration == 0)
            {
                Log("Could not get video duration.\n");
                return;
            }

            var priority = (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), (PrioritySelector.SelectedItem as ComboBoxItem)?.Content as string ?? "Normal", true);

            _ffmpegService.StartProcess(ffmpegPath, arguments, duration, priority);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _ffmpegService.StopProcess();
        }

        private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsEncoding) return;

            if (_viewModel.IsPaused)
            {
                _ffmpegService.ResumeProcess();
                _viewModel.IsPaused = false;
                PauseResumeButton.Content = "Pause";
            }
            else
            {
                _ffmpegService.PauseProcess();
                _viewModel.IsPaused = true;
                PauseResumeButton.Content = "Resume";
            }
        }

        private async void SetFfmpegButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "FFmpeg Executable|ffmpeg.exe",
                Title = "Select FFmpeg Executable"
            };

            if (openFileDialog.ShowDialog() is true)
            {
                _viewModel.FfmpegPath = openFileDialog.FileName;
                _ffmpegService.SetFfmpegPath(openFileDialog.FileName);
                Log($"FFmpeg path set to: {openFileDialog.FileName}");
                await _ffmpegService.EnsureEncoderProbe(openFileDialog.FileName, Log);
                UpdateEncoderSelection();
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpPopup.IsOpen = !HelpPopup.IsOpen;
        }

        private void SelectVideo_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Video Files|*.mkv;*.mp4;*.avi;*.mov;*.wmv;*.flv;*.webm|All Files|*.*",
                Title = "Select a Video File"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                _viewModel.VideoPath = openFileDialog.FileName;
            }
        }

        private void SelectAudio_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio Files|*.m4a;*.mp3;*.aac;*.wav;*.flac;*.ogg|All Files|*.*",
                Title = "Select an Audio File"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                _viewModel.AudioPath = openFileDialog.FileName;
            }
        }



        private void VideoPath_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    _viewModel.VideoPath = files[0];
                }
            }
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
            }
        }

        private void BrowseVideo_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Video Files|*.mkv;*.mp4;*.avi;*.mov;*.wmv;*.flv;*.webm|All Files|*.*",
                Title = "Select a Video File"
            };
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _viewModel.VideoPath = openFileDialog.FileName;
            }
        }

        private void BrowseAudio_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Audio Files|*.m4a;*.mp3;*.aac;*.wav;*.flac;*.ogg|All Files|*.*",
                Title = "Select an Audio File"
            };
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _viewModel.AudioPath = openFileDialog.FileName;
            }
        }

        private void Input_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                if (sender is Border border)
                {
                    border.Background = new SolidColorBrush(System.Windows.Media.Colors.LightGray);
                }
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        private void Input_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
            }
        }

        private void AudioPath_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    _viewModel.AudioPath = files[0];
                }
            }
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
            }
        }

        private void ModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null) return;
            var operationMode = (OperationMode)_viewModel.ModeSelectedIndex;
            UpdateUiForMode(operationMode);
        }

        private void UpdateUiForMode(OperationMode mode)
        {
            var isCustomEncode = mode == OperationMode.CustomEncode;
            var isExtractAudio = mode == OperationMode.ExtractAudio;

            PresetSelector.IsEnabled = isCustomEncode;
            EncoderSelector.IsEnabled = isCustomEncode;
            ContainerSelector.IsEnabled = isCustomEncode;

            AudioCodecTextBlock.Visibility = isExtractAudio ? Visibility.Visible : Visibility.Collapsed;
            AudioCodecSelector.Visibility = isExtractAudio ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PrioritySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel.IsEncoding)
            {
                var priority = (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), (PrioritySelector.SelectedItem as ComboBoxItem)?.Content as string ?? "Normal", true);
                _ffmpegService.UpdatePriority(priority);
            }
        }

        private void LogOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            LogOutput.ScrollToEnd();
        }

        private void EncoderSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((e.AddedItems.Count > 0 && (e.AddedItems[0] as ComboBoxItem)?.Content as string == "Auto") || e.AddedItems.Count == 0)
            {
                UpdateEncoderSelection();
            }
            else if (e.AddedItems.Count > 0)
            {
                _viewModel.CodecHint = $"Using: {(e.AddedItems[0] as ComboBoxItem)?.Content as string}";
            }
        }

        private void UpdateEncoderSelection()
        {
            var h264Encoder = _ffmpegService.DetermineBestHardwareEncoder("h264");
            var hevcEncoder = _ffmpegService.DetermineBestHardwareEncoder("hevc");

            // Assuming EncoderSelector is a ComboBox
            var autoItem = EncoderSelector.Items[0] as ComboBoxItem;
            var libx264Item = EncoderSelector.Items[1] as ComboBoxItem;
            var libx265Item = EncoderSelector.Items[2] as ComboBoxItem;
            var h264_nvenc = EncoderSelector.Items[3] as ComboBoxItem;
            var hevc_nvenc = EncoderSelector.Items[4] as ComboBoxItem;
            var h264_amf = EncoderSelector.Items[5] as ComboBoxItem;
            var hevc_amf = EncoderSelector.Items[6] as ComboBoxItem;
            var h264_qsv = EncoderSelector.Items[7] as ComboBoxItem;
            var hevc_qsv = EncoderSelector.Items[8] as ComboBoxItem;

            if (h264Encoder != null)
            {
                Log($"H.264 hardware encoder found: {h264Encoder}\n");
                if (h264_nvenc != null) h264_nvenc.IsEnabled = h264Encoder == "h264_nvenc";
                if (h264_amf != null) h264_amf.IsEnabled = h264Encoder == "h264_amf";
                if (h264_qsv != null) h264_qsv.IsEnabled = h264Encoder == "h264_qsv";
            }
            else
            {
                if (h264_nvenc != null) h264_nvenc.IsEnabled = false;
                if (h264_amf != null) h264_amf.IsEnabled = false;
                if (h264_qsv != null) h264_qsv.IsEnabled = false;
            }

            if (hevcEncoder != null)
            {
                Log($"HEVC hardware encoder found: {hevcEncoder}\n");
                if (hevc_nvenc != null) hevc_nvenc.IsEnabled = hevcEncoder == "hevc_nvenc";
                if (hevc_amf != null) hevc_amf.IsEnabled = hevcEncoder == "hevc_amf";
                if (hevc_qsv != null) hevc_qsv.IsEnabled = hevcEncoder == "hevc_qsv";
            }
            else
            {
                if (hevc_nvenc != null) hevc_nvenc.IsEnabled = false;
                if (hevc_amf != null) hevc_amf.IsEnabled = false;
                if (hevc_qsv != null) hevc_qsv.IsEnabled = false;
            }

            // Select best available encoder
            if (h264Encoder != null)
            {
                for (int i = 0; i < EncoderSelector.Items.Count; i++)
                {
                    if ((EncoderSelector.Items[i] as ComboBoxItem)?.Content as string == h264Encoder)
                    {
                        EncoderSelector.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                EncoderSelector.SelectedIndex = 1; // libx264
            }
        }
        private void SelectFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "FFmpeg Executable (ffmpeg.exe)|ffmpeg.exe"
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _viewModel.FfmpegPath = openFileDialog.FileName;
            }
        }
    }
}
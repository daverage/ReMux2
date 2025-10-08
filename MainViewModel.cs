using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ReMux2
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _logText = "";
        private string _videoPath = "";
        private string _audioPath = "";
        private string _ffmpegPath = "";
        private double _progress;
        private string _eta = "";
        private string _codecHint = "";
        private bool _isEncoding;
        private bool _isPaused;
        private int _modeSelectedIndex;
        private int _presetSelectedIndex;
        private int _encoderSelectedIndex;
        private int _containerSelectedIndex;
        private int _prioritySelectedIndex;
        private int _audioCodecSelectedIndex;
        private bool _isPresetSelectorEnabled;
        private bool _isEncoderSelectorEnabled;
        private bool _isContainerSelectorEnabled;

        public bool IsEncoding
        {
            get => _isEncoding;
            set
            {
                if (SetProperty(ref _isEncoding, value))
                {
                    OnPropertyChanged(nameof(IsNotEncoding));
                }
            }
        }

        public bool IsNotEncoding => !IsEncoding;

        public bool IsPaused { get => _isPaused; set => SetProperty(ref _isPaused, value); }

        public string VideoPath
        {
            get => _videoPath;
            set
            {
                if (SetProperty(ref _videoPath, value))
                {
                    OnPropertyChanged(nameof(VideoFileName));
                }
            }
        }

        public string AudioPath
        {
            get => _audioPath;
            set
            {
                if (SetProperty(ref _audioPath, value))
                {
                    OnPropertyChanged(nameof(AudioFileName));
                }
            }
        }

        public string VideoFileName => string.IsNullOrEmpty(VideoPath) ? "Drop Video File Here" : System.IO.Path.GetFileName(VideoPath);
        public string AudioFileName => string.IsNullOrEmpty(AudioPath) ? "Drop Audio File Here" : System.IO.Path.GetFileName(AudioPath);

        public string FfmpegPath { get => _ffmpegPath; set => SetProperty(ref _ffmpegPath, value); }

        public int ModeSelectedIndex { get => _modeSelectedIndex; set => SetProperty(ref _modeSelectedIndex, value); }

        public int PresetSelectedIndex { get => _presetSelectedIndex; set => SetProperty(ref _presetSelectedIndex, value); }

        public int EncoderSelectedIndex { get => _encoderSelectedIndex; set => SetProperty(ref _encoderSelectedIndex, value); }

        public int ContainerSelectedIndex { get => _containerSelectedIndex; set => SetProperty(ref _containerSelectedIndex, value); }

        public int PrioritySelectedIndex { get => _prioritySelectedIndex; set => SetProperty(ref _prioritySelectedIndex, value); }

        public int AudioCodecSelectedIndex { get => _audioCodecSelectedIndex; set => SetProperty(ref _audioCodecSelectedIndex, value); }

        public double Progress { get => _progress; set => SetProperty(ref _progress, value); }

        public string Eta { get => _eta; set => SetProperty(ref _eta, value); }

        public string CodecHint { get => _codecHint; set => SetProperty(ref _codecHint, value); }

        public string LogText { get => _logText; set => SetProperty(ref _logText, value); }

        public bool IsPresetSelectorEnabled { get => _isPresetSelectorEnabled; set => SetProperty(ref _isPresetSelectorEnabled, value); }

        public bool IsEncoderSelectorEnabled { get => _isEncoderSelectorEnabled; set => SetProperty(ref _isEncoderSelectorEnabled, value); }

        public bool IsContainerSelectorEnabled { get => _isContainerSelectorEnabled; set => SetProperty(ref _isContainerSelectorEnabled, value); }

        private bool _isFfmpegAvailable;
        public bool IsFfmpegAvailable
        {
            get => _isFfmpegAvailable;
            set
            {
                if (SetProperty(ref _isFfmpegAvailable, value))
                {
                    OnPropertyChanged(nameof(IsFfmpegNotAvailable));
                }
            }
        }
        public bool IsFfmpegNotAvailable => !IsFfmpegAvailable;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
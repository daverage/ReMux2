using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace ReMux2
{
    public enum VideoEncoderOption { Auto, libx264, libx265, h264_nvenc, hevc_nvenc, h264_amf, hevc_amf, h264_qsv, hevc_qsv }
    public enum EncodePreset { Ultrafast, Superfast, Veryfast, Faster, Fast, Medium, Slow, Slower, Veryslow }
    public enum ContainerOption { mkv, mp4 }
    public enum OperationMode { ExtractAudio, RemuxAudio, YouTubeOptimize, YifyReencode, CustomEncode }

    public class FfmpegService
    {
        private string? _ffmpegPath;
        public List<string> AvailableEncoders { get; } = new List<string>();
        public bool EncodersProbed { get; private set; } = false;

        public string? ResolveFfmpegPath(string? ffmpegPath = null)
        {
            _ffmpegPath = ffmpegPath;
            if (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath))
            {
                return _ffmpegPath;
            }

            var local = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(local)) return local;

            var bundle = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "bin", "ffmpeg.exe");
            if (File.Exists(bundle)) return bundle;

            return null;
        }

        public string? ResolveFfprobePath(string? ffmpegPath)
        {
            try
            {
                var dir = Path.GetDirectoryName(ffmpegPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    var sibling = Path.Combine(dir, "ffprobe.exe");
                    if (File.Exists(sibling)) return sibling;
                }

                var local = Path.Combine(AppContext.BaseDirectory, "ffprobe.exe");
                if (File.Exists(local)) return local;

                var bundle = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "bin", "ffprobe.exe");
                if (File.Exists(bundle)) return bundle;
            }
            catch { }
            return null;
        }

        public async Task<double> GetVideoDuration(string ffmpegPath, string videoPath, Action<string> log)
        {
            string? ffprobePath = ResolveFfprobePath(ffmpegPath);
            if (ffprobePath != null && File.Exists(ffprobePath))
            {
                var psiProbe = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                try
                {
                    using var procProbe = Process.Start(psiProbe);
                    if (procProbe != null)
                    {
                        string output = await procProbe.StandardOutput.ReadToEndAsync();
                        await procProbe.WaitForExitAsync();
                        if (double.TryParse(output, out double duration))
                        {
                            return duration;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log($"ffprobe duration lookup failed: {ex.Message}");
                }
            }

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-hide_banner -i \"{videoPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                using var proc = Process.Start(psi);
                if (proc == null) return 0;
                string stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                var match = Regex.Match(stderr, @"Duration:\s*(\d{2}):(\d{2}):(\d{2}\.\d{2})");
                if (match.Success)
                {
                    int hours = int.Parse(match.Groups[1].Value);
                    int minutes = int.Parse(match.Groups[2].Value);
                    double seconds = double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                    return (hours * 3600) + (minutes * 60) + seconds;
                }
            }
            catch (Exception ex)
            {
                log($"Could not get video duration: {ex.Message}");
            }

            return 0;
        }

        public string BuildRemuxArgs(string videoPath, string audioPath, string outputPath)
        {
            return $"-y -hide_banner -nostats -progress pipe:1 -i \"{videoPath}\" -i \"{audioPath}\" -c copy -map 0:v:0 -map 1:a:0 \"{outputPath}\"";
        }

        public string BuildEncodingArgs(string videoPath, string? audioPath, string outputPath, VideoEncoderOption encoder, EncodePreset preset, bool isYouTube, bool isYify)
        {
            var encoderString = ResolveEncoderChoice(encoder);
            var presetString = preset.ToString().ToLower();
            var args = new System.Text.StringBuilder();

            args.Append($"-y -hide_banner -nostats -v verbose -progress pipe:1 -i \"{videoPath}\"");

            if (!string.IsNullOrEmpty(audioPath))
            {
                args.Append($" -i \"{audioPath}\"");
            }

            if (isYouTube)
            {
                args.Append($" -c:v {encoderString} -preset slow -crf 18 -c:a aac -b:a 192k -movflags +faststart");
            }
            else if (isYify)
            {
                args.Append($" -c:v {encoderString} -preset slow -crf 22 -c:a aac -b:a 128k");
            }
            else
            {
                args.Append($" -c:v {encoderString} -preset {presetString}");
                if (!string.IsNullOrEmpty(audioPath))
                {
                    args.Append(" -c:a aac -b:a 192k");
                }
                else
                {
                    args.Append(" -c:a copy");
                }
            }

            if (!string.IsNullOrEmpty(audioPath))
            {
                args.Append(" -map 0:v:0 -map 1:a:0");
            }
            else
            {
                args.Append(" -map 0:v:0 -map 0:a:0?");
            }

            args.Append($" \"{outputPath}\"");

            return args.ToString();
        }

        public string ResolveEncoderChoice(VideoEncoderOption encoderOpt)
        {
            if (encoderOpt == VideoEncoderOption.Auto) return DetermineAutoEncoder();
            return encoderOpt.ToString();
        }

        public string DetermineAutoEncoder()
        {
            string[] h264Priority = { "h264_qsv", "h264_nvenc", "h264_amf", "h264_vaapi", "h264_v4l2m2m" };
            foreach (var enc in h264Priority)
            {
                if (AvailableEncoders.Contains(enc)) return enc;
            }
            if (AvailableEncoders.Contains("libx264")) return "libx264";

            string[] hevcPriority = { "hevc_qsv", "hevc_nvenc", "hevc_amf", "hevc_vaapi", "hevc_v4l2m2m" };
            foreach (var enc in hevcPriority)
            {
                if (AvailableEncoders.Contains(enc)) return enc;
            }
            if (AvailableEncoders.Contains("libx265")) return "libx265";

            return "libx264";
        }

        public string? DetermineBestHardwareEncoder(string desiredCodec)
        {
            bool isH264 = desiredCodec.IndexOf("x264", StringComparison.OrdinalIgnoreCase) >= 0 || desiredCodec.IndexOf("h264", StringComparison.OrdinalIgnoreCase) >= 0;
            string[] priority = isH264
                ? new[] { "h264_qsv", "h264_nvenc", "h264_amf", "h264_vaapi", "h264_v4l2m2m" }
                : new[] { "hevc_qsv", "hevc_nvenc", "hevc_amf", "hevc_vaapi", "hevc_v4l2m2m" };
            foreach (var enc in priority)
            {
                if (AvailableEncoders.Contains(enc)) return enc;
            }
            return null;
        }

        public async Task EnsureEncoderProbe(string ffmpegExe, Action<string> log)
        {
            if (EncodersProbed) return;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = "-hide_banner -encoders",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return;

                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                AvailableEncoders.Clear();
                var matches = Regex.Matches(output, @"V.....\s+([a-zA-Z0-9_]+)");
                foreach (Match match in matches)
                {
                    AvailableEncoders.Add(match.Groups[1].Value);
                }

                EncodersProbed = true;
            }
            catch (Exception ex)
            {
                log($"Could not probe encoders: {ex.Message}");
            }
        }

        public string GetOutputPath(string inputPath, ContainerOption container)
        {
            string dir = Path.GetDirectoryName(inputPath) ?? AppContext.BaseDirectory;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string ext = "." + container.ToString();
            return Path.Combine(dir, name + "_encoded" + ext);
        }

        public string GetAudioOutputPath(string inputPath, string audioCodec)
        {
            string dir = Path.GetDirectoryName(inputPath) ?? AppContext.BaseDirectory;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string extension = audioCodec switch
            {
                "aac" => ".m4a",
                "mp3" => ".mp3",
                "wav" => ".wav",
                "flac" => ".flac",
                _ => ".wav",
            };
            return Path.Combine(dir, name + extension);
        }

        public (string, string) BuildExtractAudioArgs(string inputPath, string outputPath, string audioCodec)
        {
            var resolvedCodec = ResolveAudioCodec(audioCodec);
            return ($"-y -hide_banner -nostats -progress pipe:1 -i \"{inputPath}\" -vn -acodec {resolvedCodec} \"{outputPath}\"", "Extracting audio");
        }

        private string ResolveAudioCodec(string codec)
        {
            return codec switch
            {
                "aac" => "aac",
                "mp3" => "libmp3lame",
                "wav" => "pcm_s16le",
                "flac" => "flac",
                _ => "pcm_s16le", // Default to wav codec
            };
        }

        private string GetAudioCodec(string outputPath)
        {
            string extension = Path.GetExtension(outputPath).ToLowerInvariant();
            return extension switch
            {
                ".m4a" => "aac",
                ".mp3" => "libmp3lame",
                ".wav" => "pcm_s16le",
                ".flac" => "flac",
                _ => "aac",
            };
        }

        // Process management
        private Process? _ffmpegProcess;
        private string? _currentOutputPath;
        public Action<string>? Logger { get; set; }
        public Action<double>? ProgressUpdater { get; set; }
        public Action<string>? EtaSetter { get; set; }
        public Action<bool>? UiStateSetter { get; set; }
        public Action<string?>? ProcessCompleted { get; set; }

        public void SetFfmpegPath(string path)
        {
            _ffmpegPath = path;
            EncodersProbed = false;
        }

        public void StartProcess(string ffmpegPath, string args, double duration, ProcessPriorityClass priority, string? outputPath = null)
        {
            _currentOutputPath = outputPath;
            
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _ffmpegProcess = Process.Start(psi);

            if (_ffmpegProcess == null)
            {
                Logger?.Invoke("Failed to start ffmpeg process.\n");
                return;
            }

            _ffmpegProcess.PriorityClass = priority;
            UiStateSetter?.Invoke(true);

            Task.Run(() => MonitorProgress(_ffmpegProcess, duration));
            Task.Run(() => MonitorErrors(_ffmpegProcess));
        }

        public void StopProcess()
        {
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                _ffmpegProcess.Kill();
                Logger?.Invoke("Encoding stopped by user.\n");
            }
            UiStateSetter?.Invoke(false);
        }

        public void PauseProcess()
        {
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                SuspendProcess(_ffmpegProcess.Id);
            }
        }

        public void ResumeProcess()
        {
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                ResumeProcess(_ffmpegProcess.Id);
            }
        }

        private async Task MonitorProgress(Process process, double totalDuration)
        {
            var progressReader = process.StandardOutput;

            while (!process.HasExited)
            {
                string? line = await progressReader.ReadLineAsync();
                if (line == null) break;

                if (line.StartsWith("out_time_us="))
                {
                    var timeString = line.Substring("out_time_us=".Length).Trim();
                    if (timeString != "N/A" && double.TryParse(timeString, out double currentTimeUs))
                    {
                        double currentTime = currentTimeUs / 1_000_000; // Convert to seconds
                        double percentage = (currentTime / totalDuration) * 100;
                        ProgressUpdater?.Invoke(percentage);

                        // ETA calculation
                        if (percentage > 0)
                        {
                            var elapsed = TimeSpan.FromSeconds(currentTime);
                            var estimatedTotal = TimeSpan.FromSeconds(totalDuration);
                            var remaining = estimatedTotal - elapsed;
                            var eta = remaining.ToString(@"hh\:mm\:ss");
                            EtaSetter?.Invoke(eta);
                        }
                    }
                }

                Logger?.Invoke(line + "\n");
            }
        }

        private async Task MonitorErrors(Process process)
        {
            var errorReader = process.StandardError;
            while (!process.HasExited)
            {
                string? line = await errorReader.ReadLineAsync();
                if (line == null) break;
                Logger?.Invoke(line + "\n");
            }

            UiStateSetter?.Invoke(false);
            ProcessCompleted?.Invoke(_currentOutputPath);
        }

        [Flags]
        public enum ThreadAccess : int
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200)
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hThread);
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);
        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        private void SuspendProcess(int pid)
        {
            var process = Process.GetProcessById(pid);

            if (process.ProcessName == string.Empty)
                return;

            foreach (ProcessThread pT in process.Threads)
            {
                IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

                if (pOpenThread == IntPtr.Zero)
                {
                    continue;
                }

                SuspendThread(pOpenThread);
                CloseHandle(pOpenThread);
            }
        }

        public void ResumeProcess(int pid)
        {
            var process = Process.GetProcessById(pid);

            if (process.ProcessName == string.Empty)
                return;

            foreach (ProcessThread pT in process.Threads)
            {
                IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

                if (pOpenThread == IntPtr.Zero)
                {
                    continue;
                }

                var suspendCount = 0;
                do
                {
                    suspendCount = ResumeThread(pOpenThread);
                } while (suspendCount > 0);

                CloseHandle(pOpenThread);
            }
        }

        public void UpdatePriority(ProcessPriorityClass priority)
        {
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                try
                {
                    _ffmpegProcess.PriorityClass = priority;
                    Logger?.Invoke($"Process priority updated to {priority}.\n");
                }
                catch (Exception ex)
                {
                    Logger?.Invoke($"Failed to update process priority: {ex.Message}\n");
                }
            }
        }
    }


}
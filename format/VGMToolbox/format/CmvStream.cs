using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace VGMToolbox.format
{
    public class CmvStream
    {
        private string sourcePath;

        public CmvStream(string path)
        {
            this.sourcePath = path;
        }

        public void DemultiplexStreams(MpegStream.DemuxOptionsStruct demuxOptions)
        {
            string cmvDecoderPath = Path.Combine(Application.StartupPath, "tools", "CMVDecode.exe");

            if (!File.Exists(cmvDecoderPath))
            {
                throw new FileNotFoundException($"CMV解码器未找到: {cmvDecoderPath}");
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"源文件未找到: {sourcePath}");
            }

            string outputDirectory = Path.GetDirectoryName(sourcePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourcePath);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = cmvDecoderPath,
                Arguments = $"\"{sourcePath}\"",
                WorkingDirectory = outputDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine($"CMVdecode输出: {e.Data}");
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine($"CMVdecode错误: {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new Exception($"CMV解码器返回错误代码: {process.ExitCode}");
                }

                string expectedVideo = Path.Combine(outputDirectory, $"{fileNameWithoutExtension}.avi");
                string expectedAudio = Path.Combine(outputDirectory, $"{fileNameWithoutExtension}.ogg");

                if (!File.Exists(expectedVideo) && !File.Exists(expectedAudio))
                {
                    throw new FileNotFoundException("CMV解码器未生成预期的输出文件");
                }
            }
        }
    }
}
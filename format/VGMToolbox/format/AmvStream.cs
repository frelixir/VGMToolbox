using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace VGMToolbox.format
{
    public class AmvStream
    {
        private string sourcePath;

        public AmvStream(string path)
        {
            this.sourcePath = path;
        }

        public void DemultiplexStreams(MpegStream.DemuxOptionsStruct demuxOptions)
        {
            string toolsPath = Path.Combine(Application.StartupPath, "tools");
            string amvDecoderPath = Path.Combine(toolsPath, "AlphaMovieDecoderFake.exe");

            if (!File.Exists(amvDecoderPath))
            {
                throw new FileNotFoundException($"AMV解码器未找到: {amvDecoderPath}");
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"源文件未找到: {sourcePath}");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = amvDecoderPath,
                Arguments = $"-amvpath=\"{sourcePath}\"",
                WorkingDirectory = Application.StartupPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"AMV解码器返回错误代码: {process.ExitCode}");
                    }
                }

                Console.WriteLine("AMV解包完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解包AMV文件时出错: {ex.Message}");
                throw;
            }
        }
    }
}
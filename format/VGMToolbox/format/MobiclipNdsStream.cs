using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using VGMToolbox.util;

namespace VGMToolbox.format
{
    public class MobiclipNdsStream
    {
        private string sourcePath;

        public MobiclipNdsStream(string path)
        {
            this.sourcePath = path;
        }

        private string FindMobiusPath()
        {
            string currentDirMobius = Path.Combine(Directory.GetCurrentDirectory(), "Mobius.exe");
            if (File.Exists(currentDirMobius))
            {
                return currentDirMobius;
            }

            string ffmpegPath = FindExecutablePath("ffmpeg");
            if (!string.IsNullOrEmpty(ffmpegPath))
            {
                string ffmpegDir = Path.GetDirectoryName(ffmpegPath);
                string mobiusPath = Path.Combine(ffmpegDir, "Mobius.exe");
                if (File.Exists(mobiusPath))
                {
                    return mobiusPath;
                }
            }

            string mobiusInPath = FindExecutablePath("Mobius.exe");
            if (!string.IsNullOrEmpty(mobiusInPath))
            {
                return mobiusInPath;
            }

            return null;
        }

        private string FindExecutablePath(string executableName)
        {
            try
            {
                if (File.Exists(executableName))
                {
                    return Path.GetFullPath(executableName);
                }

                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    string[] paths = pathEnv.Split(Path.PathSeparator);
                    foreach (string path in paths)
                    {
                        try
                        {
                            string fullPath = Path.Combine(path, executableName);
                            if (File.Exists(fullPath))
                            {
                                return fullPath;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }

                if (!executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return FindExecutablePath(executableName + ".exe");
                }
            }
            catch
            {
            }

            return null;
        }

        public void DemultiplexStreams(MpegStream.DemuxOptionsStruct demuxOptions)
        {
            using (FileStream fs = File.OpenRead(sourcePath))
            {
                byte[] ndsMagicBytes = new byte[] { 0x4C, 0x32 };
                byte[] actualHeaderBytes = ParseFile.ParseSimpleOffset(fs, 0, 2);

                if (ParseFile.CompareSegment(actualHeaderBytes, 0, ndsMagicBytes))
                {
                    ConvertWithMobius();
                }
                else
                {
                    throw new Exception("不支持的NDS Mobiclip格式");
                }
            }
        }

        private void ConvertWithMobius()
        {
            string mobiusPath = FindMobiusPath();
            if (string.IsNullOrEmpty(mobiusPath) || !File.Exists(mobiusPath))
            {
                throw new Exception("未找到Mobius.exe");
            }

            string configPath = Path.Combine(Path.GetDirectoryName(mobiusPath), "Mobius.exe.config");
            if (!File.Exists(configPath))
            {
                CreateDefaultMobiusConfig(mobiusPath);
            }

            string ffmpegInMobiusDir = Path.Combine(Path.GetDirectoryName(mobiusPath), "ffmpeg.exe");
            if (!File.Exists(ffmpegInMobiusDir))
            {
                string systemFfmpeg = FindExecutablePath("ffmpeg");
                if (string.IsNullOrEmpty(systemFfmpeg))
                {
                    throw new Exception("未找到ffmpeg.exe");
                }
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"源文件未找到: {sourcePath}");
            }

            string outputFile = Path.ChangeExtension(sourcePath, ".mp4");

            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{mobiusPath}\" \"{sourcePath}\" && exit\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(mobiusPath)
            };

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();

                    bool processExited = process.WaitForExit(300000);

                    if (!processExited)
                    {
                        process.Kill();
                        throw new Exception("Mobius转换超时");
                    }

                    Thread.Sleep(2000);

                    if (!File.Exists(outputFile))
                    {
                        throw new Exception("MP4文件未生成");
                    }

                    if (!IsValidMp4File(outputFile))
                    {
                        throw new Exception("生成的MP4文件无效或损坏");
                    }

                    Console.WriteLine($"成功转换: {Path.GetFileName(sourcePath)} -> {Path.GetFileName(outputFile)}");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(outputFile))
                    {
                        FileInfo fileInfo = new FileInfo(outputFile);
                        if (fileInfo.Length < 1024)
                        {
                            File.Delete(outputFile);
                        }
                    }
                }
                catch
                {
                }

                throw new Exception($"转换MOFLEX文件时出错: {ex.Message}");
            }
        }

        private bool IsValidMp4File(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;

                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 10240) return false;

                using (FileStream fs = File.OpenRead(filePath))
                {
                    byte[] header = new byte[8];
                    if (fs.Read(header, 0, 8) == 8)
                    {
                        return header[4] == 'f' && header[5] == 't' && header[6] == 'y' && header[7] == 'p';
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void CreateDefaultMobiusConfig(string mobiusPath)
        {
            string configContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <startup> 
    <supportedRuntime version=""v4.0"" sku="".NETFramework,Version=v4.7""/>
  </startup>
  <appSettings>
    <add key=""ffmpegPath"" value=""ffmpeg.exe""/>
    <add key=""options"" value=""-c:v libx265 -preset medium -crf 14 -r 60 -vf scale=2048:1080:flags=lanczos+full_chroma_inp+full_chroma_int -pix_fmt yuv420p10le -x265-params profile=main10:high-tier=1:level=6.2:aq-mode=3:deblock=-1,-1 -movflags +faststart -hide_banner""/>
    <add key=""stereoTarget"" value=""sbs2l""/>
    <add key=""maxQueueSize"" value=""256""/>
  </appSettings>
</configuration>";

            string configPath = Path.Combine(Path.GetDirectoryName(mobiusPath), "Mobius.exe.config");
            File.WriteAllText(configPath, configContent);
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Stone_Application.InitService
{
    public class DisposeService
    {
        static public void cleanupService()
        {
            try
            {
                CommandResult result = RunDockerCommand("compose down", FindComposeDirectory());
                WriteCommandOutput(result);

                if (result.ExitCode == 0)
                    Console.WriteLine("[INFO] PostgreSQL service cleaned up successfully.");
                else
                    Console.WriteLine($"[ERROR] Failed to cleanup PostgreSQL service. Exit code: {result.ExitCode}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to cleanup PostgreSQL: {ex.Message}");
            }
        }

        private static CommandResult RunDockerCommand(string p_arguments, string p_workingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = p_arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(p_workingDirectory))
                startInfo.WorkingDirectory = p_workingDirectory;

            using (var process = new Process())
            {
                var output = new StringBuilder();
                var error = new StringBuilder();

                process.StartInfo = startInfo;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        output.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        error.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                process.WaitForExit();

                return new CommandResult(process.ExitCode, output.ToString(), error.ToString());
            }
        }

        private static void WriteCommandOutput(CommandResult p_result)
        {
            if (!string.IsNullOrWhiteSpace(p_result.Output))
                Console.WriteLine(p_result.Output.Trim());

            if (!string.IsNullOrWhiteSpace(p_result.Error))
                Console.WriteLine(p_result.Error.Trim());
        }

        private static string FindComposeDirectory()
        {
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            for (int i = 0; i < 8 && !string.IsNullOrEmpty(currentDirectory); i++)
            {
                string candidate = Path.Combine(currentDirectory, "InitService", "docker-compose.yml");
                if (File.Exists(candidate))
                    return Path.GetDirectoryName(candidate);

                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            }

            throw new FileNotFoundException("Unable to locate InitService\\docker-compose.yml from the application directory.");
        }

        private class CommandResult
        {
            public CommandResult(int p_exitCode, string p_output, string p_error)
            {
                ExitCode = p_exitCode;
                Output = p_output;
                Error = p_error;
            }

            public int ExitCode { get; }
            public string Output { get; }
            public string Error { get; }
        }
    }
}

using Stone_Application.Repository;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Stone_Application.InitService
{
    public class PostgreSQLInit
    {
        private const string m_CONTAINER_NAME = "measurements_postgres";
        private const string m_DATABASE = "measurements_db";
        private const string m_USER = "postgres";
        private const int m_READY_TIMEOUT_SECONDS = 30;
        private const int m_DOCKER_READY_TIMEOUT_SECONDS = 60;

        public static void PostgreSQLInitSrv()
        {
            try
            {
                if (!EnsureDockerEngineReady())
                {
                    Console.WriteLine($"[ERROR] Failed to start Docker Engine.");
                    return;
                }

                string composeDirectory = FindComposeDirectory();

                CommandResult startResult = RunDockerCommand("compose up -d", composeDirectory);
                WriteCommandOutput(startResult);

                if (startResult.ExitCode != 0)
                {
                    Console.WriteLine($"[ERROR] Failed to start PostgreSQL service. Exit code: {startResult.ExitCode}.");
                    return;
                }

                Console.WriteLine("[INFO] PostgreSQL service started. Waiting for database readiness...");

                if (WaitForPostgreSqlReady())
                    Console.WriteLine("[INFO] PostgreSQL service is running and accessible.");
                else
                    Console.WriteLine($"[ERROR] PostgreSQL service did not become ready within {m_READY_TIMEOUT_SECONDS} seconds.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to start PostgreSQL service: {ex.Message}");
            }

            Common.s_repositoryInstance = new PostgreSqlRepository();
            Console.WriteLine($"[INFO] SQL init successfully");
        }

        private static bool WaitForPostgreSqlReady()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(m_READY_TIMEOUT_SECONDS);

            while (DateTime.UtcNow < deadline)
            {
                CommandResult result = RunDockerCommand(
                    $"exec {m_CONTAINER_NAME} pg_isready -U {m_USER} -d {m_DATABASE}",
                    null);

                if (result.ExitCode == 0)
                    return true;

                Thread.Sleep(1000);
            }

            return false;
        }

        private static bool EnsureDockerEngineReady()
        {
            CommandResult dockerInfoResult = TryRunDockerCommand("info", null);
            if (dockerInfoResult != null && dockerInfoResult.ExitCode == 0)
            {
                Console.WriteLine("[INFO] Docker Desktop engine is already running.");
                return true;
            }

            string dockerDesktopPath = FindDockerDesktopPath();
            if (dockerDesktopPath == null)
            {
                Console.WriteLine("[ERROR] Docker Desktop executable was not found.");
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dockerDesktopPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                Console.WriteLine("[INFO] Docker Desktop is starting...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Could not start Docker Desktop: {ex.Message}");
                return false;
            }

            DateTime deadline = DateTime.UtcNow.AddSeconds(m_DOCKER_READY_TIMEOUT_SECONDS);
            while (DateTime.UtcNow < deadline)
            {
                dockerInfoResult = TryRunDockerCommand("info", null);
                if (dockerInfoResult != null && dockerInfoResult.ExitCode == 0)
                {
                    Console.WriteLine("[INFO] Docker Desktop engine is ready.");
                    return true;
                }

                Thread.Sleep(1000);
            }

            return false;
        }

        private static CommandResult TryRunDockerCommand(string p_arguments, string p_workingDirectory)
        {
            try
            {
                return RunDockerCommand(p_arguments, p_workingDirectory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Docker command could not be executed: {ex.Message}");
                return null;
            }
        }

        private static string FindDockerDesktopPath()
        {
            string[] candidatePaths =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "Docker Desktop.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Docker", "Docker", "Docker Desktop.exe")
            };

            foreach (string candidatePath in candidatePaths)
            {
                if (File.Exists(candidatePath))
                    return candidatePath;
            }

            return null;
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

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Clicksrv.ResetGitRepo
{
    public static class Program
    {
        private static readonly Regex urlRegex = new(@"Fetch URL: (.git)");

        public static void Main(string[] args)
        {
            foreach (var path in args)
            {
                var determinedPath = GetGitProcess(path).RunWithArg($"-C (path) rev-parse show toplevel").Replace("\n", string.Empty).Replace("\n", string.Empty).Trim();
                Console.WriteLine($"Git Repo Directory Identified at:\n  {determinedPath}\n");

                var git = GetGitProcess(determinedPath);
                var output = git.RunWithArg("remote show origin");
                var remoteUrl = urlRegex.Match(output).Groups[1].Value;

                Console.WriteLine($"Origin is \n  {remoteUrl}\n");

                Console.WriteLine("Deleting old files, please wait...\n");

                string? locks = DeleteExistingRepo(determinedPath);
                if (locks == null)
                    Console.WriteLine($"Delete repo cancelled, some files in the repo are locked by below process(es): \n{locks}\n");

                Console.WriteLine("Repo deleted successfully!\n");

                Console.WriteLine("Fetching fresh repo from origin.\n");
                string parentDir = Directory.GetParent(determinedPath)!.FullName;

                var gitParentDir = GetGitProcess(parentDir);
                gitParentDir.RunWithArg($"clone {remoteUrl}");

                Console.WriteLine($"Repo has been reset successfully!\nGit Repo Path: {parentDir}\\{Path.GetFileNameWithoutExtension(remoteUrl)}\n");
            }

            Console.WriteLine("Press any key to close...");
            _ = Console.ReadKey();
        }

        private static string? DeleteExistingRepo(string determinedPath)
        {
            var directory = new DirectoryInfo(determinedPath) { Attributes = FileAttributes.Normal };

            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
                info.Attributes = FileAttributes.Normal;

            directory.Delete(true);

            return null;
        }

        private static string RunWithArg(this ProcessStartInfo startInfo, string arg, bool writeToConsole = false)
        {
            startInfo.Arguments = arg;
            return startInfo.Run(writeToConsole);
        }

        private static string Run(this ProcessStartInfo startInfo, bool writeToConsole)
        {
            _ = Directory.CreateDirectory(startInfo.WorkingDirectory);

            var timeout = 10000;
            using var process = new Process();
            process.StartInfo = startInfo;
            StringBuilder output = new();
            StringBuilder error = new();
            using AutoResetEvent outputWaitHandle = new(false);
            using AutoResetEvent errorWaitHandle = new(false);
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                    outputWaitHandle.Set();
                else
                    output.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                    errorWaitHandle.Set();
                else
                    error.AppendLine(e.Data);
            };

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (process.WaitForExit(timeout) &&
                outputWaitHandle.WaitOne(timeout) &&
                errorWaitHandle.WaitOne(timeout))
            {
                if (process.ExitCode == 0)
                {
                    string outputStr = output.ToString();
                    if (writeToConsole)
                        Console.WriteLine(outputStr);
                    return outputStr;
                }
                else
                {
                    string outputStr = output.ToString();
                    string errorStr = error.ToString();
                    throw new ApplicationException($"Exit Code {process.ExitCode}\n\n--- Error Stream ---\n{errorStr}\n\n--- Output Stream ---\n{outputStr}");
                }
            }
            else
            {
                throw new ApplicationException($"Process Timed Out - " + startInfo.FileName + " " + startInfo.Arguments);
            }
        }

        private static ProcessStartInfo GetGitProcess(string workingDirectory) => new()
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
    }
}
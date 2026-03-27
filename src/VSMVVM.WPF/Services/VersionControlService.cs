using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// Git 기반 형상관리 정보 조회 서비스 구현체.
    /// git CLI를 호출하여 브랜치, 커밋 해시, 변경 상태 등을 조회합니다.
    /// </summary>
    public sealed class VersionControlService : IVersionControlService
    {
        #region Fields

        private string _branchName;
        private string _commitHash;
        private string _commitDate;
        private string _commitMessage;
        private bool _isDirty;
        private bool _isLoaded;

        #endregion

        #region IVersionControlService

        public string BranchName
        {
            get
            {
                EnsureLoaded();
                return _branchName;
            }
        }

        public string CommitHash
        {
            get
            {
                EnsureLoaded();
                return _commitHash;
            }
        }

        public string CommitDate
        {
            get
            {
                EnsureLoaded();
                return _commitDate;
            }
        }

        public string CommitMessage
        {
            get
            {
                EnsureLoaded();
                return _commitMessage;
            }
        }

        public bool IsDirty
        {
            get
            {
                EnsureLoaded();
                return _isDirty;
            }
        }

        #endregion

        #region Private Methods

        private void EnsureLoaded()
        {
            if (_isLoaded)
            {
                return;
            }

            _isLoaded = true;

            try
            {
                _branchName = RunGitCommand("rev-parse --abbrev-ref HEAD")?.Trim() ?? "Unknown";
                _commitHash = RunGitCommand("rev-parse --short HEAD")?.Trim() ?? "Unknown";
                _commitDate = RunGitCommand("log -1 --format=%cd --date=format:\"%Y-%m-%d %H:%M:%S\"")?.Trim() ?? "Unknown";
                _commitMessage = RunGitCommand("log -1 --format=%s")?.Trim() ?? "Unknown";

                var status = RunGitCommand("status --porcelain") ?? string.Empty;
                _isDirty = !string.IsNullOrWhiteSpace(status);
            }
            catch
            {
                _branchName = "Unknown";
                _commitHash = "Unknown";
                _commitDate = "Unknown";
                _commitMessage = "Unknown";
                _isDirty = false;
            }
        }

        private static string RunGitCommand(string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return null;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        return null;
                    }

                    return output;
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}

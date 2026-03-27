namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// Git 형상관리 정보 조회 서비스 인터페이스.
    /// </summary>
    public interface IVersionControlService
    {
        /// <summary>
        /// 현재 브랜치 이름 (예: "main").
        /// </summary>
        string BranchName { get; }

        /// <summary>
        /// 커밋 해시 (예: "2f4b8e1...").
        /// </summary>
        string CommitHash { get; }

        /// <summary>
        /// 커밋 날짜 (예: "2026-03-24 16:30:00").
        /// </summary>
        string CommitDate { get; }

        /// <summary>
        /// 커밋 메시지.
        /// </summary>
        string CommitMessage { get; }

        /// <summary>
        /// 작업 트리 변경 여부.
        /// </summary>
        bool IsDirty { get; }
    }
}

using System;
using System.Collections.Generic;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Compilation;
using VSMVVM.Core.Scheduler.Nodes;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// 한 카테고리의 사용자 C# 코드 조각. 멀티 조각 에디터의 ListBox 한 행에 해당.
    /// <para>
    /// <see cref="Category"/> 는 사용자가 직접 입력하지 않고 <see cref="VSMVVM.Core.Scheduler.Nodes.UserCodeCategoryExtractor"/>
    /// 가 컴파일된 어셈블리에서 자동 추출하여 채움. 코드 본문의 <c>[MethodNode(Category="OpenCV")]</c> 가 그대로
    /// 팔레트 카테고리로 노출된다.
    /// </para>
    /// <para>
    /// <see cref="LastCompile"/> 은 마지막 컴파일 결과 — 진단 표시 + 어셈블리 보관용. 컴파일 전이면 null.
    /// </para>
    /// </summary>
    public partial class UserCodeSnippet : ViewModelBase
    {
        /// <summary>
        /// 카테고리 이름. 컴파일 성공 시 자동으로 추출된 값으로 갱신됨. 직접 편집은 비권장.
        /// (사용자 코드 안의 <c>[MethodNode(Category="X")]</c> 를 수정하는 게 표준 흐름.)
        /// </summary>
        [Property] private string _category = UserCodeCategoryExtractor.FallbackCategory;

        /// <summary>C# 소스 코드. 빈 문자열 허용.</summary>
        [Property] private string _sourceCode = string.Empty;

        /// <summary>마지막 컴파일 결과 (성공/실패, 진단, 어셈블리). 미컴파일이면 null.</summary>
        [Property] private CompilationResult _lastCompile;

        /// <summary>
        /// 마지막 컴파일에서 등록된 노드 TypeId 목록. 조각 삭제 시 NodeMetadataRegistry 정리에 사용.
        /// 컴파일 전이면 빈 리스트.
        /// </summary>
        public List<string> RegisteredTypeIds { get; } = new();

        public UserCodeSnippet() { }

        public UserCodeSnippet(string category, string sourceCode)
        {
            _category = string.IsNullOrWhiteSpace(category) ? UserCodeCategoryExtractor.FallbackCategory : category;
            _sourceCode = sourceCode ?? string.Empty;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VSMVVM.Core.Scheduler.Attributes;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// 사용자가 컴파일한 어셈블리를 reflection 해서 <see cref="MethodNodeAttribute.Category"/> 빈도를 집계,
    /// "이 코드 조각의 대표 카테고리" 1개를 결정한다.
    /// <para>
    /// 사용 시나리오: 멀티 조각 사용자 코드 에디터에서 각 조각이 어느 팔레트 카테고리(OpenCV / Math / ...) 에 속하는지
    /// 자동 식별. 사용자가 카테고리 이름을 직접 입력하지 않아도, 작성한 [MethodNode(Category="...")] 이 그대로 사용된다.
    /// </para>
    /// <para>
    /// 결정 규칙:
    /// 1) 모든 메서드의 <c>[MethodNode]</c> 속성을 수집.
    /// 2) Category 가 빈/null 인 메서드는 <see cref="FallbackCategory"/> ("User Code") 로 카운트.
    /// 3) 빈도 1위가 PrimaryCategory. 동률 시 알파벳 순 첫 번째 (deterministic).
    /// 4) <c>[MethodNode]</c> 가 하나도 없으면 PrimaryCategory == null, TotalMethodNodes == 0.
    /// </para>
    /// </summary>
    public static class UserCodeCategoryExtractor
    {
        /// <summary>
        /// <see cref="MethodNodeAttribute.Category"/> 가 비어있는 메서드에 적용할 fallback 카테고리.
        /// <see cref="Services.NodePaletteService.UserCodeCategory"/> 와 일관된 값.
        /// </summary>
        public const string FallbackCategory = "User Code";

        public sealed class Result
        {
            /// <summary>빈도 1위 카테고리. 메서드가 하나도 없으면 null.</summary>
            public string PrimaryCategory { get; }

            /// <summary>모든 카테고리별 메서드 개수 (Category → count). 메서드 0개면 빈 dict.</summary>
            public IReadOnlyDictionary<string, int> AllCategoryCounts { get; }

            /// <summary>[MethodNode] 가 부착된 메서드 총개수.</summary>
            public int TotalMethodNodes { get; }

            public Result(string primaryCategory, IReadOnlyDictionary<string, int> allCategoryCounts, int totalMethodNodes)
            {
                PrimaryCategory = primaryCategory;
                AllCategoryCounts = allCategoryCounts ?? new Dictionary<string, int>();
                TotalMethodNodes = totalMethodNodes;
            }
        }

        /// <summary>
        /// 어셈블리 전체를 스캔하여 카테고리 빈도를 집계.
        /// </summary>
        /// <param name="assembly">대상 어셈블리.</param>
        /// <param name="typeFilter">
        /// 선택: 특정 타입만 검사하도록 필터링. 단위 테스트에서 같은 어셈블리의 다른 테스트 픽스처와 분리하기 위해 사용.
        /// null 이면 모든 public/internal 타입.
        /// </param>
        public static Result Extract(Assembly assembly, Func<Type, bool> typeFilter = null)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            int total = 0;

            foreach (var type in SafeGetTypes(assembly))
            {
                if (typeFilter != null && !typeFilter(type)) continue;
                if (!type.IsClass) continue;

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                                     | BindingFlags.Static | BindingFlags.Instance
                                                     | BindingFlags.DeclaredOnly))
                {
                    var attr = method.GetCustomAttribute<MethodNodeAttribute>();
                    if (attr == null) continue;

                    var category = string.IsNullOrWhiteSpace(attr.Category) ? FallbackCategory : attr.Category;
                    counts.TryGetValue(category, out var c);
                    counts[category] = c + 1;
                    total++;
                }
            }

            string primary = null;
            if (counts.Count > 0)
            {
                // 빈도 내림차순, 동률이면 Category 알파벳 오름차순 — deterministic.
                primary = counts
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .First()
                    .Key;
            }

            return new Result(primary, counts, total);
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 로드 실패한 타입은 건너뛰고 가능한 것만.
                return ex.Types.Where(t => t != null);
            }
        }
    }
}

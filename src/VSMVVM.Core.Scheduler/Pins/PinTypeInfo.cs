using System;
using System.Linq;
using System.Text;

namespace VSMVVM.Core.Scheduler.Pins
{
    /// <summary>
    /// 데이터 핀 타입의 메타 정보 — UI/직렬화/호환성 판정에서 일관되게 사용되는 단일 진실 소스.
    /// 호스트는 사용자 타입(OpenCvSharp.Mat, System.Net.Sockets.Socket 등)을 PinTypeInfo 로
    /// 등록하여 그래프 에디터에 노출.
    /// </summary>
    public sealed class PinTypeInfo
    {
        public Type ClrType { get; }

        /// <summary>UI 표시명. 미지정 시 ClrType.Name.</summary>
        public string DisplayName { get; }

        /// <summary>분류 — Primitive / Collection / OpenCV / Network / Domain / ... — 팔레트 그루핑.</summary>
        public string Category { get; }

        /// <summary>
        /// 직렬화/타입 ID embedding 용 안정적 이름.
        /// open generic: "System.Collections.Generic.List`1"
        /// closed generic: "System.Collections.Generic.List`1[System.Int32]"
        /// </summary>
        public string StableName { get; }

        /// <summary>기본 인스턴스 팩토리 (선택). null 이면 default(T) 사용.</summary>
        public Func<object> DefaultFactory { get; }

        /// <summary>literal 텍스트 → 값 파서 (인스펙터 편집 시 사용). null 이면 편집 비활성.</summary>
        public Func<string, object> Parse { get; }

        /// <summary>값 → 표시 텍스트 (인스펙터 read-only 표시 시). null 이면 ToString().</summary>
        public Func<object, string> Format { get; }

        public PinTypeInfo(
            Type clrType,
            string displayName = null,
            string category = null,
            Func<object> defaultFactory = null,
            Func<string, object> parse = null,
            Func<object, string> format = null)
        {
            ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
            DisplayName = string.IsNullOrEmpty(displayName) ? clrType.Name : displayName;
            Category = category ?? InferCategory(clrType);
            StableName = ComputeStableName(clrType);
            DefaultFactory = defaultFactory;
            Parse = parse;
            Format = format;
        }

        private static string InferCategory(Type t)
        {
            if (t.IsPrimitive || t == typeof(string) || t == typeof(decimal)) return "Primitive";
            if (t.IsArray) return "Collection";
            if (t.IsGenericType)
            {
                var def = t.IsGenericTypeDefinition ? t : t.GetGenericTypeDefinition();
                if (def == typeof(System.Collections.Generic.List<>) ||
                    def == typeof(System.Collections.Generic.Dictionary<,>) ||
                    def == typeof(System.Collections.Generic.HashSet<>) ||
                    def == typeof(System.Collections.Generic.IEnumerable<>))
                    return "Collection";
            }
            return "Custom";
        }

        /// <summary>
        /// StableName 의 역변환. 단순 케이스 (FullName 정확 일치) 만 지원 — 호스트가 등록한 어셈블리에서 검색.
        /// closed generic / 배열 환원은 호출자가 IPinTypeRegistry.GetByStableName 을 사용하길 권장.
        /// 본 함수는 직렬화 라운드트립의 fallback 경로용.
        /// </summary>
        public static Type ResolveStableName(string stableName)
        {
            if (string.IsNullOrEmpty(stableName)) return null;
            // 배열
            if (stableName.EndsWith("[]"))
            {
                var inner = ResolveStableName(stableName.Substring(0, stableName.Length - 2));
                return inner?.MakeArrayType();
            }
            // closed generic — "Name`N[Arg1,Arg2,...]" 형태
            int genIdx = stableName.IndexOf('[');
            if (genIdx > 0 && stableName.EndsWith("]"))
            {
                var defName = stableName.Substring(0, genIdx);
                var argsBlob = stableName.Substring(genIdx + 1, stableName.Length - genIdx - 2);
                var defType = ResolveSimpleName(defName);
                if (defType == null) return null;
                var argNames = SplitTopLevel(argsBlob);
                var argTypes = new Type[argNames.Count];
                for (int i = 0; i < argNames.Count; i++)
                {
                    argTypes[i] = ResolveStableName(argNames[i]);
                    if (argTypes[i] == null) return null;
                }
                return defType.MakeGenericType(argTypes);
            }
            return ResolveSimpleName(stableName);
        }

        private static Type ResolveSimpleName(string fullName)
        {
            // 우선 mscorlib/System.Private.CoreLib 같은 기본 어셈블리에서 시도.
            var t = Type.GetType(fullName);
            if (t != null) return t;
            // 그 외에는 모든 로드된 어셈블리 순회.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        private static System.Collections.Generic.List<string> SplitTopLevel(string blob)
        {
            // generic args 를 콤마 단위로 split — 중첩된 대괄호는 무시.
            var result = new System.Collections.Generic.List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < blob.Length; i++)
            {
                var ch = blob[i];
                if (ch == '[') depth++;
                else if (ch == ']') depth--;
                else if (ch == ',' && depth == 0)
                {
                    result.Add(blob.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < blob.Length) result.Add(blob.Substring(start));
            return result;
        }

        /// <summary>
        /// CLR 타입 → 안정적 직렬화 이름. AssemblyQualifiedName 은 어셈블리 버전 변경에 취약하므로
        /// FullName 베이스 + generic args 를 재귀적으로 stable name 으로 펼친 형태 사용.
        /// </summary>
        public static string ComputeStableName(Type t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (t.IsArray)
            {
                return ComputeStableName(t.GetElementType()) + "[]";
            }
            if (!t.IsGenericType)
            {
                return t.FullName ?? t.Name;
            }
            var def = t.IsGenericTypeDefinition ? t : t.GetGenericTypeDefinition();
            var defName = def.FullName ?? def.Name; // 예: "System.Collections.Generic.List`1"
            if (t.IsGenericTypeDefinition)
            {
                return defName;
            }
            var args = t.GetGenericArguments();
            var sb = new StringBuilder(defName);
            sb.Append('[');
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ComputeStableName(args[i]));
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}

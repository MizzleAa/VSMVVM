using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Pins
{
    /// <summary>
    /// 기본 PinTypeRegistry — 원시 타입 + 컬렉션 open generic 사전 등록.
    /// 호스트가 생성 후 사용자 타입 추가 등록.
    /// </summary>
    public sealed class DefaultPinTypeRegistry : IPinTypeRegistry
    {
        private readonly List<PinTypeInfo> _orderedAll = new();
        private readonly Dictionary<Type, PinTypeInfo> _byClrType = new();
        private readonly Dictionary<string, PinTypeInfo> _byStableName = new(StringComparer.Ordinal);

        public DefaultPinTypeRegistry()
        {
            // 원시 타입
            RegisterInternal(new PinTypeInfo(typeof(int), category: "Primitive"));
            RegisterInternal(new PinTypeInfo(typeof(long), category: "Primitive"));
            RegisterInternal(new PinTypeInfo(typeof(double), category: "Primitive"));
            RegisterInternal(new PinTypeInfo(typeof(float), category: "Primitive"));
            RegisterInternal(new PinTypeInfo(typeof(bool), category: "Primitive"));
            RegisterInternal(new PinTypeInfo(typeof(string), category: "Primitive"));
            RegisterInternal(new PinTypeInfo(typeof(object), category: "Primitive"));
            RegisterInternal(new PinTypeInfo(typeof(Guid), category: "Primitive"));
            RegisterInternal(new PinTypeInfo(typeof(DateTime), category: "Primitive"));

            // 컬렉션 open generic — GetOrCreate 가 closed generic 자동 생성에 사용.
            RegisterInternal(new PinTypeInfo(typeof(List<>), category: "Collection"));
            RegisterInternal(new PinTypeInfo(typeof(Dictionary<,>), category: "Collection"));
            RegisterInternal(new PinTypeInfo(typeof(HashSet<>), category: "Collection"));
            RegisterInternal(new PinTypeInfo(typeof(IEnumerable<>), category: "Collection"));
        }

        public IReadOnlyList<PinTypeInfo> All => _orderedAll;

        public void Register(PinTypeInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (_byClrType.ContainsKey(info.ClrType))
            {
                throw new InvalidOperationException(
                    $"Pin type for CLR type '{info.ClrType.FullName}' is already registered.");
            }
            RegisterInternal(info);
        }

        private void RegisterInternal(PinTypeInfo info)
        {
            _orderedAll.Add(info);
            _byClrType[info.ClrType] = info;
            _byStableName[info.StableName] = info;
        }

        public PinTypeInfo Get(Type clrType)
        {
            if (clrType == null) return null;
            return _byClrType.TryGetValue(clrType, out var info) ? info : null;
        }

        public PinTypeInfo GetByStableName(string stableName)
        {
            if (string.IsNullOrEmpty(stableName)) return null;
            return _byStableName.TryGetValue(stableName, out var info) ? info : null;
        }

        public PinTypeInfo GetOrCreate(Type clrType)
        {
            if (clrType == null) return null;
            if (_byClrType.TryGetValue(clrType, out var existing)) return existing;

            // 배열 — 요소 타입이 등록되어 있으면 자동 생성
            if (clrType.IsArray)
            {
                var elem = clrType.GetElementType();
                if (elem == null) return null;
                var elemInfo = GetOrCreate(elem);
                if (elemInfo == null) return null;
                var arrInfo = new PinTypeInfo(clrType, category: "Collection");
                RegisterInternal(arrInfo);
                return arrInfo;
            }

            // closed generic — open generic 정의가 등록되어 있으면 자동 생성.
            if (clrType.IsGenericType && !clrType.IsGenericTypeDefinition)
            {
                var def = clrType.GetGenericTypeDefinition();
                if (!_byClrType.ContainsKey(def)) return null;
                // 모든 타입 인수도 등록(또는 자동 생성 가능) 되어 있어야 함.
                foreach (var arg in clrType.GetGenericArguments())
                {
                    if (GetOrCreate(arg) == null) return null;
                }
                var info = new PinTypeInfo(clrType, category: "Collection");
                RegisterInternal(info);
                return info;
            }

            // 사용자 정의 타입은 명시적 Register 만 허용 — null 반환.
            return null;
        }
    }
}

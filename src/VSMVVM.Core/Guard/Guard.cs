using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VSMVVM.Core.Guard
{
    /// <summary>
    /// Assert 스타일의 fail-fast 방어적 검증 유틸리티.
    /// 조건 미충족 시 즉시 예외를 발생시킵니다.
    /// </summary>
    public static class Guard
    {
        #region Null Validation

        public static void IsNotNull(object value, string parameterName)
        {
            if (value == null)
                throw new ArgumentNullException(parameterName, $"Parameter '{parameterName}' must not be null.");
        }

        public static void IsNotNullOrEmpty(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException($"Parameter '{parameterName}' must not be null or empty.", parameterName);
        }

        public static void IsNotNullOrWhiteSpace(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Parameter '{parameterName}' must not be null, empty, or whitespace.", parameterName);
        }

        #endregion

        #region Type Validation

        public static void IsOfType<T>(object value, string parameterName)
        {
            IsNotNull(value, parameterName);

            if (!(value is T))
                throw new ArgumentException($"Parameter '{parameterName}' must be of type '{typeof(T).FullName}', but was '{value.GetType().FullName}'.", parameterName);
        }

        public static void IsAssignableTo<T>(object value, string parameterName)
        {
            IsNotNull(value, parameterName);

            if (!typeof(T).IsAssignableFrom(value.GetType()))
                throw new ArgumentException($"Parameter '{parameterName}' must be assignable to '{typeof(T).FullName}', but was '{value.GetType().FullName}'.", parameterName);
        }

        public static void IsNotOfType<T>(object value, string parameterName)
        {
            IsNotNull(value, parameterName);

            if (value is T)
                throw new ArgumentException($"Parameter '{parameterName}' must not be of type '{typeof(T).FullName}'.", parameterName);
        }

        #endregion

        #region Comparison Validation

        public static void IsEqualTo<T>(T value, T expected, string parameterName) where T : IEquatable<T>
        {
            if (!value.Equals(expected))
                throw new ArgumentException($"Parameter '{parameterName}' must be equal to '{expected}', but was '{value}'.", parameterName);
        }

        public static void IsNotEqualTo<T>(T value, T unexpected, string parameterName) where T : IEquatable<T>
        {
            if (value.Equals(unexpected))
                throw new ArgumentException($"Parameter '{parameterName}' must not be equal to '{unexpected}'.", parameterName);
        }

        public static void IsGreaterThan<T>(T value, T minimum, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(minimum) <= 0)
                throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter '{parameterName}' must be greater than '{minimum}'.");
        }

        public static void IsGreaterThanOrEqualTo<T>(T value, T minimum, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(minimum) < 0)
                throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter '{parameterName}' must be greater than or equal to '{minimum}'.");
        }

        public static void IsLessThan<T>(T value, T maximum, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(maximum) >= 0)
                throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter '{parameterName}' must be less than '{maximum}'.");
        }

        public static void IsLessThanOrEqualTo<T>(T value, T maximum, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(maximum) > 0)
                throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter '{parameterName}' must be less than or equal to '{maximum}'.");
        }

        public static void IsInRange<T>(T value, T minimum, T maximum, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(minimum) < 0 || value.CompareTo(maximum) > 0)
                throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter '{parameterName}' must be between '{minimum}' and '{maximum}'.");
        }

        #endregion

        #region Collection Validation

        public static void IsNotEmpty<T>(ICollection<T> collection, string parameterName)
        {
            IsNotNull(collection, parameterName);

            if (collection.Count == 0)
                throw new ArgumentException($"Parameter '{parameterName}' must not be empty.", parameterName);
        }

        public static void HasSizeEqualTo<T>(ICollection<T> collection, int expectedSize, string parameterName)
        {
            IsNotNull(collection, parameterName);

            if (collection.Count != expectedSize)
                throw new ArgumentException($"Parameter '{parameterName}' must have size {expectedSize}, but has {collection.Count}.", parameterName);
        }

        public static void HasSizeGreaterThan<T>(ICollection<T> collection, int minSize, string parameterName)
        {
            IsNotNull(collection, parameterName);

            if (collection.Count <= minSize)
                throw new ArgumentException($"Parameter '{parameterName}' must have size greater than {minSize}, but has {collection.Count}.", parameterName);
        }

        #endregion

        #region Boolean Validation

        public static void IsTrue(bool condition, string parameterName)
        {
            if (!condition)
                throw new ArgumentException($"Parameter '{parameterName}' must be true.", parameterName);
        }

        public static void IsFalse(bool condition, string parameterName)
        {
            if (condition)
                throw new ArgumentException($"Parameter '{parameterName}' must be false.", parameterName);
        }

        #endregion
    }
}

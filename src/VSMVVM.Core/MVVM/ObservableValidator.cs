using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// INotifyDataErrorInfo 구현 기반 유효성 검증 ViewModel.
    /// DataAnnotation 어트리뷰트를 사용한 선언적 검증을 지원합니다.
    /// </summary>
    public class ObservableValidator : ViewModelBase, INotifyDataErrorInfo
    {
        #region Fields

        private readonly Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

        #endregion

        #region INotifyDataErrorInfo

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public bool HasErrors => _errors.Count > 0;

        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return _errors.SelectMany(e => e.Value);

            if (_errors.TryGetValue(propertyName, out var errors))
                return errors;

            return Array.Empty<string>();
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// 프로퍼티 값을 설정하면서 DataAnnotation 기반 검증을 수행합니다.
        /// </summary>
        protected new bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (!base.SetProperty(ref storage, value, propertyName))
                return false;

            ValidateProperty(value, propertyName);
            return true;
        }

        /// <summary>
        /// 특정 프로퍼티의 유효성을 검증합니다.
        /// </summary>
        protected void ValidateProperty<T>(T value, [CallerMemberName] string propertyName = null)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(this) { MemberName = propertyName };

            Validator.TryValidateProperty(value, context, results);

            ClearErrors(propertyName);

            if (results.Count > 0)
            {
                var errorMessages = results.Select(r => r.ErrorMessage).ToList();
                SetErrors(propertyName, errorMessages);
            }
        }

        /// <summary>
        /// 모든 프로퍼티의 유효성을 검증합니다.
        /// </summary>
        protected void ValidateAllProperties()
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(this);

            _errors.Clear();
            Validator.TryValidateObject(this, context, results, validateAllProperties: true);

            foreach (var result in results)
            {
                foreach (var memberName in result.MemberNames)
                {
                    if (!_errors.ContainsKey(memberName))
                    {
                        _errors[memberName] = new List<string>();
                    }

                    _errors[memberName].Add(result.ErrorMessage);
                    OnErrorsChanged(memberName);
                }
            }

            OnPropertyChanged(nameof(HasErrors));
        }

        /// <summary>
        /// 특정 프로퍼티의 에러를 수동으로 설정합니다.
        /// </summary>
        protected void SetErrors(string propertyName, IEnumerable<string> errors)
        {
            if (string.IsNullOrEmpty(propertyName)) return;

            var errorList = errors?.ToList();
            if (errorList == null || errorList.Count == 0)
            {
                ClearErrors(propertyName);
                return;
            }

            _errors[propertyName] = errorList;
            OnErrorsChanged(propertyName);
            OnPropertyChanged(nameof(HasErrors));
        }

        /// <summary>
        /// 특정 프로퍼티의 에러를 제거합니다.
        /// </summary>
        protected void ClearErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return;

            if (_errors.Remove(propertyName))
            {
                OnErrorsChanged(propertyName);
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        /// <summary>
        /// 모든 에러를 제거합니다.
        /// </summary>
        protected void ClearAllErrors()
        {
            var propertyNames = _errors.Keys.ToList();
            _errors.Clear();

            foreach (var name in propertyNames)
            {
                OnErrorsChanged(name);
            }

            OnPropertyChanged(nameof(HasErrors));
        }

        #endregion

        #region Private Methods

        private void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// Variables 관리 다이얼로그 ViewModel.
    /// 부모(SchedulerDemoViewModel) 가 NodeGraph 참조를 DialogParameter 로 주입 →
    /// 본 VM 은 그 그래프의 Variables 를 직접 추가/제거. 닫힐 때까지 연속 작업 가능.
    /// </summary>
    public partial class VariablesManagerWindowViewModel : ViewModelBase
    {
        private NodeGraph _graph;

        /// <summary>현재 그래프의 변수 거울 (ObservableCollection).</summary>
        public ObservableCollection<GraphVariable> Variables { get; } = new ObservableCollection<GraphVariable>();

        [Property] private string _newName = "myVar";
        [Property] private string _newTypeLabel = "int";
        [Property] private string _newDefault = "0";
        [Property] private string _status = string.Empty;

        public IReadOnlyList<string> TypeOptions { get; } = new[] { "int", "double", "string", "bool", "long", "Mat" };

        /// <summary>WindowService 가 ShowDialog 시 NodeGraph 주입.</summary>
        public NodeGraph DialogParameter
        {
            get => _graph;
            set
            {
                _graph = value;
                RefreshFromGraph();
            }
        }

        /// <summary>다이얼로그 결과 — 직접 그래프를 변이했으므로 별도 결과 없음 (null 반환).</summary>
        public object DialogResultData => null;

        public event EventHandler RequestClose;

        private void RefreshFromGraph()
        {
            Variables.Clear();
            if (_graph == null) return;
            foreach (var v in _graph.Variables.Values) Variables.Add(v);
        }

        private static Type ResolveType(string label) => label switch
        {
            "int" => typeof(int),
            "double" => typeof(double),
            "string" => typeof(string),
            "bool" => typeof(bool),
            "long" => typeof(long),
            "Mat" => typeof(OpenCvSharp.Mat),
            _ => null,
        };

        private static object ParseDefault(Type t, string raw)
        {
            if (t == typeof(string)) return raw ?? string.Empty;
            // 참조 타입(Mat 등)은 기본값 null — 런타임에 SetVariable 로 채움.
            if (!t.IsValueType) return null;
            if (string.IsNullOrWhiteSpace(raw)) return Activator.CreateInstance(t);
            try { return Convert.ChangeType(raw, t, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return Activator.CreateInstance(t); }
        }

        /// <summary>
        /// 임의 타입 T 에 대해 GetVariableNode/SetVariableNode 를 NodeMetadataRegistry 에 등록.
        /// 추가 후에도 다이얼로그는 닫히지 않아 연속 등록 가능.
        /// </summary>
        [RelayCommand]
        private void Add()
        {
            if (_graph == null) { Status = "Graph not set."; return; }
            var name = (NewName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name)) { Status = "Name is empty."; return; }
            if (_graph.Variables.ContainsKey(name)) { Status = $"'{name}' already exists."; return; }

            var t = ResolveType(NewTypeLabel);
            if (t == null) { Status = $"Unknown type '{NewTypeLabel}'."; return; }
            var def = ParseDefault(t, NewDefault);

            try
            {
                _graph.AddVariable(name, t, def);
                Variables.Add(_graph.Variables[name]);
                Status = $"Added '{name}' ({t.Name}).";
                NewName = string.Empty; // 연속 추가 편의 — 이름만 초기화, 타입/기본값은 유지
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }

        [RelayCommand]
        private void Remove(string name)
        {
            if (_graph == null || string.IsNullOrEmpty(name)) return;
            if (_graph.RemoveVariable(name))
            {
                for (int i = Variables.Count - 1; i >= 0; i--)
                {
                    if (Variables[i].Name == name) Variables.RemoveAt(i);
                }
                Status = $"Removed '{name}'.";
            }
        }

        [RelayCommand]
        private void Close()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

    }
}

using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// 창 위치/크기 복원 서비스 인터페이스.
    /// </summary>
    public interface IWindowPlacementService
    {
        /// <summary>
        /// 창 위치/크기를 저장합니다.
        /// </summary>
        void Save(Window window);

        /// <summary>
        /// 저장된 위치/크기를 복원합니다.
        /// </summary>
        void Restore(Window window);
    }

    /// <summary>
    /// Json 파일 기반 창 위치/크기 저장/복원 서비스 구현체.
    /// </summary>
    public sealed class WindowPlacementService : IWindowPlacementService
    {
        #region Inner Types

        /// <summary>
        /// 직렬화용 창 위치 데이터.
        /// </summary>
        private sealed class PlacementData
        {
            /// <summary>
            /// 창 왼쪽 좌표.
            /// </summary>
            public double Left { get; set; }

            /// <summary>
            /// 창 상단 좌표.
            /// </summary>
            public double Top { get; set; }

            /// <summary>
            /// 창 폭.
            /// </summary>
            public double Width { get; set; }

            /// <summary>
            /// 창 높이.
            /// </summary>
            public double Height { get; set; }

            /// <summary>
            /// 최대화 상태 여부.
            /// </summary>
            public bool IsMaximized { get; set; }
        }

        #endregion

        #region Fields

        private readonly string _configFilePath;

        #endregion

        #region Constructor

        /// <summary>
        /// 설정 파일 이름을 지정하여 생성합니다.
        /// </summary>
        public WindowPlacementService(string configName)
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VSMVVM");

            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }

            _configFilePath = Path.Combine(appDataDir, configName + ".placement.json");
        }

        #endregion

        #region IWindowPlacementService

        public void Save(Window window)
        {
            if (window == null)
            {
                return;
            }

            var data = new PlacementData
            {
                Left = window.Left,
                Top = window.Top,
                Width = window.Width,
                Height = window.Height,
                IsMaximized = window.WindowState == WindowState.Maximized
            };

            try
            {
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch
            {
                // 저장 실패는 무시합니다.
            }
        }

        public void Restore(Window window)
        {
            if (window == null)
            {
                return;
            }

            if (!File.Exists(_configFilePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_configFilePath);
                var data = JsonSerializer.Deserialize<PlacementData>(json);

                if (data == null)
                {
                    return;
                }

                window.Left = data.Left;
                window.Top = data.Top;
                window.Width = data.Width;
                window.Height = data.Height;

                if (data.IsMaximized)
                {
                    window.WindowState = WindowState.Maximized;
                }

                // 화면 범위 검증
                EnsureWindowIsOnScreen(window);
            }
            catch
            {
                // 복원 실패는 무시합니다.
            }
        }

        #endregion

        #region Private Methods

        private static void EnsureWindowIsOnScreen(Window window)
        {
            var screenWidth = SystemParameters.VirtualScreenWidth;
            var screenHeight = SystemParameters.VirtualScreenHeight;
            var screenLeft = SystemParameters.VirtualScreenLeft;
            var screenTop = SystemParameters.VirtualScreenTop;

            if (window.Left < screenLeft)
            {
                window.Left = screenLeft;
            }

            if (window.Top < screenTop)
            {
                window.Top = screenTop;
            }

            if (window.Left + window.Width > screenLeft + screenWidth)
            {
                window.Left = screenLeft + screenWidth - window.Width;
            }

            if (window.Top + window.Height > screenTop + screenHeight)
            {
                window.Top = screenTop + screenHeight - window.Height;
            }
        }

        #endregion
    }
}

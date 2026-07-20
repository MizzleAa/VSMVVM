using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace VSMVVM.WPF.Sample.Scheduler
{
    /// <summary>
    /// MNIST CSV 다운로드/캐시/파싱 헬퍼. 사용자 스니펫에서 호출.
    /// Sample 어셈블리에 두는 이유: System.Net.Http 가 스니펫 컴파일 시점에 AppDomain 에 로드되지 않을 수 있어
    /// 사용자 스니펫 내부에서 직접 HttpClient 를 쓰면 참조 실패 위험이 있음.
    /// </summary>
    public static class MnistDataLoader
    {
        /// <summary>URL 에서 CSV 를 다운로드 (없으면) 후 캐시 경로 반환. 캐시: %LOCALAPPDATA%/VSMVVM/Data/mnist/.</summary>
        public static string EnsureCached(string url)
        {
            var cacheDir = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "VSMVVM", "Data", "mnist");
            Directory.CreateDirectory(cacheDir);
            var safe = string.Join("_", new Uri(url).AbsolutePath.Trim('/').Split('/'));
            var cachePath = Path.Combine(cacheDir, safe);
            if (!File.Exists(cachePath))
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);
                var bytes = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(cachePath, bytes);
            }
            return cachePath;
        }

        /// <summary>MNIST CSV (label,pixel0..pixel783) 파싱. 픽셀 [0,1] 정규화. sampleCap 로 처음 N 행만.</summary>
        public static (double[][] X, int[] Y) LoadCsv(string url, int sampleCap)
        {
            var path = EnsureCached(url);
            var xs = new List<double[]>();
            var ys = new List<int>();
            using var reader = new StreamReader(path);
            string line;
            while ((line = reader.ReadLine()) != null && xs.Count < sampleCap)
            {
                if (line.Length == 0) continue;
                var parts = line.Split(',');
                if (parts.Length < 785) continue;
                if (!int.TryParse(parts[0], out int label)) continue;
                var pix = new double[784];
                for (int i = 0; i < 784; i++)
                    pix[i] = double.Parse(parts[i + 1], System.Globalization.CultureInfo.InvariantCulture) / 255.0;
                xs.Add(pix);
                ys.Add(label);
            }
            return (xs.ToArray(), ys.ToArray());
        }
    }
}

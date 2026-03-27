using System;
using System.Collections.Generic;
using System.Windows;

namespace VSMVVM.WPF.Design.Core
{
    /// <summary>
    /// 동일한 Source URI를 가진 ResourceDictionary를 캐싱하여 재사용.
    /// BAML 재파싱 없이 리소스를 제공하여 성능을 최적화합니다.
    /// </summary>
    public class SharedResourceDictionary : ResourceDictionary
    {
        #region Fields

        private static readonly Dictionary<Uri, Dictionary<object, object>> _cachedEntries = new Dictionary<Uri, Dictionary<object, object>>();
        private static readonly Dictionary<Uri, List<Uri>> _cachedMergedSources = new Dictionary<Uri, List<Uri>>();
        private Uri _sourceUri;

        #endregion

        #region Properties

        /// <summary>
        /// ResourceDictionary Source URI. 캐시된 항목이 있으면 재사용합니다.
        /// </summary>
        public new Uri Source
        {
            get => _sourceUri;
            set
            {
                _sourceUri = value;

                if (value == null)
                {
                    return;
                }

                if (_cachedEntries.TryGetValue(value, out var entries))
                {
                    foreach (var kvp in entries)
                    {
                        this[kvp.Key] = kvp.Value;
                    }

                    if (_cachedMergedSources.TryGetValue(value, out var mergedUris))
                    {
                        foreach (var mergedUri in mergedUris)
                        {
                            var merged = new SharedResourceDictionary
                            {
                                Source = mergedUri
                            };
                            MergedDictionaries.Add(merged);
                        }
                    }
                }
                else
                {
                    base.Source = value;

                    var entryCache = new Dictionary<object, object>();
                    foreach (var key in Keys)
                    {
                        entryCache[key] = this[key];
                    }
                    _cachedEntries[value] = entryCache;

                    var mergedSources = new List<Uri>();
                    foreach (var merged in MergedDictionaries)
                    {
                        if (merged.Source != null)
                        {
                            mergedSources.Add(merged.Source);
                        }
                    }
                    _cachedMergedSources[value] = mergedSources;
                }
            }
        }

        #endregion
    }
}

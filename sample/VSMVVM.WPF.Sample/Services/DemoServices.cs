using System;

namespace VSMVVM.WPF.Sample.Services
{
    /// <summary>
    /// Shared counter service registered as Singleton.
    /// Demonstrates cross-window state sharing via DI.
    /// </summary>
    public class SharedCounterService
    {
        public int Count { get; set; }
    }

    /// <summary>
    /// DI Lifetime demo interfaces and implementations.
    /// </summary>
    public interface ILifetimeDemo
    {
        string InstanceId { get; }
    }

    public class SingletonDemo : ILifetimeDemo
    {
        public string InstanceId { get; } = Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    public class ScopedDemo : ILifetimeDemo
    {
        public string InstanceId { get; } = Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    public class TransientDemo : ILifetimeDemo
    {
        public string InstanceId { get; } = Guid.NewGuid().ToString("N").Substring(0, 8);
    }
}

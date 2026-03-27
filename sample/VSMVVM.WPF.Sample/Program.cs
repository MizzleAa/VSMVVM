using VSMVVM.WPF.Host;

namespace VSMVVM.WPF.Sample
{
    public static class Program
    {
        [System.STAThread]
        public static void Main(string[] args)
        {
            VSMVVMHost
                .CreateHost<Bootstrapper, App>(args, "SampleApp")
                .UseSplash<Views.SplashWindow>()
                .Build()
                .RunApp<Views.MainWindow>();
        }
    }
}

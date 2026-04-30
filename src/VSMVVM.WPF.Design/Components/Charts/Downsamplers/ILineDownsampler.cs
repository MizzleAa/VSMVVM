namespace VSMVVM.WPF.Design.Components.Charts.Downsamplers
{
    public interface ILineDownsampler
    {
        void Downsample(double[] xs, double[] ys, int start, int end, int targetBuckets,
                        out double[] outXs, out double[] outYs);
    }
}

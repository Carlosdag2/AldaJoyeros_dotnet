namespace AldaJoyeros.Helpers;

public sealed record MetricChartScale(double Maximum, double Step)
{
    public static MetricChartScale Create(IEnumerable<double> values, bool integers)
    {
        var maximum = values.Where(v=>double.IsFinite(v)&&v>=0).DefaultIfEmpty(0).Max();
        if (maximum == 0) return integers ? new(4,1) : new(1,0.25);
        var raw = Math.Max(maximum / 4, integers ? 0.25 : 0.01);
        var power = Math.Pow(10,Math.Floor(Math.Log10(raw)));
        var factor = new[]{1d,2d,2.5d,5d,10d}.First(v=>v*power>=raw);
        var step = factor*power;
        if(integers)step=Math.Max(1,Math.Ceiling(step));
        else step=Math.Ceiling(step*100-1e-8)/100;
        return new(step*4,step);
    }
}

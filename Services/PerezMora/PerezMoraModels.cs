using System.ComponentModel.DataAnnotations;
using AldaJoyeros.Services.Munreco;

namespace AldaJoyeros.Services.PerezMora;

public sealed class PerezMoraOptions {
    public bool WorkerEnabled { get; set; }=true;
    public string DataDirectory { get; set; }="App_Data/PerezMora";
    public string DotnetPath { get; set; }="dotnet";
    public string? RunnerPath { get; set; }
}
public sealed class PerezMoraSchedule {
    public bool Enabled { get; set; }
    [Range(1,7)] public int Day { get; set; }=1;
    [Range(0,23)] public int Hour { get; set; }=10;
    public DateTime? NextUtc { get; set; }
    public static DateTime Next(DateTime utc,int day,int hour) {
        if(day is <1 or >7 || hour is <0 or >23)throw new ArgumentOutOfRangeException(nameof(day));
        var local=TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc,DateTimeKind.Utc),MunrecoSchedule.Zone);
        var isoDay=local.DayOfWeek==DayOfWeek.Sunday?7:(int)local.DayOfWeek;
        var next=local.Date.AddDays((day-isoDay+7)%7).AddHours(hour);
        if(next<=local)next=next.AddDays(7);
        while(MunrecoSchedule.Zone.IsInvalidTime(next))next=next.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(next,MunrecoSchedule.Zone);
    }
}
public sealed record PerezMoraJob(string Id,string Trigger,string State,string Progress,DateTime CreatedUtc,DateTime? StartedUtc,DateTime? EndedUtc,string? ResultJson) {
    public bool Active=>State is "queued" or "running";
    public string Label=>State switch {"queued"=>"En espera","running"=>"En proceso","previewed"=>"Comparación terminada","completed"=>"Completada","needs_login"=>"Renovar enlace","interrupted"=>"Interrumpida",_=>"Revisar incidencia"};
}
public sealed record PerezMoraDashboard(PerezMoraSchedule Schedule,IReadOnlyList<PerezMoraJob> Jobs,bool Connected,bool WorkerEnabled,decimal Coefficient=1,PerezMoraPricePreview? PricePreview=null);
public sealed record PerezMoraPriceRow(long Id,string Reference,decimal Cost,decimal CurrentPrice,decimal NewPrice,bool Manual,string State);
public sealed record PerezMoraPricePreview(decimal Coefficient,int Updated,int Protected,IReadOnlyList<PerezMoraPriceRow> Examples);
public sealed class PerezMoraConnection {
    [Required,MaxLength(1000)] public string DownloadUrl { get; set; }="";
}

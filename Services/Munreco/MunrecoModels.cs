using System.ComponentModel.DataAnnotations;

namespace AldaJoyeros.Services.Munreco;

public sealed class MunrecoOptions {
    public bool WorkerEnabled { get; set; } = true;
    public string DataDirectory { get; set; } = "App_Data/Munreco";
    public string? ImageStore { get; set; }
    public string? RunnerPath { get; set; }
    public string DotnetPath { get; set; } = "dotnet";
    public string? BootstrapSessionFile { get; set; }
}

public sealed class MunrecoSchedule {
    public bool Enabled { get; set; }
    [Range(1,28)] public int Day { get; set; } = 1;
    [Range(0,23)] public int Hour { get; set; } = 10;
    public DateTime? NextUtc { get; set; }
    public static TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows()?"Romance Standard Time":"Europe/Madrid");
    public static DateTime Next(DateTime utc,int day,int hour) {
        if(day is <1 or >28 || hour is <0 or >23)throw new ArgumentOutOfRangeException(nameof(day));
        var local=TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc,DateTimeKind.Utc),Zone);
        var next=new DateTime(local.Year,local.Month,day,hour,0,0,DateTimeKind.Unspecified);
        if(next<=local)next=next.AddMonths(1);
        while(Zone.IsInvalidTime(next))next=next.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(next,Zone);
    }
    public static string Format(DateTime? utc)=>utc.HasValue?TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value,DateTimeKind.Utc),Zone).ToString("dd/MM/yyyy HH:mm"):"—";
}

public sealed record MunrecoJob(string Id,string Trigger,string State,string Progress,DateTime CreatedUtc,DateTime? StartedUtc,DateTime? EndedUtc,string? ResultJson) {
    public bool Active=>State is "queued" or "running";
    public string Label=>State switch {"queued"=>"En espera","running"=>"En curso","previewed"=>"Comparación terminada","completed"=>"Completada","needs_login"=>"Renovar acceso","interrupted"=>"Interrumpida",_=>"Revisar incidencia"};
}
public sealed record MunrecoDashboard(MunrecoSchedule Schedule,IReadOnlyList<MunrecoJob> Jobs,bool Connected,bool WorkerEnabled,DateTime? SessionUpdatedUtc);
public sealed class MunrecoLogin {
    [Required,MaxLength(200)] public string Username { get; set; } = "";
    [Required,MaxLength(500)] public string Password { get; set; } = "";
}

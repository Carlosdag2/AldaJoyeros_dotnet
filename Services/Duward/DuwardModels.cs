namespace AldaJoyeros.Services.Duward;
public sealed class DuwardOptions {
 public bool WorkerEnabled{get;set;}=true;
 public string DataDirectory{get;set;}="App_Data/Duward";
 public string DotnetPath{get;set;}="dotnet";
 public string? RunnerPath{get;set;}
}
public sealed record DuwardJob(string Id,string Trigger,string State,string Progress,DateTime CreatedUtc,DateTime? StartedUtc,DateTime? EndedUtc,string? ResultJson) {
 public bool Active=>State is "queued" or "running";
 public string Label=>State switch{"queued"=>"En espera","running"=>"En proceso","previewed"=>"Comprobación terminada","completed"=>"Actualización completada","interrupted"=>"Interrumpida",_=>"Revisar incidencia"};
}
public sealed record DuwardDashboard(IReadOnlyList<DuwardJob> Jobs,bool WorkerEnabled);

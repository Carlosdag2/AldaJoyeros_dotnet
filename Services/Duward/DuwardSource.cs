using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
namespace AldaJoyeros.Services.Duward;
public sealed class DuwardSource {
 readonly DuwardOptions options;
 public string Root{get;}
 public string Images=>Path.Combine(Root,"images");
 public DuwardSource(IOptions<DuwardOptions> options,IWebHostEnvironment env) {
  this.options=options.Value;Root=Path.GetFullPath(options.Value.DataDirectory,env.ContentRootPath);
  var web=Path.GetFullPath(env.WebRootPath??Path.Combine(env.ContentRootPath,"wwwroot"));
  if(Root.Equals(web,StringComparison.OrdinalIgnoreCase)||Root.StartsWith(web+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Duward necesita una carpeta privada fuera de wwwroot.");
  Directory.CreateDirectory(Root);Directory.CreateDirectory(Images);Directory.CreateDirectory(Path.Combine(Root,"runs"));
 }
 public string Run(string id){if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Identificador no válido.");return Path.Combine(Root,"runs",id);}
 public ProcessStartInfo StartInfo() {
  var runner=options.RunnerPath??Path.Combine(AppContext.BaseDirectory,"DuwardRunner","DuwardSync.dll");
  if(!File.Exists(runner))throw new InvalidOperationException("Compila o publica la web completa para instalar el importador Duward.");
  var p=new ProcessStartInfo(options.DotnetPath){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Root};p.ArgumentList.Add(runner);return p;
 }
 public async Task Analyze(string[] files,CancellationToken ct) {
  var p=StartInfo();p.ArgumentList.Add("--analyze");foreach(var file in files)p.ArgumentList.Add(file);
  using var process=new Process{StartInfo=p};process.Start();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(60));
  using var stop=timeout.Token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
  var output=process.StandardOutput.ReadToEndAsync(timeout.Token);var errors=process.StandardError.ReadToEndAsync(timeout.Token);await process.WaitForExitAsync(timeout.Token);var error=await errors;
  if(process.ExitCode!=0)throw new InvalidDataException(error.Trim());using var json=JsonDocument.Parse(await output);
 }
}

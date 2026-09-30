using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace AldaJoyeros.Services.Munreco;

public sealed class MunrecoSession {
    readonly IDataProtector protector;readonly MunrecoPaths paths;readonly MunrecoOptions options;
    readonly SemaphoreSlim gate=new(1,1);
    public MunrecoSession(IDataProtectionProvider protection,MunrecoPaths paths,IOptions<MunrecoOptions> options,IWebHostEnvironment env) {
        protector=protection.CreateProtector("AldaJoyeros.Munreco.Session.v1");this.paths=paths;this.options=options.Value;
        if(env.IsDevelopment()&&!File.Exists(paths.Session)&&!string.IsNullOrEmpty(this.options.BootstrapSessionFile)&&File.Exists(this.options.BootstrapSessionFile)) {
            var session=File.ReadAllText(this.options.BootstrapSessionFile);Validate(session);File.WriteAllText(paths.Session,protector.Protect(session));
        }
    }
    public bool Connected=>File.Exists(paths.Session);
    public DateTime? UpdatedUtc=>Connected?File.GetLastWriteTimeUtc(paths.Session):null;
    static void Validate(string value) {
        if(value.Length>128*1024)throw new InvalidOperationException("Sesion invalida");
        using var doc=JsonDocument.Parse(value);
        if(doc.RootElement.GetProperty("cookies").GetArrayLength()==0)throw new InvalidOperationException("Sesion vacia");
    }
    public async Task<string> Read(CancellationToken ct) {
        await gate.WaitAsync(ct);try{return protector.Unprotect(await File.ReadAllTextAsync(paths.Session,ct));}finally{gate.Release();}
    }
    public async Task Save(string value,CancellationToken ct) {
        Validate(value);await gate.WaitAsync(ct);
        try {var temp=paths.Session+".tmp";await File.WriteAllTextAsync(temp,protector.Protect(value),ct);File.Move(temp,paths.Session,true);}finally{gate.Release();}
    }
    public ProcessStartInfo StartInfo() {
        if(!File.Exists(paths.Runner))throw new InvalidOperationException("El componente de sincronizacion no esta instalado. Compila o publica el proyecto completo.");
        var start=new ProcessStartInfo(options.DotnetPath){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=paths.Root};
        start.ArgumentList.Add(paths.Runner);return start;
    }
    public async Task Connect(MunrecoLogin login,CancellationToken ct) {
        var start=StartInfo();start.ArgumentList.Add("--connect");
        using var process=new Process{StartInfo=start};process.Start();
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(2));
        using var stop=timeout.Token.Register(()=>{try {if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
        var output=process.StandardOutput.ReadToEndAsync(timeout.Token);var error=process.StandardError.ReadToEndAsync(timeout.Token);
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username=login.Username,password=login.Password}));process.StandardInput.Close();
        await process.WaitForExitAsync(timeout.Token);await error;
        if(process.ExitCode!=0)throw new InvalidOperationException("No se ha podido conectar. Comprueba tu usuario y contraseña B2B. Si Munreco solicita una verificación adicional, completa el acceso con el proveedor.");
        await Save(await output,ct);
    }
}

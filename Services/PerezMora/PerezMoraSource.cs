using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace AldaJoyeros.Services.PerezMora;

public sealed class PerezMoraSource {
    readonly IDataProtector protector;readonly PerezMoraOptions options;
    readonly SemaphoreSlim gate=new(1,1);
    public string Root { get; }
    public string Images=>Path.Combine(Root,"imagenes");
    string ConnectionFile=>Path.Combine(Root,"download.protected");
    public bool Connected=>File.Exists(ConnectionFile);
    public PerezMoraSource(IDataProtectionProvider protection,IOptions<PerezMoraOptions> options,IWebHostEnvironment env) {
        this.options=options.Value;protector=protection.CreateProtector("AldaJoyeros.PerezMora.Download.v1");
        Root=Path.GetFullPath(options.Value.DataDirectory,env.ContentRootPath);
        var web=Path.GetFullPath(env.WebRootPath??Path.Combine(env.ContentRootPath,"wwwroot"));
        if(Root.Equals(web,StringComparison.OrdinalIgnoreCase)||Root.StartsWith(web+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Pérez Mora requiere una carpeta privada fuera de wwwroot.");
        Directory.CreateDirectory(Root);Directory.CreateDirectory(Images);Directory.CreateDirectory(Path.Combine(Root,"runs"));
    }
    public string Run(string id) {if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Invalid job id");return Path.Combine(Root,"runs",id);}
    public static bool ValidUrl(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var url)&&url.Scheme=="https"&&url.IsDefaultPort&&url.UserInfo.Length==0
        &&url.Host.Equals("perezmorajewelry.com",StringComparison.OrdinalIgnoreCase)&&Regex.IsMatch(url.AbsolutePath,@"^/exporta_excel/[0-9]+/[A-Z]{2}/0/?$")&&url.Query.Length==0&&url.Fragment.Length==0;
    public async Task<byte[]> Download(string url,CancellationToken ct) {
        if(!ValidUrl(url))throw new InvalidDataException("Utiliza el enlace HTTPS del botón de descarga oficial de Pérez Mora.");
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false};using var client=new HttpClient(handler){Timeout=TimeSpan.FromMinutes(2)};
        using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct);
        if(!response.IsSuccessStatusCode)throw new InvalidDataException("El enlace del proveedor no permite descargar el catálogo. Renueva el enlace desde Descargas.");
        if(response.Content.Headers.ContentLength>30*1024*1024)throw new InvalidDataException("El catálogo supera 30 MB.");
        using var buffer=new MemoryStream();await using var stream=await response.Content.ReadAsStreamAsync(ct);
        var bytes=new byte[65536];int read;
        while((read=await stream.ReadAsync(bytes,ct))>0){if(buffer.Length+read>30*1024*1024)throw new InvalidDataException("El catálogo supera 30 MB.");await buffer.WriteAsync(bytes.AsMemory(0,read),ct);}
        return buffer.ToArray();
    }
    public async Task Save(string url,CancellationToken ct) {
        if(!ValidUrl(url))throw new InvalidDataException("El enlace no corresponde a la descarga oficial del proveedor.");
        await gate.WaitAsync(ct);try{var temp=ConnectionFile+".tmp";await File.WriteAllTextAsync(temp,protector.Protect(url),ct);File.Move(temp,ConnectionFile,true);}finally{gate.Release();}
    }
    public async Task<byte[]> Download(CancellationToken ct) {
        await gate.WaitAsync(ct);string url;
        try{url=protector.Unprotect(await File.ReadAllTextAsync(ConnectionFile,ct));}finally{gate.Release();}
        return await Download(url,ct);
    }
    public ProcessStartInfo StartInfo() {
        var runner=options.RunnerPath??Path.Combine(AppContext.BaseDirectory,"PerezMoraRunner","PerezMoraSync.dll");
        if(!File.Exists(runner))throw new InvalidOperationException("Publica o compila el proyecto completo para instalar el importador.");
        var start=new ProcessStartInfo(options.DotnetPath){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Root};
        start.ArgumentList.Add(runner);return start;
    }
    public async Task<string> Analyze(string file,CancellationToken ct) {
        var start=StartInfo();start.ArgumentList.Add("--analyze");start.ArgumentList.Add(file);
        using var process=new Process{StartInfo=start};process.Start();
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(1));
        using var stop=timeout.Token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
        var output=process.StandardOutput.ReadToEndAsync(timeout.Token);var error=process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);var errors=await error;
        if(process.ExitCode!=0)throw new InvalidDataException(errors.Trim());
        var result=await output;using var json=JsonDocument.Parse(result);return result;
    }
}

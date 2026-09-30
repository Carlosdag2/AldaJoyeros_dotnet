using Microsoft.Extensions.Options;
namespace AldaJoyeros.Services.Munreco;

public sealed class MunrecoPaths {
    public string Root { get; }
    public string Images { get; }
    public string Runner { get; }
    public string Session=>Path.Combine(Root,"session.protected");
    public string History=>Path.Combine(Root,"runs");
    public string Private=>Path.Combine(Root,"private");
    public MunrecoPaths(IWebHostEnvironment env,IOptions<MunrecoOptions> options) {
        Root=Path.GetFullPath(options.Value.DataDirectory,env.ContentRootPath);
        var web=Path.GetFullPath(env.WebRootPath??Path.Combine(env.ContentRootPath,"wwwroot"));
        if(Root.Equals(web,StringComparison.OrdinalIgnoreCase)||Root.StartsWith(web+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Munreco requiere una carpeta privada fuera de wwwroot");
        Images=Path.GetFullPath(options.Value.ImageStore??Path.Combine(Root,"catalogo"),env.ContentRootPath);
        Runner=Path.GetFullPath(options.Value.RunnerPath??Path.Combine(AppContext.BaseDirectory,"MunrecoRunner","MunrecoSync.dll"),env.ContentRootPath);
        Directory.CreateDirectory(Root);Directory.CreateDirectory(History);Directory.CreateDirectory(Private);Directory.CreateDirectory(Images);
    }
    public string Run(string id) {if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Invalid job id");return Path.Combine(History,id);}
}

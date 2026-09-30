using System.Text.Json;
using PerezMoraSync;

try {
    if(args.Length==2&&args[0]=="--analyze") {
        var catalog=Catalog.Parse(await File.ReadAllBytesAsync(args[1]));Console.WriteLine(JsonSerializer.Serialize(catalog.Summary));return;
    }
    string Arg(string key){var i=Array.IndexOf(args,key);if(i<0||i+1>=args.Length)throw new ArgumentException("Falta "+key);return args[i+1];}
    var input=Arg("--input");var root=Path.GetFullPath(Arg("--root"));var images=Path.GetFullPath(Arg("--images"));
    Directory.CreateDirectory(root);var data=Catalog.Parse(await File.ReadAllBytesAsync(input));
    await Engine.Run(data,root,images,args.Contains("--apply"));
}catch(Exception e) {
    // Never print connection strings or private download addresses from nested exceptions.
    Console.Error.WriteLine(e is InvalidDataException or ArgumentException?e.Message:"Importación interrumpida ("+e.GetType().Name+"). Puede reintentarse sin duplicar productos.");
    Environment.ExitCode=1;
}

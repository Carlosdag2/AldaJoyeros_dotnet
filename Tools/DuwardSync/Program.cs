using System.Text.Json;
using DuwardSync;
try {
 if(args.Length>=2&&args[0]=="--analyze") {var data=Catalog.Merge(args.Skip(1).Select(p=>Catalog.Parse(File.ReadAllBytes(p))));Console.WriteLine(JsonSerializer.Serialize(data.Summary));return;}
 string Arg(string key){var i=Array.IndexOf(args,key);if(i<0||i+1>=args.Length)throw new ArgumentException("Falta "+key);return args[i+1];}
 var input=Path.GetFullPath(Arg("--input"));var root=Path.GetFullPath(Arg("--root"));var images=Path.GetFullPath(Arg("--images"));
 using var manifest=JsonDocument.Parse(await File.ReadAllTextAsync(input));var folder=Path.GetDirectoryName(input)!;
 var paths=manifest.RootElement.GetProperty("files").EnumerateArray().Select(x=>x.GetString()!).ToArray();
 if(paths.Length is <1 or >10||paths.Any(x=>Path.GetFileName(x)!=x||!x.EndsWith(".csv",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Lote de CSV no válido.");
 var catalog=Catalog.Merge(paths.Select(x=>Catalog.Parse(File.ReadAllBytes(Path.Combine(folder,x)))));Directory.CreateDirectory(root);
 await Engine.Run(catalog,root,images,args.Contains("--apply"),args.Contains("--offline-images"),manifest.RootElement.TryGetProperty("retireMissing",out var retire)&&retire.GetBoolean());
}catch(Exception e){Console.Error.WriteLine(e is InvalidDataException or ArgumentException?e.Message:"Importación interrumpida ("+e.GetType().Name+"). Puede reintentarse sin duplicar productos.");Environment.ExitCode=1;}

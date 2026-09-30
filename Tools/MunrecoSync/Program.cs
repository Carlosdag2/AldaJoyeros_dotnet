if(args.Contains("--self-test")){SyncTests.Run();return;}
if(args.Contains("--connect")) {
    try {await ProviderLogin.Run();}
    catch {Console.Error.WriteLine("No se ha podido validar el acceso B2B. Comprueba las credenciales o si Munreco requiere acceso interactivo.");Environment.ExitCode=2;}
    return;
}
try {
    var verify=Array.IndexOf(args,"--verify-run");
    var repair=Array.IndexOf(args,"--repair-initial-enrollment");
    var configIndex=Array.IndexOf(args,"--config");
    var config=configIndex>=0?args[configIndex+1]:"appsettings.json";
    if(repair>=0)await MonthlySync.RepairInitialEnrollment(config,args[repair+1]);
    else if(verify>=0) {
        await SyncVerification.Run(config,args[verify+1]);
    } else await MonthlySync.Run(args);
}
catch(Exception e) { Console.Error.WriteLine("SINCRONIZACION DETENIDA: "+e.Message); Environment.ExitCode=1; }

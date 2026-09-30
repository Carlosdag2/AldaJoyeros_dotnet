using AldaJoyeros.Services.Munreco;
using AldaJoyeros.Services.Implementations;
using AldaJoyeros.Configuration;
using AldaJoyeros.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Microsoft.Playwright;
using System.Text.Json;

var project=Path.GetFullPath(args.FirstOrDefault()??".");
var config=new ConfigurationBuilder().SetBasePath(project).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json",true).Build();
if(args.Contains("--inspect")){await Inspect(config,project);return;}
int passed=0;
void Check(bool ok,string text) {if(!ok)throw new Exception("FAIL: "+text);passed++;Console.WriteLine("PASS: "+text);}
Check(MunrecoSchedule.Next(new DateTime(2026,9,30,12,0,0),1,10)==new DateTime(2026,10,1,8,0,0),"Madrid summer time");
Check(MunrecoSchedule.Next(new DateTime(2026,12,31,12,0,0),1,10)==new DateTime(2027,1,1,9,0,0),"Year boundary and winter time");
Check(MunrecoSchedule.Next(new DateTime(2027,3,1,0,0,0),28,2)==new DateTime(2027,3,28,1,0,0),"Nonexistent DST hour moves forward");
var source=new MySqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection")!);
var testDb="alda_sync_test_"+Guid.NewGuid().ToString("N");
await using(var sql=new MySqlConnection(source.ConnectionString)) {await sql.OpenAsync();await using var c=new MySqlCommand("CREATE DATABASE `"+testDb+"`",sql);await c.ExecuteNonQueryAsync();}
try {
    source.Database=testDb;
    var testConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:DefaultConnection",source.ConnectionString}}).Build();
    var store=new MunrecoStore(testConfig);await store.Ensure();
    var requests=await Task.WhenAll(Enumerable.Range(0,10).Select(_=>store.Enqueue("manual","test")));
    Check(requests.Count(x=>x!=null)==1,"Concurrent requests produce one durable job");
    var id=requests.Single(x=>x!=null)!;await store.Update(id,"running","test");
    await store.RecoverInterrupted(CancellationToken.None);
    Check((await store.Jobs()).Single().State=="interrupted","Restart marks in-flight job interrupted and releases queue");
    await store.SaveSchedule(new(){Enabled=true,Day=1,Hour=10});
    await using(var sql=store.Connection()) {await sql.OpenAsync();await using var c=new MySqlCommand("UPDATE munreco_schedule SET siguiente_utc='2020-01-01'",sql);await c.ExecuteNonQueryAsync();}
    await Task.WhenAll(store.ScheduleDue(CancellationToken.None),store.ScheduleDue(CancellationToken.None));
    Check((await store.Jobs()).Count(x=>x.Active)==1,"Overdue monthly schedule queues only one catch-up");
    Check((await store.Schedule()).NextUtc>DateTime.UtcNow,"Next month persists after queuing");
    var reloaded=new MunrecoStore(testConfig);
    Check((await reloaded.Jobs()).Any(x=>x.State=="queued"),"Queued job survives store restart");
} finally {
    // The only deleted database is the random test schema created above.
    source.Database="";
    await using var sql=new MySqlConnection(source.ConnectionString);await sql.OpenAsync();
    await using var c=new MySqlCommand("DROP DATABASE `"+testDb+"`",sql);await c.ExecuteNonQueryAsync();
}

using var pw=await Playwright.CreateAsync();
await using var browser=await pw.Chromium.LaunchAsync(new(){Channel="msedge",Headless=true});
await using var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=1440,Height=1000}});
var page=await context.NewPageAsync();var errors=new List<string>();page.PageError+=(_,e)=>errors.Add(e);
const string url="http://127.0.0.1:5089";
await page.GotoAsync(url+"/AdminMunreco");
Check(page.Url.Contains("/Auth/Login"),"Anonymous visitor cannot open panel");
var jwt=new JwtService(Options.Create(config.GetSection("JwtSettings").Get<JwtSettings>()!));
string Token(string role)=>jwt.GenerateToken(new UsuarioDto{Id=1,Email="local-verification@example.invalid",Rol=role});
await context.AddCookiesAsync(new[]{new Cookie{Name="jwt_token",Value=Token("USER"),Url=url}});
var forbidden=await context.APIRequest.GetAsync(url+"/AdminMunreco");Check(forbidden.Status==403,"Non-admin cannot open panel");
await context.AddCookiesAsync(new[]{new Cookie{Name="jwt_token",Value=Token("ADMIN"),Url=url}});
var response=await page.GotoAsync(url+"/AdminMunreco",new(){WaitUntil=WaitUntilState.NetworkIdle});
Check(response?.Status==200,"Admin panel renders");
var noCsrf=await context.APIRequest.PostAsync(url+"/AdminMunreco/Actualizar");Check(noCsrf.Status==400,"Update requires anti-forgery token");
var badSchedule=await context.APIRequest.PostAsync(url+"/AdminMunreco/Programar");Check(badSchedule.Status==400,"Schedule requires anti-forgery token");
var badLogin=await context.APIRequest.PostAsync(url+"/AdminMunreco/Conectar");Check(badLogin.Status==400,"Provider login requires anti-forgery token");
var token=await page.Locator("form[action='/AdminMunreco/Programar'] input[name='__RequestVerificationToken']").InputValueAsync();
var invalid=context.APIRequest.CreateFormData();invalid.Set("__RequestVerificationToken",token);invalid.Set("Day","31");invalid.Set("Hour","10");
var scheduleBefore=await new MunrecoStore(config).Schedule();
await context.APIRequest.PostAsync(url+"/AdminMunreco/Programar",new(){Form=invalid});
var scheduleAfter=await new MunrecoStore(config).Schedule();
Check(scheduleBefore.Enabled==scheduleAfter.Enabled&&scheduleBefore.NextUtc==scheduleAfter.NextUtc,"Invalid schedule preserves saved automation");
var output=Path.Combine(project,"App_Data","Munreco","checks");Directory.CreateDirectory(output);
await page.ScreenshotAsync(new(){Path=Path.Combine(output,"panel-desktop.png"),FullPage=true});
await page.SetViewportSizeAsync(390,844);
Check(!await page.EvaluateAsync<bool>("document.documentElement.scrollWidth>window.innerWidth"),"Panel fits a mobile screen");
await page.ScreenshotAsync(new(){Path=Path.Combine(output,"panel-mobile.png"),FullPage=true});
Check(errors.Count==0,"No browser script errors");
if(args.Contains("--preview")) {
    await page.Locator("#preview-button").ClickAsync();
    await page.WaitForURLAsync(url+"/AdminMunreco");
    var jobs=await new MunrecoStore(config).Jobs();var job=jobs.First();
    Check(job.Active&&job.Trigger=="preview","Panel queued a read-only supplier comparison");
    var form=context.APIRequest.CreateFormData();form.Set("__RequestVerificationToken",token);
    await context.APIRequest.PostAsync(url+"/AdminMunreco/Comparar",new(){Form=form});
    Check((await new MunrecoStore(config).Jobs()).Count(x=>x.Active)==1,"A second click does not duplicate the active job");
    Console.WriteLine("REAL_JOB: "+job.Id);
}
await File.WriteAllTextAsync(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(new{passed,at=DateTime.UtcNow,browserErrors=errors}));
Console.WriteLine("ALL "+passed+" PANEL CHECKS PASSED");

static async Task Inspect(IConfiguration config,string project) {
    var rows=await new MunrecoStore(config).Jobs();
    foreach(var row in rows)Console.WriteLine($"JOB {row.Id} {row.Trigger} {row.State} {row.CreatedUtc:O}");
    var job=rows.First(x=>x.State=="previewed");
    if(job.State!="previewed"||job.ResultJson==null)throw new Exception("Comparison did not finish: "+job.Id+" / "+job.State+" / "+job.Progress);
    using var result=JsonDocument.Parse(job.ResultJson);
    if(result.RootElement.GetProperty("apply").GetBoolean()||result.RootElement.GetProperty("offlineSimulation").GetBoolean())throw new Exception("Expected a live read-only comparison");
    var privateRun=Path.Combine(project,"App_Data","Munreco","private",job.Id);
    if(File.Exists(Path.Combine(privateRun,"auth.json"))||File.Exists(Path.Combine(privateRun,"input-auth.json")))throw new Exception("Temporary session was not removed");
    using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Channel="msedge",Headless=true});
    await using var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=1440,Height=1000}});
    const string url="http://127.0.0.1:5089";
    var jwt=new JwtService(Options.Create(config.GetSection("JwtSettings").Get<JwtSettings>()!));
    await context.AddCookiesAsync(new[]{new Cookie{Name="jwt_token",Url=url,Value=jwt.GenerateToken(new UsuarioDto{Id=1,Email="local-verification@example.invalid",Rol="ADMIN"})}});
    var page=await context.NewPageAsync();var response=await page.GotoAsync(url+"/AdminMunreco",new(){WaitUntil=WaitUntilState.NetworkIdle});
    if(response?.Status!=200||!await page.GetByText("Comparación terminada",new(){Exact=true}).First.IsVisibleAsync())throw new Exception("Completed history did not render");
    var download=await context.APIRequest.GetAsync(url+"/AdminMunreco/Informe?id="+job.Id+"&tipo=comparacion");
    if(download.Status!=200)throw new Exception("Report download failed");
    var output=Path.Combine(project,"App_Data","Munreco","checks");
    await page.ScreenshotAsync(new(){Path=Path.Combine(output,"panel-desktop.png"),FullPage=true});
    await page.SetViewportSizeAsync(390,844);
    if(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth>window.innerWidth"))throw new Exception("Mobile history overflow");
    await page.ScreenshotAsync(new(){Path=Path.Combine(output,"panel-mobile.png"),FullPage=true});
    await File.WriteAllTextAsync(Path.Combine(output,"comparison-check.json"),JsonSerializer.Serialize(new{job.Id,job.State,products=result.RootElement.GetProperty("products").GetInt32(),readOnly=true,temporarySessionRemoved=true,reportDownload=true}));
    Console.WriteLine("PASS: comparación real terminada, historial y descarga correctos, sesión temporal eliminada, móvil sin desbordamiento");
}

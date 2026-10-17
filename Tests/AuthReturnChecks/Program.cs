using System.Reflection;
using System.Security.Claims;
using AldaJoyeros.Attributes;
using AldaJoyeros.Controllers;
using AldaJoyeros.DTOs;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

var target="/Pedidos/Detalle/42?origen=correo&vista=completa";
int checks=0;
void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;Console.WriteLine("PASS: "+message);}
AuthController Make(string mode="ok",string? role=null)
{
    var dto=new UsuarioDto{Id=7,Email="cliente@example.invalid",Rol=mode=="admin"?"ADMIN":"USER"};
    var users=Stub.Make<IUsuarioService>((name,_)=> name switch {
        "CreateAsync" => (object)Task.FromResult(dto),
        "LoginAsync" => mode=="throw"?Task.FromException<UsuarioDto?>(new Exception("Prueba")):Task.FromResult<UsuarioDto?>(mode is "wrong" or "register-fallback"?null:dto),
        _ => throw new Exception("Unexpected user call")
    });
    var controller=new AuthController(users,Stub.Make<ICarritoService>((_,_)=>throw new Exception("No carrito en modo escaparate")),
        Stub.Make<IJwtService>((name,_)=>name=="GenerateToken"?"test-token":throw new Exception("Unexpected token call")),
        Stub.Make<IPasswordResetTokenRepository>((_,_)=>throw new Exception("No reset")),Stub.Make<IEmailService>((_,_)=>throw new Exception("No SMTP")),NullLogger<AuthController>.Instance);
    var context=new DefaultHttpContext();context.Request.Scheme="https";context.Request.Host=new HostString("tienda.example");
    if(role!=null)context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"7"),new Claim(ClaimTypes.Role,role)},"test"));
    controller.ControllerContext=new ControllerContext{HttpContext=context};
    controller.Url=new UrlHelper(new ActionContext(context,new RouteData(),new ActionDescriptor()));
    controller.TempData=new TempDataDictionary(context,new MemoryTempData());return controller;
}
var login=Make();Check(login.Login(target) is ViewResult&&(string?)login.ViewBag.ReturnUrl==target,"El login conserva la ruta y la consulta");
foreach(var mode in new[]{"ok","admin"}){
    var c=Make(mode);var result=await c.Login(new(){Email="cliente@example.invalid",Password="test"},target);
    Check(result is LocalRedirectResult r&&r.Url==target,"Login correcto vuelve al enlace: "+mode);
}
foreach(var mode in new[]{"wrong","throw","invalid-model"}){
    var c=Make(mode);if(mode=="invalid-model")c.ModelState.AddModelError("Email","Inválido");
    Check(await c.Login(new(){Email="cliente@example.invalid",Password="test"},target) is ViewResult&&(string?)c.ViewBag.ReturnUrl==target,"Reintento conserva la ruta: "+mode);
}
Check(Make(role:"USER").Login(target) is LocalRedirectResult r1&&r1.Url==target,"Sesión existente continúa al destino");
Check(Make(role:"USER").Register(target) is LocalRedirectResult r2&&r2.Url==target,"Sesión existente en registro continúa al destino");
foreach(var bad in new[]{"https://evil.example/","//evil.example/","/\\evil.example/"}){
    var c=Make();c.Login(bad);Check(c.ViewBag.ReturnUrl==null,"El formulario descarta destino externo: "+bad);
    Check(await c.Login(new(){Email="cliente@example.invalid",Password="test"},bad) is RedirectToActionResult,"El login no redirige fuera de la web");
}
var registered=Make();Check(await registered.Register(new(){Email="cliente@example.invalid",Password="test"},target) is LocalRedirectResult rr&&rr.Url==target,"Registro correcto conserva el enlace");
var fallback=Make("register-fallback");Check(await fallback.Register(new(){Email="cliente@example.invalid",Password="test"},target) is RedirectToActionResult fr&&fr.ActionName=="Login"&&(string?)fr.RouteValues?["returnUrl"]==target,"Registro sin login automático conserva el destino");
foreach(var mode in new[]{"ok","admin"}){
    var c=Make(mode);Check(await c.Login(new LoginDto(){Email="cliente@example.invalid",Password="test"}) is RedirectToActionResult dr&&dr.ControllerName==(mode=="admin"?"Admin":"Home"),"Sin enlace se conserva el destino habitual: "+mode);
}
var request=new DefaultHttpContext();request.Request.PathBase="/joyeria";request.Request.Path="/Pedidos/Detalle/42";request.Request.QueryString=new QueryString("?origen=correo");
AuthorizationFilterContext Filter(HttpContext ctx)=>new(new ActionContext(ctx,new RouteData(),new ActionDescriptor()),new List<IFilterMetadata>());
var filter=Filter(request);new JwtAuthorizeAttribute().OnAuthorization(filter);
Check(filter.Result is RedirectToActionResult ar&&(string?)ar.RouteValues?["returnUrl"]=="/joyeria/Pedidos/Detalle/42?origen=correo","Protección conserva subruta y consulta");
request.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"7"),new Claim(ClaimTypes.Role,"USER")},"test"));
var denied=Filter(request);new AdminOnlyAttribute().OnAuthorization(denied);Check(denied.Result is ForbidResult,"Un usuario sin permisos sigue sin entrar en administración");
Console.WriteLine($"{checks} comprobaciones superadas; sin bases de datos ni correos.");
public class Stub:DispatchProxy{
    public Func<string,object?[]?,object?> Handler=null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!.Name,args);
    public static T Make<T>(Func<string,object?[]?,object?> handler)where T:class{var proxy=Create<T,Stub>();((Stub)(object)proxy).Handler=handler;return proxy;}
}
public class MemoryTempData:ITempDataProvider{
    public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();
    public void SaveTempData(HttpContext context,IDictionary<string,object> values){}
}

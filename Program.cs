using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
//using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SystemAll.Components;
using SystemAll.Data;
using SystemAll.Services;
using static SystemAll.Services.RegisterService;

var builder = WebApplication.CreateBuilder(args);

// Razor Components (Blazor) + Razor Pages
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = true; // ✅ บังคับเปิด
    });
//options.DetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddRazorPages();

// MudBlazor
builder.Services.AddMudServices();

// Session Timeout (idle / absolute / keepalive) — ดู appsettings.json → "SessionTimeout"
builder.Services.Configure<SessionTimeoutOptions>(builder.Configuration.GetSection("SessionTimeout"));

// Cookie Authentication (สำหรับ Login/Logout)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        //options.LoginPath = "/ENG4_System/Login";
        //options.LogoutPath = "/ENG4_System/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "SystemAll.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;

        // ให้ SharedSessions เป็นตัวตัดสินจริง: ทุก request เช็ค + ต่อเวลา session
        // ถ้า session หมด/ถูก logout (เช่น login ซ้ำจากเครื่องอื่น) → เตะออกไป Login
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async ctx =>
            {
                var sid = ctx.Principal?.FindFirst("SharedSessionId")?.Value;
                var svc = ctx.HttpContext.RequestServices.GetRequiredService<SharedSessionService>();

                SessionStatus? status;
                try
                {
                    status = await svc.TouchAsync(sid);
                }
                catch (Exception ex)
                {
                    // DB มีปัญหาชั่วคราว: ไม่เตะผู้ใช้ออก
                    ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
                        .LogWarning(ex, "SharedSession touch failed");
                    return;
                }

                if (status is null)
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// EF Core - PMManualDB
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UserConnection")));

//builder.Services.AddScoped<AppDbContext>(sp =>
//sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

//builder.Services.AddDataProtection()
//    .PersistKeysToFileSystem(new DirectoryInfo(@"D:\DataProtectionKeys\SystemAll"))
//    .SetApplicationName("SystemAll");

// Session (สำหรับ Shared Session ระหว่าง ASP.NET Core และ Web Forms .NET 4.0)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".SystemAll.Session";
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Custom Services
builder.Services.AddScoped<SharedSessionService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<HrmsService>();
builder.Services.AddScoped<RegisterService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<EngTeamService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    //app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseExceptionHandler("/ENG4_System/Error", createScopeForErrors: true);
    app.UseHsts();
}

// ชั่วคราว
app.UseDeveloperExceptionPage();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Razor Pages (Login, Logout)
//app.MapGet("/", () => Results.Redirect("~/Login"));
//app.MapGet("/ENG4_System", () => Results.Redirect("~/Login"));
//app.MapGet("/ENG4_System/", () => Results.Redirect("~/Login"));

app.MapRazorPages();


// Blazor Components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ===== Session Keepalive API (เรียกจาก wwwroot/js/session-timeout.js) =====
//   POST = มีการใช้งาน → ต่อเวลา
//   GET  = เช็คอย่างเดียว (ถูก logout จากที่อื่นหรือยัง)
app.MapMethods("/api/session/keepalive", new[] { "GET", "POST" }, async (
    HttpContext ctx, SharedSessionService svc, ILogger<Program> log) =>
{
    ctx.Response.Headers.CacheControl = "no-store";

    var sid = ctx.User.FindFirst("SharedSessionId")?.Value
              ?? ctx.Request.Cookies[SharedSessionService.CookieName];
    try
    {
        var status = HttpMethods.IsPost(ctx.Request.Method)
            ? await svc.TouchAsync(sid)
            : await svc.CheckAsync(sid);

        return status is null
            ? Results.Json(new { ok = false }, statusCode: StatusCodes.Status401Unauthorized)
            : Results.Json(new { ok = true });
    }
    catch (Exception ex)
    {
        log.LogWarning(ex, "Session keepalive failed");
        return Results.Json(new { ok = true, warn = "db" });   // DB มีปัญหา ไม่เตะผู้ใช้ออก
    }
});

// ===== Forgot Password API =====
app.MapPost("/api/forgot/request", async (
    HttpContext ctx, PasswordResetService svc) =>
{
    var form = await ctx.Request.ReadFromJsonAsync<ForgotRequest>();
    if (form == null || string.IsNullOrWhiteSpace(form.UserName))
        return Results.Json(new { success = false, message = "กรุณากรอก Username" });

    var (success, maskedEmail, msg) = await svc.RequestOtpAsync(form.UserName.Trim());
    return Results.Json(new { success, maskedEmail, message = msg });
});

app.MapPost("/api/forgot/verify", async (
    HttpContext ctx, PasswordResetService svc) =>
{
    var form = await ctx.Request.ReadFromJsonAsync<VerifyRequest>();
    if (form == null)
        return Results.Json(new { success = false });

    var valid = await svc.VerifyOtpAsync(form.UserName.Trim(), form.Otp.Trim());
    return Results.Json(new { success = valid });
});

app.MapPost("/api/forgot/reset", async (
    HttpContext ctx, PasswordResetService svc) =>
{
    var form = await ctx.Request.ReadFromJsonAsync<ResetRequest>();
    if (form == null)
        return Results.Json(new { success = false, message = "ข้อมูลไม่ครบ" });

    var success = await svc.ResetPasswordAsync(
        form.UserName.Trim(), form.Otp.Trim(), form.NewPassword);
    return Results.Json(new
    {
        success,
        message = success ? "เปลี่ยนรหัสผ่านสำเร็จ" : "ไม่สามารถบันทึกได้ รหัสอาจหมดอายุ"
    });
});

app.MapPost("/api/reset/change", async (
    HttpContext ctx, PasswordResetService svc) =>
{
    var form = await ctx.Request.ReadFromJsonAsync<ChangeRequest>();
    if (form == null)
        return Results.Json(new { success = false, message = "ข้อมูลไม่ครบ" });

    var (success, msg) = await svc.ChangePasswordAsync(
        form.UserName.Trim(), form.CurrentPassword, form.NewPassword);

    return Results.Json(new
    {
        success,
        message = success ? "เปลี่ยนรหัสผ่านสำเร็จ" : msg
    });
});

// ===== Register API =====
app.MapPost("/api/register/lookup", async (
    HttpContext ctx, RegisterService svc) =>
{
    var req = await ctx.Request.ReadFromJsonAsync<LookupRequest>();
    if (req == null || string.IsNullOrWhiteSpace(req.EmpId))
        return Results.Json(new RegisterLookupResult { Success = false, Message = "กรุณากรอกรหัสพนักงาน" });

    var result = await svc.LookupAsync(req.EmpId.Trim());
    return Results.Json(result);
});

app.MapPost("/api/register/submit", async (
    HttpContext ctx, RegisterService svc) =>
{
    var req = await ctx.Request.ReadFromJsonAsync<RegisterSubmitDto>();
    if (req == null || string.IsNullOrWhiteSpace(req.EmpId))
        return Results.Json(new RegisterResult { Success = false, Message = "ข้อมูลไม่ครบ" });

    var result = await svc.RegisterAsync(req);
    return Results.Json(result);
});

// ===== Request Access (user ที่ login แล้ว ขอเมนูเพิ่ม) =====
app.MapPost("/api/request/menus", async (
    HttpContext ctx, RegisterService svc) =>
{
    var req = await ctx.Request.ReadFromJsonAsync<RequestMenuDto>();
    if (req == null || string.IsNullOrWhiteSpace(req.UserName))
        return Results.Json(new RegisterResult { Success = false, Message = "ข้อมูลไม่ครบ" });

    var result = await svc.RequestMenusAsync(req.UserName.Trim(), req.SelectedColumns ?? new());
    return Results.Json(result);
});

app.MapGet("/api/request/menus/{userName}", async (
    string userName, RegisterService svc) =>
{
    var data = await svc.GetAccessMenusAsync(userName.Trim());
    return Results.Json(data);
});

app.Run();


// DTO records
record ForgotRequest(string UserName);
record VerifyRequest(string UserName, string Otp);
record ResetRequest(string UserName, string Otp, string NewPassword);
record ChangeRequest(string UserName, string CurrentPassword, string NewPassword);
record LookupRequest(string EmpId);
record RequestMenuDto(string UserName, List<string>? SelectedColumns);

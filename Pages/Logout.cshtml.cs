using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemAll.Services;

namespace SystemAll.Pages;

public class LogoutModel : PageModel
{
    private readonly SharedSessionService _sharedSession;

    public LogoutModel(SharedSessionService sharedSession)
    {
        _sharedSession = sharedSession;
    }

    public async Task<IActionResult> OnGetAsync([FromQuery] string? reason = null)
    {
        var sharedSessionId = HttpContext.Session.GetString("SharedSessionId")
                           ?? Request.Cookies["SHARED_SID"];

        if (!string.IsNullOrEmpty(sharedSessionId))
            await _sharedSession.InvalidateSessionAsync(sharedSessionId);

        // ลบทุก cookie ที่มีอยู่ — ครอบคลุมทุก path
        var cookieOptions = new CookieOptions
        {
            Path = "/",
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(-1)  // force expire
        };       

        Response.Cookies.Delete("SHARED_SID", cookieOptions);
        Response.Cookies.Delete("SystemAll.Auth", new CookieOptions { Path = "/" });
        Response.Cookies.Delete(".SystemAll.Session", new CookieOptions { Path = "/" });

        // ลบทุก cookie ใน request เพื่อให้แน่ใจ
        foreach (var cookie in Request.Cookies.Keys)
        {
            Response.Cookies.Delete(cookie, new CookieOptions { Path = "/" });
        }

        HttpContext.Session.Clear();

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var loginPath = env.IsProduction()
            ? "/ENG4_System/Login"
            : "/Login";

        // timeout จาก session-timeout.js → ส่งต่อไปแสดงข้อความที่หน้า Login
        if (string.Equals(reason, "timeout", StringComparison.OrdinalIgnoreCase))
            loginPath += "?reason=timeout";

        //Redirect ผ่าน LogoutBridge ของระบบเก่าก่อน แล้วค่อยไป Login
        var logoutBridgeUrl = env.IsProduction()
            ? $"http://cptsvs552.canon-pt.co.th/user_master/LogoutBridge.aspx" +
              $"?returnUrl={Uri.EscapeDataString("http://cptsvs552.canon-pt.co.th" + loginPath)}"
            : loginPath;
        return Redirect(logoutBridgeUrl);

        //return Redirect(loginPath);
    }
}
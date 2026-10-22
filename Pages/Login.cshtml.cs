using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using SystemAll.Data;
using SystemAll.Models;
using SystemAll.Services;

namespace SystemAll.Pages;

public class LoginModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly SharedSessionService _sharedSession;

    public LoginModel(AppDbContext db, SharedSessionService sharedSession)
    {
        _db = db;
        _sharedSession = sharedSession;
    }

    [BindProperty] public string UserId { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string ErrorMessage { get; set; } = "";

    public IActionResult OnGet([FromQuery] string? reason = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("~/");

        if (string.Equals(reason, "timeout", StringComparison.OrdinalIgnoreCase))
            ErrorMessage = "Session หมดอายุเนื่องจากไม่มีการใช้งาน กรุณาเข้าสู่ระบบใหม่";

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "กรุณากรอก Username และ Password";
            return Page();
        }

        var loginInput = UserId.Trim();

        // 1) ค้นหา User
        var user = await _db.SysUserMasters
            .FirstOrDefaultAsync(u => u.UserName == loginInput);

        if (user == null)
        {
            ErrorMessage = "ไม่พบ Username นี้ในระบบ";
            return Page();
        }

        if (user.IsActive != 1)
        {
            ErrorMessage = "บัญชีผู้ใช้นี้ถูกระงับการใช้งาน กรุณาติดต่อผู้ดูแลระบบ";
            return Page();
        }

        if (!VerifyPassword(Password, user.UserPass))
        {
            ErrorMessage = "Password ไม่ถูกต้อง";
            return Page();
        }

        // บัญชีสำหรับจอ Monitor : session อายุยาว (ไม่ timeout)
        // บัญชีปกติ : sliding ตาม SessionTimeout (idle 60 นาที / สูงสุด 12 ชม.) — SharedSessions เป็นตัวตัดสิน
        var timeout = _sharedSession.Options;
        var isMonitorAccount = timeout.IsMonitorAccount(user.UserName);

        TimeSpan? fixedLifetime = isMonitorAccount ? TimeSpan.FromDays(timeout.MonitorLifetimeDays) : null;
        var authExpiresAt = DateTimeOffset.UtcNow.Add(fixedLifetime ?? TimeSpan.FromHours(timeout.AbsoluteHours));

        // 2) สร้าง FullName / ShortName จาก fname + lname
        var fname = user.FName?.Trim() ?? "";
        var lname = user.LName?.Trim() ?? "";
        var fullName = $"{fname} {lname}".Trim();
        if (string.IsNullOrEmpty(fullName)) fullName = user.UserName;

        // ShortName = fname + ตัวแรกของ lname + "."
        var shortName = string.IsNullOrEmpty(lname)
            ? fname
            : $"{fname} {lname[..1]}.";

        // 3) ดึง Department จาก View_Dept_List_Master
        string deptCode = "", deptName = "", deptNameFull = "";
        try
        {
            // div_id = 1 -> compare user_dept, div_id = 2 -> compare oth_dept_id
            var deptQuery = _db.ViewDeptListMasters.AsQueryable();

            ViewDeptListMaster? dept = null;
            if (user.DivId == 1 && user.UserDept.HasValue)
                dept = await deptQuery.FirstOrDefaultAsync(d => d.UserDept == user.UserDept);
            else if (user.DivId == 2 && user.UserDept.HasValue)
                dept = await deptQuery.FirstOrDefaultAsync(d => d.OthDeptId == user.UserDept);

            if (dept != null)
            {
                deptCode = dept.DeptCode;
                deptName = dept.DeptName ?? "";
                deptNameFull = dept.DeptNameFull ?? "";
            }
        }
        catch { /* ถ้า View ยังไม่มีให้ข้ามไป */ }

        // 4) ดึง Group Name จาก eng_group_master
        string groupName = "";
        try
        {
            if (user.EngGroupId.HasValue && user.UserDept.HasValue)
            {
                var group = await _db.EngGroupMasters.FirstOrDefaultAsync(g =>
                    g.GroupId == user.EngGroupId &&
                    g.Dept == user.UserDept &&
                    g.Active);
                groupName = group?.GroupName ?? "";
            }
        }
        catch { }

        // 5) ดึง Position จาก Position_master
        string positionName = "", positionCode = "";
        try
        {
            if (user.PositionId.HasValue)
            {
                var pos = await _db.PositionMasters
                    .FirstOrDefaultAsync(p => p.PositionId == user.PositionId);
                positionName = pos?.PositionName ?? "";
                positionCode = pos?.PositionCode ?? "";
            }
        }
        catch { }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        // 6) สร้าง Shared Session พร้อมข้อมูลครบ
        var dto = new SessionCreateDto
        {
            UserId = user.UserName,
            UserName = user.UserName,
            FullName = fullName,
            ShortName = shortName,
            EmpId = user.EmpId,
            Email = user.Email,
            Ext = user.Ext,
            Department = deptName,
            DeptCode = deptCode,
            DeptNameFull = deptNameFull,
            GroupName = groupName,
            PositionName = positionName,
            PositionCode = positionCode,
            PermJigTool = user.JigTool==1,
            PermSparePart = user.SparePart == 1,
            PermPmTpm = user.PmTpm == 1,
            PermPm = user.Pm == 1,
            PermPmProblem = user.PmProblem == 1,
            PermBusRequest = user.BusRequest == 1,
            PermDashboard = user.Dashboard == 1,
            PermPersonal = user.Personal == 1,
            PermToolEquipment = user.ToolEquipment == 1,
            PermWms = user.Wms == 1,
            PermApproval = user.Approval == 1,
            PermPmChat = user.PmChat == 1,
            PermBugReport = user.BugReport == 1,
            PermManual = user.Manual == 1,
            PermDevelopSystem = user.DevelopSystem == 1,
            Admin = user.Admin == 1,
            IpAddress = ipAddress,
            Lifetime = fixedLifetime            // null = sliding
        };

        var sharedSessionId = await _sharedSession.CreateSessionAsync(dto);

        // 7) Set SHARED_SID cookie (สำหรับ Web Forms .NET 4.0)
        //    ปกติเป็น session cookie — อายุจริงอยู่ที่ SharedSessions.expires_at
        SharedSessionService.AppendSidCookie(Response, sharedSessionId,
            isMonitorAccount ? authExpiresAt : null);

        // 8) ASP.NET Core Session
        HttpContext.Session.SetString("UserId", user.UserName);
        HttpContext.Session.SetString("UserName", user.UserName);
        HttpContext.Session.SetString("FullName", fullName);
        HttpContext.Session.SetString("ShortName", shortName);
        HttpContext.Session.SetString("EmpId", user.EmpId ?? "");
        HttpContext.Session.SetString("Email", user.Email ?? "");
        HttpContext.Session.SetString("Ext", user.Ext ?? "");
        HttpContext.Session.SetString("Department", deptName);
        HttpContext.Session.SetString("DeptCode", deptCode);
        HttpContext.Session.SetString("DeptNameFull", deptNameFull);
        HttpContext.Session.SetString("GroupName", groupName);
        HttpContext.Session.SetString("PositionName", positionName);
        HttpContext.Session.SetString("PositionCode", positionCode);
        HttpContext.Session.SetString("SharedSessionId", sharedSessionId);

        // 9) Cookie Auth Claims สำหรับ Blazor
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserName),
            new(ClaimTypes.Name,           user.UserName),
            new("FullName",        fullName),
            new("ShortName",       shortName),
            new("EmpId",           user.EmpId  ?? ""),
            new("Email",           user.Email  ?? ""),
            new("Ext",             user.Ext    ?? ""),
            new("Department",      deptName),
            new("DeptCode",        deptCode),
            new("DeptNameFull",    deptNameFull),
            new("GroupName",       groupName),
            new("PositionName",    positionName),
            new("PositionCode",    positionCode),
            new("SharedSessionId", sharedSessionId),
            // สิทธิ์ระบบ (เก็บเป็น "true"/"false")
            new("Perm_Admin", (user.Admin == 1).ToString()),
            new("Perm_Dashboard", (user.Dashboard == 1).ToString()),
            new("Perm_Wms", (user.Wms == 1).ToString()),
            new("Perm_JMS",       (user.JigTool == 1).ToString()),
            new("Perm_SparePart", (user.SparePart == 1).ToString()),
            new("Perm_Approval", (user.Approval == 1).ToString()),
            new("Perm_PMS",       (user.Pm == 1).ToString()),
            new("Perm_PMProblem", (user.PmProblem == 1).ToString()),
            new("Perm_PMManual",  (user.PmTpm == 1).ToString()),
            new("Perm_Equipment", (user.ToolEquipment == 1).ToString()),
            new("Perm_BUS",       (user.BusRequest == 1).ToString()),
            new("Perm_PmChat",       (user.PmChat == 1).ToString()),
            new("Perm_BugReport",       (user.BugReport == 1).ToString()),
            new("Perm_Manual",       (user.Manual == 1).ToString()),
            new("Perm_DevelopSystem",       (user.DevelopSystem == 1).ToString()),
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties
            {
                IsPersistent = isMonitorAccount,   // ← Admin: อยู่รอดแม้ปิด browser
                ExpiresUtc = authExpiresAt,     // เพดาน; ตัดสินจริงด้วย OnValidatePrincipal → SharedSessions
                AllowRefresh = true
            });

        return Redirect("~/");
    }

    private static bool VerifyPassword(string input, string stored)
    {
        if (input == stored) return true;
        using var md5 = MD5.Create();
        var hash = Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
        return string.Equals(hash, stored, StringComparison.OrdinalIgnoreCase);
    }
}
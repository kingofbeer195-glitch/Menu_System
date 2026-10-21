using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using SystemAll.Services;

namespace SystemAll.Pages;

[Authorize]
public class SSOBridgeModel(SharedSessionService sharedSession,
                            IHttpContextAccessor httpCtx) : PageModel
{
    public string TargetUrl { get; private set; } = "/";

    public async Task<IActionResult> OnGetAsync([FromQuery] string? returnUrl = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var ip = httpCtx.HttpContext?.Connection.RemoteIpAddress?.ToString();

        // เช็คว่ามี SHARED_SID cookie อยู่แล้วไหม และยัง valid อยู่ไหม
        var existingSid = Request.Cookies["SHARED_SID"];
        string sid = "";
        var empId = User.FindFirst("EmpId")?.Value ?? "";

        if (!string.IsNullOrEmpty(existingSid))
        {
            // ตรวจสอบว่า session ยังใช้งานได้
            var existing = await sharedSession.ValidateSessionAsync(existingSid);
            if (existing != null && existing.UserId == userId)
            {
                // ใช้ session เดิม ไม่ต้องสร้างใหม่
                sid = existingSid;
            }
        }

        // ถ้าไม่มี session ที่ valid → สร้างใหม่
        if (string.IsNullOrEmpty(sid))
        {
            var fullName = User.FindFirst("FullName")?.Value ?? userId;
            var shortName = User.FindFirst("ShortName")?.Value ?? fullName;
            //var empId = User.FindFirst("EmpId")?.Value;
            //var empId = User.FindFirst("EmpId")?.Value ?? "";
            var email = User.FindFirst("Email")?.Value;
            var ext = User.FindFirst("Ext")?.Value;
            var department = User.FindFirst("Department")?.Value ?? "";
            var deptCode = User.FindFirst("DeptCode")?.Value;
            var deptNameFull = User.FindFirst("DeptNameFull")?.Value;
            var groupName = User.FindFirst("GroupName")?.Value;
            var positionName = User.FindFirst("PositionName")?.Value;
            var positionCode = User.FindFirst("PositionCode")?.Value;

            static bool GetPerm(ClaimsPrincipal u, string claim)
                => u.FindFirst(claim)?.Value == "True";

            var dto = new SessionCreateDto
            {
                UserId = userId,
                UserName = userId,
                FullName = fullName,
                ShortName = shortName,
                EmpId = empId,
                Email = email,
                Ext = ext,
                Department = department,
                DeptCode = deptCode,
                DeptNameFull = deptNameFull,
                GroupName = groupName,
                PositionName = positionName,
                PositionCode = positionCode,
                PermJigTool = GetPerm(User, "Perm_JMS"),
                PermSparePart = GetPerm(User, "Perm_SparePart"),
                PermPmTpm = GetPerm(User, "Perm_PMManual"),
                PermPm = GetPerm(User, "Perm_PMS"),
                PermPmProblem = GetPerm(User, "Perm_PMProblem"),
                PermBusRequest = GetPerm(User, "Perm_BUS"),
                PermDashboard = GetPerm(User, "Perm_Dashboard"),
                PermPersonal = GetPerm(User, "Perm_WMS"),
                PermToolEquipment = GetPerm(User, "Perm_Equipment"),
                PermWms = GetPerm(User, "Perm_Wms"),
                PermApproval = GetPerm(User, "Perm_Approval"),
                PermPmChat = GetPerm(User, "Perm_PmChat"),
                PermBugReport = GetPerm(User, "Perm_BugReport"),
                PermManual = GetPerm(User, "Perm_Manual"),
                PermDevelopSystem = GetPerm(User, "Perm_DevelopSystem"),
                IpAddress = ip
            };

            sid = await sharedSession.CreateSessionAsync(dto);
        }

        // Set/refresh cookie
        Response.Cookies.Append("SHARED_SID", sid, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.Now.AddHours(2),
            IsEssential = true
        });

        // ใน OnGetAsync — แก้ TargetUrl        
        var baseTarget = string.IsNullOrWhiteSpace(returnUrl)
            ? "http://cptsvs552.canon-pt.co.th"
            : returnUrl;

        // ถ้า returnUrl เป็น Dashboard → เปลี่ยนไปผ่าน Default.aspx แทน
        if (baseTarget.Contains("Dashboard_system"))
        {
            TargetUrl = $"http://cptsvs552.canon-pt.co.th/Dashboard_system/Default.aspx?emp_id={empId}";
        }
        else
        {
            TargetUrl = baseTarget;
        }

        //TargetUrl = string.IsNullOrWhiteSpace(returnUrl)
        //    ? "http://cptsvs552.canon-pt.co.th"
        //    : returnUrl;
        TargetUrl = string.IsNullOrWhiteSpace(returnUrl)
            ? "http://cptsvs552.canon-pt.co.th"
            : Uri.UnescapeDataString(returnUrl);  // ← decode ก่อน

        return Page();
    }
}
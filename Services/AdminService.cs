using Microsoft.EntityFrameworkCore;
using SystemAll.Data;
using SystemAll.Models;

namespace SystemAll.Services;

public class AdminService
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;
    private readonly HrmsService _hrms;

    private const int PENDING = 50;
    private const int APPROVED = 1;

    public AdminService(AppDbContext db, EmailService email, HrmsService hrms)
    {
        _db = db;
        _email = email;
        _hrms = hrms;
    }

    // ============================================================
    //  NEW USER — ดึงคำขอที่รออนุมัติ
    // ============================================================
    public async Task<List<PendingRequestDto>> GetPendingAsync()
    {
        var users = await _db.SysUserMasters
            .Where(u => u.IsActive == PENDING
                     || u.JigTool == PENDING || u.PmTpm == PENDING || u.SparePart == PENDING
                     || u.Pm == PENDING || u.PmProblem == PENDING || u.Dashboard == PENDING
                     || u.BusRequest == PENDING || u.Wms == PENDING || u.Approval == PENDING
                     || u.BugReport == PENDING || u.Manual == PENDING)
            .ToListAsync();

        var result = new List<PendingRequestDto>();
        foreach (var u in users)
        {
            var isNewUser = u.IsActive == PENDING;

            result.Add(new PendingRequestDto
            {
                UserName = u.UserName,
                FullName = $"{u.FName} {u.LName}".Trim(),
                EmpId = u.EmpId ?? "",
                Email = u.Email ?? "",
                Ext = u.Ext ?? "",
                IsNewUser = isNewUser,
                RequestType = isNewUser ? "New User" : "New Menu",
                PendingMenus = GetPendingMenuNames(u)
            });
        }
        return result.OrderByDescending(r => r.IsNewUser).ToList();
    }

    private List<string> GetPendingMenuNames(SysUserMaster u)
    {
        var map = new Dictionary<string, int?>
        {
            ["jigTool"] = u.JigTool,
            ["pm_tpm"] = u.PmTpm,
            ["spare_part"] = u.SparePart,
            ["pm"] = u.Pm,
            ["pm_problem"] = u.PmProblem,
            ["dashboard"] = u.Dashboard,
            ["bus_requst"] = u.BusRequest,
            ["wms"] = u.Wms,
            ["approval"] = u.Approval,
            ["bug_report"] = u.BugReport,
            ["manual"] = u.Manual
        };

        return map.Where(kv => kv.Value == PENDING).Select(kv => kv.Key).ToList();
    }

    // ============================================================
    //  APPROVE
    // ============================================================
    public async Task<bool> ApproveAsync(string userName)
    {
        var u = await _db.SysUserMasters.FirstOrDefaultAsync(x => x.UserName == userName);
        if (u == null) return false;

        var now = DateTime.Now;
        var wasNewUser = u.IsActive == PENDING;
        var approvedMenus = GetPendingMenuNames(u);   // เก็บก่อนเปลี่ยนค่า

        if (u.IsActive == PENDING)
        {
            u.IsActive = APPROVED;
            u.RegistDate = now;
        }

        if (u.JigTool == PENDING) u.JigTool = APPROVED;
        if (u.PmTpm == PENDING) u.PmTpm = APPROVED;
        if (u.SparePart == PENDING) u.SparePart = APPROVED;
        if (u.Pm == PENDING) u.Pm = APPROVED;
        if (u.PmProblem == PENDING) u.PmProblem = APPROVED;
        if (u.Dashboard == PENDING) u.Dashboard = APPROVED;
        if (u.BusRequest == PENDING) u.BusRequest = APPROVED;
        if (u.Wms == PENDING) u.Wms = APPROVED;
        if (u.Approval == PENDING) u.Approval = APPROVED;
        if (u.BugReport == PENDING) u.BugReport = APPROVED;
        if (u.Manual == PENDING) u.Manual = APPROVED;

        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(u.Email))
        {
            var fullName = $"{u.FName} {u.LName}".Trim();
            try
            {
                await SendApprovedMailAsync(u.Email, fullName, wasNewUser, approvedMenus);
            }
            catch { /* ไม่ให้เมลล้มทำให้ approve fail */ }
        }

        return true;
    }

    private async Task SendApprovedMailAsync(
        string toEmail, string fullName, bool wasNewUser, List<string> menus)
    {
        var typeText = wasNewUser ? "บัญชีผู้ใช้งานใหม่" : "สิทธิ์เข้าถึงเมนูเพิ่มเติม";
        var menuList = menus.Count > 0
            ? string.Join("", menus.Select(m => $"<li>{m}</li>"))
            : "<li>-</li>";

        var subject = "[ENG4 System] คำขอของคุณได้รับการอนุมัติแล้ว";
        var body = $@"
            <div style='font-family:Segoe UI,sans-serif; max-width:480px; margin:auto;
                        border:1px solid #e0e0e0; border-radius:12px; overflow:hidden;'>
              <div style='background:#43a047; color:#fff; padding:20px; text-align:center;'>
                <h2 style='margin:0;'>✓ อนุมัติสำเร็จ</h2>
                <p style='margin:4px 0 0; font-size:13px;'>ENG4 System</p>
              </div>
              <div style='padding:24px; color:#333;'>
                <p>เรียน คุณ {fullName},</p>
                <p>{typeText} ของคุณได้รับการอนุมัติจากผู้ดูแลระบบเรียบร้อยแล้ว</p>
                <p style='margin-top:12px;'><b>เมนูที่เข้าถึงได้:</b></p>
                <ul>{menuList}</ul>
                <p style='margin-top:16px; color:#1565C0;'>คุณสามารถเข้าสู่ระบบเพื่อใช้งานได้ทันที</p>
              </div>
              <div style='background:#f5f5f5; padding:12px; text-align:center; font-size:12px; color:#999;'>
                ENG4 Engineering Department · ต่อ 5504
              </div>
            </div>";

        await _email.SendAsync(toEmail, subject, body);
    }

    // ============================================================
    //  UPDATE USER — sync sys_user_master กับ HRMS
    // ============================================================
    public async Task<List<UserSyncDto>> GetUsersToSyncAsync()
    {
        var users = await _db.SysUserMasters
            .Where(u => u.IsActive == APPROVED && u.EmpId != null && u.EmpId != "")
            .ToListAsync();

        var positions = await _db.PositionMasters.ToListAsync();
        var result = new List<UserSyncDto>();

        foreach (var u in users)
        {
            var hr = await _hrms.GetEmployeeAsync(u.EmpId!);
            if (hr == null) continue;

            int? newPosId = positions
                .FirstOrDefault(p => (p.PositionName ?? "").Trim()
                    .Equals(hr.PosnEname.Trim(), StringComparison.OrdinalIgnoreCase))?.PositionId;

            var diffs = new List<string>();
            if ((u.FName ?? "").Trim() != hr.GnameEng.Trim()) diffs.Add("fname");
            if ((u.LName ?? "").Trim() != hr.FnameEng.Trim()) diffs.Add("lname");
            if ((u.Email ?? "").Trim() != hr.Email.Trim()) diffs.Add("email");
            if ((u.DeptCode ?? "").Trim() != hr.DeptCode.Trim()) diffs.Add("dept_code");
            if (newPosId != null && u.PositionId != newPosId) diffs.Add("position_id");

            if (diffs.Count == 0) continue;   // แสดงเฉพาะที่ไม่ตรง

            result.Add(new UserSyncDto
            {
                UserName = u.UserName,
                EmpId = u.EmpId!,
                OldFName = u.FName ?? "",
                NewFName = hr.GnameEng,
                OldLName = u.LName ?? "",
                NewLName = hr.FnameEng,
                OldEmail = u.Email ?? "",
                NewEmail = hr.Email,
                OldDeptCode = u.DeptCode ?? "",
                NewDeptCode = hr.DeptCode,
                OldPositionId = u.PositionId,
                NewPositionId = newPosId,
                NewPosnName = hr.PosnEname,
                DiffFields = diffs
            });
        }
        return result;
    }

    public async Task<bool> SyncUserAsync(string userName)
    {
        var u = await _db.SysUserMasters.FirstOrDefaultAsync(x => x.UserName == userName);
        if (u == null || string.IsNullOrWhiteSpace(u.EmpId)) return false;

        var hr = await _hrms.GetEmployeeAsync(u.EmpId);
        if (hr == null) return false;

        u.FName = hr.GnameEng;
        u.LName = hr.FnameEng;
        u.Email = hr.Email;
        u.DeptCode = hr.DeptCode;

        var pos = await _db.PositionMasters
            .FirstOrDefaultAsync(p => p.PositionName != null
                && p.PositionName.Trim().ToUpper() == hr.PosnEname.Trim().ToUpper());
        if (pos != null) u.PositionId = pos.PositionId;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> SyncUsersAsync(IEnumerable<string> userNames)
    {
        var count = 0;
        foreach (var name in userNames)
        {
            if (await SyncUserAsync(name)) count++;
        }
        return count;
    }
}

// ============================================================
//  DTOs
// ============================================================
public class PendingRequestDto
{
    public string UserName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string EmpId { get; set; } = "";
    public string Email { get; set; } = "";
    public string Ext { get; set; } = "";
    public bool IsNewUser { get; set; }
    public string RequestType { get; set; } = "";
    public List<string> PendingMenus { get; set; } = new();
}

public class UserSyncDto
{
    public string UserName { get; set; } = "";
    public string EmpId { get; set; } = "";
    public string OldFName { get; set; } = ""; public string NewFName { get; set; } = "";
    public string OldLName { get; set; } = ""; public string NewLName { get; set; } = "";
    public string OldEmail { get; set; } = ""; public string NewEmail { get; set; } = "";
    public string OldDeptCode { get; set; } = ""; public string NewDeptCode { get; set; } = "";
    public int? OldPositionId { get; set; }
    public int? NewPositionId { get; set; }
    public string NewPosnName { get; set; } = "";
    public List<string> DiffFields { get; set; } = new();
}
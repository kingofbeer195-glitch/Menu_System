using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using SystemAll.Data;
using SystemAll.Models;

namespace SystemAll.Services;

public class RegisterService
{
    private readonly AppDbContext _db;
    private readonly HrmsService _hrms;
    private readonly EmailService _email;   // ✅ inject เพิ่ม

    private const int PENDING = 50;         // รออนุมัติ
    private const string ADMIN_EMAIL = "anirut@mail.canon";

    public RegisterService(AppDbContext db, HrmsService hrms , EmailService email)
    {
        _db = db;
        _hrms = hrms;
        _email = email;
    }

    // ---- STEP 1: lookup HRMS + เช็คว่ามี user แล้วหรือยัง ----
    public async Task<RegisterLookupResult> LookupAsync(string empId)
    {
        var emp = await _hrms.GetEmployeeAsync(empId);
        if (emp == null)
            return new RegisterLookupResult { Success = false, Message = "ไม่พบข้อมูลพนักงานรหัสนี้" };

        // เช็คใน sys_user_master ด้วย emp_id
        var existing = await _db.SysUserMasters
            .FirstOrDefaultAsync(u => u.EmpId == emp.EmpNo);

        // User Name: ENG → GNAME_ENG, else → EmpNo
        var userName = existing?.UserName
            ?? (emp.IsEng ? emp.GnameEng.Trim() : emp.EmpNo);

        // เมนูที่ใช้งานได้ (filter ENG4_Only ถ้าไม่ใช่ ENG4)
        var menus = await GetMenusAsync(emp.IsEng4);

        // สิทธิ์ที่ user มีอยู่แล้ว (เพื่อ pre-check checkbox + กัน update ซ้ำ)
        var currentPerms = existing != null
            ? GetCurrentPermissions(existing, menus)
            : new Dictionary<string, bool>();

        return new RegisterLookupResult
        {
            Success = true,
            IsNewUser = existing == null,
            EmpId = emp.EmpNo,
            UserName = userName,
            FullName = emp.FullName,
            Division = $"{emp.DivName} ({emp.DivAbb})",
            Department = $"{emp.DeptAbb} ({emp.DeptCode})",
            Position = $"{emp.PosnEname} ({emp.Band})",
            Email = emp.Email,
            IsEng4 = emp.IsEng4,
            Menus = menus.Select(m => new MenuItemDto
            {
                Column = m.SysUserMasterColumn ?? "",
                Name = m.SystemName ?? "",
                Detail = m.SystemDetail ?? "",
                Eng4Only = m.Eng4Only == true,
                AlreadyGranted = currentPerms.TryGetValue(m.SysUserMasterColumn ?? "", out var g) && g
            }).ToList()
        };
    }

    private async Task<List<SystemListAll>> GetMenusAsync(bool isEng4)
    {
        var query = _db.SystemListAlls.Where(s => s.StatusUse == true);
        if (!isEng4)
            query = query.Where(s => s.Eng4Only != true);  // ซ่อนเมนู ENG4-only
        return await query.OrderBy(s => s.Id).ToListAsync();
    }

    // อ่านค่าสิทธิ์ปัจจุบันแบบ dynamic ตามชื่อ column
    private static Dictionary<string, bool> GetCurrentPermissions(
    SysUserMaster user, List<SystemListAll> menus)
    {
        var result = new Dictionary<string, bool>();
        var type = typeof(SysUserMaster);

        foreach (var m in menus)
        {
            var col = m.SysUserMasterColumn;
            if (string.IsNullOrWhiteSpace(col)) continue;

            var prop = FindPropertyByColumn(type, col);
            if (prop == null) continue;

            var val = prop.GetValue(user);
            // มีสิทธิ์แล้ว หรือ กำลังรออนุมัติ (50) = ถือว่า "มีแล้ว" ไม่ต้องขอซ้ำ
            result[col] = val is int i && i != 0;
        }
        return result;
    }

    // ---- STEP 2: register / update ----
    public async Task<RegisterResult> RegisterAsync(RegisterSubmitDto dto)
    {
        var emp = await _hrms.GetEmployeeAsync(dto.EmpId);
        if (emp == null)
            return new RegisterResult { Success = false, Message = "ไม่พบข้อมูลพนักงาน" };

        var menus = await GetMenusAsync(emp.IsEng4);
        var validColumns = menus
            .Select(m => m.SysUserMasterColumn)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requested = dto.SelectedColumns
            .Where(c => validColumns.Contains(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existing = await _db.SysUserMasters
            .FirstOrDefaultAsync(u => u.EmpId == emp.EmpNo);

        //int? userDept = int.TryParse(emp.DeptCode?.Trim(), out var dc) ? dc : null;

        string userName;
        List<string> grantedMenuNames;

        if (existing == null)
        {
            var pwErr = ValidatePassword(dto.Password);
            if (pwErr != null)
                return new RegisterResult { Success = false, Message = pwErr };

            userName = emp.IsEng ? emp.GnameEng.Trim() : emp.EmpNo;

            // 4.1 resolve position_id จาก POSN_ENAME + BAND
            int? positionId = null;
            var pos = await _db.PositionMasters.FirstOrDefaultAsync(p =>
                p.PositionName == emp.PosnEname && p.PositionCode == emp.Band);
            positionId = pos?.PositionId;

            // 4.2 resolve user_dept (dept_id) จาก DEPT_CODE
            int? userDeptId = null;
            var deptRow = await _db.DeptMasters.FirstOrDefaultAsync(d => d.DeptCode == emp.DeptCode);
            userDeptId = deptRow?.DeptId;

            var newUser = new SysUserMaster
            {
                UserName = userName,
                UserPass = dto.Password,          // ✅ ข้อ 3: เก็บ plain-text ตามที่ user กรอก
                EmpId = emp.EmpNo,
                FName = emp.GnameEng.Trim(),
                LName = emp.FnameEng.Trim(),
                Email = emp.Email,
                Ext = string.IsNullOrWhiteSpace(dto.Tel) ? null : dto.Tel.Trim(),
                DivId = emp.IsEng ? 1 : 2,
                UserDept = userDeptId,            // ✅ 4.2: dept_id
                DeptCode = emp.DeptCode,
                PositionId = positionId,          // ✅ 4.1: position_id
                IsActive = PENDING                // status = 50 รออนุมัติ
            };

            ApplyPermissions(newUser, requested, PENDING);
            _db.SysUserMasters.Add(newUser);
            await _db.SaveChangesAsync();

            grantedMenuNames = menus
                .Where(m => requested.Contains(m.SysUserMasterColumn ?? ""))
                .Select(m => m.SystemName ?? m.SysUserMasterColumn ?? "")
                .ToList();

            await SendAdminNotifyAsync(emp, userName, grantedMenuNames, isNewUser: true);

            return new RegisterResult
            {
                Success = true,
                Message = "ลงทะเบียนสำเร็จ! กรุณารอผู้ดูแลระบบอนุมัติก่อนเข้าใช้งาน",
                UserName = userName
            };
        }
        else
        {
            // ===== 7.2 UPDATE EXISTING → เมนูใหม่ = 50 =====
            userName = existing.UserName;
            var current = GetCurrentPermissions(existing, menus);

            var toAdd = requested.Where(c =>
                !(current.TryGetValue(c, out var g) && g)).ToHashSet();

            if (toAdd.Count == 0)
                return new RegisterResult
                {
                    Success = false,
                    AlreadyComplete = true,
                    Message = "คุณมีสิทธิ์เข้าถึงเมนูที่เลือกครบอยู่แล้ว (หรือกำลังรออนุมัติ)"
                };

            ApplyPermissions(existing, toAdd, PENDING);   // ✅ เมนูใหม่ = 50
            await _db.SaveChangesAsync();

            grantedMenuNames = menus
                .Where(m => toAdd.Contains(m.SysUserMasterColumn ?? ""))
                .Select(m => m.SystemName ?? m.SysUserMasterColumn ?? "")
                .ToList();

            await SendAdminNotifyAsync(emp, userName, grantedMenuNames, isNewUser: false);

            return new RegisterResult
            {
                Success = true,
                Message = $"ส่งคำขอเพิ่มสิทธิ์ {toAdd.Count} เมนูแล้ว กรุณารออนุมัติ",
                UserName = userName
            };
        }
    }

    // ===== ส่งอีเมลแจ้ง Admin =====
    private async Task SendAdminNotifyAsync(
    HrmsEmployee emp, string userName, List<string> menuNames, bool isNewUser)
    {
        var typeText = isNewUser ? "ผู้ใช้งานใหม่" : "ขอเพิ่มสิทธิ์เมนู";
        var menuList = menuNames.Count > 0
            ? string.Join("", menuNames.Select(n => $"<li>{n}</li>"))
            : "<li>-</li>";

        var subject = $"[ENG4 System] คำขอ{typeText} รออนุมัติ — {userName}";
        var bodyHtml = $@"
                <div style='font-family:Segoe UI,Tahoma,sans-serif; max-width:480px; margin:auto;
                            border:1px solid #e0e0e0; border-radius:12px; overflow:hidden;'>
                  <div style='background:#1a2a6c; color:#fff; padding:20px; text-align:center;'>
                    <h2 style='margin:0;'>ENG4 System</h2>
                    <p style='margin:4px 0 0; font-size:13px;'>แจ้งเตือนคำขอรออนุมัติ</p>
                  </div>
                  <div style='padding:24px; color:#333;'>
                    <h3 style='color:#1a2a6c; margin-top:0;'>มีคำขอ{typeText} รอการอนุมัติ</h3>
                    <table style='border-collapse:collapse; font-size:14px;'>
                      <tr><td style='padding:4px 12px 4px 0;'><b>User Name</b></td><td>{userName}</td></tr>
                      <tr><td style='padding:4px 12px 4px 0;'><b>ชื่อ-สกุล</b></td><td>{emp.FullName}</td></tr>
                      <tr><td style='padding:4px 12px 4px 0;'><b>รหัสพนักงาน</b></td><td>{emp.EmpNo}</td></tr>
                      <tr><td style='padding:4px 12px 4px 0;'><b>แผนก</b></td><td>{emp.DeptAbb} ({emp.DeptCode})</td></tr>
                      <tr><td style='padding:4px 12px 4px 0;'><b>ประเภทคำขอ</b></td><td>{typeText}</td></tr>
                    </table>
                    <p style='margin-top:16px;'><b>เมนูที่ขอเข้าถึง:</b></p>
                    <ul>{menuList}</ul>
                    <p style='color:#d32f2f; font-size:13px; margin-top:16px;'>
                      ⏳ สถานะปัจจุบัน: รออนุมัติ (status = 50)</p>
                  </div>
                  <div style='background:#f5f5f5; padding:12px; text-align:center; font-size:12px; color:#999;'>
                    ENG4 Engineering Department · ต่อ 5504
                  </div>
                </div>";

        // SendAsync คืน bool — ไม่ throw ถ้าล้ม การลงทะเบียนสำเร็จไปแล้ว
        // ไม่ให้เมลล้มทำ transaction พัง (ถ้าอยากรู้ว่าล้ม แนะนำเพิ่ม ILogger)
        await _email.SendAsync(ADMIN_EMAIL, subject, bodyHtml);
    }

    // set สิทธิ์ตาม column ที่เลือก — ใช้ค่า status ที่ส่งเข้ามา (50 = รออนุมัติ)
    private static void ApplyPermissions(
        SysUserMaster user, HashSet<string> columns, int permValue)
    {
        var type = typeof(SysUserMaster);
        foreach (var col in columns)
        {
            var prop = FindPropertyByColumn(type, col);
            if (prop == null || prop.PropertyType != typeof(int?)) continue;
            prop.SetValue(user, permValue);
        }
    }

    private static System.Reflection.PropertyInfo? FindPropertyByColumn(Type type, string columnName)
    {
        foreach (var p in type.GetProperties())
        {
            var attr = p.GetCustomAttributes(
                typeof(System.ComponentModel.DataAnnotations.Schema.ColumnAttribute), false)
                .FirstOrDefault() as System.ComponentModel.DataAnnotations.Schema.ColumnAttribute;
            if (attr != null && string.Equals(attr.Name, columnName, StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }

    private static string Md5Hash(string input)
    {
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
    }

    private static string? ValidatePassword(string pw)
    {
        if (string.IsNullOrEmpty(pw) || pw.Length < 6)
            return "รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร";
        return null;
    }

    // user ที่ login แล้ว ขอเมนูเพิ่ม (set = 50)
    public async Task<RegisterResult> RequestMenusAsync(string userName, List<string> selectedColumns)
    {
        var user = await _db.SysUserMasters.FirstOrDefaultAsync(u => u.UserName == userName);
        if (user == null)
            return new RegisterResult { Success = false, Message = "ไม่พบผู้ใช้งาน" };

        // เมนูที่ user คนนี้เห็นได้ (ENG4 filter ตาม dept)
        var isEng4 = user.DeptCode == "5390";   // หรือเช็คตามที่ระบบกำหนด
        var menus = await GetMenusAsync(isEng4);
        var validColumns = menus
            .Select(m => m.SysUserMasterColumn)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requested = selectedColumns
            .Where(c => validColumns.Contains(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var current = GetCurrentPermissions(user, menus);

        // เฉพาะที่ยังไม่มี (ทั้งไม่มี และไม่ได้รออยู่)
        var toAdd = requested.Where(c => !(current.TryGetValue(c, out var g) && g)).ToHashSet();

        if (toAdd.Count == 0)
            return new RegisterResult
            {
                Success = false,
                AlreadyComplete = true,
                Message = "คุณมีสิทธิ์เข้าถึงเมนูที่เลือกครบอยู่แล้ว (หรือกำลังรออนุมัติ)"
            };

        ApplyPermissions(user, toAdd, PENDING);   // = 50
        await _db.SaveChangesAsync();

        // แจ้ง admin
        var emp = new HrmsEmployee
        {
            EmpNo = user.EmpId ?? "",
            GnameEng = user.FName ?? "",
            FnameEng = user.LName ?? "",
            DeptCode = user.DeptCode ?? ""
        };
        var menuNames = menus
            .Where(m => toAdd.Contains(m.SysUserMasterColumn ?? ""))
            .Select(m => m.SystemName ?? m.SysUserMasterColumn ?? "")
            .ToList();
        await SendAdminNotifyAsync(emp, userName, menuNames, isNewUser: false);

        return new RegisterResult
        {
            Success = true,
            Message = $"ส่งคำขอเพิ่มสิทธิ์ {toAdd.Count} เมนูแล้ว กรุณารออนุมัติ",
            UserName = userName
        };
    }

    // method ดึงรายการเมนู + สถานะ สำหรับแสดงในหน้า Request Access
    public async Task<RequestAccessData> GetAccessMenusAsync(string userName)
    {
        var user = await _db.SysUserMasters.FirstOrDefaultAsync(u => u.UserName == userName);
        if (user == null)
            return new RequestAccessData { Success = false };

        var isEng4 = user.DeptCode == "5390";
        var menus = await GetMenusAsync(isEng4);
        var type = typeof(SysUserMaster);

        var items = new List<AccessMenuItem>();
        foreach (var m in menus)
        {
            var col = m.SysUserMasterColumn;
            if (string.IsNullOrWhiteSpace(col)) continue;

            var prop = FindPropertyByColumn(type, col);
            var val = prop?.GetValue(user) as int?;

            items.Add(new AccessMenuItem
            {
                Column = col,
                Name = m.SystemName ?? "",
                Detail = m.SystemDetail ?? "",
                Eng4Only = m.Eng4Only == true,
                Status = val ?? 0   // 0=ไม่มี, 1=มีสิทธิ์, 50=รออนุมัติ
            });
        }

        return new RequestAccessData { Success = true, Menus = items };
    }

    public class RegisterLookupResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public bool IsNewUser { get; set; }
        public string EmpId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Division { get; set; } = "";
        public string Department { get; set; } = "";
        public string Position { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsEng4 { get; set; }
        public List<MenuItemDto> Menus { get; set; } = new();
    }

    public class MenuItemDto
    {
        public string Column { get; set; } = "";
        public string Name { get; set; } = "";
        public string Detail { get; set; } = "";
        public bool Eng4Only { get; set; }
        public bool AlreadyGranted { get; set; }
    }

    public class RegisterSubmitDto
    {
        public string EmpId { get; set; } = "";
        public string? Tel { get; set; }
        public string Password { get; set; } = "";
        public List<string> SelectedColumns { get; set; } = new();
    }

    public class RegisterResult
    {
        public bool Success { get; set; }
        public bool AlreadyComplete { get; set; }
        public string Message { get; set; } = "";
        public string UserName { get; set; } = "";
    }
   
}

public class RequestAccessData
{
    public bool Success { get; set; }
    public List<AccessMenuItem> Menus { get; set; } = new();
}

public class AccessMenuItem
{
    public string Column { get; set; } = "";
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public bool Eng4Only { get; set; }
    public int Status { get; set; }   // 0 / 1 / 50
}

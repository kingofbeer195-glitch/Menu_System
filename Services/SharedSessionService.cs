using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SystemAll.Data;
using SystemAll.Models;

namespace SystemAll.Services;

public class SharedSessionService
{
    public const string CookieName = "SHARED_SID";

    private readonly AppDbContext _db;
    private readonly SessionTimeoutOptions _opt;

    public SharedSessionService(AppDbContext db, IOptions<SessionTimeoutOptions> opt)
    {
        _db = db;
        _opt = opt.Value;
    }

    public SessionTimeoutOptions Options => _opt;

    public async Task<string> CreateSessionAsync(SessionCreateDto dto)
    {
        // Deactivate session เดิมของ User นี้
        // ยกเว้น session อายุยาว (จอ Monitor) — login ซ้ำจากเครื่องอื่นต้องไม่ทำให้จอ Monitor หลุด
        var longLived = TimeSpan.FromDays(1);
        var now = DateTime.Now;

        var oldSessions = await _db.SharedSessions
            .Where(s => s.UserId == dto.UserId && s.IsActive)
            .ToListAsync();

        foreach (var s in oldSessions)
        {
            var isLongLivedSession = (s.ExpiresAt - s.CreatedAt) > longLived;
            if (isLongLivedSession && s.ExpiresAt > now)
                continue;                                   // จอ Monitor : ปล่อยไว้

            s.IsActive = false;
        }

        var sessionId = Guid.NewGuid().ToString("N");

        _db.SharedSessions.Add(new SharedSession
        {
            SessionId = sessionId,
            UserId = dto.UserId,
            UserName = dto.UserName,
            FullName = dto.FullName,
            ShortName = dto.ShortName,
            EmpId = dto.EmpId,
            Email = dto.Email,
            Ext = dto.Ext,
            Department = dto.Department,
            DeptCode = dto.DeptCode,
            DeptNameFull = dto.DeptNameFull,
            GroupName = dto.GroupName,
            PositionName = dto.PositionName,
            PositionCode = dto.PositionCode,
            PermJigTool = dto.PermJigTool,
            PermSparePart = dto.PermSparePart,
            PermPmTpm = dto.PermPmTpm,
            PermPm = dto.PermPm,
            PermPmProblem = dto.PermPmProblem,
            PermBusRequest = dto.PermBusRequest,
            PermDashboard = dto.PermDashboard,
            PermPersonal = dto.PermPersonal,
            PermToolEquipment = dto.PermToolEquipment,
            PermWms = dto.PermWms,
            PermApproval = dto.PermApproval,
            PermPmChat = dto.PermPmChat,
            PermBugReport = dto.PermBugReport,
            PermManual = dto.PermManual,
            PermDevelopSystem = dto.PermDevelopSystem,
            Admin = dto.Admin,
            CreatedAt = now,
            // ปกติ: idle ServerIdleMinutes แล้วต่อเวลาเรื่อยๆ ด้วย TouchAsync (ไม่เกิน AbsoluteHours)
            // จอ Monitor: ส่ง Lifetime มา (อายุยาว ไม่ sliding)
            ExpiresAt = now.Add(dto.Lifetime ?? TimeSpan.FromMinutes(_opt.ServerIdleMinutes)),
            LastActivity = now,
            AbsoluteExpiresAt = dto.Lifetime is null ? now.AddHours(_opt.AbsoluteHours) : null,
            IsActive = true,
            IpAddress = dto.IpAddress
        });

        await _db.SaveChangesAsync();
        return sessionId;
    }

    public async Task<SharedSession?> ValidateSessionAsync(string sessionId)
    {
        return await _db.SharedSessions.FirstOrDefaultAsync(s =>
            s.SessionId == sessionId &&
            s.IsActive &&
            s.ExpiresAt > DateTime.Now);
    }

    /// <summary>เช็ค + ต่อเวลา (sliding) คืน null ถ้าหมดอายุ/ถูก logout แล้ว</summary>
    public async Task<SessionStatus?> TouchAsync(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        var rows = await _db.Database.SqlQuery<SessionStatus>(
            $"EXEC dbo.usp_SharedSession_Touch @SessionId = {sessionId}, @IdleMinutes = {_opt.ServerIdleMinutes}, @AbsoluteHours = {_opt.AbsoluteHours}, @ThrottleSeconds = {60}")
            .ToListAsync();
        return rows.FirstOrDefault();
    }

    /// <summary>เช็คอย่างเดียว ไม่ต่อเวลา</summary>
    public async Task<SessionStatus?> CheckAsync(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        var rows = await _db.Database.SqlQuery<SessionStatus>(
            $"EXEC dbo.usp_SharedSession_Check @SessionId = {sessionId}")
            .ToListAsync();
        return rows.FirstOrDefault();
    }

    /// <summary>
    /// ตั้ง cookie SHARED_SID
    /// - session ปกติ: session cookie (ไม่กำหนด Expires) ให้ DB เป็นตัวตัดสินอายุ
    ///   (เดิมกำหนด Expires 1-2 ชม. ทำให้ browser ลบ cookie ทิ้งทั้งที่ยังใช้งานอยู่)
    /// - จอ Monitor: persistent ตามอายุ session
    /// </summary>
    public static void AppendSidCookie(HttpResponse response, string sid, DateTimeOffset? persistentUntil = null)
    {
        response.Cookies.Append(CookieName, sid, new CookieOptions
        {
            Path = "/",
            HttpOnly = false,           // Web Forms อ่านผ่าน Request.Cookies (ฝั่ง server) — คงค่าเดิมไว้
            SameSite = SameSiteMode.Lax,
            Expires = persistentUntil,
            IsEssential = true
        });
    }

    public async Task InvalidateSessionAsync(string sessionId)
    {
        var session = await _db.SharedSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session != null)
        {
            session.IsActive = false;
            await _db.SaveChangesAsync();
        }
    }
}

// ผลจาก usp_SharedSession_Check / usp_SharedSession_Touch
public class SessionStatus
{
    public string? EmpId { get; set; }
    public string UserId { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool IsLongLived { get; set; }
}

// DTO สำหรับส่งข้อมูล Session
public class SessionCreateDto
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string? EmpId { get; set; }
    public string? Email { get; set; }
    public string? Ext { get; set; }
    public string Department { get; set; } = "";
    public string? DeptCode { get; set; }
    public string? DeptNameFull { get; set; }
    public string? GroupName { get; set; }
    public string? PositionName { get; set; }
    public string? PositionCode { get; set; }
    public bool PermJigTool { get; set; }
    public bool PermSparePart { get; set; }
    public bool PermPmTpm { get; set; }
    public bool PermPm { get; set; }
    public bool PermPmProblem { get; set; }
    public bool PermBusRequest { get; set; }
    public bool PermDashboard { get; set; }
    public bool PermPersonal { get; set; }
    public bool PermToolEquipment { get; set; }
    public bool PermWms { get; set; }
    public bool PermApproval { get; set; }
    public bool PermPmChat { get; set; }
    public bool PermBugReport { get; set; }
    public bool PermManual { get; set; }
    public bool PermDevelopSystem { get; set; }
    public string? IpAddress { get; set; }
    public bool Admin { get; set; }

    /// <summary>อายุ session แบบตายตัว (null = sliding ตาม SessionTimeoutOptions) ใช้กับจอ Monitor</summary>
    public TimeSpan? Lifetime { get; set; }

}
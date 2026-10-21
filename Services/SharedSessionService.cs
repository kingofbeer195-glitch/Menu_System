using Microsoft.EntityFrameworkCore;
using SystemAll.Data;
using SystemAll.Models;

namespace SystemAll.Services;

public class SharedSessionService
{
    private readonly AppDbContext _db;

    public SharedSessionService(AppDbContext db)
    {
        _db = db;
    }

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
            CreatedAt = DateTime.Now,
            //ExpiresAt = DateTime.Now.AddHours(1),
            ExpiresAt = DateTime.Now.Add(dto.Lifetime ?? TimeSpan.FromHours(1)),
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

    /// <summary>อายุ session (null = ค่า default 1 ชม.)</summary>
    public TimeSpan? Lifetime { get; set; }

}
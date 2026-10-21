using Microsoft.EntityFrameworkCore;
using SystemAll.Data;
using SystemAll.Models;

namespace SystemAll.Services;

// ============================================================
//  ENG3,4 Team — จัดการ Team และกำหนด Team ให้พนักงาน
//  1 คนอยู่ได้หลาย Team (ตาราง eng_team_member)
//  ใช้ IDbContextFactory: สร้าง DbContext ใหม่ทุกครั้ง
//  (กันข้อมูลค้าง/tracking ข้ามคำสั่งใน Blazor Server circuit)
// ============================================================
public class EngTeamService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    // dept_code ของ ENG3 / ENG4
    public const string DEPT_ENG3 = "5330";
    public const string DEPT_ENG4 = "5390";
    private const int ACTIVE = 1;

    public EngTeamService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    // ------------------------------------------------------------
    //  รายชื่อพนักงาน ENG3,4 (status = 1, dept_code 5330/5390) + Team ที่สังกัด
    // ------------------------------------------------------------
    public async Task<List<EngMemberDto>> GetMembersAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var users = await (
            from u in db.SysUserMasters.AsNoTracking()
            where u.IsActive == ACTIVE
               && (u.DeptCode!.Trim() == DEPT_ENG3 || u.DeptCode!.Trim() == DEPT_ENG4)
            join p in db.PositionMasters.AsNoTracking() on u.PositionId equals (int?)p.PositionId into pj
            from p in pj.DefaultIfEmpty()
            orderby u.EmpId
            select new EngMemberDto
            {
                UserName = u.UserName,
                EmpId = u.EmpId ?? "",
                FName = u.FName ?? "",
                LName = u.LName ?? "",
                DeptCode = (u.DeptCode ?? "").Trim(),
                Ext = u.Ext ?? "",
                Position = p != null ? (p.PositionName ?? "") : ""
            }).ToListAsync();

        // Team ทั้งหมดของทุกคน (ดึงครั้งเดียวแล้วจับคู่ในหน่วยความจำ)
        var links = await (
            from m in db.EngTeamMembers.AsNoTracking()
            join t in db.EngTeamMasters.AsNoTracking() on m.TeamId equals t.TeamId
            select new { m.UserName, t.TeamId, t.TeamName }).ToListAsync();

        var byUser = links
            .GroupBy(l => l.UserName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key,
                          g => g.OrderBy(x => x.TeamName)
                                .Select(x => new TeamRef(x.TeamId, x.TeamName)).ToList(),
                          StringComparer.OrdinalIgnoreCase);

        foreach (var u in users)
            if (byUser.TryGetValue(u.UserName, out var teams)) u.Teams = teams;

        return users;
    }

    // ------------------------------------------------------------
    //  กำหนด Team ของ user (แทนที่ทั้งชุด) — ว่าง = ไม่มี Team
    // ------------------------------------------------------------
    public async Task<bool> SetUserTeamsAsync(string userName, IEnumerable<int> teamIds, string? by)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var wanted = teamIds.Distinct().ToList();

        // ทุก Team ที่เลือกต้องยังมีอยู่ (ตาราง Team เล็ก — เช็คในหน่วยความจำ
        // เลี่ยง Contains(list) ที่ EF9 แปลเป็น OPENJSON ซึ่งต้องใช้ compat level 130+)
        var allIds = (await db.EngTeamMasters.Select(t => t.TeamId).ToListAsync()).ToHashSet();
        if (wanted.Any(id => !allIds.Contains(id))) return false;

        var current = await db.EngTeamMembers
            .Where(m => m.UserName == userName)
            .ToListAsync();

        db.EngTeamMembers.RemoveRange(current.Where(c => !wanted.Contains(c.TeamId)));

        var now = DateTime.Now;
        foreach (var id in wanted.Where(id => !current.Any(c => c.TeamId == id)))
        {
            db.EngTeamMembers.Add(new EngTeamMember
            {
                UserName = userName,
                TeamId = id,
                CreatedDate = now,
                CreatedBy = by
            });
        }

        await db.SaveChangesAsync();
        return true;
    }

    // ------------------------------------------------------------
    //  Team master
    // ------------------------------------------------------------
    public async Task<List<EngTeamDto>> GetTeamsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        return await db.EngTeamMasters.AsNoTracking()
            .OrderBy(t => t.TeamName)
            .Select(t => new EngTeamDto
            {
                TeamId = t.TeamId,
                TeamName = t.TeamName,
                MemberCount = (
                    from m in db.EngTeamMembers
                    join u in db.SysUserMasters on m.UserName equals u.UserName
                    where m.TeamId == t.TeamId && u.IsActive == ACTIVE
                    select m).Count()
            })
            .ToListAsync();
    }

    public async Task<(bool Ok, string Message)> AddTeamAsync(string name, string? by)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return (false, "กรุณากรอกชื่อ Team");
        if (name.Length > 100) return (false, "ชื่อ Team ยาวเกิน 100 ตัวอักษร");

        await using var db = await _factory.CreateDbContextAsync();

        if (await db.EngTeamMasters.AnyAsync(t => t.TeamName == name))
            return (false, $"มี Team \"{name}\" อยู่แล้ว");

        db.EngTeamMasters.Add(new EngTeamMaster
        {
            TeamName = name,
            CreatedDate = DateTime.Now,
            CreatedBy = by
        });
        await db.SaveChangesAsync();
        return (true, $"เพิ่ม Team \"{name}\" แล้ว");
    }

    public async Task<(bool Ok, string Message)> RenameTeamAsync(int teamId, string name, string? by)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return (false, "กรุณากรอกชื่อ Team");
        if (name.Length > 100) return (false, "ชื่อ Team ยาวเกิน 100 ตัวอักษร");

        await using var db = await _factory.CreateDbContextAsync();

        var team = await db.EngTeamMasters.FirstOrDefaultAsync(t => t.TeamId == teamId);
        if (team == null) return (false, "ไม่พบ Team");

        if (await db.EngTeamMasters.AnyAsync(t => t.TeamName == name && t.TeamId != teamId))
            return (false, $"มี Team \"{name}\" อยู่แล้ว");

        team.TeamName = name;
        team.UpdatedDate = DateTime.Now;
        team.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, "แก้ไขชื่อ Team แล้ว");
    }

    // ลบ Team: ลบสมาชิกของ Team นั้นก่อน แล้วลบ Team — ทำใน transaction เดียว
    // (สมาชิกยังอยู่ใน Team อื่นได้ตามปกติ)
    public async Task<(bool Ok, string Message)> DeleteTeamAsync(int teamId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var removed = await db.EngTeamMembers
            .Where(m => m.TeamId == teamId)
            .ExecuteDeleteAsync();

        var deleted = await db.EngTeamMasters
            .Where(t => t.TeamId == teamId)
            .ExecuteDeleteAsync();

        if (deleted == 0)
        {
            await tx.RollbackAsync();
            return (false, "ไม่พบ Team");
        }

        await tx.CommitAsync();
        return (true, removed > 0 ? $"ลบ Team แล้ว (เอาสมาชิกออก {removed} คน)" : "ลบ Team แล้ว");
    }
}

// ============================================================
//  DTOs
// ============================================================
public record TeamRef(int TeamId, string TeamName);

public class EngMemberDto
{
    public string UserName { get; set; } = "";
    public string EmpId { get; set; } = "";
    public string FName { get; set; } = "";
    public string LName { get; set; } = "";
    public string FullName => $"{FName} {LName}".Trim();
    public string DeptCode { get; set; } = "";
    public string DeptLabel => DeptCode == EngTeamService.DEPT_ENG3 ? "ENG3"
                             : DeptCode == EngTeamService.DEPT_ENG4 ? "ENG4" : DeptCode;
    public string Ext { get; set; } = "";
    public string Position { get; set; } = "";
    public List<TeamRef> Teams { get; set; } = new();
    public bool HasTeam => Teams.Count > 0;
}

public class EngTeamDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = "";
    public int MemberCount { get; set; }
}

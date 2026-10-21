using Microsoft.EntityFrameworkCore;
using SystemAll.Data;
using SystemAll.Models;

namespace SystemAll.Services;

// ============================================================
//  ENG3,4 Team — จัดการ Team และกำหนด Team ให้พนักงาน
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
    //  รายชื่อพนักงาน ENG3,4 (status = 1, dept_code 5330/5390)
    // ------------------------------------------------------------
    public async Task<List<EngMemberDto>> GetMembersAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var query =
            from u in db.SysUserMasters.AsNoTracking()
            where u.IsActive == ACTIVE
               && (u.DeptCode!.Trim() == DEPT_ENG3 || u.DeptCode!.Trim() == DEPT_ENG4)
            join t in db.EngTeamMasters.AsNoTracking() on u.EngTeamId equals (int?)t.TeamId into tj
            from t in tj.DefaultIfEmpty()
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
                Position = p != null ? (p.PositionName ?? "") : "",
                TeamId = u.EngTeamId,
                TeamName = t != null ? t.TeamName : null
            };

        return await query.ToListAsync();
    }

    // ------------------------------------------------------------
    //  กำหนด / เปลี่ยน / เอาออกจาก Team  (teamId = null → ไม่มี Team)
    // ------------------------------------------------------------
    public async Task<bool> SetUserTeamAsync(string userName, int? teamId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        if (teamId != null && !await db.EngTeamMasters.AnyAsync(t => t.TeamId == teamId))
            return false;

        var rows = await db.SysUserMasters
            .Where(u => u.UserName == userName)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EngTeamId, teamId));

        return rows > 0;
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
                MemberCount = db.SysUserMasters.Count(u => u.EngTeamId == t.TeamId && u.IsActive == ACTIVE)
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

    // ลบ Team: เอาสมาชิกออกจาก Team ก่อน (eng_team_id = NULL) แล้วลบ — ทำใน transaction เดียว
    public async Task<(bool Ok, string Message)> DeleteTeamAsync(int teamId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var cleared = await db.SysUserMasters
            .Where(u => u.EngTeamId == teamId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EngTeamId, (int?)null));

        var deleted = await db.EngTeamMasters
            .Where(t => t.TeamId == teamId)
            .ExecuteDeleteAsync();

        if (deleted == 0)
        {
            await tx.RollbackAsync();
            return (false, "ไม่พบ Team");
        }

        await tx.CommitAsync();
        return (true, cleared > 0 ? $"ลบ Team แล้ว (เอาสมาชิกออก {cleared} คน)" : "ลบ Team แล้ว");
    }
}

// ============================================================
//  DTOs
// ============================================================
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
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
}

public class EngTeamDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = "";
    public int MemberCount { get; set; }
}

using Microsoft.EntityFrameworkCore;
using SystemAll.Models;

namespace SystemAll.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<SysUserMaster> SysUserMasters => Set<SysUserMaster>();
    public DbSet<SharedSession> SharedSessions => Set<SharedSession>();
    public DbSet<SysAnnouncement> SysAnnouncements => Set<SysAnnouncement>();
    public DbSet<ViewDeptListMaster> ViewDeptListMasters => Set<ViewDeptListMaster>();
    public DbSet<EngGroupMaster> EngGroupMasters => Set<EngGroupMaster>();
    public DbSet<PositionMaster> PositionMasters => Set<PositionMaster>();
    public DbSet<PasswordResetOtp> PasswordResetOtps => Set<PasswordResetOtp>();
    public DbSet<SystemListAll> SystemListAlls => Set<SystemListAll>();
    public DbSet<DeptMaster> DeptMasters => Set<DeptMaster>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SysUserMaster>(entity =>
        {
            entity.ToTable("sys_user_master");
            entity.HasKey(e => e.UserName);
        });

        modelBuilder.Entity<SharedSession>(entity =>
        {
            entity.ToTable("SharedSessions");
            entity.HasKey(e => e.SessionId);
        });

        modelBuilder.Entity<SysAnnouncement>(entity =>
        {
            entity.ToTable("sys_announcements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ViewDeptListMaster>(entity =>
        {
            entity.ToTable("View_Dept_List_Master");
            entity.HasKey(e => e.DeptCode);
            // View — ไม่สร้าง/แก้ไขด้วย Migration
        });

        modelBuilder.Entity<EngGroupMaster>(entity =>
        {
            entity.ToTable("eng_group_master");
            entity.HasKey(e => e.GroupId);
        });

        modelBuilder.Entity<PositionMaster>(entity =>
        {
            entity.ToTable("Position_master");
            entity.HasKey(e => e.PositionId);
        });

        modelBuilder.Entity<PasswordResetOtp>(e =>
        {
            e.ToTable("PasswordResetOtp");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<SystemListAll>(entity =>
        {
            entity.ToTable("System_List_All");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<DeptMaster>(entity =>
        {
            entity.ToTable("Dept_master");
            entity.HasKey(e => e.DeptId);
        });
    }
}
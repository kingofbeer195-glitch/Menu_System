using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemAll.Models;

[Table("SharedSessions")]
public class SharedSession
{
    [Key]
    [Column("session_id")]
    [StringLength(100)]
    public string SessionId { get; set; } = "";

    [Column("user_id")]
    [StringLength(50)]
    public string UserId { get; set; } = "";

    [Column("user_name")]
    [StringLength(200)]
    public string? UserName { get; set; }

    [Column("full_name")]
    [StringLength(200)]
    public string? FullName { get; set; }

    [Column("short_name")]
    [StringLength(100)]
    public string? ShortName { get; set; }

    [Column("emp_id")]
    [StringLength(20)]
    public string? EmpId { get; set; }

    [Column("email")]
    [StringLength(200)]
    public string? Email { get; set; }

    [Column("ext")]
    [StringLength(20)]
    public string? Ext { get; set; }

    [Column("department")]
    [StringLength(100)]
    public string? Department { get; set; }

    [Column("dept_code")]
    [StringLength(20)]
    public string? DeptCode { get; set; }

    [Column("dept_name_full")]
    [StringLength(200)]
    public string? DeptNameFull { get; set; }

    [Column("group_name")]
    [StringLength(150)]
    public string? GroupName { get; set; }

    [Column("position_name")]
    [StringLength(150)]
    public string? PositionName { get; set; }

    [Column("position_code")]
    [StringLength(50)]
    public string? PositionCode { get; set; }

    // สิทธิ์ระบบ (เก็บไว้เพื่อให้ Web Forms อ่านได้)
    [Column("perm_jig_tool")]
    public bool PermJigTool { get; set; }

    [Column("perm_spare_part")]
    public bool PermSparePart { get; set; }

    [Column("perm_pm_tpm")]
    public bool PermPmTpm { get; set; }

    [Column("perm_pm")]
    public bool PermPm { get; set; }

    [Column("perm_pm_problem")]
    public bool PermPmProblem { get; set; }

    [Column("perm_bus_request")]
    public bool PermBusRequest { get; set; }

    [Column("perm_dashboard")]
    public bool PermDashboard { get; set; }

    [Column("perm_personal")]
    public bool PermPersonal { get; set; }

    [Column("perm_tool_equipment")]
    public bool PermToolEquipment { get; set; }

    [Column("perm_wms")]
    public bool PermWms { get; set; }

    [Column("perm_approval")]
    public bool PermApproval { get; set; }

    [Column("perm_pmchat")]
    public bool PermPmChat { get; set; }

    [Column("perm_bug_report")]
    public bool PermBugReport { get; set; }

    [Column("perm_manual")]
    public bool PermManual { get; set; }

    [Column("perm_develop")]
    public bool PermDevelopSystem { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("ip_address")]
    [StringLength(50)]
    public string? IpAddress { get; set; }

    [Column("Admin")]
    public bool? Admin { get; set; }
}
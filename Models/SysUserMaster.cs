using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemAll.Models;

[Table("sys_user_master")]
public class SysUserMaster
{
    [Key]
    [Column("user_name")]
    [StringLength(100)]
    public string UserName { get; set; } = "";

    [Column("user_pass")]
    [StringLength(255)]
    public string UserPass { get; set; } = "";

    [Column("emp_id")]
    [StringLength(20)]
    public string? EmpId { get; set; }

    [Column("fname")]
    [StringLength(150)]
    public string? FName { get; set; }

    [Column("lname")]
    [StringLength(150)]
    public string? LName { get; set; }

    [Column("email")]
    [StringLength(200)]
    public string? Email { get; set; }

    [Column("ext")]
    [StringLength(20)]
    public string? Ext { get; set; }

    // div_id: 1 = Internal, 2 = Other
    [Column("div_id")]
    public int? DivId { get; set; }

    [Column("user_dept")]
    public int? UserDept { get; set; }

    [Column("eng_group_id")]
    public int? EngGroupId { get; set; }

    [Column("position_id")]
    public int? PositionId { get; set; }

    [Column("status")]
    public int? IsActive { get; set; }

    // ===== สิทธิ์ระบบ =====
    [Column("jigTool")]
    public int? JigTool { get; set; }

    [Column("spare_part")]
    public int? SparePart { get; set; }

    [Column("pm_tpm")]
    public int? PmTpm { get; set; }

    [Column("pm")]
    public int? Pm { get; set; }

    [Column("pm_problem")]
    public int? PmProblem { get; set; }

    [Column("bus_requst")]
    public int? BusRequest { get; set; }

    [Column("dashboard")]
    public int? Dashboard { get; set; }

    [Column("personal")]
    public int? Personal { get; set; }

    [Column("tool_equipment")]
    public int? ToolEquipment { get; set; }

    [Column("wms")]
    public int? Wms { get; set; }

    [Column("approval")]
    public int? Approval { get; set; }

    [Column("pm_chat")]
    public int? PmChat { get; set; }

    [Column("bug_report")]
    public int? BugReport { get; set; }

    [Column("manual")]
    public int? Manual { get; set; }

    [Column("develop_system")]
    public int? DevelopSystem { get; set; }

    [Column("Admin")]
    public int? Admin { get; set; }

    [Column("dept_code")]
    public string? DeptCode { get; set; }

    [Column("Regist_date")]
    public DateTime? RegistDate { get; set; }
}
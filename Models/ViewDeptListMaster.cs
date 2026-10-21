using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemAll.Models;

// View: View_Dept_List_Master
[Table("View_Dept_List_Master")]
public class ViewDeptListMaster
{
    [Key]
    [Column("detp_code")]   // ตาม column จริง (typo ใน DB)
    [StringLength(20)]
    public string DeptCode { get; set; } = "";

    [Column("dept_name")]
    [StringLength(100)]
    public string? DeptName { get; set; }

    [Column("dept_name_full")]
    [StringLength(200)]
    public string? DeptNameFull { get; set; }

    // สำหรับ div_id = 1
    [Column("user_dept")]
    public int? UserDept { get; set; }

    // สำหรับ div_id = 2
    [Column("oth_dept_id")]
    public int? OthDeptId { get; set; }

    // division field (ถ้ามี)
    [Column("division")]
    public int? Division { get; set; }
}
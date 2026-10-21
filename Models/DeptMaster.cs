using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemAll.Models;

[Table("Dept_master")]
public class DeptMaster
{
    [Key]
    [Column("dept_id")]
    public int DeptId { get; set; }

    [Column("dept_name")]
    [StringLength(20)]
    public string? DeptName { get; set; }

    [Column("dept_code")]
    [StringLength(10)]
    public string? DeptCode { get; set; }

    [Column("dept_eng")]
    public int? DeptEng { get; set; }

    [Column("dept_name_full")]
    [StringLength(80)]
    public string? DeptNameFull { get; set; }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemAll.Models;

[Table("System_List_All")]
public class SystemListAll
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Column("System_Name")]
    [StringLength(50)]
    public string? SystemName { get; set; }

    [Column("Status_Use")]
    public bool? StatusUse { get; set; }

    [Column("System_Detail")]
    [StringLength(200)]
    public string? SystemDetail { get; set; }

    [Column("ENG4_Only")]
    public bool? Eng4Only { get; set; }

    [Column("sys_user_master_column")]
    [StringLength(50)]
    public string? SysUserMasterColumn { get; set; }
}
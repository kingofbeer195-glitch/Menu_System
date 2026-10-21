using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemAll.Models;

[Table("eng_group_master")]
public class EngGroupMaster
{
    [Key]
    [Column("group_id")]
    public int GroupId { get; set; }

    [Column("group_name")]
    [StringLength(150)]
    public string? GroupName { get; set; }

    [Column("dept")]
    public int? Dept { get; set; }

    [Column("active")]
    public bool Active { get; set; }
}
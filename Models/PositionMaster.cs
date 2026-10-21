using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemAll.Models;

[Table("Position_master")]
public class PositionMaster
{
    [Key]
    [Column("position_id")]
    public int PositionId { get; set; }

    [Column("position_name")]
    [StringLength(150)]
    public string? PositionName { get; set; }

    [Column("position_code")]
    [StringLength(50)]
    public string? PositionCode { get; set; }
}
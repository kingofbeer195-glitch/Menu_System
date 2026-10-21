using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemAll.Models;

// Team ของพนักงาน ENG3,4 (dept_code 5330 / 5390)
[Table("eng_team_master")]
public class EngTeamMaster
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("team_id")]
    public int TeamId { get; set; }

    [Column("team_name")]
    [StringLength(100)]
    public string TeamName { get; set; } = "";

    [Column("created_date")]
    public DateTime? CreatedDate { get; set; }

    [Column("created_by")]
    [StringLength(100)]
    public string? CreatedBy { get; set; }

    [Column("updated_date")]
    public DateTime? UpdatedDate { get; set; }

    [Column("updated_by")]
    [StringLength(100)]
    public string? UpdatedBy { get; set; }
}

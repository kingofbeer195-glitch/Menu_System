using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemAll.Models;

// สมาชิก Team ENG3,4 — 1 คนอยู่ได้หลาย Team (PK = user_name + team_id)
[Table("eng_team_member")]
public class EngTeamMember
{
    [Column("user_name")]
    [StringLength(100)]
    public string UserName { get; set; } = "";

    [Column("team_id")]
    public int TeamId { get; set; }

    [Column("created_date")]
    public DateTime? CreatedDate { get; set; }

    [Column("created_by")]
    [StringLength(100)]
    public string? CreatedBy { get; set; }
}

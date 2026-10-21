using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemAll.Models;

[Table("sys_announcements")]
public class SysAnnouncement
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("title")]
    [StringLength(300)]
    public string Title { get; set; } = "";

    [Column("content")]
    public string? Content { get; set; }

    // ชื่อระบบที่เกี่ยวข้อง เช่น "PM Manual", "WMS"
    [Column("system_name")]
    [StringLength(100)]
    public string? SystemName { get; set; }

    [Column("created_date")]
    public DateTime CreatedDate { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    // ลำดับการแสดงผล (น้อย = แสดงก่อน)
    [Column("sort_order")]
    public int SortOrder { get; set; }
}

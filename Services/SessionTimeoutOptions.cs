namespace SystemAll.Services;

/// <summary>ตั้งค่าใน appsettings.json → "SessionTimeout"</summary>
public class SessionTimeoutOptions
{
    /// <summary>ไม่มีการใช้งานกี่นาทีแล้ว logout (ฝั่ง browser)</summary>
    public int IdleMinutes { get; set; } = 60;

    /// <summary>แสดง popup เตือนที่นาทีที่เท่าไร</summary>
    public int WarnMinutes { get; set; } = 55;

    /// <summary>อายุ session ใน DB หลัง action ล่าสุด (เผื่อ PingMinutes ให้ server หมดหลัง browser)</summary>
    public int ServerIdleMinutes { get; set; } = 65;

    /// <summary>อายุสูงสุดของ session ไม่ว่าจะใช้งานต่อเนื่องแค่ไหน</summary>
    public int AbsoluteHours { get; set; } = 12;

    /// <summary>ยิง keepalive ไป server ไม่ถี่กว่ากี่นาที (เฉพาะตอนมีการใช้งาน)</summary>
    public int PingMinutes { get; set; } = 5;

    /// <summary>เช็คว่า session ยังอยู่ไหม (ถูก logout จากที่อื่น) ทุกกี่วินาที</summary>
    public int CheckSeconds { get; set; } = 60;

    /// <summary>บัญชีจอ Monitor: session อายุยาว ไม่ timeout</summary>
    public string[] MonitorAccounts { get; set; } = ["Admin"];

    public int MonitorLifetimeDays { get; set; } = 365;

    public bool IsMonitorAccount(string? userName) =>
        !string.IsNullOrWhiteSpace(userName) &&
        MonitorAccounts.Any(a => string.Equals(a, userName.Trim(), StringComparison.OrdinalIgnoreCase));
}

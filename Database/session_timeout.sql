/* =====================================================================
   Session Timeout + Sliding Expiration สำหรับ user_master.dbo.SharedSessions
   - idle 65 นาที (ฝั่ง server) ต่อเวลาทุกครั้งที่มีการใช้งาน
   - absolute 12 ชม. (ใช้งานต่อเนื่องแค่ไหนก็ต้อง login ใหม่)
   - session อายุยาว (จอ Monitor: expires - created > 1 วัน) ไม่ถูกแตะ
   รันซ้ำได้ (idempotent)
   ===================================================================== */
USE user_master;
GO

IF COL_LENGTH('dbo.SharedSessions', 'last_activity') IS NULL
    ALTER TABLE dbo.SharedSessions ADD last_activity DATETIME NULL;
GO
IF COL_LENGTH('dbo.SharedSessions', 'absolute_expires_at') IS NULL
    ALTER TABLE dbo.SharedSessions ADD absolute_expires_at DATETIME NULL;
GO

/* ===== Check: เช็คอย่างเดียว ไม่ต่อเวลา ===== */
IF OBJECT_ID('dbo.usp_SharedSession_Check', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_SharedSession_Check;
GO
CREATE PROCEDURE dbo.usp_SharedSession_Check
    @SessionId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @now DATETIME = GETDATE();

    SELECT  emp_id     AS EmpId,
            user_id    AS UserId,
            expires_at AS ExpiresAt,
            CAST(CASE WHEN DATEDIFF(MINUTE, created_at, expires_at) > 1440
                      THEN 1 ELSE 0 END AS BIT) AS IsLongLived
    FROM dbo.SharedSessions
    WHERE session_id = @SessionId
      AND is_active = 1
      AND expires_at > @now
      AND (absolute_expires_at IS NULL OR absolute_expires_at > @now);
END
GO

/* ===== Touch: เช็ค + ต่อเวลา (sliding) =====
   - ต่อ expires_at = now + @IdleMinutes แต่ไม่เกิน absolute_expires_at
   - throttle: เขียน DB ไม่ถี่กว่า @ThrottleSeconds
   - คืนผลเหมือน Check (ไม่มีแถว = หมดอายุ) */
IF OBJECT_ID('dbo.usp_SharedSession_Touch', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_SharedSession_Touch;
GO
CREATE PROCEDURE dbo.usp_SharedSession_Touch
    @SessionId       NVARCHAR(100),
    @IdleMinutes     INT = 65,
    @AbsoluteHours   INT = 12,
    @ThrottleSeconds INT = 60
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @now DATETIME = GETDATE();

    UPDATE s
       SET last_activity       = @now,
           absolute_expires_at = x.AbsEnd,
           expires_at          = CASE WHEN DATEADD(MINUTE, @IdleMinutes, @now) > x.AbsEnd
                                      THEN x.AbsEnd
                                      ELSE DATEADD(MINUTE, @IdleMinutes, @now) END
    FROM dbo.SharedSessions s
    CROSS APPLY (SELECT ISNULL(s.absolute_expires_at,
                               DATEADD(HOUR, @AbsoluteHours, s.created_at)) AS AbsEnd) x
    WHERE s.session_id = @SessionId
      AND s.is_active = 1
      AND s.expires_at > @now
      AND x.AbsEnd > @now
      AND DATEDIFF(MINUTE, s.created_at, s.expires_at) <= 1440      -- ไม่แตะ session จอ Monitor
      AND (s.last_activity IS NULL
           OR s.last_activity < DATEADD(SECOND, -@ThrottleSeconds, @now));

    EXEC dbo.usp_SharedSession_Check @SessionId = @SessionId;
END
GO

/* ===== ทดสอบ =====
SELECT TOP 5 session_id, user_id, created_at, expires_at, last_activity, absolute_expires_at, is_active
FROM dbo.SharedSessions ORDER BY created_at DESC;

EXEC dbo.usp_SharedSession_Check @SessionId = N'<sid>';
EXEC dbo.usp_SharedSession_Touch @SessionId = N'<sid>';
*/

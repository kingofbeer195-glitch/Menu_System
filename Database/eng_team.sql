-- ============================================================
--  ENG3,4 Team — Menu_System (Admin > ENG3,4 Team)
--  Database : user_master   (CPTSVS552\SQLEXPRESS)
--  1 คนอยู่ได้หลาย Team → ตาราง eng_team_member (user_name + team_id)
--
--  รันได้ซ้ำ (idempotent) ใช้ได้ทั้ง
--   - ติดตั้งใหม่
--   - เครื่องที่เคยรันเวอร์ชันแรก (sys_user_master.eng_team_id)
--     → ย้ายข้อมูลเข้า eng_team_member แล้วลบคอลัมน์เดิม
--  ต้องรันก่อน deploy
-- ============================================================
USE [user_master];
GO

-- 1) ตาราง Team
IF OBJECT_ID(N'dbo.eng_team_master', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.eng_team_master
    (
        team_id       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_eng_team_master PRIMARY KEY,
        team_name     NVARCHAR(100)     NOT NULL,
        created_date  DATETIME          NULL CONSTRAINT DF_eng_team_master_created DEFAULT (GETDATE()),
        created_by    NVARCHAR(100)     NULL,
        updated_date  DATETIME          NULL,
        updated_by    NVARCHAR(100)     NULL,
        CONSTRAINT UQ_eng_team_master_name UNIQUE (team_name)
    );
END
GO

-- 2) สมาชิก Team (หลาย Team ต่อคน)
IF OBJECT_ID(N'dbo.eng_team_member', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.eng_team_member
    (
        user_name     NVARCHAR(100) NOT NULL,
        team_id       INT           NOT NULL,
        created_date  DATETIME      NULL CONSTRAINT DF_eng_team_member_created DEFAULT (GETDATE()),
        created_by    NVARCHAR(100) NULL,
        CONSTRAINT PK_eng_team_member PRIMARY KEY (user_name, team_id),
        CONSTRAINT FK_eng_team_member_team FOREIGN KEY (team_id)
            REFERENCES dbo.eng_team_master (team_id) ON DELETE CASCADE
    );

    CREATE INDEX IX_eng_team_member_team_id ON dbo.eng_team_member (team_id);
END
GO

-- 3) ย้ายข้อมูลจากเวอร์ชันแรก (ถ้ามีคอลัมน์ eng_team_id) แล้วลบคอลัมน์เดิม
IF COL_LENGTH(N'dbo.sys_user_master', N'eng_team_id') IS NOT NULL
BEGIN
    EXEC(N'
        INSERT INTO dbo.eng_team_member (user_name, team_id, created_by)
        SELECT u.user_name, u.eng_team_id, ''migrate''
        FROM dbo.sys_user_master u
        WHERE u.eng_team_id IS NOT NULL
          AND EXISTS (SELECT 1 FROM dbo.eng_team_master t WHERE t.team_id = u.eng_team_id)
          AND NOT EXISTS (SELECT 1 FROM dbo.eng_team_member m
                          WHERE m.user_name = u.user_name AND m.team_id = u.eng_team_id);
    ');

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_sys_user_master_eng_team')
        ALTER TABLE dbo.sys_user_master DROP CONSTRAINT FK_sys_user_master_eng_team;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_sys_user_master_eng_team_id'
               AND object_id = OBJECT_ID(N'dbo.sys_user_master'))
        DROP INDEX IX_sys_user_master_eng_team_id ON dbo.sys_user_master;

    ALTER TABLE dbo.sys_user_master DROP COLUMN eng_team_id;
END
GO

-- ตรวจสอบ: รายชื่อ + Team (คนเดียวมีได้หลายแถว)
-- SELECT u.emp_id, u.fname, u.lname, u.dept_code, t.team_name
-- FROM dbo.sys_user_master u
-- LEFT JOIN dbo.eng_team_member m ON m.user_name = u.user_name
-- LEFT JOIN dbo.eng_team_master t ON t.team_id = m.team_id
-- WHERE u.status = 1 AND LTRIM(RTRIM(u.dept_code)) IN ('5330', '5390')
-- ORDER BY u.emp_id, t.team_name;

-- ============================================================
--  ENG3,4 Team — Menu_System (Admin > ENG3,4 Team)
--  Database : user_master   (CPTSVS552\SQLEXPRESS)
--  รันได้ซ้ำ (idempotent) — ต้องรันก่อน deploy เพราะ EF map คอลัมน์ eng_team_id แล้ว
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

-- 2) คอลัมน์เก็บ Team ของพนักงาน (NULL = ยังไม่มี Team)
IF COL_LENGTH(N'dbo.sys_user_master', N'eng_team_id') IS NULL
BEGIN
    ALTER TABLE dbo.sys_user_master ADD eng_team_id INT NULL;
END
GO

-- 3) FK → ลบ Team ไม่ได้ถ้ายังมีคนอ้างอิง (ระบบเคลียร์สมาชิกให้ก่อนลบอยู่แล้ว)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_sys_user_master_eng_team')
BEGIN
    ALTER TABLE dbo.sys_user_master WITH CHECK
        ADD CONSTRAINT FK_sys_user_master_eng_team
        FOREIGN KEY (eng_team_id) REFERENCES dbo.eng_team_master (team_id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_sys_user_master_eng_team_id'
               AND object_id = OBJECT_ID(N'dbo.sys_user_master'))
BEGIN
    CREATE INDEX IX_sys_user_master_eng_team_id ON dbo.sys_user_master (eng_team_id);
END
GO

-- ตรวจสอบ: รายชื่อที่จะแสดงในหน้า ENG3,4 Team
-- SELECT u.emp_id, u.fname, u.lname, u.dept_code, t.team_name
-- FROM dbo.sys_user_master u
-- LEFT JOIN dbo.eng_team_master t ON t.team_id = u.eng_team_id
-- WHERE u.status = 1 AND LTRIM(RTRIM(u.dept_code)) IN ('5330', '5390')
-- ORDER BY u.emp_id;

-- ---------- INITIALIZATION ----------

-- Use the Penumbra database.
USE Penumbra;
GO

-- Allows for the use of identifiers that contain characters not generally allowed,
-- or are the same as Transact-SQL reserved words.
SET QUOTED_IDENTIFIER ON;
GO

-- ---------- DROP TABLES ----------

-- ----------------------------------------
-- dbo.ActiveOverride

-- Check that the table doesn't already exist.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.ActiveOverride') AND type in (N'U'))
BEGIN
	-- The table exists, drop it.
	DROP TABLE dbo.ActiveOverride;
END
GO
-- ----------------------------------------

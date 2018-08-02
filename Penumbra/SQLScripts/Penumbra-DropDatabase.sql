-- ---------- INITIALIZATION ----------

-- Use the server's master database.
USE master;
GO

-- ---------- DROP DATABASE ----------

-- Check that the database exists.
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'Penumbra')
BEGIN
	-- The database exists.
	
	-- Set the database to single user mode in preparation for the drop.
	ALTER DATABASE Penumbra SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

	-- Drop the database.
	DROP DATABASE Penumbra;
END
GO

-- ---------- REMOVE USERS ----------

-- Remove the service account.
-- DROP LOGIN penumbra;
-- GO
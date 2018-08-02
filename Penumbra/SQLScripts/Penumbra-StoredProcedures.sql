-- ---------- INITIALIZATION ----------

-- Use the Penumbra database.
USE Penumbra;
GO

-- Allows for the use of identifiers that contain characters not generally allowed,
-- or are the same as Transact-SQL reserved words.
SET QUOTED_IDENTIFIER ON;
GO

-- ---------- STORED PROCEDURES ----------

-- ----------------------------------------
-- UpsertOverride
-- Summary: Upserts an override into the list of active overrides.
-- Param: @ip = The IPv4 address of the system to receive an override.

-- Check that the procedure doesn't already exist.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.UpsertOverride') AND type in (N'P', N'PC'))
BEGIN
	-- The procedure exists, drop it.
	DROP PROCEDURE dbo.UpsertOverride;
END
GO

-- Create the procedure.
CREATE PROCEDURE dbo.UpsertOverride
	@ip nvarchar(15),
	@userName nvarchar(256)
AS
-- Upserts an IP address with the current time into the list of active overrides.
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
UPDATE dbo.ActiveOverride
	SET ip = @ip,
	userName = @userName,
	[date] = GETDATE()
	WHERE @ip = ip;
IF @@ROWCOUNT = 0
BEGIN
	INSERT INTO dbo.ActiveOverride (ip, userName, [date])
		VALUES (@ip, @userName, GETDATE());
END
COMMIT TRANSACTION;
GO
-- ----------------------------------------

-- ----------------------------------------
-- DeleteOverride
-- Summary: Deletes an override from the list of active overrides.
-- Param: @ip = The IPv4 address of the override to delete.

-- Check that the procedure doesn't already exist.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.DeleteOverride') AND type in (N'P', N'PC'))
BEGIN
	-- The procedure exists, drop it.
	DROP PROCEDURE dbo.DeleteOverride;
END
GO

-- Create the procedure.
CREATE PROCEDURE dbo.DeleteOverride
	@ip nvarchar(15)
AS
-- Deletes an override from the list that matches the supplied IP address.
DELETE FROM dbo.ActiveOverride
	WHERE @ip = ip;
GO
-- ----------------------------------------

-- ----------------------------------------
-- GetExpiredOverrides
-- Summary: Gets overrides from the list of active overrides that are older than the expiration period.
-- Param: @overrideDuration = The period in minutes that overrides are allowed to be active.

-- Check that the procedure doesn't already exist.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.GetExpiredOverrides') AND type in (N'P', N'PC'))
BEGIN
	-- The procedure exists, drop it.
	DROP PROCEDURE dbo.GetExpiredOverrides;
END
GO

-- Create the procedure.
CREATE PROCEDURE dbo.GetExpiredOverrides
	@overrideDuration int
AS
-- Gets overrides from the list that are older than the duration period.
SELECT * FROM dbo.ActiveOverride
	WHERE [date] <= DATEADD(mi,-@overrideDuration, GETDATE());
GO
-- ----------------------------------------

-- ---------- GRANT PERMISSIONS TO STORED PROCEDURES ----------

-- Grant permissions for the service account on the database.
GRANT EXECUTE ON dbo.UpsertOverride TO [domain\penumbra];
GRANT EXECUTE ON dbo.DeleteOverride TO [domain\penumbra];
GRANT EXECUTE ON dbo.GetExpiredOverrides TO [domain\penumbra]
GO
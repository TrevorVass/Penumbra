-- ---------- INITIALIZATION ----------

-- Use the Penumbra database.
USE Penumbra;
GO

-- Allows for the use of identifiers that contain characters not generally allowed,
-- or are the same as Transact-SQL reserved words.
SET QUOTED_IDENTIFIER ON;
GO

-- ---------- CREATE TABLES ----------

-- Create a table to contain a list of IPv4 addresses with active overrides.
CREATE TABLE dbo.ActiveOverride
(
	ip nvarchar(15) NOT NULL PRIMARY KEY,
	userName nvarchar(256),
	"date" datetime2 NOT NULL
)

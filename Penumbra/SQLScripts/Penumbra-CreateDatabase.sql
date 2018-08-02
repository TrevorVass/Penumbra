-- ---------- INITIALIZATION ----------

-- Use the server's master database.
USE master;
GO

-- ---------- CREATE DATABASE ----------

-- Create the Penumbra database.
CREATE DATABASE Penumbra;
GO

-- ---------- SETUP USERS ----------

-- Create a service account login to access the server via Windows Authentication.
CREATE LOGIN [domain\penumbra] FROM WINDOWS;
GO

-- Use the Penumbra database.
USE Penumbra;
GO

-- Create a database user for the service account.
CREATE USER [domain\penumbra];
GO
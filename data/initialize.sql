-- Create a new database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'SampleDb')
BEGIN
    CREATE DATABASE SampleDb;
END
GO

-- Switch to the new database
USE SampleDb;
GO

-- Create a sample table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Users] (
        [UserId] INT IDENTITY(1,1) PRIMARY KEY,
        [FirstName] NVARCHAR(100) NOT NULL,
        [LastName] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(255) NOT NULL UNIQUE
    );
END
GO

-- Insert sample data if not already present
IF NOT EXISTS (SELECT 1 FROM [dbo].[Users])
BEGIN
    INSERT INTO [dbo].[Users] (FirstName, LastName, Email)
    VALUES 
        ('John', 'Doe', 'john.doe@example.com'),
        ('Jane', 'Smith', 'jane.smith@example.com'),
        ('Alice', 'Johnson', 'alice.johnson@example.com');
END
GO
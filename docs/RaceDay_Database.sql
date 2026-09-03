-- =============================================
-- RaceDay Event Management Platform
-- SQL Server Database Script (T-SQL)
-- =============================================

-- Drop existing tables in reverse dependency order to prevent foreign key constraint errors
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

-- 1. Create Users Table
CREATE TABLE dbo.Users (
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    FullName VARCHAR(100) NOT NULL,
    Email VARCHAR(150) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    Role VARCHAR(20) NOT NULL CHECK (Role IN ('Organiser', 'Participant')),
    ProfilePictureUrl VARCHAR(500) NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
);
GO

-- 2. Create Events Table
CREATE TABLE dbo.Events (
    EventID INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID INT NOT NULL,
    Name VARCHAR(150) NOT NULL,
    Description TEXT NULL,
    EventDate DATETIME NOT NULL,
    Location VARCHAR(200) NOT NULL,
    Distance DECIMAL(5,2) NOT NULL,
    EventType VARCHAR(50) NOT NULL CHECK (EventType IN ('Marathon', 'Cycling', 'Trail Run')),
    EventBannerUrl VARCHAR(500) NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserID) REFERENCES dbo.Users(UserID)
);
GO

-- 3. Create Categories Table
CREATE TABLE dbo.Categories (
    CategoryID INT IDENTITY(1,1) PRIMARY KEY,
    EventID INT NOT NULL,
    CategoryName VARCHAR(100) NOT NULL,
    MinAge INT NULL,
    MaxAge INT NULL,
    Distance DECIMAL(5,2) NULL,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID)
);
GO

-- 4. Create Enrolments Table
CREATE TABLE dbo.Enrolments (
    EnrolmentID INT IDENTITY(1,1) PRIMARY KEY,
    EventID INT NOT NULL,
    ParticipantID INT NOT NULL,
    CategoryID INT NOT NULL,
    EnrolmentDate DATETIME DEFAULT GETDATE(),
    Status VARCHAR(20) NOT NULL CHECK (Status IN ('Pending', 'Confirmed', 'Cancelled')),
    CONSTRAINT FK_Enrolments_Events FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID),
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantID) REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryID) REFERENCES dbo.Categories(CategoryID)
);
GO

-- 5. Create Results Table
CREATE TABLE dbo.Results (
    ResultID INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID INT NOT NULL UNIQUE,
    FinishTime VARCHAR(20) NOT NULL,
    Position INT NOT NULL,
    CapturedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentID) REFERENCES dbo.Enrolments(EnrolmentID)
);
GO

-- =============================================
-- Seed Data Generation
-- =============================================

-- Insert Users (1 Organiser, 2 Participants)
INSERT INTO dbo.Users (FullName, Email, PasswordHash, Role)
VALUES 
('Sarah Jenkins', 'sarah.jenkins@raceday.com', '$2a$10$EixZaYVK1fsbw1ZfbX3OXePaWxn96p36PfLzEg', 'Organiser'),
('Marcus Wright', 'marcus.wright@email.com', '$2a$10$hash_participant_marcus_88492', 'Participant'),
('Elena Rodriguez', 'elena.r@runnershub.com', '$2a$10$hash_participant_elena_10938', 'Participant');
GO
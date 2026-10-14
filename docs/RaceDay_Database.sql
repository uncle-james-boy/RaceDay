-- RaceDay SQL Server setup script (Part 1).
-- This script creates a fresh schema and sample data. It never drops or
-- recreates tables. If any RaceDay tables already exist, it stops safely.

IF DB_ID(N'RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END;
GO

USE RaceDayDB;
GO

IF EXISTS (
    SELECT 1
    FROM sys.tables
    WHERE schema_id = SCHEMA_ID(N'dbo')
      AND name IN (N'Users', N'Events', N'Categories', N'EventRoutes', N'Enrolments', N'Results')
)
BEGIN
    THROW 50001, 'RaceDay tables already exist. This setup script will not alter or replace them; use a reviewed migration for an existing database.', 1;
END;
GO

CREATE TABLE dbo.Users (
    UserID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    FullName VARCHAR(100) NOT NULL,
    Email VARCHAR(150) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    Role VARCHAR(20) NOT NULL,
    ProfilePictureUrl VARCHAR(500) NULL,
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT GETDATE(),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser', 'Participant'))
);
GO

CREATE TABLE dbo.Events (
    EventID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Events PRIMARY KEY,
    OrganiserID INT NOT NULL,
    Name VARCHAR(150) NOT NULL,
    Description VARCHAR(MAX) NULL,
    EventDate DATETIME NOT NULL,
    Location VARCHAR(200) NOT NULL,
    Distance DECIMAL(5,2) NOT NULL,
    EventType VARCHAR(20) NOT NULL,
    EventBannerUrl VARCHAR(500) NULL,
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_Events_CreatedAt DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserID) REFERENCES dbo.Users(UserID),
    CONSTRAINT CK_Events_EventType CHECK (EventType IN ('Run', 'Walk', 'Cycle')),
    CONSTRAINT CK_Events_Distance CHECK (Distance > 0)
);
GO

CREATE TABLE dbo.Categories (
    CategoryID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    EventID INT NOT NULL,
    CategoryName VARCHAR(100) NOT NULL,
    MinAge INT NULL,
    MaxAge INT NULL,
    Distance DECIMAL(5,2) NULL,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID),
    CONSTRAINT UQ_Categories_CategoryID_EventID UNIQUE (CategoryID, EventID),
    CONSTRAINT CK_Categories_MinAge CHECK (MinAge IS NULL OR MinAge >= 0),
    CONSTRAINT CK_Categories_MaxAge CHECK (MaxAge IS NULL OR MaxAge >= 0),
    CONSTRAINT CK_Categories_AgeRange CHECK (MinAge IS NULL OR MaxAge IS NULL OR MinAge <= MaxAge),
    CONSTRAINT CK_Categories_Distance CHECK (Distance IS NULL OR Distance > 0)
);
GO

CREATE TABLE dbo.EventRoutes (
    RouteID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EventRoutes PRIMARY KEY,
    EventID INT NOT NULL,
    RouteName VARCHAR(100) NOT NULL,
    Distance DECIMAL(5,2) NOT NULL,
    RouteDescription VARCHAR(MAX) NULL,
    RouteMapUrl VARCHAR(500) NULL,
    CONSTRAINT FK_EventRoutes_Events FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID),
    CONSTRAINT UQ_EventRoutes_EventID_RouteName UNIQUE (EventID, RouteName),
    CONSTRAINT CK_EventRoutes_Distance CHECK (Distance > 0)
);
GO

CREATE TABLE dbo.Enrolments (
    EnrolmentID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Enrolments PRIMARY KEY,
    EventID INT NOT NULL,
    ParticipantID INT NOT NULL,
    CategoryID INT NOT NULL,
    EnrolmentDate DATETIME NOT NULL CONSTRAINT DF_Enrolments_EnrolmentDate DEFAULT GETDATE(),
    Status VARCHAR(20) NOT NULL CONSTRAINT DF_Enrolments_Status DEFAULT 'Pending',
    CONSTRAINT FK_Enrolments_Events FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID),
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantID) REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_Enrolments_CategoryEvent FOREIGN KEY (CategoryID, EventID)
        REFERENCES dbo.Categories(CategoryID, EventID),
    CONSTRAINT UQ_Enrolments_Participant_Event_Category UNIQUE (ParticipantID, EventID, CategoryID),
    CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Pending', 'Confirmed', 'Cancelled'))
);
GO

CREATE TABLE dbo.Results (
    ResultID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Results PRIMARY KEY,
    EnrolmentID INT NOT NULL,
    FinishTime TIME(0) NOT NULL,
    Position INT NOT NULL,
    CapturedAt DATETIME NOT NULL CONSTRAINT DF_Results_CapturedAt DEFAULT GETDATE(),
    CONSTRAINT UQ_Results_EnrolmentID UNIQUE (EnrolmentID),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentID) REFERENCES dbo.Enrolments(EnrolmentID),
    CONSTRAINT CK_Results_Position CHECK (Position > 0)
);
GO

-- Example records are for schema demonstration only; their password hashes
-- are placeholders and cannot be used to sign in.
INSERT INTO dbo.Users (FullName, Email, PasswordHash, Role)
VALUES
('Sarah Jenkins', 'sarah.jenkins@raceday.com', '$2a$10$placeholder.organiser.sarah', 'Organiser'),
('Marcus Wright', 'marcus.wright@email.com', '$2a$10$placeholder.participant.marcus', 'Participant'),
('Elena Rodriguez', 'elena.r@runnershub.com', '$2a$10$placeholder.participant.elena', 'Participant'),
('David Okafor', 'david.okafor@raceday.com', '$2a$10$placeholder.organiser.david', 'Organiser'),
('Priya Naidoo', 'priya.naidoo@email.com', '$2a$10$placeholder.participant.priya', 'Participant');
GO

INSERT INTO dbo.Events (OrganiserID, Name, Description, EventDate, Location, Distance, EventType)
VALUES
(1, 'Metro Spring Marathon 2026', 'Annual city marathon.', '2026-10-15T07:00:00', 'Cape Town', 42.19, 'Run'),
(1, 'Alpine Trail Challenge', 'Mountain trail race.', '2026-11-22T08:30:00', 'Drakensberg', 21.10, 'Run'),
(4, 'Harbourside Community Fun Walk', 'Community walk along the harbour.', '2026-11-01T08:00:00', 'Cape Town', 5.00, 'Walk'),
(4, 'Highveld Gran Fondo Cycle Tour', 'Road cycling event through the Highveld.', '2026-11-29T06:30:00', 'Johannesburg', 80.00, 'Cycle');
GO

INSERT INTO dbo.Categories (EventID, CategoryName, MinAge, MaxAge, Distance)
VALUES
(1, 'Elite Full Marathon', 18, 65, 42.19),
(1, 'Community 10K', 12, NULL, 10.00),
(2, 'Half-Trail Pro', 18, NULL, 21.10),
(2, 'Trail Sprint 10K', 16, NULL, 10.50),
(3, 'Family Fun Walk 5K', NULL, NULL, 5.00),
(3, 'Senior Walk 3K', 50, NULL, 3.00),
(4, 'Gran Fondo 80K', 18, NULL, 80.00),
(4, 'Half Fondo 40K', 16, NULL, 40.00);
GO

INSERT INTO dbo.EventRoutes (EventID, RouteName, Distance, RouteDescription, RouteMapUrl)
VALUES
(1, 'Full Marathon Course', 42.19, 'Published event route details can be added here.', NULL),
(1, 'Community 10K Course', 10.00, 'Published event route details can be added here.', NULL),
(2, 'Half-Trail Course', 21.10, 'Published event route details can be added here.', NULL),
(2, 'Trail Sprint Course', 10.50, 'Published event route details can be added here.', NULL),
(3, 'Family Walk Course', 5.00, 'Published event route details can be added here.', NULL),
(3, 'Senior Walk Course', 3.00, 'Published event route details can be added here.', NULL),
(4, 'Gran Fondo Course', 80.00, 'Published event route details can be added here.', NULL),
(4, 'Half Fondo Course', 40.00, 'Published event route details can be added here.', NULL);
GO

INSERT INTO dbo.Enrolments (EventID, ParticipantID, CategoryID, Status)
VALUES
(1, 2, 1, 'Confirmed'),
(1, 3, 2, 'Confirmed'),
(2, 2, 3, 'Pending'),
(2, 5, 4, 'Confirmed'),
(3, 3, 5, 'Confirmed'),
(4, 5, 7, 'Cancelled'),
(4, 2, 8, 'Pending');
GO

INSERT INTO dbo.Results (EnrolmentID, FinishTime, Position)
VALUES
(1, '03:12:45', 1),
(2, '00:45:10', 1),
(5, '01:05:00', 3);
GO

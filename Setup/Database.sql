CREATE TABLE tblZendeskAssigneeChange (
	AssigneeChange_ID int IDENTITY(1,1) NOT NULL,
	Ticket_ID bigint NULL,
	Zendesk_Identifier varchar(255) NULL,
	[User] varchar(500) NULL,
	Old_Assignee varchar(50) NULL,
	New_Assignee varchar(50) NULL,
	Created_Date datetime NULL,
	By_Customer bit NULL,
	CONSTRAINT PK_tblZendeskAssigneeChange PRIMARY KEY CLUSTERED (AssigneeChange_ID ASC)
) 
GO

CREATE TABLE tblZendeskComment (
	Comment_ID bigint IDENTITY(1,1) NOT NULL,
	Ticket_ID bigint NULL,
	Zendesk_Identifier varchar(255) NULL,
	[User] varchar(500) NULL,
	Type varchar(50) NULL,
	Created_Date datetime NULL,
	CONSTRAINT PK_tblZendeskComment PRIMARY KEY CLUSTERED (Comment_ID ASC)
) 
GO

CREATE TABLE tblZendeskGroupChange (
	GroupChange_ID int IDENTITY(1,1) NOT NULL,
	Ticket_ID bigint NULL,
	Zendesk_Identifier varchar(255) NULL,
	[User] varchar(500) NULL,
	Old_Group varchar(50) NULL,
	New_Group varchar(50) NULL,
	Created_Date datetime NULL,
	By_Customer bit NULL,
	CONSTRAINT PK_tblZendeskgroupchange PRIMARY KEY CLUSTERED (GroupChange_ID ASC)
)
GO

CREATE TABLE tblZendeskStatusChange (
	StatusChange_ID int IDENTITY(1,1) NOT NULL,
	Ticket_ID bigint NULL,
	Zendesk_Identifier varchar(255) NULL,
	[User] varchar(500) NULL,
	Old_Status varchar(50) NULL,
	New_Status varchar(50) NULL,
	Created_Date datetime NULL,
	By_Customer bit NULL,
	CONSTRAINT PK_tblZendeskStatusChange PRIMARY KEY CLUSTERED (StatusChange_ID ASC) 
)
GO

CREATE TABLE tblZendeskTicket (
	Ticket_ID bigint NOT NULL,
	Organization varchar(500) NULL,
	Subject varchar(4000) NULL,
	Created_Date datetime NULL,
	Tags varchar(4000) NULL,
	Category varchar(500) NULL,
	[Group] varchar(500) NULL,
	Status varchar(100) NULL,
	Modified_Date datetime NULL,
	Inserted_Date datetime NULL,
	Assignee varchar(500) NULL,
	Priority varchar(50) NULL,
	Satisfaction_Comments varchar(max) NULL,
	Satisfaction_Rating varchar(50) NULL,
	Satisfaction_Date datetime NULL,
	Action varchar(500) NULL,
	Close_Code varchar(500) NULL,
	CONSTRAINT PK_tblZendeskTicket PRIMARY KEY CLUSTERED (Ticket_ID ASC)
) 
GO

CREATE TABLE tblZendeskTimelog (
	Timelog_ID INT IDENTITY(1,1) NOT NULL,
	Ticket_ID BIGINT NOT NULL,
	Zendesk_Identifier VARCHAR(255) NULL,
	[User] VARCHAR(500) NULL,
	Duration INT NULL,
	Created_Date DATETIME NULL,
	CONSTRAINT PK_tblZendeskTimelog PRIMARY KEY CLUSTERED (Timelog_ID ASC) 
) 
GO

ALTER TABLE tblZendeskAssigneeChange  WITH NOCHECK ADD  CONSTRAINT FK_assigneechange_ticket_CASCADE FOREIGN KEY(Ticket_ID) 
	REFERENCES tblZendeskTicket (Ticket_ID) 
	ON DELETE CASCADE
GO

ALTER TABLE tblZendeskAssigneeChange CHECK CONSTRAINT FK_assigneechange_ticket_CASCADE
GO

ALTER TABLE tblZendeskComment  WITH NOCHECK ADD  CONSTRAINT FK_comment_ticket_CASCADE FOREIGN KEY(Ticket_ID)
	REFERENCES tblZendeskTicket (Ticket_ID)
	ON DELETE CASCADE
GO

ALTER TABLE tblZendeskComment CHECK CONSTRAINT FK_comment_ticket_CASCADE
GO

ALTER TABLE tblZendeskGroupChange  WITH NOCHECK ADD  CONSTRAINT FK_groupchange_ticket_CASCADE FOREIGN KEY(Ticket_ID)
	REFERENCES tblZendeskTicket (Ticket_ID)
	ON DELETE CASCADE
GO

ALTER TABLE tblZendeskGroupChange CHECK CONSTRAINT FK_groupchange_ticket_CASCADE
GO

ALTER TABLE tblZendeskStatusChange  WITH NOCHECK ADD  CONSTRAINT FK_statuschange_ticket_CASCADE FOREIGN KEY(Ticket_ID)
	REFERENCES tblZendeskTicket (Ticket_ID)
	ON DELETE CASCADE
GO

ALTER TABLE tblZendeskStatusChange CHECK CONSTRAINT FK_statuschange_ticket_CASCADE
GO

ALTER TABLE tblZendeskTimelog  WITH NOCHECK ADD  CONSTRAINT FK_timelog_ticket_CASCADE FOREIGN KEY(Ticket_ID)
	REFERENCES tblZendeskTicket (Ticket_ID)
	ON DELETE CASCADE
GO

ALTER TABLE tblZendeskTimelog CHECK CONSTRAINT FK_timelog_ticket_CASCADE
GO

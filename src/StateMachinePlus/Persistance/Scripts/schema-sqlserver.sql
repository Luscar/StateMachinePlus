-- Schema de persistance de StateMachinePlus (SQL Server).
-- A adapter au besoin (schema, nommage) avant execution dans la base du client.

CREATE TABLE dbo.InstanceProcessus
(
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InstanceProcessus PRIMARY KEY,
    CodeDefinition      NVARCHAR(200)    NOT NULL,
    VersionDefinition   INT              NOT NULL,
    NoeudCourantId      NVARCHAR(200)    NOT NULL,
    Statut              INT              NOT NULL,
    VariablesJson       NVARCHAR(MAX)    NOT NULL,
    DateCreation        DATETIME2        NOT NULL,
    DateModification    DATETIME2        NOT NULL,
    DateEcheance        DATETIME2        NULL,
    NomSignalAttendu    NVARCHAR(200)    NULL,
    IdTacheExterne      NVARCHAR(200)    NULL,
    InstanceParentId    UNIQUEIDENTIFIER NULL,
    NoeudParentId       NVARCHAR(200)    NULL,
    MessageErreur       NVARCHAR(MAX)    NULL
);

CREATE INDEX IX_InstanceProcessus_Statut_DateEcheance
    ON dbo.InstanceProcessus (Statut, DateEcheance)
    WHERE DateEcheance IS NOT NULL;

CREATE INDEX IX_InstanceProcessus_Statut_NomSignalAttendu
    ON dbo.InstanceProcessus (Statut, NomSignalAttendu)
    WHERE NomSignalAttendu IS NOT NULL;

CREATE INDEX IX_InstanceProcessus_InstanceParentId
    ON dbo.InstanceProcessus (InstanceParentId)
    WHERE InstanceParentId IS NOT NULL;

CREATE TABLE dbo.HistoriqueExecution
(
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_HistoriqueExecution PRIMARY KEY,
    InstanceId      UNIQUEIDENTIFIER NOT NULL,
    NoeudId         NVARCHAR(200)    NOT NULL,
    TypeNoeud       NVARCHAR(50)     NOT NULL,
    DateExecution   DATETIME2        NOT NULL,
    Succes          BIT              NOT NULL,
    Message         NVARCHAR(MAX)    NULL,
    CONSTRAINT FK_HistoriqueExecution_InstanceProcessus
        FOREIGN KEY (InstanceId) REFERENCES dbo.InstanceProcessus (Id)
);

CREATE INDEX IX_HistoriqueExecution_InstanceId
    ON dbo.HistoriqueExecution (InstanceId);

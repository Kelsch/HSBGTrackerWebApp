-- HSBGTracker schema.
-- Visibility values match Core.Snapshots.ResultVisibility: 0 = Private, 1 = Public.

CREATE TABLE Users (
    Id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_Id DEFAULT NEWID() PRIMARY KEY,
    DisplayName        NVARCHAR(100)    NOT NULL,
    BattleTag          NVARCHAR(100)    NULL,       -- matched against opponent names captured from Power.log, to cross-link
    ApiKeyHash         VARBINARY(32)    NOT NULL,   -- SHA-256 hash of the API key - the raw key is shown once, at creation, and never stored
    DefaultVisibility  TINYINT          NOT NULL CONSTRAINT DF_Users_DefaultVisibility DEFAULT 1,
    CreatedAtUtc       DATETIME2        NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT SYSUTCDATETIME()
);

-- A friend's BattleTag should only ever map to one account.
CREATE UNIQUE INDEX UQ_Users_BattleTag ON Users(BattleTag) WHERE BattleTag IS NOT NULL;

CREATE TABLE Games (
    Id                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Games_Id DEFAULT NEWID() PRIMARY KEY,
    ClientGameId           NVARCHAR(100)    NOT NULL,  -- Hearthstone's own game id, from the log - used for upload dedupe
    OwnerUserId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Games_Owner REFERENCES Users(Id),
    Visibility             TINYINT          NOT NULL,
    PlayedAtUtc            DATETIME2        NOT NULL,
    Placement              INT              NOT NULL,

    MyBoardJson            NVARCHAR(MAX)    NOT NULL,  -- serialized Core.Snapshots.BoardSnapshot
    MyBoardScore           FLOAT            NULL,

    OpponentBoardJson      NVARCHAR(MAX)    NOT NULL,  -- board from the final combat of the game - always populated
    OpponentBoardScore     FLOAT            NULL,
    OpponentPlayerName     NVARCHAR(100)    NOT NULL,
    OpponentOwnerUserId    UNIQUEIDENTIFIER NULL CONSTRAINT FK_Games_OpponentOwner REFERENCES Users(Id),

    CreatedAtUtc           DATETIME2        NOT NULL CONSTRAINT DF_Games_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

    -- Re-uploading the same game (e.g. a retried upload after a dropped connection) should
    -- be a no-op, not a duplicate row.
    CONSTRAINT UQ_Games_Owner_ClientGameId UNIQUE (OwnerUserId, ClientGameId)
);

CREATE INDEX IX_Games_Owner_PlayedAt      ON Games(OwnerUserId, PlayedAtUtc DESC);
CREATE INDEX IX_Games_Visibility_PlayedAt ON Games(Visibility, PlayedAtUtc DESC);

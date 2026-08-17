-- HSBGTracker schema (SQLite)
-- Visibility: 0 = Private, 1 = Public (same as Core.Snapshots.ResultVisibility)

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Users (
    Id                 TEXT            NOT NULL PRIMARY KEY,           -- store Guid as string
    DisplayName        TEXT            NOT NULL,
    BattleTag          TEXT            NULL,                          -- matched against opponent names from Power.log
    ApiKeyHash         BLOB            NOT NULL,                      -- SHA-256 of the API key
    DefaultVisibility  INTEGER         NOT NULL DEFAULT 1,
    CreatedAtUtc       TEXT            NOT NULL DEFAULT (datetime('now'))
);

-- A friend's BattleTag should only ever map to one account
CREATE UNIQUE INDEX IF NOT EXISTS UQ_Users_BattleTag ON Users(BattleTag) WHERE BattleTag IS NOT NULL;

CREATE TABLE IF NOT EXISTS Games (
    Id                     TEXT            NOT NULL PRIMARY KEY,
    ClientGameId           TEXT            NOT NULL,
    OwnerUserId            TEXT            NOT NULL REFERENCES Users(Id),
    Visibility             INTEGER         NOT NULL,
    PlayedAtUtc            TEXT            NOT NULL,

    Placement              INTEGER         NOT NULL,

    MyBoardJson            TEXT            NOT NULL,
    MyBoardScore           REAL            NULL,

    OpponentBoardJson      TEXT            NOT NULL,
    OpponentBoardScore     REAL            NULL,
    OpponentPlayerName     TEXT            NOT NULL,
    OpponentOwnerUserId    TEXT            NULL REFERENCES Users(Id),

    CreatedAtUtc           TEXT            NOT NULL DEFAULT (datetime('now')),

    CONSTRAINT UQ_Games_Owner_ClientGameId UNIQUE (OwnerUserId, ClientGameId)
);

CREATE INDEX IF NOT EXISTS IX_Games_Owner_PlayedAt      ON Games(OwnerUserId, PlayedAtUtc DESC);
CREATE INDEX IF NOT EXISTS IX_Games_Visibility_PlayedAt ON Games(Visibility, PlayedAtUtc DESC);
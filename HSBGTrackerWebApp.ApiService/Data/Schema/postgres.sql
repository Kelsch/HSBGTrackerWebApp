-- HSBGTracker schema (PostgreSQL). Visibility values match Core.Snapshots.ResultVisibility:
-- 0 = Private, 1 = Public. Column/table names are unquoted throughout, so Postgres folds
-- them to lowercase consistently between this DDL and the Dapper queries in Data/ - Dapper
-- maps result columns to C# properties case-insensitively, so nothing else needs to change.

CREATE EXTENSION IF NOT EXISTS pgcrypto; -- provides gen_random_uuid() on Postgres < 13

CREATE TABLE Users (
    Id                 UUID             NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    DisplayName        VARCHAR(100)     NOT NULL,
    BattleTag          VARCHAR(100)     NULL,       -- matched against opponent names captured from Power.log, to cross-link
    ApiKeyHash         BYTEA            NOT NULL,   -- SHA-256 hash of the API key - the raw key is shown once, at creation
    DefaultVisibility  SMALLINT         NOT NULL DEFAULT 1,
    CreatedAtUtc       TIMESTAMPTZ      NOT NULL DEFAULT now()
);

-- A friend's BattleTag should only ever map to one account.
CREATE UNIQUE INDEX UQ_Users_BattleTag ON Users(BattleTag) WHERE BattleTag IS NOT NULL;

CREATE TABLE Games (
    Id                     UUID             NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    ClientGameId           VARCHAR(100)     NOT NULL,  -- Hearthstone's own game id, from the log - used for upload dedupe
    OwnerUserId            UUID             NOT NULL REFERENCES Users(Id),
    Visibility             SMALLINT         NOT NULL,
    PlayedAtUtc            TIMESTAMPTZ      NOT NULL,
    Placement              INT              NOT NULL,

    MyBoardJson            TEXT             NOT NULL,  -- serialized Core.Snapshots.BoardSnapshot
    MyBoardScore           DOUBLE PRECISION NULL,

    OpponentBoardJson      TEXT             NOT NULL,  -- board from the final combat of the game - always populated
    OpponentBoardScore     DOUBLE PRECISION NULL,
    OpponentPlayerName     VARCHAR(100)     NOT NULL,
    OpponentOwnerUserId    UUID             NULL REFERENCES Users(Id),

    CreatedAtUtc           TIMESTAMPTZ      NOT NULL DEFAULT now(),

    -- Re-uploading the same game (e.g. a retried upload after a dropped connection) should
    -- be a no-op, not a duplicate row.
    CONSTRAINT UQ_Games_Owner_ClientGameId UNIQUE (OwnerUserId, ClientGameId)
);

CREATE INDEX IX_Games_Owner_PlayedAt      ON Games(OwnerUserId, PlayedAtUtc DESC);
CREATE INDEX IX_Games_Visibility_PlayedAt ON Games(Visibility, PlayedAtUtc DESC);

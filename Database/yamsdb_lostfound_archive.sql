-- Run once on an existing YAMSDB. Keeps all Lost & Found records and claims.
USE YAMSDB;

ALTER TABLE LostFoundItems
    ADD COLUMN ArchivedAt DATETIME(6) NULL AFTER DateClaimed,
    ADD INDEX idx_lostfound_archived (ArchivedAt);

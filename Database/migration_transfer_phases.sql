-- ============================================================================
-- MIGRATION: UNIT TRANSFER PROCESS PHASE
-- ----------------------------------------------------------------------------
-- Run this ONCE against an existing database that was built before the transfer
-- process phase existed. It is NOT part of ynclino_schema.sql: that script
-- already creates the column and constraints, so running both would fail on a
-- duplicate column.
--
--   Use ynclino_schema.sql  -> building the database from nothing
--   Use this file           -> you already have data you do not want to lose
--
-- WHY THIS EXISTS
-- The old design had no middle state: approving a request moved the tenant in
-- the same click, so 'Approved' meant "already moved". The new design splits
-- that into a decision (Approved) and the move itself (Completed). Existing
-- rows therefore have to be re-read: an old 'Approved' row is a new 'Completed'
-- one.
--
-- THE ORDER MATTERS
-- The UPDATE must run BEFORE the CHECK constraints are added. Those old
-- 'Approved' rows have no DateCompleted, so adding the constraints first would
-- reject the very rows this script is meant to fix.
-- ============================================================================

-- 1. add the new column
ALTER TABLE UnitTransferRequests
    ADD COLUMN DateCompleted DATETIME(6) NULL AFTER DateReviewed;

-- 2. Re-interpret the history. Every existing 'Approved' row is really a
--    finished move. The only date available for it is the date it was reviewed.
UPDATE UnitTransferRequests
   SET DateCompleted = DateReviewed,
       Status        = 'Completed'
 WHERE Status = 'Approved';

-- 3. only now is it safe to enforce the state machine
ALTER TABLE UnitTransferRequests
    ADD CONSTRAINT chk_transfer_status CHECK (
        Status IN ('Pending', 'Approved', 'Completed', 'Rejected', 'Cancelled')
    ),
    ADD CONSTRAINT chk_transfer_completed_after_review CHECK (
        DateCompleted IS NULL OR DateReviewed IS NOT NULL
    ),
    ADD CONSTRAINT chk_transfer_completed_date CHECK (
        (Status = 'Completed' AND DateCompleted IS NOT NULL) OR
        (Status <> 'Completed' AND DateCompleted IS NULL)
    ),
    ADD INDEX idx_transfer_open (RequestedUnitID, Status);

-- 4. Re-derive unit reservations. 'Approved' now holds a unit as Reserved, so
--    any unit with an open request against it must reflect that. The
--    application does this in UnitStatusHelper; this brings existing rows in
--    line without waiting for the next save.
UPDATE Units u
   SET u.Status = 'Reserved'
 WHERE u.Status = 'Available'
   AND EXISTS (
           SELECT 1 FROM UnitTransferRequests r
            WHERE r.RequestedUnitID = u.UnitID
              AND r.Status IN ('Pending', 'Approved')
       );

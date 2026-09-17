-- Run against an existing YAMSDB after backing it up, before deploying the
-- application version that removes the four TenantProfiles date columns.
-- This preserves existing profile dates on the tenant's latest assignment.
USE YAMSDB;

-- A profile with dates but no unit or assignment cannot be represented in
-- TenantUnitAssignments. Stop before dropping columns if one exists.
DELIMITER $$
CREATE PROCEDURE CheckTenantDateMigration()
BEGIN
    IF EXISTS (
        SELECT 1 FROM TenantProfiles t
        WHERE t.UnitID IS NULL
          AND (t.MoveInDate IS NOT NULL OR t.MoveOutDate IS NOT NULL
               OR t.LeaseStart IS NOT NULL OR t.LeaseEnd IS NOT NULL)
          AND NOT EXISTS (
              SELECT 1 FROM TenantUnitAssignments a WHERE a.TenantID = t.TenantID
          )
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Tenant dates exist without a unit or assignment; resolve these rows before dropping columns';
    END IF;
END$$
DELIMITER ;
CALL CheckTenantDateMigration();
DROP PROCEDURE CheckTenantDateMigration;

START TRANSACTION;

UPDATE TenantUnitAssignments a
JOIN (
    SELECT TenantID, MAX(AssignmentID) AS AssignmentID
    FROM TenantUnitAssignments
    GROUP BY TenantID
) latest ON latest.AssignmentID = a.AssignmentID
JOIN TenantProfiles t ON t.TenantID = a.TenantID
SET a.MoveInDate = COALESCE(t.MoveInDate, a.MoveInDate),
    a.MoveOutDate = COALESCE(t.MoveOutDate, a.MoveOutDate),
    a.LeaseStart = COALESCE(t.LeaseStart, a.LeaseStart),
    a.LeaseEnd = COALESCE(t.LeaseEnd, a.LeaseEnd);

INSERT INTO TenantUnitAssignments
    (TenantID, UnitID, MoveInDate, MoveOutDate, LeaseStart, LeaseEnd, Status)
SELECT t.TenantID, t.UnitID, t.MoveInDate, t.MoveOutDate,
       t.LeaseStart, t.LeaseEnd,
       CASE WHEN t.Status = 'Active' THEN 'Active' ELSE 'Ended' END
FROM TenantProfiles t
WHERE t.UnitID IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM TenantUnitAssignments a WHERE a.TenantID = t.TenantID
  );

COMMIT;

-- Inspect the migrated rows before running this final schema change.
-- MySQL DDL commits implicitly, so keep it separate from the data transaction.
ALTER TABLE TenantProfiles
    DROP COLUMN MoveInDate,
    DROP COLUMN MoveOutDate,
    DROP COLUMN LeaseStart,
    DROP COLUMN LeaseEnd;

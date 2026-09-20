-- Existing databases ONLY. Do not run ynclino_schema.sql: it drops YAMSDB.
-- 1. Stop the application and take a full database backup first.
-- 2. Run this complete script in MySQL Workbench against your localhost server.
-- 3. Start the updated application only after this script succeeds.
-- This migration does not delete tenants or assignment history and does not
-- guess which unit is correct if existing data disagrees. It aborts instead.
-- MySQL DDL commits implicitly: rollback requires your backup, not ROLLBACK.
USE YAMSDB;

DELIMITER $$
CREATE PROCEDURE migrate_remove_tenantprofiles_unitid()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'TenantProfiles'
          AND COLUMN_NAME = 'UnitID'
    ) THEN
        SELECT 'UnitID is already absent; no changes made.' AS Result;
    ELSE
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'TenantUnitAssignments'
              AND INDEX_NAME = 'uq_tenant_one_active_assignment'
              AND COLUMN_NAME = 'ActiveTenantID' AND NON_UNIQUE = 0
        ) THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'STOP: the unique active-assignment safeguard is missing.';
        END IF;

        IF EXISTS (
            SELECT 1 FROM TenantProfiles t
            LEFT JOIN TenantUnitAssignments a
                ON a.TenantID = t.TenantID AND a.Status = 'Active'
            WHERE (t.Status = 'Active' AND NOT (t.UnitID <=> a.UnitID))
               OR (t.Status <> 'Active' AND a.AssignmentID IS NOT NULL)
               OR (t.Status <> 'Active' AND t.UnitID IS NOT NULL AND NOT (
                   t.UnitID <=> (SELECT h.UnitID FROM TenantUnitAssignments h
                       WHERE h.TenantID = t.TenantID ORDER BY h.AssignmentID DESC LIMIT 1)
               ))
        ) THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'STOP: profile units and assignment history disagree. Reconcile them before dropping UnitID.';
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM information_schema.TABLE_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'TenantProfiles'
              AND CONSTRAINT_NAME = 'fk_tenant_profiles_current_unit'
              AND CONSTRAINT_TYPE = 'FOREIGN KEY'
        ) THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'STOP: expected UnitID foreign key was not found; inspect the schema first.';
        END IF;

        ALTER TABLE TenantProfiles
            DROP FOREIGN KEY fk_tenant_profiles_current_unit,
            DROP COLUMN UnitID;
        SELECT 'Migration complete: TenantUnitAssignments now owns current occupancy.' AS Result;
    END IF;
END$$
DELIMITER ;

CALL migrate_remove_tenantprofiles_unitid();
DROP PROCEDURE migrate_remove_tenantprofiles_unitid;

-- If CALL fails and your SQL client stops before DROP PROCEDURE, remove only
-- this helper procedure before rerunning after the reported issue is resolved:
-- DROP PROCEDURE IF EXISTS migrate_remove_tenantprofiles_unitid;

-- Run once in MySQL Workbench against an existing YAMSDB created from the
-- original ynclino_schema.sql. This only adds fields required by current app
-- screens; it does not delete rows or rebuild the database.
USE YAMSDB;

ALTER TABLE TenantProfiles
    ADD COLUMN UnitID INT UNSIGNED NULL AFTER UserID,
    ADD COLUMN FirstName VARCHAR(80) NOT NULL DEFAULT '' AFTER UnitID,
    ADD COLUMN LastName VARCHAR(80) NOT NULL DEFAULT '' AFTER FirstName,
    ADD COLUMN ContactNumber VARCHAR(30) NULL AFTER LastName,
    ADD COLUMN EmergencyContactName VARCHAR(100) NULL AFTER ContactNumber,
    ADD COLUMN EmergencyContactRelationship VARCHAR(50) NULL AFTER EmergencyContactName,
    ADD COLUMN EmergencyContactNumber VARCHAR(20) NULL AFTER EmergencyContactRelationship,
    ADD COLUMN PhotoPath VARCHAR(300) NULL AFTER EmergencyContactNumber,
    ADD CONSTRAINT fk_tenant_profiles_current_unit
        FOREIGN KEY (UnitID) REFERENCES Units(UnitID) ON DELETE RESTRICT;

ALTER TABLE UnitTransferRequests
    ADD COLUMN CurrentUnitID INT UNSIGNED NULL AFTER RequestedUnitID,
    ADD CONSTRAINT fk_transfer_current_unit
        FOREIGN KEY (CurrentUnitID) REFERENCES Units(UnitID) ON DELETE RESTRICT;

ALTER TABLE Billings
    ADD COLUMN AmountPaid DECIMAL(10,2) NULL AFTER AmountDue;

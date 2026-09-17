-- =============================================================================
--  YNCLINO APARTMENT MANAGEMENT SYSTEM — database schema
--  Bayotas · Lariosa · Frejoles · Palicte   |   BSIT, Asian College of Technology
--
--  Generated from the application's own models, so this is the same shape the
--  program builds for itself — no hand-written drift between the two.
--
--  MySQL 8 / MariaDB 10.4+.  Run it as a whole:
--      mysql -u root -p < ynclino_schema.sql
--
--  It creates the database, the nine tables, their keys and their relationships,
--  and (at the end, clearly marked) the one administrator account you need in
--  order to sign in the first time.
-- =============================================================================

DROP DATABASE IF EXISTS `YnclinoApartmentManagementSystemDb`;
CREATE DATABASE `YnclinoApartmentManagementSystemDb`
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_general_ci;
USE `YnclinoApartmentManagementSystemDb`;

-- Tables are created parents-first, so every foreign key has something to point
-- at by the time it is declared.

-- -----------------------------------------------------------------------------
-- tblUnits
--   The apartment units themselves. Status is Available, Occupied,
--   Reserved or Under Maintenance.
-- -----------------------------------------------------------------------------
CREATE TABLE `tblUnits` (
  `UnitID` int NOT NULL AUTO_INCREMENT,
  `UnitNumber` varchar(20) NOT NULL,
  `UnitType` varchar(50) NOT NULL,
  `RentPrice` decimal(10,2) NOT NULL,
  `Deposit` decimal(10,2) NOT NULL,
  `AdvancePayment` decimal(10,2) NOT NULL,
  `Capacity` int NOT NULL,
  `Status` varchar(20) NOT NULL DEFAULT 'Available',
  `DateAdded` datetime(6) NOT NULL,
  PRIMARY KEY (`UnitID`),
  UNIQUE KEY `IX_tblUnits_UnitNumber` (`UnitNumber`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- tblUsers
--   Every account that can sign in. Role is Admin, Maintenance or Tenant.
--   IsMainAdmin marks the one account that cannot be deleted or deactivated.
--   MustChangePassword forces a new password on the next sign-in.
-- -----------------------------------------------------------------------------
CREATE TABLE `tblUsers` (
  `UserID` int NOT NULL AUTO_INCREMENT,
  `Username` varchar(50) NOT NULL,
  `Password` varchar(255) NOT NULL,
  `Role` varchar(20) NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `MustChangePassword` tinyint(1) NOT NULL,
  `IsMainAdmin` tinyint(1) NOT NULL,
  `DateCreated` datetime(6) NOT NULL,
  PRIMARY KEY (`UserID`),
  UNIQUE KEY `IX_tblUsers_Username` (`Username`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- tblLostFoundItems
--   The lost & found board. Status is Reported or Claimed.
-- -----------------------------------------------------------------------------
CREATE TABLE `tblLostFoundItems` (
  `ItemID` int NOT NULL AUTO_INCREMENT,
  `ReportedByUserID` int NOT NULL,
  `ItemName` varchar(100) NOT NULL,
  `Description` varchar(500) DEFAULT NULL,
  `ItemType` varchar(10) NOT NULL,
  `Location` varchar(200) DEFAULT NULL,
  `Status` varchar(20) NOT NULL DEFAULT 'Reported',
  `DateReported` datetime(6) NOT NULL,
  `ClaimedByUserID` int DEFAULT NULL,
  `DateClaimed` datetime(6) DEFAULT NULL,
  `Notes` varchar(500) DEFAULT NULL,
  `ImagePath` varchar(260) DEFAULT NULL,
  PRIMARY KEY (`ItemID`),
  KEY `IX_tblLostFoundItems_ClaimedByUserID` (`ClaimedByUserID`),
  KEY `IX_tblLostFoundItems_ReportedByUserID` (`ReportedByUserID`),
  CONSTRAINT `FK_tblLostFoundItems_tblUsers_ClaimedByUserID` FOREIGN KEY (`ClaimedByUserID`) REFERENCES `tblUsers` (`UserID`),
  CONSTRAINT `FK_tblLostFoundItems_tblUsers_ReportedByUserID` FOREIGN KEY (`ReportedByUserID`) REFERENCES `tblUsers` (`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- tblTenants
--   A tenant's record: who they are, which unit, and the advance payment
--   they are holding. UserID is their login; UnitID is null before they move in.
-- -----------------------------------------------------------------------------
CREATE TABLE `tblTenants` (
  `TenantID` int NOT NULL AUTO_INCREMENT,
  `UserID` int DEFAULT NULL,
  `UnitID` int DEFAULT NULL,
  `FirstName` varchar(50) NOT NULL,
  `LastName` varchar(50) NOT NULL,
  `ContactNumber` varchar(20) DEFAULT NULL,
  `EmergencyContactName` varchar(100) DEFAULT NULL,
  `EmergencyContactRelationship` varchar(50) DEFAULT NULL,
  `EmergencyContactNumber` varchar(20) DEFAULT NULL,
  `MoveInDate` datetime(6) DEFAULT NULL,
  `MoveOutDate` datetime(6) DEFAULT NULL,
  `LeaseStart` datetime(6) DEFAULT NULL,
  `LeaseEnd` datetime(6) DEFAULT NULL,
  `Status` varchar(20) NOT NULL DEFAULT 'Active',
  `AdvanceCredit` decimal(10,2) NOT NULL,
  `DateRecorded` datetime(6) NOT NULL,
  `PhotoPath` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`TenantID`),
  KEY `IX_tblTenants_UnitID` (`UnitID`),
  KEY `IX_tblTenants_UserID` (`UserID`),
  CONSTRAINT `FK_tblTenants_tblUnits_UnitID` FOREIGN KEY (`UnitID`) REFERENCES `tblUnits` (`UnitID`),
  CONSTRAINT `FK_tblTenants_tblUsers_UserID` FOREIGN KEY (`UserID`) REFERENCES `tblUsers` (`UserID`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- tblUnitTransferRequests
--   A tenant asking to move, or applying for a first unit.
--   Archived per side, like maintenance.
-- -----------------------------------------------------------------------------
CREATE TABLE `tblUnitTransferRequests` (
  `TransferID` int NOT NULL AUTO_INCREMENT,
  `TenantID` int NOT NULL,
  `CurrentUnitID` int DEFAULT NULL,
  `RequestedUnitID` int NOT NULL,
  `Reason` varchar(500) NOT NULL,
  `Status` varchar(20) NOT NULL DEFAULT 'Pending',
  `DateRequested` datetime(6) NOT NULL,
  `DateReviewed` datetime(6) DEFAULT NULL,
  `TenantArchivedAt` datetime(6) DEFAULT NULL,
  `StaffArchivedAt` datetime(6) DEFAULT NULL,
  `AdminNotes` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`TransferID`),
  KEY `IX_tblUnitTransferRequests_CurrentUnitID` (`CurrentUnitID`),
  KEY `IX_tblUnitTransferRequests_RequestedUnitID` (`RequestedUnitID`),
  KEY `IX_tblUnitTransferRequests_TenantID` (`TenantID`),
  CONSTRAINT `FK_tblUnitTransferRequests_tblTenants_TenantID` FOREIGN KEY (`TenantID`) REFERENCES `tblTenants` (`TenantID`) ON DELETE CASCADE,
  CONSTRAINT `FK_tblUnitTransferRequests_tblUnits_CurrentUnitID` FOREIGN KEY (`CurrentUnitID`) REFERENCES `tblUnits` (`UnitID`),
  CONSTRAINT `FK_tblUnitTransferRequests_tblUnits_RequestedUnitID` FOREIGN KEY (`RequestedUnitID`) REFERENCES `tblUnits` (`UnitID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- tblBillings
--   One row per bill. AmountPaid is what was paid against THIS bill;
--   AdvanceFromOverpayment is the part of a payment that went past it and became
--   credit. ArchivedAt files a settled bill out of the working list without
--   removing it from the tenant's record or the reports.
-- -----------------------------------------------------------------------------
CREATE TABLE `tblBillings` (
  `BillingID` int NOT NULL AUTO_INCREMENT,
  `TenantID` int NOT NULL,
  `BillingPeriod` datetime(6) NOT NULL,
  `AmountDue` decimal(10,2) NOT NULL,
  `Deposit` decimal(10,2) NOT NULL,
  `Advance` decimal(10,2) NOT NULL,
  `DueDate` datetime(6) NOT NULL,
  `AmountPaid` decimal(10,2) DEFAULT NULL,
  `AdvanceFromOverpayment` decimal(10,2) NOT NULL,
  `IssuedFromAdvance` tinyint(1) NOT NULL,
  `DatePaid` datetime(6) DEFAULT NULL,
  `Status` varchar(20) NOT NULL DEFAULT 'Unpaid',
  `Notes` varchar(500) DEFAULT NULL,
  `DateIssued` datetime(6) NOT NULL,
  `ArchivedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`BillingID`),
  KEY `IX_tblBillings_TenantID` (`TenantID`),
  CONSTRAINT `FK_tblBillings_tblTenants_TenantID` FOREIGN KEY (`TenantID`) REFERENCES `tblTenants` (`TenantID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- tblClaimRequests
--   Someone claiming a found item, with the details they gave to
--   prove it is theirs.
-- -----------------------------------------------------------------------------
CREATE TABLE `tblClaimRequests` (
  `ClaimID` int NOT NULL AUTO_INCREMENT,
  `ItemID` int NOT NULL,
  `ClaimantUserID` int NOT NULL,
  `VerificationDetails` varchar(1000) NOT NULL,
  `SubmittedAt` datetime(6) NOT NULL,
  `Status` varchar(20) NOT NULL DEFAULT 'Pending',
  `AdminNotes` varchar(500) DEFAULT NULL,
  `ImagePath` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`ClaimID`),
  KEY `IX_tblClaimRequests_ClaimantUserID` (`ClaimantUserID`),
  KEY `IX_tblClaimRequests_ItemID` (`ItemID`),
  CONSTRAINT `FK_tblClaimRequests_tblLostFoundItems_ItemID` FOREIGN KEY (`ItemID`) REFERENCES `tblLostFoundItems` (`ItemID`) ON DELETE CASCADE,
  CONSTRAINT `FK_tblClaimRequests_tblUsers_ClaimantUserID` FOREIGN KEY (`ClaimantUserID`) REFERENCES `tblUsers` (`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- tblMaintenanceRequests
--   Repairs. Archived per side: a tenant clearing their own list
--   does not clear the staff's, and the other way round.
-- -----------------------------------------------------------------------------
CREATE TABLE `tblMaintenanceRequests` (
  `RequestID` int NOT NULL AUTO_INCREMENT,
  `TenantID` int NOT NULL,
  `UnitID` int DEFAULT NULL,
  `AssignedStaffID` int DEFAULT NULL,
  `Category` varchar(50) NOT NULL,
  `Description` varchar(500) NOT NULL,
  `Priority` varchar(20) NOT NULL,
  `Status` varchar(30) NOT NULL DEFAULT 'Pending',
  `DateSubmitted` datetime(6) NOT NULL,
  `DateResolved` datetime(6) DEFAULT NULL,
  `TenantArchivedAt` datetime(6) DEFAULT NULL,
  `StaffArchivedAt` datetime(6) DEFAULT NULL,
  `StaffNotes` varchar(500) DEFAULT NULL,
  `ImagePath` varchar(260) DEFAULT NULL,
  PRIMARY KEY (`RequestID`),
  KEY `IX_tblMaintenanceRequests_AssignedStaffID` (`AssignedStaffID`),
  KEY `IX_tblMaintenanceRequests_TenantID` (`TenantID`),
  KEY `IX_tblMaintenanceRequests_UnitID` (`UnitID`),
  CONSTRAINT `FK_tblMaintenanceRequests_tblTenants_TenantID` FOREIGN KEY (`TenantID`) REFERENCES `tblTenants` (`TenantID`) ON DELETE CASCADE,
  CONSTRAINT `FK_tblMaintenanceRequests_tblUnits_UnitID` FOREIGN KEY (`UnitID`) REFERENCES `tblUnits` (`UnitID`),
  CONSTRAINT `FK_tblMaintenanceRequests_tblUsers_AssignedStaffID` FOREIGN KEY (`AssignedStaffID`) REFERENCES `tblUsers` (`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- tblPayments
--   Every payment, kept separately from the bill it settled so a bill can
--   be paid in instalments and still add up.
-- -----------------------------------------------------------------------------
CREATE TABLE `tblPayments` (
  `PaymentID` int NOT NULL AUTO_INCREMENT,
  `BillingID` int NOT NULL,
  `Amount` decimal(10,2) NOT NULL,
  `DatePaid` datetime(6) NOT NULL,
  `Method` varchar(50) DEFAULT NULL,
  `Remarks` varchar(300) DEFAULT NULL,
  `RecordedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`PaymentID`),
  KEY `IX_tblPayments_BillingID` (`BillingID`),
  CONSTRAINT `FK_tblPayments_tblBillings_BillingID` FOREIGN KEY (`BillingID`) REFERENCES `tblBillings` (`BillingID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- =============================================================================
--  THE FIRST ACCOUNT
--
--  Without this the database is complete but nobody can get in. The password is
--  not stored: what follows is a PBKDF2-SHA256 hash (100,000 iterations, with a
--  16-byte salt in front of it) of the starting password below.
--
--      admin        / Admin@123     (signs straight in)
--      maintenance1 / Staff@123     (asked to set its own password first)
--
--  Change the administrator's password from inside the system the first time
--  you sign in. The program can also create both of these by itself if the
--  table is empty, so if you would rather start with nothing, leave this whole
--  section out — everything above it is the schema and stands on its own.
-- =============================================================================

INSERT INTO `tblUsers`
    (`Username`, `Password`, `Role`, `IsActive`, `IsMainAdmin`, `MustChangePassword`, `DateCreated`)
VALUES
    -- the administrator — the one account that cannot be deleted or switched off
    ('admin', 'djYghAu8O+sp9Cvf31GmHYlY3jXA5UjsxHi0K88subOp77uz49xjukaeeYt2LqGh', 'Admin', 1, 1, 0, NOW()),
    -- a maintenance staff account, asked for a new password on first sign-in
    ('maintenance1', 'vzHaWGruR2VMjyAOyLhqOpdwix266UvV3peVJqo+OxKuKTAMYKKMynXHxPb4VOyf', 'Maintenance', 1, 0, 1, NOW());

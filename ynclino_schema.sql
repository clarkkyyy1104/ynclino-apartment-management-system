-- =====================================================================
--  YNCLINO APARTMENT MANAGEMENT SYSTEM
--  Complete database schema (MySQL 8 / MariaDB, InnoDB, utf8mb4)
--
--  Generated from the live database the application builds at startup
--  (EF Core EnsureCreated + the AddColumnIfMissing patches in Program.cs)
--  on branch crud(copy).
--
--  Tables are ordered so that every foreign key target already exists.
--  10 tables:
--    tblUsers  ->  tblUnits  ->  tblTenants  ->  tblBillings  ->  tblPayments
--    tblMaintenanceRequests, tblUnitTransferRequests,
--    tblLostFoundItems -> tblClaimRequests, tblNotifications
-- =====================================================================

CREATE DATABASE IF NOT EXISTS `YnclinoApartmentManagementSystemDb`
    DEFAULT CHARACTER SET utf8mb4
    DEFAULT COLLATE utf8mb4_general_ci;

USE `YnclinoApartmentManagementSystemDb`;


-- ---------------------------------------------------------------------
-- 1. tblUsers — every login account: Admin, Maintenance staff, Tenant
-- ---------------------------------------------------------------------
CREATE TABLE `tblUsers` (
  `UserID`             int(11)      NOT NULL AUTO_INCREMENT,
  `Username`           varchar(50)  NOT NULL,
  `Password`           varchar(255) NOT NULL,   -- BCrypt hash, never plain text
  `Role`               varchar(20)  NOT NULL,   -- Admin | Maintenance | Tenant
  `IsActive`           tinyint(1)   NOT NULL,   -- 0 = deactivated, blocked at login
  `IsMainAdmin`        tinyint(1)   NOT NULL,   -- the one account nobody may disable
  `DateCreated`        datetime(6)  NOT NULL,
  `MustChangePassword` tinyint(1)   NOT NULL DEFAULT 0,
  PRIMARY KEY (`UserID`),
  UNIQUE KEY `IX_tblUsers_Username` (`Username`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


-- ---------------------------------------------------------------------
-- 2. tblUnits — the apartment units themselves
-- ---------------------------------------------------------------------
CREATE TABLE `tblUnits` (
  `UnitID`         int(11)       NOT NULL AUTO_INCREMENT,
  `UnitNumber`     varchar(20)   NOT NULL,
  `UnitType`       varchar(50)   NOT NULL,      -- Studio | 1-Bedroom | ...
  `RentPrice`      decimal(10,2) NOT NULL,
  `Deposit`        decimal(10,2) NOT NULL,      -- held until move-out
  `AdvancePayment` decimal(10,2) NOT NULL,      -- one month, charged at move-in
  `Capacity`       int(11)       NOT NULL,
  `Status`         varchar(20)   NOT NULL DEFAULT 'Vacant',  -- Vacant | Occupied | Under Maintenance
  `DateAdded`      datetime(6)   NOT NULL,
  PRIMARY KEY (`UnitID`),
  UNIQUE KEY `IX_tblUnits_UnitNumber` (`UnitNumber`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


-- ---------------------------------------------------------------------
-- 3. tblTenants — the tenancy record, linked to a login and a unit
-- ---------------------------------------------------------------------
CREATE TABLE `tblTenants` (
  `TenantID`                     int(11)       NOT NULL AUTO_INCREMENT,
  `UserID`                       int(11)       DEFAULT NULL,
  `UnitID`                       int(11)       DEFAULT NULL,
  `FirstName`                    varchar(50)   NOT NULL,
  `LastName`                     varchar(50)   NOT NULL,
  `ContactNumber`                varchar(20)   DEFAULT NULL,
  `EmergencyContactName`         varchar(100)  DEFAULT NULL,
  `EmergencyContactRelationship` varchar(50)   DEFAULT NULL,
  `EmergencyContactNumber`       varchar(20)   DEFAULT NULL,
  `MoveInDate`                   datetime(6)   DEFAULT NULL,
  `MoveOutDate`                  datetime(6)   DEFAULT NULL,
  `LeaseStart`                   datetime(6)   DEFAULT NULL,
  `LeaseEnd`                     datetime(6)   DEFAULT NULL,
  `Status`                       varchar(20)   NOT NULL DEFAULT 'Active',  -- Active | Inactive
  `DateRecorded`                 datetime(6)   NOT NULL,
  `PhotoPath`                    varchar(300)  DEFAULT NULL,
  -- Change from an overpayment, held for this tenant and applied
  -- automatically to their next bill. NOT the move-in advance.
  `AdvanceCredit`                decimal(10,2) NOT NULL DEFAULT 0.00,
  PRIMARY KEY (`TenantID`),
  KEY `IX_tblTenants_UnitID` (`UnitID`),
  KEY `IX_tblTenants_UserID` (`UserID`),
  CONSTRAINT `FK_tblTenants_tblUnits_UnitID`
      FOREIGN KEY (`UnitID`) REFERENCES `tblUnits` (`UnitID`),
  CONSTRAINT `FK_tblTenants_tblUsers_UserID`
      FOREIGN KEY (`UserID`) REFERENCES `tblUsers` (`UserID`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


-- ---------------------------------------------------------------------
-- 4. tblBillings — one bill per tenant per month
-- ---------------------------------------------------------------------
CREATE TABLE `tblBillings` (
  `BillingID`     int(11)       NOT NULL AUTO_INCREMENT,
  `TenantID`      int(11)       NOT NULL,
  `BillingPeriod` datetime(6)   NOT NULL,   -- first day of the month being billed
  `AmountDue`     decimal(10,2) NOT NULL,
  `DueDate`       datetime(6)   NOT NULL,   -- last day of the billing month
  `AmountPaid`    decimal(10,2) DEFAULT NULL,
  `DatePaid`      datetime(6)   DEFAULT NULL,
  `Status`        varchar(20)   NOT NULL DEFAULT 'Unpaid',  -- Unpaid | Partial | Paid | Overdue
  `Notes`         varchar(500)  DEFAULT NULL,
  `DateIssued`    datetime(6)   NOT NULL,
  -- Breakdown of AmountDue on the MOVE-IN bill only. On an ordinary
  -- rent bill both are 0, and Monthly Rent = AmountDue - Deposit - Advance.
  `Deposit`       decimal(10,2) NOT NULL DEFAULT 0.00,
  `Advance`       decimal(10,2) NOT NULL DEFAULT 0.00,
  PRIMARY KEY (`BillingID`),
  KEY `IX_tblBillings_TenantID` (`TenantID`),
  CONSTRAINT `FK_tblBillings_tblTenants_TenantID`
      FOREIGN KEY (`TenantID`) REFERENCES `tblTenants` (`TenantID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


-- ---------------------------------------------------------------------
-- 5. tblPayments — the payment history behind each bill.
--    Method 'Advance' means it was settled from held advance payment
--    rather than money handed over that day.
-- ---------------------------------------------------------------------
CREATE TABLE `tblPayments` (
  `PaymentID`  int(11)       NOT NULL AUTO_INCREMENT,
  `BillingID`  int(11)       NOT NULL,
  `Amount`     decimal(10,2) NOT NULL,
  `DatePaid`   datetime(6)   NOT NULL,
  `Method`     varchar(50)   DEFAULT NULL,   -- Cash | Advance
  `Remarks`    varchar(300)  DEFAULT NULL,
  `RecordedAt` datetime(6)   NOT NULL,
  PRIMARY KEY (`PaymentID`),
  KEY `FK_tblPayments_tblBillings_BillingID` (`BillingID`),
  CONSTRAINT `FK_tblPayments_tblBillings_BillingID`
      FOREIGN KEY (`BillingID`) REFERENCES `tblBillings` (`BillingID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


-- ---------------------------------------------------------------------
-- 6. tblMaintenanceRequests — repair requests raised by tenants
--    or logged by the admin, carried out by maintenance staff.
--    Each side archives independently.
-- ---------------------------------------------------------------------
CREATE TABLE `tblMaintenanceRequests` (
  `RequestID`        int(11)      NOT NULL AUTO_INCREMENT,
  `TenantID`         int(11)      NOT NULL,
  `UnitID`           int(11)      DEFAULT NULL,   -- kept with the APARTMENT, not the tenant
  `AssignedStaffID`  int(11)      DEFAULT NULL,   -- tblUsers.UserID of the Maintenance account
  `Category`         varchar(50)  NOT NULL,       -- Plumbing | Electrical | Structural | Appliance | Other
  `Description`      varchar(500) NOT NULL,
  `Priority`         varchar(20)  NOT NULL,       -- Minor | Moderate | Major | Urgent
  `Status`           varchar(30)  NOT NULL DEFAULT 'Pending',  -- Pending | In Progress | Resolved | Cancelled
  `DateSubmitted`    datetime(6)  NOT NULL,
  `DateResolved`     datetime(6)  DEFAULT NULL,
  `StaffNotes`       varchar(500) DEFAULT NULL,   -- what the staff reported after the work
  `ImagePath`        varchar(260) DEFAULT NULL,
  `TenantArchivedAt` datetime(6)  DEFAULT NULL,   -- NULL = still in the tenant's active list
  `StaffArchivedAt`  datetime(6)  DEFAULT NULL,   -- NULL = still in the admin/staff active list
  PRIMARY KEY (`RequestID`),
  KEY `IX_tblMaintenanceRequests_TenantID` (`TenantID`),
  KEY `IX_tblMaintenanceRequests_UnitID` (`UnitID`),
  KEY `IX_tblMaintenanceRequests_AssignedStaffID` (`AssignedStaffID`),
  CONSTRAINT `FK_tblMaintenanceRequests_tblTenants_TenantID`
      FOREIGN KEY (`TenantID`) REFERENCES `tblTenants` (`TenantID`) ON DELETE CASCADE,
  CONSTRAINT `FK_tblMaintenanceRequests_tblUnits_UnitID`
      FOREIGN KEY (`UnitID`) REFERENCES `tblUnits` (`UnitID`),
  CONSTRAINT `FK_tblMaintenanceRequests_tblUsers_AssignedStaffID`
      FOREIGN KEY (`AssignedStaffID`) REFERENCES `tblUsers` (`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


-- ---------------------------------------------------------------------
-- 7. tblUnitTransferRequests — a tenant asking to move to another unit
-- ---------------------------------------------------------------------
CREATE TABLE `tblUnitTransferRequests` (
  `TransferID`       int(11)      NOT NULL AUTO_INCREMENT,
  `TenantID`         int(11)      NOT NULL,
  `CurrentUnitID`    int(11)      DEFAULT NULL,
  `RequestedUnitID`  int(11)      NOT NULL,
  `Reason`           varchar(500) NOT NULL,
  `Status`           varchar(20)  NOT NULL DEFAULT 'Pending',  -- Pending | Approved | Rejected | Cancelled
  `DateRequested`    datetime(6)  NOT NULL,
  `DateReviewed`     datetime(6)  DEFAULT NULL,
  `AdminNotes`       varchar(500) DEFAULT NULL,
  `TenantArchivedAt` datetime(6)  DEFAULT NULL,
  `StaffArchivedAt`  datetime(6)  DEFAULT NULL,
  PRIMARY KEY (`TransferID`),
  KEY `IX_tblUnitTransferRequests_CurrentUnitID` (`CurrentUnitID`),
  KEY `IX_tblUnitTransferRequests_RequestedUnitID` (`RequestedUnitID`),
  KEY `IX_tblUnitTransferRequests_TenantID` (`TenantID`),
  CONSTRAINT `FK_tblUnitTransferRequests_tblTenants_TenantID`
      FOREIGN KEY (`TenantID`) REFERENCES `tblTenants` (`TenantID`) ON DELETE CASCADE,
  CONSTRAINT `FK_tblUnitTransferRequests_tblUnits_CurrentUnitID`
      FOREIGN KEY (`CurrentUnitID`) REFERENCES `tblUnits` (`UnitID`),
  CONSTRAINT `FK_tblUnitTransferRequests_tblUnits_RequestedUnitID`
      FOREIGN KEY (`RequestedUnitID`) REFERENCES `tblUnits` (`UnitID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


-- ---------------------------------------------------------------------
-- 8. tblLostFoundItems — items reported lost or found in the building
-- ---------------------------------------------------------------------
CREATE TABLE `tblLostFoundItems` (
  `ItemID`           int(11)      NOT NULL AUTO_INCREMENT,
  `ReportedByUserID` int(11)      NOT NULL,
  `ItemName`         varchar(100) NOT NULL,
  `Description`      varchar(500) DEFAULT NULL,
  `ItemType`         varchar(10)  NOT NULL,       -- Lost | Found
  `Location`         varchar(200) DEFAULT NULL,
  `Status`           varchar(20)  NOT NULL DEFAULT 'Reported',  -- Reported | Claimed | Returned | Closed
  `DateReported`     datetime(6)  NOT NULL,
  `Notes`            varchar(500) DEFAULT NULL,
  `ImagePath`        varchar(260) DEFAULT NULL,
  `ClaimedByUserID`  int(11)      DEFAULT NULL,   -- who actually took the item home
  `DateClaimed`      datetime(6)  DEFAULT NULL,
  PRIMARY KEY (`ItemID`),
  KEY `IX_tblLostFoundItems_ReportedByUserID` (`ReportedByUserID`),
  KEY `IX_tblLostFoundItems_ClaimedByUserID` (`ClaimedByUserID`),
  -- Restrict, not Cascade: deleting a user must never erase the
  -- lost & found paper trail.
  CONSTRAINT `FK_tblLostFoundItems_tblUsers_ReportedByUserID`
      FOREIGN KEY (`ReportedByUserID`) REFERENCES `tblUsers` (`UserID`),
  CONSTRAINT `FK_tblLostFoundItems_tblUsers_ClaimedByUserID`
      FOREIGN KEY (`ClaimedByUserID`) REFERENCES `tblUsers` (`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


-- ---------------------------------------------------------------------
-- 9. tblClaimRequests — a tenant claiming a found item, reviewed by admin
-- ---------------------------------------------------------------------
CREATE TABLE `tblClaimRequests` (
  `ClaimID`              int(11)       NOT NULL AUTO_INCREMENT,
  `ItemID`               int(11)       NOT NULL,
  `ClaimantUserID`       int(11)       NOT NULL,
  `VerificationDetails`  varchar(1000) NOT NULL,  -- proof the item is theirs
  `SubmittedAt`          datetime(6)   NOT NULL,
  `Status`               varchar(20)   NOT NULL DEFAULT 'Pending',  -- Pending | Approved | Rejected
  `AdminNotes`           varchar(500)  DEFAULT NULL,
  `ImagePath`            varchar(300)  DEFAULT NULL,
  PRIMARY KEY (`ClaimID`),
  KEY `IX_tblClaimRequests_ClaimantUserID` (`ClaimantUserID`),
  KEY `IX_tblClaimRequests_ItemID` (`ItemID`),
  CONSTRAINT `FK_tblClaimRequests_tblLostFoundItems_ItemID`
      FOREIGN KEY (`ItemID`) REFERENCES `tblLostFoundItems` (`ItemID`) ON DELETE CASCADE,
  CONSTRAINT `FK_tblClaimRequests_tblUsers_ClaimantUserID`
      FOREIGN KEY (`ClaimantUserID`) REFERENCES `tblUsers` (`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


-- ---------------------------------------------------------------------
-- 10. tblNotifications — the alert feed shown on every dashboard
-- ---------------------------------------------------------------------
CREATE TABLE `tblNotifications` (
  `NotificationID` int(11)      NOT NULL AUTO_INCREMENT,
  `UserID`         int(11)      NOT NULL,
  `Module`         varchar(30)  NOT NULL,   -- Billing | Maintenance | LostFound | Transfers | Tenants
  `Message`        varchar(300) NOT NULL,
  `Link`           varchar(300) DEFAULT NULL,
  `TargetId`       int(11)      DEFAULT NULL,  -- the record this alert points at
  `IsRead`         tinyint(1)   NOT NULL,
  `CreatedAt`      datetime(6)  NOT NULL,
  PRIMARY KEY (`NotificationID`),
  KEY `IX_tblNotifications_UserID_IsRead` (`UserID`,`IsRead`),
  CONSTRAINT `FK_tblNotifications_tblUsers_UserID`
      FOREIGN KEY (`UserID`) REFERENCES `tblUsers` (`UserID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

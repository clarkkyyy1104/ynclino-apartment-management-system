-- ============================================================================
-- 1. ROLES
-- ============================================================================

CREATE TABLE Roles (
    RoleID TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
    RoleName VARCHAR(50) NOT NULL,

    PRIMARY KEY (RoleID),
    CONSTRAINT uq_roles_name UNIQUE (RoleName)
);

-- ============================================================================
-- 2. USERS
-- Central account table for Admin, Tenant, and Maintenance users.
-- Role-specific operational data belongs in separate profile/history tables.
-- ============================================================================

CREATE TABLE Users (
    UserID INT UNSIGNED NOT NULL AUTO_INCREMENT,
    RoleID TINYINT UNSIGNED NOT NULL,

    Username VARCHAR(50) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,

    FirstName VARCHAR(80) NOT NULL,
    LastName VARCHAR(80) NOT NULL,
    ContactNumber VARCHAR(30) NULL,

    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    MustChangePassword BOOLEAN NOT NULL DEFAULT TRUE,
    IsMainAdmin BOOLEAN NOT NULL DEFAULT FALSE,

    LastLoginAt DATETIME(6) NULL,
    DateCreated DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    DateUpdated DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (UserID),
    CONSTRAINT uq_users_username UNIQUE (Username),

    CONSTRAINT fk_users_role
        FOREIGN KEY (RoleID)
        REFERENCES Roles(RoleID)
        ON DELETE RESTRICT,

    INDEX idx_users_role (RoleID),
    INDEX idx_users_active (IsActive),
    INDEX idx_users_name (LastName, FirstName)
);

-- ============================================================================
-- 3. UNITS
-- Unit master data. Historical occupancy is stored in TenantUnitAssignments.
-- ============================================================================

CREATE TABLE Units (
    UnitID INT UNSIGNED NOT NULL AUTO_INCREMENT,
    UnitNumber VARCHAR(20) NOT NULL,
    UnitType VARCHAR(50) NOT NULL,

    RentPrice DECIMAL(10,2) NOT NULL,
    Deposit DECIMAL(10,2) NOT NULL,
    AdvancePayment DECIMAL(10,2) NOT NULL,
    Capacity INT UNSIGNED NOT NULL,

    Status VARCHAR(20) NOT NULL DEFAULT 'Available',
    DateAdded DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    DateUpdated DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (UnitID),
    CONSTRAINT uq_units_number UNIQUE (UnitNumber),

    CONSTRAINT chk_units_rent_nonnegative CHECK (RentPrice >= 0),
    CONSTRAINT chk_units_deposit_nonnegative CHECK (Deposit >= 0),
    CONSTRAINT chk_units_advance_nonnegative CHECK (AdvancePayment >= 0),
    CONSTRAINT chk_units_capacity_positive CHECK (Capacity > 0),

    INDEX idx_units_status (Status),
    INDEX idx_units_type (UnitType)
);

-- ============================================================================
-- 4. TENANT PROFILES
-- One tenant profile per user account.
-- Current unit and lease fields support the existing tenant screens. Every
-- change of current unit is also recorded in TenantUnitAssignments.
-- FirstName, LastName and ContactNumber are current tenant details edited on
-- tenant/profile screens. The application copies them to Users on each save so
-- account displays agree. Run schema_consistency_checks.sql after direct SQL
-- edits, which bypass that application synchronization.
-- ============================================================================

CREATE TABLE TenantProfiles (
    TenantID INT UNSIGNED NOT NULL AUTO_INCREMENT,
    UserID INT UNSIGNED NOT NULL,

    -- Existing tenant screens still edit these details. User identity is also
    -- copied to Users on save so account lists and tenant records agree.
    FirstName VARCHAR(80) NOT NULL,
    LastName VARCHAR(80) NOT NULL,
    ContactNumber VARCHAR(30) NULL,
    EmergencyContactName VARCHAR(100) NULL,
    EmergencyContactRelationship VARCHAR(50) NULL,
    EmergencyContactNumber VARCHAR(20) NULL,
    PhotoPath VARCHAR(300) NULL,

    AdvanceCredit DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    Status VARCHAR(20) NOT NULL DEFAULT 'Active',
    DateRecorded DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    DateUpdated DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (TenantID),
    CONSTRAINT uq_tenant_profiles_user UNIQUE (UserID),

    CONSTRAINT fk_tenant_profiles_user
        FOREIGN KEY (UserID)
        REFERENCES Users(UserID)
        ON DELETE RESTRICT,

    CONSTRAINT chk_tenant_advance_credit_nonnegative CHECK (AdvanceCredit >= 0),

    INDEX idx_tenant_profiles_status (Status)
);

-- ============================================================================
-- 5. TENANT UNIT ASSIGNMENTS / LEASE HISTORY
-- Sole source of current occupancy and historical unit assignments.
-- A tenant can have many historical assignments but only one row with
-- Status = 'Active' at a time.
-- The Active row identifies the tenant's current unit. No Active row means
-- no current unit; Ended rows are history and must not count as occupants.
-- ============================================================================

CREATE TABLE TenantUnitAssignments (
    AssignmentID BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    TenantID INT UNSIGNED NOT NULL,
    UnitID INT UNSIGNED NOT NULL,

    MoveInDate DATETIME(6) NULL,
    MoveOutDate DATETIME(6) NULL,
    LeaseStart DATETIME(6) NULL,
    LeaseEnd DATETIME(6) NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Active',

    DateRecorded DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    DateUpdated DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    -- If Status is Active, this stores TenantID; otherwise NULL.
    -- UNIQUE therefore prevents two Active assignments for the same tenant.
    ActiveTenantID INT UNSIGNED
        GENERATED ALWAYS AS (
            CASE WHEN Status = 'Active' THEN TenantID ELSE NULL END
        ) STORED,

    PRIMARY KEY (AssignmentID),
    CONSTRAINT uq_tenant_one_active_assignment UNIQUE (ActiveTenantID),

    CONSTRAINT fk_assignments_tenant
        FOREIGN KEY (TenantID)
        REFERENCES TenantProfiles(TenantID)
        ON DELETE RESTRICT,

    CONSTRAINT fk_assignments_unit
        FOREIGN KEY (UnitID)
        REFERENCES Units(UnitID)
        ON DELETE RESTRICT,

    CONSTRAINT chk_assignment_move_dates CHECK (
        MoveOutDate IS NULL OR MoveInDate IS NULL OR MoveOutDate >= MoveInDate
    ),
    CONSTRAINT chk_assignment_lease_dates CHECK (
        LeaseEnd IS NULL OR LeaseStart IS NULL OR LeaseEnd >= LeaseStart
    ),

    INDEX idx_assignments_tenant (TenantID),
    INDEX idx_assignments_unit (UnitID),
    INDEX idx_assignments_status (Status),
    INDEX idx_assignments_lease_period (LeaseStart, LeaseEnd)
);

-- ============================================================================
-- 6. LOST AND FOUND ITEMS
-- Any authenticated user may report or claim an item, so these FKs correctly
-- reference the centralized Users table rather than a tenant-specific table.
-- ============================================================================

CREATE TABLE LostFoundItems (
    ItemID BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    ReportedByUserID INT UNSIGNED NOT NULL,
    ClaimedByUserID INT UNSIGNED NULL,

    ItemName VARCHAR(100) NOT NULL,
    Description VARCHAR(500) NULL,
    ItemType VARCHAR(20) NOT NULL,
    Location VARCHAR(200) NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Reported',

    DateReported DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    DateClaimed DATETIME(6) NULL,
    ArchivedAt DATETIME(6) NULL,
    Notes VARCHAR(500) NULL,
    ImagePath VARCHAR(260) NULL,

    PRIMARY KEY (ItemID),

    CONSTRAINT fk_lostfound_reported_by
        FOREIGN KEY (ReportedByUserID)
        REFERENCES Users(UserID)
        ON DELETE RESTRICT,

    CONSTRAINT fk_lostfound_claimed_by
        FOREIGN KEY (ClaimedByUserID)
        REFERENCES Users(UserID)
        ON DELETE RESTRICT,

    INDEX idx_lostfound_reporter (ReportedByUserID),
    INDEX idx_lostfound_claimant (ClaimedByUserID),
    INDEX idx_lostfound_status (Status),
    INDEX idx_lostfound_archived (ArchivedAt),
    INDEX idx_lostfound_reported_date (DateReported)
);

-- ============================================================================
-- 7. CLAIM REQUESTS
-- Any user may submit a claim request.
-- ============================================================================

CREATE TABLE ClaimRequests (
    ClaimID BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    ItemID BIGINT UNSIGNED NOT NULL,
    ClaimantUserID INT UNSIGNED NOT NULL,

    VerificationDetails VARCHAR(1000) NOT NULL,
    SubmittedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    Status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    AdminNotes VARCHAR(500) NULL,
    ImagePath VARCHAR(300) NULL,

    PRIMARY KEY (ClaimID),

    CONSTRAINT fk_claims_item
        FOREIGN KEY (ItemID)
        REFERENCES LostFoundItems(ItemID)
        ON DELETE RESTRICT,

    CONSTRAINT fk_claims_user
        FOREIGN KEY (ClaimantUserID)
        REFERENCES Users(UserID)
        ON DELETE RESTRICT,

    INDEX idx_claims_item (ItemID),
    INDEX idx_claims_user (ClaimantUserID),
    INDEX idx_claims_status (Status),
    INDEX idx_claims_submitted (SubmittedAt)
);

-- ============================================================================
-- 8. UNIT TRANSFER REQUESTS
-- Tenant-only transaction: references TenantProfiles rather than Users.
-- CurrentUnitID records the unit at request time; assignment history is also
-- kept in TenantUnitAssignments.
-- CurrentUnitID is a snapshot, so an approved transfer does not change what
-- the request originally asked to move from.
-- ============================================================================

CREATE TABLE UnitTransferRequests (
    TransferID BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    TenantID INT UNSIGNED NOT NULL,
    RequestedUnitID INT UNSIGNED NOT NULL,
    CurrentUnitID INT UNSIGNED NULL,

    Reason VARCHAR(500) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    DateRequested DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    DateReviewed DATETIME(6) NULL,

    TenantArchivedAt DATETIME(6) NULL,
    StaffArchivedAt DATETIME(6) NULL,
    AdminNotes VARCHAR(500) NULL,

    PRIMARY KEY (TransferID),

    CONSTRAINT fk_transfer_tenant
        FOREIGN KEY (TenantID)
        REFERENCES TenantProfiles(TenantID)
        ON DELETE RESTRICT,

    CONSTRAINT fk_transfer_requested_unit
        FOREIGN KEY (RequestedUnitID)
        REFERENCES Units(UnitID)
        ON DELETE RESTRICT,

    CONSTRAINT fk_transfer_current_unit
        FOREIGN KEY (CurrentUnitID) REFERENCES Units(UnitID) ON DELETE RESTRICT,

    INDEX idx_transfer_tenant (TenantID),
    INDEX idx_transfer_unit (RequestedUnitID),
    INDEX idx_transfer_status (Status),
    INDEX idx_transfer_requested_date (DateRequested)
);

-- ============================================================================
-- 9. BILLINGS
-- Tenant-only financial record: references TenantProfiles.
-- Payments is the source of truth. AmountPaid is a compatibility cache for
-- existing screens and should equal the sum of related payment rows.
-- DatePaid caches the latest payment date. Deposit and Advance are amounts
-- charged on this particular bill, while Units.Deposit/AdvancePayment are
-- current unit prices and can change independently.
-- ============================================================================

CREATE TABLE Billings (
    BillingID BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    TenantID INT UNSIGNED NOT NULL,

    BillingPeriod DATE NOT NULL,
    AmountDue DECIMAL(10,2) NOT NULL,
    -- Compatibility cache for existing billing screens; Payments remains the
    -- authoritative ledger and the application refreshes this on each payment.
    AmountPaid DECIMAL(10,2) NULL,
    Deposit DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    Advance DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    DueDate DATE NOT NULL,

    AdvanceFromOverpayment DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    IssuedFromAdvance BOOLEAN NOT NULL DEFAULT FALSE,

    DatePaid DATETIME(6) NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Unpaid',
    Notes VARCHAR(500) NULL,
    DateIssued DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    ArchivedAt DATETIME(6) NULL,

    PRIMARY KEY (BillingID),

    CONSTRAINT fk_billings_tenant
        FOREIGN KEY (TenantID)
        REFERENCES TenantProfiles(TenantID)
        ON DELETE RESTRICT,

    CONSTRAINT chk_billings_amount_due_nonnegative CHECK (AmountDue >= 0),
    CONSTRAINT chk_billings_deposit_nonnegative CHECK (Deposit >= 0),
    CONSTRAINT chk_billings_advance_nonnegative CHECK (Advance >= 0),
    CONSTRAINT chk_billings_overpayment_nonnegative CHECK (AdvanceFromOverpayment >= 0),

    INDEX idx_billings_tenant (TenantID),
    INDEX idx_billings_period (BillingPeriod),
    INDEX idx_billings_due_date (DueDate),
    INDEX idx_billings_status (Status),
    INDEX idx_billings_date_issued (DateIssued)
);

-- ============================================================================
-- 10. PAYMENTS
-- Authoritative payment history for a billing record.
-- Financial history is protected from accidental cascading deletion.
-- ============================================================================

CREATE TABLE Payments (
    PaymentID BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    BillingID BIGINT UNSIGNED NOT NULL,

    Amount DECIMAL(10,2) NOT NULL,
    DatePaid DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    Method VARCHAR(50) NULL,
    Remarks VARCHAR(300) NULL,
    RecordedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (PaymentID),

    CONSTRAINT fk_payments_billing
        FOREIGN KEY (BillingID)
        REFERENCES Billings(BillingID)
        ON DELETE RESTRICT,

    CONSTRAINT chk_payments_amount_positive CHECK (Amount > 0),

    INDEX idx_payments_billing (BillingID),
    INDEX idx_payments_date_paid (DatePaid)
);

-- ============================================================================
-- 11. MAINTENANCE REQUESTS
-- TenantID identifies the tenant who submitted the request.
-- AssignedStaffUserID references Users so the assigned maintenance account
-- remains part of the centralized account model.
-- UnitID identifies the unit where this request occurred; it can differ from
-- the tenant's current UnitID after a transfer.
-- ============================================================================

CREATE TABLE MaintenanceRequests (
    RequestID BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    TenantID INT UNSIGNED NOT NULL,
    UnitID INT UNSIGNED NULL,
    AssignedStaffUserID INT UNSIGNED NULL,

    Category VARCHAR(50) NOT NULL,
    Description VARCHAR(500) NOT NULL,
    Priority VARCHAR(20) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending',

    DateSubmitted DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    DateResolved DATETIME(6) NULL,

    TenantArchivedAt DATETIME(6) NULL,
    StaffArchivedAt DATETIME(6) NULL,
    StaffNotes VARCHAR(500) NULL,
    ImagePath VARCHAR(260) NULL,

    PRIMARY KEY (RequestID),

    CONSTRAINT fk_maintenance_tenant
        FOREIGN KEY (TenantID)
        REFERENCES TenantProfiles(TenantID)
        ON DELETE RESTRICT,

    CONSTRAINT fk_maintenance_unit
        FOREIGN KEY (UnitID)
        REFERENCES Units(UnitID)
        ON DELETE RESTRICT,

    CONSTRAINT fk_maintenance_staff
        FOREIGN KEY (AssignedStaffUserID)
        REFERENCES Users(UserID)
        ON DELETE RESTRICT,

    CONSTRAINT chk_maintenance_resolved_date CHECK (
        DateResolved IS NULL OR DateResolved >= DateSubmitted
    ),

    INDEX idx_maintenance_tenant (TenantID),
    INDEX idx_maintenance_unit (UnitID),
    INDEX idx_maintenance_staff (AssignedStaffUserID),
    INDEX idx_maintenance_status (Status),
    INDEX idx_maintenance_priority (Priority),
    INDEX idx_maintenance_submitted (DateSubmitted)
);

-- ============================================================================
-- DEFAULT ROLES
-- ============================================================================

INSERT INTO Roles (RoleName)
VALUES
    ('Admin'),
    ('Maintenance'),
    ('Tenant');

-- ============================================================================
-- DEFAULT SYSTEM ACCOUNTS
-- Retains the two original seed accounts. Names are generic seed labels only.
-- PasswordHash values are carried forward from the original YAMSDB.sql.
-- ============================================================================

INSERT INTO Users
(
    RoleID,
    Username,
    PasswordHash,
    FirstName,
    LastName,
    ContactNumber,
    IsActive,
    MustChangePassword,
    IsMainAdmin
)
VALUES
(
    (SELECT RoleID FROM Roles WHERE RoleName = 'Admin'),
    'admin',
    'djYghAu8O+sp9Cvf31GmHYlY3jXA5UjsxHi0K88subOp77uz49xjukaeeYt2LqGh',
    'System',
    'Administrator',
    NULL,
    TRUE,
    FALSE,
    TRUE
),
(
    (SELECT RoleID FROM Roles WHERE RoleName = 'Maintenance'),
    'maintenance1',
    'vzHaWGruR2VMjyAOyLhqOpdwix266UvV3peVJqo+OxKuKTAMYKKMynXHxPb4VOyf',
    'Maintenance',
    'Staff',
    NULL,
    TRUE,
    TRUE,
    FALSE
);

-- ============================================================================
-- REFERENCE QUERIES (COMMENTS ONLY)
-- ============================================================================
-- Current unit for a tenant:
--
-- SELECT tua.*
-- FROM TenantUnitAssignments tua
-- WHERE tua.TenantID = ?
--   AND tua.Status = 'Active';
--
-- Amount paid and remaining balance for a bill:
--
-- SELECT
--     b.BillingID,
--     b.AmountDue,
--     COALESCE(SUM(p.Amount), 0) AS AmountPaid,
--     b.AmountDue - COALESCE(SUM(p.Amount), 0) AS Balance
-- FROM Billings b
-- LEFT JOIN Payments p ON p.BillingID = b.BillingID
-- WHERE b.BillingID = ?
-- GROUP BY b.BillingID, b.AmountDue;
--
-- IMPORTANT:
-- Prefer setting Users.IsActive = FALSE instead of deleting user accounts.
-- This preserves billing, transfer, maintenance, lost/found, and audit history.
-- ============================================================================

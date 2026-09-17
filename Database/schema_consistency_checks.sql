-- Read-only checks for application-maintained copies in YAMSDB.
-- Every result should be zero. Run after direct SQL edits or data imports.
USE YAMSDB;

SELECT 'tenant_identity_mismatch' AS CheckName, COUNT(*) AS Mismatches
FROM TenantProfiles AS t
JOIN Users AS u ON u.UserID = t.UserID
WHERE NOT (t.FirstName <=> u.FirstName)
   OR NOT (t.LastName <=> u.LastName)
   OR NOT (t.ContactNumber <=> u.ContactNumber)
UNION ALL
SELECT 'active_assignment_mismatch', COUNT(*)
FROM TenantProfiles AS t
LEFT JOIN TenantUnitAssignments AS a
    ON a.TenantID = t.TenantID AND a.Status = 'Active'
WHERE (t.Status = 'Active' AND t.UnitID IS NOT NULL
       AND NOT (t.UnitID <=> a.UnitID))
   OR ((t.Status <> 'Active' OR t.UnitID IS NULL)
       AND a.AssignmentID IS NOT NULL)
UNION ALL
SELECT 'billing_paid_mismatch', COUNT(*)
FROM Billings AS b
LEFT JOIN (
    SELECT BillingID, SUM(Amount) AS TotalPaid
    FROM Payments GROUP BY BillingID
) AS p ON p.BillingID = b.BillingID
WHERE COALESCE(b.AmountPaid, 0) <> COALESCE(p.TotalPaid, 0)
UNION ALL
SELECT 'billing_payment_date_mismatch', COUNT(*)
FROM Billings AS b
LEFT JOIN (
    SELECT BillingID, MAX(DatePaid) AS LatestDatePaid
    FROM Payments GROUP BY BillingID
) AS p ON p.BillingID = b.BillingID
WHERE NOT (b.DatePaid <=> p.LatestDatePaid);

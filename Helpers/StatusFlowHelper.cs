namespace YnclinoApartmentManagementSystem.Helpers
{
    // A record's status may only move FORWARD. Once a lost item has been claimed by
    // its owner it can never go back to "Reported", and a resolved request cannot be
    // reopened — that is how these things work in real life, so the system enforces it.
    public static class StatusFlowHelper
    {
        // Reported -> Claimed -> Resolved
        private static readonly string[] LostFoundFlow = { "Reported", "Claimed", "Resolved" };

        // Pending -> In Progress -> Resolved   (Cancelled is a separate end state)
        private static readonly string[] MaintenanceFlow = { "Pending", "In Progress", "Resolved" };

        // Statuses a Lost & Found item may still be moved to, given where it is now.
        public static List<string> AllowedLostFound(string? current)
            => Forward(LostFoundFlow, current);

        // Statuses a maintenance request may still be moved to. Resolved and Cancelled
        // are final; anything still open may also be cancelled.
        public static List<string> AllowedMaintenance(string? current)
        {
            if (current == "Resolved" || current == "Cancelled")
                return new List<string> { current };

            var allowed = Forward(MaintenanceFlow, current);
            allowed.Add("Cancelled");
            return allowed;
        }

        public static bool IsAllowedLostFound(string? current, string? next)
            => next != null && AllowedLostFound(current).Contains(next);

        public static bool IsAllowedMaintenance(string? current, string? next)
            => next != null && AllowedMaintenance(current).Contains(next);

        // A record in a final state is CLOSED: it becomes view-only, so it can no
        // longer be edited at all (not by button, and not by typing the URL).
        public static bool IsClosedLostFound(string? status)
            => status == "Claimed" || status == "Resolved";

        public static bool IsClosedMaintenance(string? status)
            => status == "Resolved" || status == "Cancelled";

        // the current status plus everything after it in the flow
        private static List<string> Forward(string[] flow, string? current)
        {
            int i = Array.IndexOf(flow, current ?? "");
            return i < 0 ? flow.ToList() : flow.Skip(i).ToList();
        }
    }
}

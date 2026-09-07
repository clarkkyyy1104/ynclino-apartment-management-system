using YnclinoApartmentManagementSystem.Models;

namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    // Everything the public landing page shows. It is deliberately limited to figures
    // a visitor may see — how many units exist, which ones are free and what they cost.
    // No tenant, billing or maintenance information ever reaches this page.
    public class LandingViewModel
    {
        public int TotalUnits { get; set; }
        public int AvailableUnits { get; set; }
        public int OccupiedUnits { get; set; }

        // the cheapest advertised rent, used for the "from ₱x a month" line
        public decimal StartingRent { get; set; }

        // the units a visitor could actually enquire about
        public List<tblUnit> Vacancies { get; set; } = new();

        public int OccupancyRate => TotalUnits == 0
            ? 0
            : (int)Math.Round(OccupiedUnits * 100.0 / TotalUnits);
    }
}

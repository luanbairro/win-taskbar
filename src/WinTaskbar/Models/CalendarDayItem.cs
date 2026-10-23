namespace WinTaskbar.Models
{
    public class CalendarDayItem
    {
        public string DayName { get; set; } = string.Empty;
        public int DayNumber { get; set; }
        public bool IsToday { get; set; }
    }
}

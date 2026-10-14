using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    // Placeholders of the restructured navigation (rail: home, development, riders, staff, career). Each is replaced
    // by its real page in its own file.

    /// <summary>Development hub: the bike's attributes, its score against the grid, and the R&amp;D and facilities buttons.</summary>
    public class DevelopmentPage : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.bike"); } }
        public override void Build(RectTransform content) { new BikeScreen().Attach(Shell); }
    }

    /// <summary>Riders hub: one card per rider with a details button.</summary>
    public class RidersHubPage : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.riders"); } }
        public override void Build(RectTransform content) { }
    }

    /// <summary>A rider's profile: header, then attributes / traits / contract / history.</summary>
    public class RiderProfilePage : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.riders"); } }
        public override bool HiddenTab { get { return true; } }
        public override void Build(RectTransform content) { }
    }

    /// <summary>Staff hub: engineers, mechanics and strategists.</summary>
    public class StaffHubPage : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.staff"); } }
        public override void Build(RectTransform content) { }
    }

    /// <summary>One staff group: a card per position.</summary>
    public class StaffGroupPage : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.staff"); } }
        public override bool HiddenTab { get { return true; } }
        public override void Build(RectTransform content) { }
    }
}

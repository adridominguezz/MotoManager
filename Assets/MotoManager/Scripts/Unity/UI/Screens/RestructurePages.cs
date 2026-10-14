using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    // Placeholders of the restructured navigation (rail: home, development, riders, staff, career). Each is replaced
    // by its real page in its own file (the development hub is in DevelopmentHub.cs).

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

    /// <summary>The team: its history and statistics (home's details button).</summary>
    public class TeamDetailPage : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.team"); } }
        public override void Build(RectTransform content) { }
    }
}

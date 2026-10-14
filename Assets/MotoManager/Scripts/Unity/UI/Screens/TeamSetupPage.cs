using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Getting a team ready to race (UX spec §3.20; opens after founding a team, and from the organisation's "..."):
    /// a step segmented (identity, riders, staff, sponsors, each with its state), at most two cards per step with
    /// secondary per-row actions, and a bottom bar with "‹ ANTERIOR" on the left and the primary SIGUIENTE / LISTO on
    /// the right. Not a tab: a wizard reached by buttons.
    /// </summary>
    public class TeamSetupPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.setup"); } }
        public override bool HiddenTab { get { return true; } }

        /// <summary>Set by the new-career flow: the management shell opens this page first.</summary>
        public static bool OpenOnStart;

        private int _step;
        private string _confirm;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null)
            {
                var c = Ui.Columns12(content, 3, 6);
                Ui.Empty(Wp4Ui.ListIn(Ui.Tile(c[1], Loc.T("tab.career.setup"))), UiIcons.Team, Loc.T("setup.noTeam"));
                return;
            }
            var db = Game.Db;
            var cat = db.GetCategory(team.CategoryId);
            int capacity = TransferMarket.Capacity(cat, db.GetRules(cat.Id, world.Season), team);
            int missingStaff = MissingRoles(world, db, team).Count;
            int offers = SponsorMarket.OffersFor(world, team).Count;

            var steps = new[]
            {
                Loc.T("setup.step.identity"),
                Loc.T("setup.step.riders") + "  " + Mark(team.RiderIds.Count >= capacity, team.RiderIds.Count + "/" + capacity),
                Loc.T("setup.step.staff") + "  " + Mark(missingStaff == 0, missingStaff.ToString()),
                Loc.T("setup.step.sponsors") + "  " + Mark(team.Sponsors.Count > 0, offers.ToString())
            };
            Ui.Segmented(Wp4Ui.ListIn(Wp4Ui.TopStrip(content, Ui.Grid.Hit), 0), steps, _step, i => { _step = i; _confirm = null; Shell.Refresh(); });
            var area = Wp4Ui.Below(content, Ui.Grid.Hit + Ui.Grid.Gap, Ui.Grid.Cta + Ui.Grid.Gap);

            switch (_step)
            {
                case 1: Riders(area, world, db, team, capacity); break;
                case 2: Staff(area, world, db, team); break;
                case 3: Sponsors(area, world, db, team); break;
                default: Identity(area, world, team); break;
            }

            var nav = Ui.Columns12(Wp4Ui.BottomStrip(content, Ui.Grid.Cta), 3, 5, 4);
            if (_step > 0) Wp4Ui.BackLink(nav[0], Loc.T("setup.prev"), () => { _step--; _confirm = null; Shell.Refresh(); });
            if (_step < steps.Length - 1)
                Wp4Ui.Chevron(Ui.PrimaryCTA(nav[2], Loc.T("setup.next"), () => { _step++; _confirm = null; Shell.Refresh(); }));
            else
                Ui.PrimaryCTA(nav[2], Loc.T("setup.done"), () => Shell.Show<HubScreen>());
        }

        private static string Mark(bool done, string pending)
        {
            return done ? Theme.GoodText(Glyph.Check) : Theme.WarnText(pending);
        }

        private void Identity(RectTransform area, WorldState world, Team team)
        {
            var cols = Ui.Columns12(area, 6, 6);
            var id = Wp4Ui.ListIn(Ui.Tile(cols[0], Loc.T("setup.identity"), null, Theme.Caps(Loc.T("setup.identitySub")), 0, null,
                Wp4Ui.Info(Loc.T("setup.identity"), Loc.T("setup.supplierNote"))), 8);
            UiFactory.Input(id, Loc.T("career.teamName"), team.Name, v =>
            {
                string name = (v ?? "").Trim();
                if (name.Length < 2) return;
                team.Name = name;
                team.ShortName = name;
                var org = world.OrganisationOf(team);
                if (org != null && org.Owner == OwnerKind.Player && org.TeamIds.Count == 1) org.Name = name;
            }, 56);
            var supplier = world.GetManufacturer(team.ManufacturerId);
            Ui.Line(id, Loc.T("setup.supplier"), "<b>" + (supplier != null ? supplier.Name : "-") + "</b>");

            var colours = Wp4Ui.ListIn(Ui.Tile(cols[1], Loc.T("setup.colours")), 8);
            FoundingPanel.Swatches(colours, Loc.T("found.primary"), team.Colors.Primary, c => { team.Colors.Primary = c; Shell.Refresh(); });
            FoundingPanel.Swatches(colours, Loc.T("found.secondary"), team.Colors.Secondary, c => { team.Colors.Secondary = c; Shell.Refresh(); });
            var plate = UiFactory.Row(colours, Ui.Grid.Cta);
            var pc = Ui.Columns12(plate, 2, 8);
            FoundingPanel.Plate(pc[1], team.Name, team.Colors.Primary, team.Colors.Secondary);
            Wp4Ui.Chevron(Ui.Secondary(colours, Loc.T("livery.edit"), () =>
            {
                LiveryEditorPage.PendingTeamId = team.Id;
                Shell.Show<LiveryEditorPage>();
            }));
        }

        private void Riders(RectTransform area, WorldState world, GameDatabase db, Team team, int capacity)
        {
            var cols = Ui.Columns12(area, 5, 7);
            var mine = Wp4Ui.ScrollIn(Ui.Tile(cols[0], Loc.T("setup.myRiders"), null, team.RiderIds.Count + "/" + capacity), 6);
            if (team.RiderIds.Count < capacity) Ui.Alert(mine, Loc.T("ux.setup.ridersMissing", capacity - team.RiderIds.Count));
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r == null) continue;
                Ui.PersonRow(mine, Fmt.Estimate(world, team.Id, r, "RaceSkill"), r.FullName,
                    Loc.T("setup.riderLine", r.AgeOn(world.CurrentDate), r.Contract != null ? r.Contract.EndSeason.ToString() : "-"), null);
            }
            if (team.RiderIds.Count == 0) Ui.Empty(mine, UiIcons.Person, Loc.T("ux.setup.noRiders"));

            var cand = new List<Rider>();
            foreach (var r in world.Riders.Values)
                if (!r.IsRetired && r.TeamId == null && r.Contract == null && (r.CategoryId == team.CategoryId || r.Generated)) cand.Add(r);
            cand.Sort((a, b) => Fmt.Estimate(world, team.Id, b, "RaceSkill").CompareTo(Fmt.Estimate(world, team.Id, a, "RaceSkill")));
            var freeBody = Ui.Tile(cols[1], Loc.T("setup.freeRiders"), null, Theme.Caps(Loc.T("setup.freeRidersSub")), 0, null,
                Wp4Ui.Info(Loc.T("setup.freeRiders"), Loc.T("setup.ridersMissing", Math.Max(0, capacity - team.RiderIds.Count))));
            Ui.Ghost(Wp4Ui.BottomStrip(freeBody, Ui.Grid.Hit), Loc.T("setup.toMarket"), () => Shell.Show<MarketScreen>());
            var free = Wp4Ui.ScrollIn(Wp4Ui.Below(freeBody, 0, Ui.Grid.Hit + 4), 6);
            var rules = db.GetRules(team.CategoryId, world.Season);
            for (int i = 0; i < cand.Count && i < 5; i++)
            {
                var r = cand[i];
                Ui.PersonRow(free, Fmt.Estimate(world, team.Id, r, "RaceSkill"), r.FullName, Loc.T("ux.setup.riderAge", r.AgeOn(world.CurrentDate)),
                    MathX.FormatMoney(TransferMarket.SalaryDemand(r, rules)), null, UiFactory.Act(Loc.T("setup.negotiate"), () =>
                    {
                        MarketScreen.PendingRiderId = r.Id;
                        Shell.Show<MarketScreen>();
                    }));
            }
            if (cand.Count == 0) Ui.Empty(free, UiIcons.Market, Loc.T("ux.setup.noFree"));
        }

        private static List<StaffRole> MissingRoles(WorldState world, GameDatabase db, Team team)
        {
            var missing = new List<StaffRole>();
            var seen = new HashSet<StaffRole>();
            int riders = Math.Max(1, Math.Min(team.RiderIds.Count, 4));
            foreach (var role in StaffService.StandardRoles(db, team, Math.Max(riders, 2)))
            {
                if (!seen.Add(role)) continue;
                double best;
                int count;
                if (StaffService.Coverage(world, db, team, role, out best, out count) == RoleCoverage.Missing) missing.Add(role);
            }
            return missing;
        }

        private void Staff(RectTransform area, WorldState world, GameDatabase db, Team team)
        {
            var missing = MissingRoles(world, db, team);
            double cash = Accounts.Cash(world, team);
            var paras = new List<string> { Loc.T("setup.staffNote", MathX.FormatMoney(cash)), Loc.T("setup.staffOptional") };
            foreach (var role in missing) paras.Add("<b>" + Loc.T("role." + role) + "</b>   " + Loc.T("roleimpact." + role));
            var body = Ui.Tile(area, Loc.T("setup.staff"), null, Theme.Caps(missing.Count == 0 ? Loc.T("setup.staffDone") : Loc.T("setup.staffMissing", missing.Count)), 0, null,
                Wp4Ui.Info(Loc.T("setup.staff"), paras.ToArray()));
            Ui.Ghost(Wp4Ui.BottomStrip(body, Ui.Grid.Hit), Loc.T("setup.toStaff"), () => Shell.Show<StaffHubPage>());
            var list = Wp4Ui.ScrollIn(Wp4Ui.Below(body, 0, Ui.Grid.Hit + 4), 6);
            if (missing.Count == 0)
            {
                Ui.Empty(list, UiIcons.Team, Loc.T("setup.staffDone"));
                return;
            }
            foreach (var role in missing)
            {
                var options = StaffMarket.Candidates(world, db, team, role, 3);
                StaffMember pick = options.Count > 1 ? options[1] : options.Count > 0 ? options[0] : null;
                if (pick == null) continue;
                var p = pick;
                var rl = role;
                string key = "staff:" + role;
                Ui.PersonRow(list, p.Skill, Loc.T("role." + role), p.FullName, MathX.FormatMoney(p.Salary), null,
                    UiFactory.Act(_confirm == key ? Loc.T("setup.hireConfirm") : Loc.T("setup.hire"), () =>
                    {
                        if (_confirm != key) { _confirm = key; Shell.Refresh(); return; }
                        _confirm = null;
                        StaffMarket.Hire(world, team, p, db);
                        Shell.Toast(Loc.T("setup.hired", p.FullName, Loc.T("role." + rl)));
                        Shell.Refresh();
                    }));
            }
        }

        private void Sponsors(RectTransform area, WorldState world, GameDatabase db, Team team)
        {
            var cols = Ui.Columns12(area, 5, 7);
            var signed = Wp4Ui.ScrollIn(Ui.Tile(cols[0], Loc.T("setup.sponsorsSigned"), null, team.Sponsors.Count.ToString()), 2);
            foreach (var d in team.Sponsors)
                Ui.Line(signed, d.Name, MathX.FormatMoney(d.PaymentPerSeason) + Theme.Dim("/" + Loc.T("unit.season")));
            if (team.Sponsors.Count == 0) Ui.Empty(signed, UiIcons.Sponsor, Loc.T("setup.noSponsors"));

            var offers = SponsorMarket.OffersFor(world, team);
            var body = Ui.Tile(cols[1], Loc.T("setup.sponsorOffers"), null, offers.Count.ToString());
            Ui.Ghost(Wp4Ui.BottomStrip(body, Ui.Grid.Hit), Loc.T("setup.toSponsors"), () => Shell.Show<SponsorsPage>());
            var list = Wp4Ui.ScrollIn(Wp4Ui.Below(body, 0, Ui.Grid.Hit + 4), 6);
            foreach (var o in offers)
            {
                var offer = o;
                Ui.Item(list, o.Name, Loc.T("setup.sponsorTerms", o.Seasons),
                    MathX.FormatMoney(o.PaymentPerSeason) + Theme.Dim("/" + Loc.T("unit.season")),
                    UiFactory.Act(Loc.T("sponsor.accept"), () =>
                    {
                        Shell.Toast(SponsorMarket.Accept(world, db, offer.Id).Message);
                        Shell.Refresh();
                    }));
            }
            if (offers.Count == 0) Ui.Empty(list, UiIcons.Sponsor, Loc.T("ux.setup.noOffers"));
        }
    }
}

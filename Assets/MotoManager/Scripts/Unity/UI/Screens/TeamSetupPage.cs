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
    /// Getting a team ready to race (opens after founding a team): its identity, the riders, the staff and the
    /// sponsors, one step at a time, each with the actions at hand. Also useful later as a checklist.
    /// </summary>
    public class TeamSetupPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.setup"); } }

        /// <summary>Set by the new-career flow: the management shell opens this page first.</summary>
        public static bool OpenOnStart;

        private int _step;
        private string _confirm;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null)
            {
                Ui.Note(Ui.Card(list, Loc.T("tab.career.setup")), Loc.T("setup.noTeam"));
                return;
            }
            var db = Game.Db;
            var cat = db.GetCategory(team.CategoryId);
            int capacity = TransferMarket.Capacity(cat, db.GetRules(cat.Id, world.Season), team);
            int missingStaff = MissingRoles(world, db, team).Count;
            int offers = SponsorMarket.OffersFor(world, team).Count;

            // Steps with their state: done (✓) or what is still missing.
            var steps = new[]
            {
                Loc.T("setup.step.identity"),
                Loc.T("setup.step.riders") + "  " + Mark(team.RiderIds.Count >= capacity, team.RiderIds.Count + "/" + capacity),
                Loc.T("setup.step.staff") + "  " + Mark(missingStaff == 0, missingStaff.ToString()),
                Loc.T("setup.step.sponsors") + "  " + Mark(team.Sponsors.Count > 0, offers.ToString())
            };
            Ui.Chips(list, steps, _step, i => { _step = i; _confirm = null; Shell.Refresh(); }, 56);

            switch (_step)
            {
                case 1: Riders(list, world, db, team, capacity); break;
                case 2: Staff(list, world, db, team); break;
                case 3: Sponsors(list, world, db, team); break;
                default: Identity(list, world, team); break;
            }

            var nav = new List<UiAct>();
            if (_step > 0) nav.Add(UiFactory.Act("◄  " + Loc.T("setup.prev"), () => { _step--; Shell.Refresh(); }));
            if (_step < steps.Length - 1) nav.Add(UiFactory.Primary(Loc.T("setup.next") + "  ►", () => { _step++; Shell.Refresh(); }));
            else nav.Add(UiFactory.Primary(Loc.T("setup.done"), () => Shell.Show<HubScreen>()));
            UiFactory.ButtonRow(list, 60, nav.ToArray());
        }

        private static string Mark(bool done, string pending)
        {
            return done ? Theme.GoodText("✓") : Theme.WarnText(pending);
        }

        private void Identity(RectTransform list, WorldState world, Team team)
        {
            RectTransform left, right;
            Ui.Columns(list, out left, out right);
            var id = Ui.Card(left, Loc.T("setup.identity"), Loc.T("setup.identitySub"));
            UiFactory.Input(id, Loc.T("career.teamName"), team.Name, v =>
            {
                string name = (v ?? "").Trim();
                if (name.Length < 2) return;
                team.Name = name;
                team.ShortName = name;
                var org = world.OrganisationOf(team);
                if (org != null && org.Owner == OwnerKind.Player && org.TeamIds.Count == 1) org.Name = name;
            });
            var supplier = world.GetManufacturer(team.ManufacturerId);
            Ui.Line(id, Loc.T("setup.supplier"), supplier != null ? supplier.Name : "-");
            Ui.Note(id, Loc.T("setup.supplierNote"));

            var colours = Ui.Card(right, Loc.T("setup.colours"));
            FoundingPanel.Swatches(colours, Loc.T("found.primary"), team.Colors.Primary, c => { team.Colors.Primary = c; Shell.Refresh(); });
            FoundingPanel.Swatches(colours, Loc.T("found.secondary"), team.Colors.Secondary, c => { team.Colors.Secondary = c; Shell.Refresh(); });
            var preview = UiFactory.Row(colours, 64);
            var plate = UiFactory.Panel("Plate", preview, UiFactory.ParseColor(team.Colors.Primary, Color.red), true);
            UiFactory.Place(plate.rectTransform, 0.25f, 0.05f, 0.75f, 0.95f);
            var num = UiFactory.Label("Name", plate.transform, team.Name.ToUpperInvariant(), Theme.FontSmall, TextAnchor.MiddleCenter, UiFactory.ParseColor(team.Colors.Secondary, Color.white));
            num.fontStyle = FontStyle.Bold;
            UiFactory.Stretch(num.rectTransform);
        }

        private void Riders(RectTransform list, WorldState world, GameDatabase db, Team team, int capacity)
        {
            RectTransform left, right;
            Ui.Columns(list, out left, out right);
            var mine = Ui.Card(left, Loc.T("setup.myRiders"), team.RiderIds.Count + "/" + capacity);
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r == null) continue;
                Ui.Item(mine, r.FullName, Loc.T("setup.riderLine", r.AgeOn(world.CurrentDate), r.Contract != null ? r.Contract.EndSeason.ToString() : "-"),
                    Fmt.Rating(Fmt.Estimate(world, team.Id, r, "RaceSkill")));
            }
            if (team.RiderIds.Count < capacity) Ui.Note(mine, Theme.WarnText(Loc.T("setup.ridersMissing", capacity - team.RiderIds.Count)));

            // Riders who can join now: free ones of the category (and nearby pools), best first.
            var cand = new List<Rider>();
            foreach (var r in world.Riders.Values)
                if (!r.IsRetired && r.TeamId == null && r.Contract == null && (r.CategoryId == team.CategoryId || r.Generated)) cand.Add(r);
            cand.Sort((a, b) => Fmt.Estimate(world, team.Id, b, "RaceSkill").CompareTo(Fmt.Estimate(world, team.Id, a, "RaceSkill")));
            var free = Ui.Card(right, Loc.T("setup.freeRiders"), Loc.T("setup.freeRidersSub"));
            var rules = db.GetRules(team.CategoryId, world.Season);
            for (int i = 0; i < cand.Count && i < 5; i++)
            {
                var r = cand[i];
                Ui.Item(free, r.FullName, Loc.T("setup.riderAsk", r.AgeOn(world.CurrentDate), MathX.FormatMoney(TransferMarket.SalaryDemand(r, rules))),
                    Fmt.Rating(Fmt.Estimate(world, team.Id, r, "RaceSkill")),
                    UiFactory.Primary(Loc.T("setup.negotiate"), () =>
                    {
                        MarketScreen.PendingRiderId = r.Id;
                        Shell.Show<MarketScreen>();
                    }));
            }
            if (cand.Count == 0) Ui.Note(free, Loc.T("setup.noFree"));
            UiFactory.ButtonRow(free, 54, UiFactory.Act(Loc.T("setup.toMarket"), () => Shell.Show<MarketScreen>()));
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

        private void Staff(RectTransform list, WorldState world, GameDatabase db, Team team)
        {
            var missing = MissingRoles(world, db, team);
            var card = Ui.Card(list, Loc.T("setup.staff"), missing.Count == 0 ? Loc.T("setup.staffDone") : Loc.T("setup.staffMissing", missing.Count));
            if (missing.Count == 0)
            {
                Ui.Note(card, Loc.T("setup.staffOptional"));
                UiFactory.ButtonRow(card, 54, UiFactory.Act(Loc.T("setup.toStaff"), () => Shell.Show<StaffPage>()));
                return;
            }
            double cash = Accounts.Cash(world, team);
            foreach (var role in missing)
            {
                var options = StaffMarket.Candidates(world, db, team, role, 3);
                // A sensible pick: the best one paid at most a little above the usual for the category.
                StaffMember pick = options.Count > 1 ? options[1] : options.Count > 0 ? options[0] : null;
                if (pick == null) continue;
                var p = pick;
                string key = "staff:" + role;
                Ui.Item(card, Loc.T("role." + role) + "  " + Theme.Dim(p.FullName),
                    Loc.T("roleimpact." + role),
                    Fmt.Rating(p.Skill) + "  " + Theme.Dim(MathX.FormatMoney(p.Salary) + "/" + Loc.T("unit.season")),
                    UiFactory.Primary(_confirm == key ? Loc.T("setup.hireConfirm") : Loc.T("setup.hire"), () =>
                    {
                        if (_confirm != key) { _confirm = key; Shell.Refresh(); return; }
                        _confirm = null;
                        StaffMarket.Hire(world, team, p, db);
                        Shell.Toast(Loc.T("setup.hired", p.FullName, Loc.T("role." + role)));
                        Shell.Refresh();
                    }));
            }
            Ui.Note(card, Loc.T("setup.staffNote", MathX.FormatMoney(cash)));
            UiFactory.ButtonRow(card, 54, UiFactory.Act(Loc.T("setup.toStaff"), () => Shell.Show<StaffPage>()));
        }

        private void Sponsors(RectTransform list, WorldState world, GameDatabase db, Team team)
        {
            RectTransform left, right;
            Ui.Columns(list, out left, out right);
            var signed = Ui.Card(left, Loc.T("setup.sponsorsSigned"), team.Sponsors.Count.ToString());
            foreach (var d in team.Sponsors)
                Ui.Line(signed, d.Name, MathX.FormatMoney(d.PaymentPerSeason) + Theme.Dim("/" + Loc.T("unit.season")));
            if (team.Sponsors.Count == 0) Ui.Note(signed, Loc.T("setup.noSponsors"));

            var offersCard = Ui.Card(right, Loc.T("setup.sponsorOffers"));
            var offers = SponsorMarket.OffersFor(world, team);
            foreach (var o in offers)
            {
                var offer = o;
                Ui.Item(offersCard, o.Name, Loc.T("setup.sponsorTerms", o.Seasons),
                    MathX.FormatMoney(o.PaymentPerSeason) + Theme.Dim("/" + Loc.T("unit.season")),
                    UiFactory.Primary(Loc.T("sponsor.accept"), () =>
                    {
                        Shell.Toast(SponsorMarket.Accept(world, db, offer.Id).Message);
                        Shell.Refresh();
                    }));
            }
            if (offers.Count == 0) Ui.Note(offersCard, Loc.T("setup.noOffers"));
            UiFactory.ButtonRow(offersCard, 54, UiFactory.Act(Loc.T("setup.toSponsors"), () => Shell.Show<SponsorsPage>()));
        }
    }
}

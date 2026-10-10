using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Starting a career: the manager's profile, then either a job at an existing team (only the teams that would
    /// hire this profile) or founding a team (capital, loan, founding sponsors and only the categories the money can
    /// sustain). Business rules live in <see cref="CareerService"/>; this only presents them.
    /// </summary>
    public class CareerStartFlow
    {
        private enum Step { Profile, Path, Jobs, Founding }

        private readonly GameSession _session;
        private readonly GameController _game;
        private readonly RectTransform _list;
        private readonly Text _status;
        private Step _step = Step.Profile;

        private string _first = "Alex", _last = "Rider", _nation = "ES", _background = "team_staff";
        private ManagerProfile _manager;
        private double _investShare = 1, _loanShare = 0;
        private string _category, _supplier, _teamName = "";

        public CareerStartFlow(GameSession session, GameController game, RectTransform list, Text status)
        {
            _session = session;
            _game = game;
            _list = list;
            _status = status;
        }

        private WorldState World { get { return _game.World; } }

        private static string Money(double v)
        {
            return (v < 0 ? "<color=#E10600>" : "") + MathX.FormatMoney(v) + (v < 0 ? "</color>" : "");
        }

        public void Render()
        {
            UiFactory.Clear(_list);
            switch (_step)
            {
                case Step.Profile: Profile(); break;
                case Step.Path: Path(); break;
                case Step.Jobs: Jobs(); break;
                case Step.Founding: Founding(); break;
            }
        }

        // ------------------------------------------------------------------ profile

        private void Profile()
        {
            _status.text = Loc.T("career.profile.status");
            UiFactory.Heading(_list, Loc.T("career.profile"));
            UiFactory.Input(_list, Loc.T("career.firstName"), _first, v => _first = v);
            UiFactory.Input(_list, Loc.T("career.lastName"), _last, v => _last = v);
            var nations = new List<string>(_game.Db.NamePools.Keys);
            nations.Sort(StringComparer.Ordinal);
            UiFactory.ButtonRow(_list, 56, UiFactory.Act(Loc.T("career.nationality", _nation), () =>
            {
                int i = nations.IndexOf(_nation);
                _nation = nations.Count > 0 ? nations[(i + 1) % nations.Count] : _nation;
                Render();
            }));

            UiFactory.Heading(_list, Loc.T("career.background"));
            var w = new[] { 3.2f, 1f, 1f, 1f, 1f, 1f, 1f, 2f, 1.6f };
            UiFactory.TableRow(_list, 38, w, new[] { "", Loc.T("career.rep"), Loc.T("career.neg"), Loc.T("career.sport"), Loc.T("career.fin"), Loc.T("career.rel"), Loc.T("career.com"), Loc.T("career.wealth"), "" }, true);
            var list = new List<ManagerBackground>(_game.Db.ManagerBackgrounds.Values);
            list.Sort((a, b) => a.PersonalWealth.CompareTo(b.PersonalWealth));
            foreach (var bg in list)
            {
                var b = bg;
                bool chosen = _background == bg.Id;
                var row = UiFactory.TableRow(_list, 52, w, new[]
                {
                    "<b>" + Loc.T("background." + bg.Id) + "</b>", bg.Reputation.ToString("0"), bg.Negotiation.ToString("0"), bg.SportingManagement.ToString("0"),
                    bg.Finance.ToString("0"), bg.Relations.ToString("0"), bg.Commercial.ToString("0"), MathX.FormatMoney(bg.PersonalWealth), ""
                }, false, chosen ? new Color(0.94f, 0.7f, 0.16f, 0.16f) : (Color?)null);
                var button = UiFactory.Button("Pick", row, chosen ? "✓" : Loc.T("career.choose"), () => { _background = b.Id; Render(); }, chosen ? Theme.Accent : Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.Place((RectTransform)button.transform, 0.86f, 0.1f, 1f, 0.9f);
                UiFactory.Paragraph(_list, "<color=#9AA4B2>" + Loc.T("background." + bg.Id + ".desc") + "</color>", Theme.FontSmall - 2);
            }
            UiFactory.ButtonRow(_list, 64, UiFactory.Act(Loc.T("career.next"), () =>
            {
                _manager = CareerService.CreatePlayer(World, _game.Db, _first, _last, _nation, _background);
                _step = Step.Path;
                Render();
            }));
        }

        // ------------------------------------------------------------------ path

        private void Path()
        {
            _status.text = _manager.FullName + "\n" + Loc.T("career.wealthNow", MathX.FormatMoney(_manager.PersonalWealth));
            UiFactory.Heading(_list, Loc.T("career.path"));
            UiFactory.Paragraph(_list, Loc.T("career.path.employed"), Theme.FontSmall);
            UiFactory.ButtonRow(_list, 64, UiFactory.Act(Loc.T("career.findJob"), () => { _step = Step.Jobs; Render(); }));
            UiFactory.Paragraph(_list, Loc.T("career.path.found"), Theme.FontSmall);
            UiFactory.ButtonRow(_list, 64, UiFactory.Act(Loc.T("career.foundTeam"), () =>
            {
                CareerService.FoundingSponsorOffers(World, _game.Db, _manager, World.Rng.Fork("founding-sponsors"));
                _step = Step.Founding;
                Render();
            }));
        }

        // ------------------------------------------------------------------ jobs

        private void Jobs()
        {
            var offers = CareerService.JobOffers(World, _game.Db, _manager);
            _status.text = Loc.T("career.jobs.status", offers.Count);
            UiFactory.Heading(_list, Loc.T("career.jobs"));
            if (offers.Count == 0) UiFactory.Paragraph(_list, Loc.T("career.jobs.none"), Theme.FontSmall);
            var w = new[] { 3.4f, 1.4f, 1.6f, 1.6f, 1.6f, 2.2f, 1.6f };
            UiFactory.TableRow(_list, 38, w, new[] { Loc.T("career.col.team"), Loc.T("career.col.category"), Loc.T("career.col.salary"), Loc.T("career.col.budget"),
                Loc.T("career.col.autonomy"), Loc.T("career.col.objective"), "" }, true);
            foreach (var o in offers)
            {
                var offer = o;
                var team = World.GetTeam(o.TeamId);
                var org = World.GetOrganisation(o.OrganisationId);
                var cat = _game.Db.GetCategory(o.CategoryId);
                var row = UiFactory.TableRow(_list, 56, w, new[]
                {
                    "<b>" + team.Name + "</b>\n<color=#9AA4B2>" + Loc.T("owner." + org.Owner) + " · " + Loc.T("career.seasons", o.Seasons) + "</color>",
                    cat.ShortName, MathX.FormatMoney(o.SalaryPerSeason), MathX.FormatMoney(o.AssignedBudget), Loc.T("autonomy." + o.Autonomy), Loc.T("objective." + o.Objective), ""
                });
                var b = UiFactory.Button("Accept", row, Loc.T("career.accept"), () =>
                {
                    var r = CareerService.AcceptJob(World, _game.Db, _manager, offer);
                    if (r.Ok) _session.StartCareer(_game);
                }, Theme.Accent, Theme.FontSmall - 4);
                UiFactory.Place((RectTransform)b.transform, 0.86f, 0.1f, 1f, 0.9f);
            }
            UiFactory.Paragraph(_list, "<color=#9AA4B2>" + Loc.T("career.jobs.note") + "</color>", Theme.FontSmall - 2);
            UiFactory.ButtonRow(_list, 56, UiFactory.Act(Loc.T("career.back"), () => { _step = Step.Path; Render(); }));
        }

        // ------------------------------------------------------------------ founding

        private void Founding()
        {
            var db = _game.Db;
            double wealth = _manager.PersonalWealth;
            double invest = Math.Round(wealth * _investShare);
            var terms = CareerService.LoanTermsFor(_manager, invest);
            double loan = Math.Round(terms.MaxAmount * _loanShare / 10000) * 10000;
            _status.text = Loc.T("career.found.status", MathX.FormatMoney(invest + loan));

            UiFactory.Heading(_list, Loc.T("career.capital"));
            UiFactory.ButtonRow(_list, 52,
                UiFactory.Act(Loc.T("career.invest", "25%"), () => { _investShare = 0.25; Render(); }),
                UiFactory.Act(Loc.T("career.invest", "50%"), () => { _investShare = 0.5; Render(); }),
                UiFactory.Act(Loc.T("career.invest", "75%"), () => { _investShare = 0.75; Render(); }),
                UiFactory.Act(Loc.T("career.invest", "100%"), () => { _investShare = 1; Render(); }));
            UiFactory.ButtonRow(_list, 52,
                UiFactory.Act(Loc.T("career.loanNone"), () => { _loanShare = 0; Render(); }),
                UiFactory.Act(Loc.T("career.loanHalf"), () => { _loanShare = 0.5; Render(); }),
                UiFactory.Act(Loc.T("career.loanMax"), () => { _loanShare = 1; Render(); }));
            var w2 = new[] { 5f, 3f };
            UiFactory.TableRow(_list, 40, w2, new[] { Loc.T("career.ownMoney"), MathX.FormatMoney(invest) + " / " + MathX.FormatMoney(wealth) });
            UiFactory.TableRow(_list, 40, w2, new[] { Loc.T("career.loan"), MathX.FormatMoney(loan) + "  <color=#9AA4B2>" + Loc.T("career.loanTerms", MathX.FormatMoney(terms.MaxAmount), (terms.Rate * 100).ToString("0.0"), terms.Seasons) + "</color>" });

            // Founding sponsors: tied to a category, they only pay if the team is founded there.
            UiFactory.Heading(_list, Loc.T("career.sponsors"));
            bool any = false;
            foreach (var o in World.SponsorOffers)
            {
                if (o.TeamId != CareerService.FoundingProbeId) continue;
                any = true;
                var cat = db.GetCategory(o.RequiredCategoryId);
                UiFactory.Paragraph(_list, "<b>" + o.Name + "</b> <color=#9AA4B2>" + o.Industry + "</color> — " + Loc.T("career.sponsorTerms", MathX.FormatMoney(o.PaymentPerSeason), o.Seasons, cat != null ? cat.ShortName : o.RequiredCategoryId), Theme.FontSmall);
            }
            if (!any) UiFactory.Paragraph(_list, "<color=#9AA4B2>" + Loc.T("career.noSponsors") + "</color>", Theme.FontSmall);

            // Only categories the money can sustain can be chosen.
            UiFactory.Heading(_list, Loc.T("career.categories"));
            var w = new[] { 2f, 2f, 1.6f, 1.6f, 1.6f, 1.6f, 1.6f, 1.6f, 1.5f };
            UiFactory.TableRow(_list, 38, w, new[] { Loc.T("career.col.category"), Loc.T("career.col.verdict"), Loc.T("career.col.startup"), Loc.T("career.col.operating"),
                Loc.T("career.col.confirmed"), Loc.T("career.col.probable"), Loc.T("career.col.lowest"), Loc.T("career.col.reserve"), "" }, true);
            foreach (var r in CareerService.FoundingOptions(World, db, _manager, invest, loan))
            {
                var rep = r;
                var cat = db.GetCategory(r.CategoryId);
                string verdict = r.Verdict == Viability.Viable ? "<color=#3CC878>" : r.Verdict == Viability.Risky ? "<color=#F0B428>" : "<color=#E10600>";
                verdict += Loc.T("viability." + r.Verdict) + "</color>";
                bool selectable = r.Verdict == Viability.Viable || r.Verdict == Viability.Risky;
                var row = UiFactory.TableRow(_list, 50, w, new[]
                {
                    cat.ShortName, verdict, Money(-r.StartupTotal), Money(-r.OperatingCost), MathX.FormatMoney(r.ConfirmedIncome), MathX.FormatMoney(r.ProbableIncome),
                    Money(r.MinCashConfirmed), MathX.FormatMoney(r.Reserve), ""
                }, false, _category == r.CategoryId ? new Color(0.94f, 0.7f, 0.16f, 0.16f) : (Color?)null);
                if (selectable)
                {
                    var b = UiFactory.Button("Pick", row, Loc.T("career.choose"), () => { _category = rep.CategoryId; _supplier = null; Render(); }, Theme.PanelLight, Theme.FontSmall - 4);
                    UiFactory.Place((RectTransform)b.transform, 0.86f, 0.1f, 1f, 0.9f);
                }
                if (r.Reasons.Count > 0 && r.Verdict != Viability.Viable)
                {
                    var reasons = new List<string>();
                    foreach (var reason in r.Reasons) reasons.Add(Loc.T("reason." + reason));
                    string missing = r.Shortfall > 0 && r.Verdict != Viability.NotAvailable ? " " + Loc.T("career.shortfall", MathX.FormatMoney(r.Shortfall)) : "";
                    UiFactory.Paragraph(_list, "<color=#9AA4B2>   " + string.Join(" · ", reasons.ToArray()) + missing + "</color>", Theme.FontSmall - 4);
                }
            }

            if (_category != null)
            {
                var cat = db.GetCategory(_category);
                UiFactory.Heading(_list, Loc.T("career.supplier", cat.Name));
                var suppliers = CareerService.Suppliers(World, _category);
                var acts = new List<KeyValuePair<string, Action>>();
                foreach (var m in suppliers)
                {
                    var maker = m;
                    acts.Add(UiFactory.Act((_supplier == m.Id ? "✓ " : "") + m.Name, () => { _supplier = maker.Id; Render(); }));
                }
                UiFactory.ButtonRow(_list, 52, acts.ToArray());
                UiFactory.Input(_list, Loc.T("career.teamName"), _teamName, v => _teamName = v);

                if (_supplier != null)
                {
                    var report = ViabilityService.Evaluate(World, db, new TeamPlan
                    {
                        CategoryId = _category, Capital = invest + loan, Reputation = CareerService.NewTeamReputation(_manager),
                        ConfirmedSponsorIncome = CareerService.ConfirmedFoundingSponsorship(World, _category),
                        LoanAmount = loan, LoanRate = terms.Rate, LoanSeasons = terms.Seasons
                    });
                    UiFactory.Paragraph(_list, Loc.T("career.summary", MathX.FormatMoney(report.StartupTotal), MathX.FormatMoney(report.LiquidityAfterStartup),
                        MathX.FormatMoney(report.EndCashConfirmed), MathX.FormatMoney(report.EndCashProbable)), Theme.FontSmall);
                    if (report.Verdict == Viability.Risky)
                        UiFactory.Paragraph(_list, "<color=#F0B428>" + Loc.T("career.riskyWarning") + "</color>", Theme.FontSmall);
                    UiFactory.ButtonRow(_list, 64, UiFactory.Act(Loc.T("career.confirmFound"), () =>
                    {
                        string name = string.IsNullOrEmpty(_teamName.Trim()) ? _manager.LastName + " Racing" : _teamName.Trim();
                        var res = CareerService.Found(World, db, _manager, new FoundingPlan { CategoryId = _category, ManufacturerId = _supplier, TeamName = name, Investment = invest, Loan = loan });
                        if (res.Ok) _session.StartCareer(_game);
                        else
                        {
                            _status.text = res.Message;
                        }
                    }));
                }
            }
            UiFactory.ButtonRow(_list, 56, UiFactory.Act(Loc.T("career.back"), () => { _step = Step.Path; _category = null; Render(); }));
        }
    }
}

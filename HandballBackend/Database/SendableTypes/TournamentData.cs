using HandballBackend.Database.Models;
using HandballBackend.Utils;

namespace HandballBackend.Database.SendableTypes;

public class TournamentData {
    public string Name { get; private set; }
    public string SearchableName { get; private set; }
    public bool Editable { get; private set; }
    public string FixturesType { get; private set; }
    public string FinalsType { get; private set; }
    public bool Ranked { get; private set; }
    public bool TwoCourts { get; private set; }
    public bool HasScorer { get; private set; }
    public bool Finished { get; private set; }
    public bool InFinals { get; private set; }
    public bool IsPooled { get; private set; }
    public bool Started { get; private set; }
    public string Notes { get; private set; }
    public string ImageUrl { get; private set; }
    public bool UsingBadmintonServes { get; private set; }
    public string Color { get; private set; }

    public TournamentData(Tournament tournament, bool isAdmin = false) {
        Name = tournament.Name;
        Started = tournament.Started;
        SearchableName = tournament.SearchableName;
        FixturesType = tournament.FixturesType;
        FinalsType = tournament.FinalsType;
        Ranked = tournament.Ranked;
        TwoCourts = tournament.TwoCourts;
        HasScorer = tournament.HasScorer;
        Finished = tournament.Finished;
        InFinals = tournament.InFinals;
        IsPooled = tournament.IsPooled;
        Notes = tournament.Notes ?? string.Empty;
        ImageUrl = Utilities.FixHandballUrl(tournament.ImageUrl);
        UsingBadmintonServes = tournament.BadmintonServes;
        Editable = tournament.Editable;
        Color = tournament.Color;
    }
}
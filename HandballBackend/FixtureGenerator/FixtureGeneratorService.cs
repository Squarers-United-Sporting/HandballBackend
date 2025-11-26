using HandballBackend.Database.Models;
using HandballBackend.EndpointHelpers;
using HandballBackend.EndpointHelpers.GameManagement;
using HandballBackend.Events;

namespace HandballBackend.FixtureGenerator;

public interface IFixtureGeneratorService {
    List<string> GetFixtureGeneratorNames();

    List<string> GetFinalsGeneratorNames();

    AbstractFixtureGenerator GetFixtureGeneratorByName(string name, int tournamentId);

    Task EndRound(Tournament tournament);

    Task BeginTournament(Tournament tournament);
}

public class FixtureGeneratorService(HandballContext db, IGameManagementService gameManager, IBackupService backup)
    : IFixtureGeneratorService, IEventHandler<RoundEndEvent> {
    public HandballContext Context { get; private set; } = db;
    public IGameManagementService GameManager { get; private set; } = gameManager;
    public IBackupService Backup { get; private set; } = backup;

    private readonly Dictionary<string, Func<int, FixtureGeneratorService, AbstractFixtureGenerator>>
        _fixtureGenerators = new();

    private readonly Dictionary<string, Func<int, FixtureGeneratorService, AbstractFixtureGenerator>>
        _finalsGenerators = new();

    private bool _isPopulated = false;


    private void Register(Func<int, FixtureGeneratorService, AbstractFixtureGenerator> func, string name,
        bool isFinal) {
        if (isFinal) {
            _finalsGenerators[name] = func;
        } else {
            _fixtureGenerators[name] = func;
        }
    }

    private void PopulateFixtures() {
        _isPopulated = true;
        Register((tid, fixtureGen) => new OneRound(tid, fixtureGen), "OneRound", false);
        Register((tid, fixtureGen) => new Pooled(tid, fixtureGen), "Pooled", false);
        Register((tid, fixtureGen) => new RoundRobin(tid, fixtureGen), "RoundRobin", false);
        Register((tid, fixtureGen) => new Swiss(tid, fixtureGen), "Swiss", false);
        Register((tid, fixtureGen) => new Pooled(tid, fixtureGen, blitz: true), "PooledBlitz", false);
        Register((tid, fixtureGen) => new RoundRobin(tid, fixtureGen, blitz: true), "RoundRobinBlitz", false);


        Register((tid, fixtureGen) => new PooledFinals(tid, fixtureGen), "PooledFinals", true);
        Register((tid, fixtureGen) => new BasicFinals(tid, fixtureGen), "BasicFinals", true);
        Register((tid, fixtureGen) => new TopThreeFinals(tid, fixtureGen), "TopThreeFinals", true);
    }

    public AbstractFixtureGenerator GetFixtureGeneratorByName(string name, int tournamentId) {
        if (!_isPopulated) {
            PopulateFixtures();
        }

        if (_fixtureGenerators.TryGetValue(name, out var func) || _finalsGenerators.TryGetValue(name, out func)) {
            return func(tournamentId, this);
        }

        throw new ArgumentException($"Unknown fixture generator {name}");
    }

    public async Task EndRound(Tournament tournament) {
        var finals = tournament.InFinals;
        if (!finals) {
            finals = await GetFixtureGeneratorForTournament(tournament).EndOfRound();
        }

        if (finals && !tournament.Finished) {
            await GetFinalGeneratorForTournament(tournament).EndOfRound();
        }
    }

    public async Task BeginTournament(Tournament tournament) {
        (await db.Tournaments.FindAsync(tournament.Id))!.Started = true;
        await db.SaveChangesAsync();
        await GetFixtureGeneratorForTournament(tournament).BeginTournament();
    }

    public AbstractFixtureGenerator GetFinalGeneratorForTournament(Tournament tournament) =>
        GetFixtureGeneratorByName(tournament.FinalsType, tournament.Id);

    public AbstractFixtureGenerator GetFixtureGeneratorForTournament(Tournament tournament) =>
        GetFixtureGeneratorByName(tournament.FixturesType, tournament.Id);


    public List<string> GetFixtureGeneratorNames() {
        if (!_isPopulated) {
            PopulateFixtures();
        }

        return _fixtureGenerators.Keys.ToList();
    }

    public List<string> GetFinalsGeneratorNames() {
        if (!_isPopulated) {
            PopulateFixtures();
        }

        return _finalsGenerators.Keys.ToList();
    }

    public async Task Handle(RoundEndEvent @event) {
        var tournament = Context.Tournaments.First(t => t.Id == @event.TournamentId);
        await EndRound(tournament);
    }
}
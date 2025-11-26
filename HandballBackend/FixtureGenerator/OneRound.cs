namespace HandballBackend.FixtureGenerator;

public class OneRound(int tournamentId, FixtureGeneratorService fixtureGen) : AbstractFixtureGenerator(tournamentId, fixtureGen, true, false);
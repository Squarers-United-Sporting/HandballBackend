namespace HandballBackend.Authentication;

[AttributeUsage(AttributeTargets.Method)]
public class TournamentSpecificAttribute : Attribute {
    public string ParameterName { get; }
    public bool IsGame { get; }

    public TournamentSpecificAttribute(string parameterName, bool isGame = false) {
        ParameterName = parameterName;
        IsGame = isGame;
    }
}
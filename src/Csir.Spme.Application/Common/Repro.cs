namespace Csir.Spme.Application.Common;

public sealed record ReproCommand(string Status, string Name);

public static class ReproClass
{
    public static async Task<string> GoAsync(ReproCommand command)
    {
        await Task.Delay(1);
        var check = command.Status.Trim() is "x";
        var requestedStatus = command.Status.Trim();
        return requestedStatus + command.Name.Trim() + check;
    }
}

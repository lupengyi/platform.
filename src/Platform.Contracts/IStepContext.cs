namespace Platform.Contracts;

public interface IStepContext
{
    IDictionary<string, object?> Items { get; }
}

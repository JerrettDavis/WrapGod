namespace Acme.Lib;

public sealed class FooService
{
    public string DoWork(string input) => $"acme:{input}";

    public int GetStatus() => 200;

    public string Name => "Foo";
}

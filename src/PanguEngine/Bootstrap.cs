namespace PanguEngine;

public static class Bootstrap
{
    public static void Launch(string[] args)
    {
        new Client.ClientEngine().Run();
    }
}

using System.CommandLine;
using HelseId.SampleApi.Configuration;
using HelseID.Samples.Configuration;

namespace HelseId.SampleAPI;

// This file is used for bootstrapping the example. Nothing of interest here.
public static class Program
{
    public static async Task Main(string[] args)
    {
        var settings = CreateSettings();
        await new Startup(settings).BuildWebApplication().RunAsync();  
    }
    
    private static Settings CreateSettings()
    {
        return new Settings
        {
            ApiPort = 5002,
            Audience = "nhn:BearerApi",
            AuthCodeApiScopeForSampleApi = "nhn:BearerApi/api",
        };
    }
}
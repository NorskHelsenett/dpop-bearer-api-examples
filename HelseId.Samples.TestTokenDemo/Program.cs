using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Json;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HelseId.Samples.Common.JwtTokens;
using HelseId.Samples.TestTokenDemo.Configuration;
using HelseId.Samples.TestTokenDemo.TttModels.Request;
using HelseId.Samples.TestTokenDemo.TttModels.Response;
using Spectre.Console.Rendering;

namespace HelseId.Samples.TestTokenDemo;

internal abstract class Program
{
    private static async Task<int> Main(string[] args)
    {
        var config = GetConfig();

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            await Console.Error.WriteLineAsync("No ApiKey specified. Add ApiKey to appsettings.json.");
            return 1;
        }

        var font = FigletFont.Load("starwars.flf");

        AnsiConsole.Write(
            new FigletText(font, "HelseID")
                .LeftJustified()
                .Color(Color.Aqua));

        using var tttHttpClient = new HttpClient();

        tttHttpClient.DefaultRequestHeaders.Add("X-Auth-Key", config.ApiKey);
        
        //await DemoApiDpopToken(tttHttpClient, config);
        await DemoApiBearerToken(tttHttpClient, config);
        return 0;
    }
    
    private static async Task DemoApiDpopToken (HttpClient tttHttpClient, TttConfig config)
    {
        var model = new TestTokenRequest
        {
            Audience = "nhn:DpopApi",
            ClientClaimsParameters = new ClientClaimsParameters
            {
                Scope = ["nhn:DpopApi/api"]
            },
            CreateDPoPTokenWithDPoPProof = true,
            DPoPProofParameters = new DPoPProofParameters
            {
                HtmClaimValue = "GET",
                HtuClaimValue = "https://localhost:5001/user-login-clients/greetings",

                // Test different invalid DPoP cases:
                // InvalidDPoPProofParameters = InvalidDPoPProofParameters.DontSetHtuClaimValue,
                // InvalidDPoPProofParameters = InvalidDPoPProofParameters.SetIatValueInThePast,
                // InvalidDPoPProofParameters = InvalidDPoPProofParameters.DontSetAthClaimValue,
                // InvalidDPoPProofParameters = InvalidDPoPProofParameters.SetAlgHeaderToASymmetricAlgorithm,
                // InvalidDPoPProofParameters = InvalidDPoPProofParameters.SetAnInvalidSignature,
            },
            UserClaimsParameters = new UserClaimsParameters
            {
                Name = "Julenissen",
                SecurityLevel = "4",
                Pid = "05898597468",
            },
            SetPidPseudonym = true,
            GetHprNumberFromHprregisteret = true

            // Test different invalid token cases:
            // SetInvalidIssuer = true,
            // SignJwtWithInvalidSigningKey = true,
            // ExpirationParameters = new ExpirationParameters
            // {
            //     SetExpirationTimeAsExpired = true,
            // },
        };

        var (accessToken, dpopProof) = await GetAccessToken(config, tttHttpClient, model);
        PrintTokens(accessToken, dpopProof);

        await ApiGet(
            "https://localhost:5001/user-login-clients/greetings", 
            accessToken, dpopProof
        );
    }

    private static async Task DemoApiBearerToken (HttpClient tttHttpClient, TttConfig config)
    {
        var model = new TestTokenRequest
        {
            Audience = "nhn:BearerApi",
            ClientClaimsParameters = new ClientClaimsParameters
            {
                Scope = ["nhn:BearerApi/api"]
            },
            UserClaimsParameters = new UserClaimsParameters
            {
                Name = "Julenissen",
                SecurityLevel = "4",
                Pid = "05898597468",
            },
            SetPidPseudonym = true,
            GetHprNumberFromHprregisteret = true

            // Test different invalid token cases:
            // SetInvalidIssuer = true,
            // SignJwtWithInvalidSigningKey = true,
            // ExpirationParameters = new ExpirationParameters
            // {
            //     SetExpirationTimeAsExpired = true,
            // },
        };

        var (accessToken, _) = await GetAccessToken(config, tttHttpClient, model);
        PrintTokens(accessToken);

        await ApiGet(
            "https://localhost:5002/user-login-clients/greetings", 
            accessToken
        );
    }
    private static async Task<(string AccessToken, string? DpopToken)> GetAccessToken(TttConfig config,
        HttpClient httpClient, TestTokenRequest model)
    {
        var response = await httpClient.PostAsJsonAsync(config.TttUri, model, options: JsonSerializerOptions);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(await response.Content.ReadAsStringAsync());
        }

        var tttResponse = await response.Content.ReadFromJsonAsync<TestTokenResponse>();

        if (tttResponse == null)
        {
            throw new Exception("Response was deserialized to null");
        }

        if (tttResponse.IsError)
        {
            throw new Exception($"Received error response from TTT: {tttResponse.ErrorResponse.ErrorMessage}");
        }

        return (tttResponse.SuccessResponse.AccessTokenJwt, tttResponse.SuccessResponse.DPoPProof);
    }

    private static TttConfig GetConfig()
    {
        var builder =
            new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Local.json", optional: true);

        IConfiguration configuration = builder.Build();

        var config = configuration.Get<TttConfig>();

        if (config == null)
        {
            throw new Exception("Config is null.");
        }

        return config;
    }

    private static void PrintTokens(string accessToken, string? dpopToken = null)
    {
        if (dpopToken != null)
        {
            PrintDpopToken(dpopToken);
        }

        PrintAccessToken(accessToken);
    }

    private static void PrintAccessToken(string accessToken)
    {
        PrintToken(accessToken, "Access token payload");
    }

    private static void PrintDpopToken(string dpopToken)
    {
        PrintToken(dpopToken, "DPoP token payload");
    }

    private static void PrintToken(string token, string heading)
    {
        var payload = JwtDecoder.Decode(token);
        PrintBorderedContent(CreateRenderableJsonText(payload), heading);
    }

    private static async Task ApiGet(string apiUri, string accessToken, string? dpopToken = null,
        Dictionary<string, string>? extraHeaders = null)
    {
        var message = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(apiUri),
        };
        
        if (extraHeaders != null)
        {
            foreach (var (key, value) in extraHeaders)
            {
                message.Headers.Add(key, value);
            }
        }

        using var httpClient = CreateApiClient(accessToken, dpopToken);

        var response = await httpClient.SendAsync(message);
        await PrintResponse(response);
    }
    
    private static HttpClient CreateApiClient(string accessToken, string? dpopToken,
        IDictionary<string, string>? extreaHeaders = null)
    {
        var httpClient = new HttpClient();
        if (dpopToken != null)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("DPoP", accessToken);
            httpClient.DefaultRequestHeaders.Add("DPoP", dpopToken);
        }
        else
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return httpClient;
    }

    private static async Task PrintResponse(HttpResponseMessage response)
    {
        var status = response.StatusCode;
        var body = await response.Content.ReadAsStringAsync();

        PrintBorderedContent(status != HttpStatusCode.OK ? new Text(status.ToString()) : CreateRenderableJsonText(body),
            "API response");
    }

    private static void PrintBorderedContent(IRenderable content, string header)
    {
        AnsiConsole.Write(
            new Panel(content)
                .Header(header)
                .Collapse()
                .RoundedBorder()
                .BorderColor(Color.Yellow));
    }

    private static JsonText CreateRenderableJsonText(string json)
    {
        return new JsonText(json)
            .MemberColor(Color.Aqua)
            .StringColor(Color.HotPink)
            .NumberColor(Color.MediumOrchid);
    }

    static Program()
    {
        JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };

        JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions;
}
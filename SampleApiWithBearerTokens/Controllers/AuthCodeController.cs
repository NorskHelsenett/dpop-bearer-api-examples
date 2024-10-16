using HelseId.SampleApi.Interfaces;
using HelseId.Samples.Common.Models;
using HelseID.Samples.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelseId.SampleAPI.Controllers;

[ApiController]
[Authorize(Policy = Startup.AuthCodePolicy, AuthenticationSchemes = Startup.BearerTokenAuthenticationScheme)]
public class AuthCodeController : ControllerBase
{
    private readonly IApiResponseCreator _responseCreator;
    public AuthCodeController(IApiResponseCreator responseCreator)
    {
        _responseCreator = responseCreator;
    }

    [HttpGet]
    [Route(ConfigurationValues.AuthCodeClientResource)]
    public ActionResult<ApiResponse> GetGreetings()
    {
        return CreateResult("Sample API (with Bearer token)");
    }


    private ActionResult<ApiResponse> CreateResult(string apiName)
    {
        var claims = User.Claims.ToList();
        
        var apiResponse = _responseCreator.CreateApiResponse(claims, apiName);
        
        return new JsonResult(apiResponse);
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Extensions;

namespace Z1R_SignalRHost.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NegotiateController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            var request = HttpContext.Request;

            // Use GetDisplayUrl() to infer exact inbound URL
            var baseUrl = UriHelper.BuildAbsolute(
                request.Scheme,
                request.Host,
                request.PathBase
            );

            // Replace path with `/hub`
            var hubUrl = $"{request.Scheme}://{request.Host}/hub";

            var response = new
            {
                url = hubUrl,
                accessToken = ""
            };

            return Ok(response);
        }
    }
}

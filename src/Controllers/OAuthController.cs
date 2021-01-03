using DevOps.Manager.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;

namespace DevOps.Manager.Controllers
{
    public class OAuthController : Controller
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly Dictionary<Guid, OAuthTokenModel> requests = new Dictionary<Guid, OAuthTokenModel>();
        private readonly IConfiguration _configuration;
        private readonly ILogger<OAuthController> _logger;

        public OAuthController(ILogger<OAuthController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        private String GetAuthorizationUrl(String state)
        {
            UriBuilder uriBuilder = new UriBuilder(_configuration["OAuth:AuthorizeUrl"]);
            var queryParams = HttpUtility.ParseQueryString(uriBuilder.Query ?? String.Empty);

            queryParams["client_id"] = _configuration["OAuth:AppID"];
            queryParams["response_type"] = "Assertion";
            queryParams["state"] = state;
            queryParams["scope"] = _configuration["OAuth:AuthorizedScopes"];
            queryParams["redirect_uri"] = _configuration["OAuth:ApplicationCallbackUrl"];

            uriBuilder.Query = queryParams.ToString();

            return uriBuilder.ToString();
        }

        public ActionResult Authorize()
        {
            Guid state = Guid.NewGuid();
            requests[state] = new OAuthTokenModel() { IsPending = true };
            string authUrl = GetAuthorizationUrl(state.ToString());
            _logger.LogDebug($"Sending authorization request to: {authUrl}");
            return new RedirectResult(authUrl);
        }

        public async Task<ActionResult> Callback(String code, Guid state)
        {
            _logger.LogDebug($"Callback got code: {code} and state: {state}");
            String error;
            if (Validate(code, state.ToString(), out error))
            {
                // Exchange the auth code for an access token and refresh token
                HttpRequestMessage requestToken = new HttpRequestMessage(HttpMethod.Post, _configuration["OAuth:RequestTokenUrl"]);
                requestToken.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                Dictionary<String, String> form = new Dictionary<String, String>()
                {
                    { "client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer" },
                    //{ "client_assertion", _configuration["OAuth:AppSecret"] },
                    { "client_assertion", _configuration["OAuth:ClientSecret"] },
                    { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                    { "assertion", code },
                    { "redirect_uri", _configuration["OAuth:ApplicationCallbackUrl"] }
                };
                requestToken.Content = new FormUrlEncodedContent(form);

                HttpResponseMessage tokenResponse = await client.SendAsync(requestToken);

                if (tokenResponse.IsSuccessStatusCode)
                {
                    String body = await tokenResponse.Content.ReadAsStringAsync();

                    OAuthTokenModel tokenModel = requests[state];
                    JsonConvert.PopulateObject(body, tokenModel);

                    ViewBag.Token = tokenModel;
                }
                else
                {
                    error = tokenResponse.ReasonPhrase;
                }
            }

            if (!String.IsNullOrEmpty(error))
            {
                ViewBag.Error = error;
            }

            ViewBag.ProfileUrl = _configuration["OAuth:ProfileUrl"];

            return View("Views/Home/Index.cshtml");
        }

        private static bool Validate(String code, String state, out String error)
        {
            error = null;

            if (String.IsNullOrEmpty(code))
            {
                error = "Invalid auth code";
            }
            else
            {
                Guid authorizationRequestKey;
                if (!Guid.TryParse(state, out authorizationRequestKey))
                {
                    error = "Invalid authorization request key";
                }
                else
                {
                    OAuthTokenModel token;
                    if (!requests.TryGetValue(authorizationRequestKey, out token))
                    {
                        error = "Unknown authorization request key";
                    }
                    else if (!token.IsPending)
                    {
                        error = "Authorization request key already used";
                    }
                    else
                    {
                        requests[authorizationRequestKey].IsPending = false; // mark the state value as used so it can't be reused
                    }
                }
            }

            return error == null;
        }

        public async Task<ActionResult> RefreshToken(string refreshToken)
        {
            String error = null;
            if (!String.IsNullOrEmpty(refreshToken))
            {
                // Form the request to exchange an auth code for an access token and refresh token
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, _configuration["OAuth:RequestTokenUrl"]);
                requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                Dictionary<String, String> form = new Dictionary<String, String>()
                {
                    { "client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer" },
                    { "client_assertion", _configuration["OAuth:ClientSecret"] },
                    { "grant_type", "refresh_token" },
                    { "assertion", refreshToken },
                    { "redirect_uri", _configuration["OAuth:ApplicationCallbackUrl"] }
                };
                requestMessage.Content = new FormUrlEncodedContent(form);

                // Make the request to exchange the auth code for an access token (and refresh token)
                HttpResponseMessage responseMessage = await client.SendAsync(requestMessage);

                if (responseMessage.IsSuccessStatusCode)
                {
                    // Handle successful request
                    String body = await responseMessage.Content.ReadAsStringAsync();
                    ViewBag.Token = JObject.Parse(body).ToObject<OAuthTokenModel>();
                }
                else
                {
                    error = responseMessage.ReasonPhrase;
                }
            }
            else
            {
                error = "Invalid refresh token";
            }

            if (!String.IsNullOrEmpty(error))
            {
                ViewBag.Error = error;
            }

            return View("Views/Home/Index.cshtml");
        }
    }
}

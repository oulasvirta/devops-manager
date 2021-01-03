# To contribute...

## Create Azure web app with Azure CLI

Login to `https://shell.azure.com` and execute following commands.

```bash
# Ensure that web app has globally unique name
$appName=App2020-12-30 

# Create a resource group.
az group create --location westeurope --name "myResourceGroup"

# Create an App Service plan in FREE tier.
az appservice plan create --name "myAppServicePlan" --resource-group "myResourceGroup" --sku FREE

# Create a web app.
az webapp create --name $myWebAppName --resource-group "myResourceGroup" --plan "myAppServicePlan"
```



## Authorize the web app

To get user's consent following request needs to be sent to Azure DevOps.

```http
https://app.vssps.visualstudio.com/oauth2/authorize
  ?client_id={App ID}
  &response_type=Assertion
  &state=ConsentRequest
  &scope=vso.work
  &redirect_uri={https://{myWebAppWithRandomSuffix}.azurewebsites.net/oauth/callback}
```

User is then sent to login experience and Azure DevOps Services asks user to authorize the web app. After consent is given, user is redirected to the callback URL (defined during app regsistration) with _authorization code_ and _state value_ added in the query string:

```http
https://{myWebAppWithRandomSuffix}.azurewebsites.net/oauth/callback
  ?code={authorization code}
  &state=ConsentPending
```

## Get an access and refresh token for the user

Now you use the authorization code to request an access token (and refresh token) for the user. Your service must make a service-to-service HTTP request to Azure DevOps Services.

### URL

```http
https://app.vssps.visualstudio.com/oauth2/token
```

### HTTP request headers

|  Header           | Value 
|-------------------|------------------------------------------------------------------
| Content-Type      | `application/x-www-form-urlencoded`
| Content-Length    | Calculated string length of the request body (see below)

```no-highlight
Content-Type: application/x-www-form-urlencoded
Content-Length: 1322
```

### HTTP request body
```no-highlight
client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer&client_assertion={0}&grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer&assertion={1}&redirect_uri={2}
```
<br>
Replace the placeholder values in the sample request body above:

* **{0}**: URL encoded client secret acquired when the app was registered
* **{1}**: URL encoded "code" provided via the `code` query parameter to your callback URL
* **{2}**: callback URL registered with the app

#### C# example to form the request body

```no-highlight
public string GenerateRequestPostData(string appSecret, string authCode, string callbackUrl)
{
   return String.Format("client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer&client_assertion={0}&grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer&assertion={1}&redirect_uri={2}",
               HttpUtility.UrlEncode(appSecret),
               HttpUtility.UrlEncode(authCode),
               callbackUrl
        );
}
```

### Response
```json
{
    "access_token": { access token for the user },
    "token_type": { type of token },
    "expires_in": { time in seconds that the token remains valid },
    "refresh_token": { refresh token to use to acquire a new access token }
}
```

> [!IMPORTANT]
> Securely persist the <em>refresh_token</em> so your app doesn't need to prompt the user to authorize again. <em>Access tokens</em> expire relatively quickly and shouldn't be persisted.

## Use the access token

To use an access token, include it as a bearer token in the Authorization header of your HTTP request:

```
Authorization: Bearer {access_token}
```

For example, the HTTP request to [get recent builds](https://visualstudio.com/api/build-release/builds.md#getalistofbuilds) for a project:

```no-highlight
GET https://dev.azure.com/myaccount/myproject/_apis/build-release/builds?api-version=3.0
Authorization: Bearer {access_token}
```

## Refresh an expired access token

If a user's access token expires, you can use the refresh token that they acquired in the authorization flow to get a new access token. It's like the original process for exchanging the authorization code for an access and refresh token.

### URL
```no-highlight
POST https://app.vssps.visualstudio.com/oauth2/token
```

### HTTP request headers

|  Header           | Value 
|-------------------|------------------------------------------------------------------
| Content-Type      | `application/x-www-form-urlencoded`
| Content-Length    | Calculated string length of the request body (see below)

```no-highlight
Content-Type: application/x-www-form-urlencoded
Content-Length: 1654
```

### HTTP request body
```no-highlight
client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer&client_assertion={0}&grant_type=refresh_token&assertion={1}&redirect_uri={2}
```
<br>
Replace the placeholder values in the sample request body above:

* **{0}**: URL encoded client secret acquired when the app was registered
* **{1}**: URL encoded refresh token for the user
* **{2}**: callback URL registered with the app


### Response
```json
{
    "access_token": { access token for this user },
    "token_type": { type of token },
    "expires_in": { time in seconds that the token remains valid },
    "refresh_token": { new refresh token to use when the token has timed out }
}
```
> [!IMPORTANT]
> A new refresh token gets issued for the user. Persist this new token and use it the next time you need to acquire a new access token for the user.

<a name="scopes"></a>

## Scopes

> [!IMPORTANT]
> Scopes only enable access to REST APIs and select Git endpoints. SOAP API access isn't supported.  

[!INCLUDE [scopes table](../../includes/scopes.md)]

[Register your app](#register-your-app) and use scopes to indicate which permissions in Azure DevOps Services that your app requires.
When your users authorize your app to access their organization, they authorize it for those scopes.
[Requesting the authorization](#authorize-your-app) passes the same scopes that you registered.

## Samples

You can find a C# sample that implements OAuth to call Azure DevOps Services REST APIs in our [C# OAuth GitHub Sample](https://github.com/Microsoft/vsts-auth-samples/tree/master/OAuthWebSample).

## Frequently asked questions (FAQs)

<!-- BEGINSECTION class="md-qanda" -->

### Q: Can I use OAuth with my mobile phone app?

A: No. Azure DevOps Services only supports the web server flow,
so there's no way to implement OAuth, as you can't securely store the app secret.

### Q: What errors or special conditions do I need to handle in my code?

A: Make sure that you handle the following conditions:

- If your user denies your app access, no authorization code gets returned. Don't use the authorization code without checking for denial.
- If your user revokes your app's authorization, the access token is no longer valid. When your app uses the token to access data, a 401 error returns. Request authorization again.

### Q: I want to debug my web app locally. Can I use localhost for the callback URL when I register my app?

A: Yes. Azure DevOps Services now allows localhost in your callback URL. Ensure you use `https://localhost` as the beginning of your callback URL when you register your app.

### Q: I get an HTTP 400 error when I try to get an access token. What might be wrong?

A: Check that you set the content type to application/x-www-form-urlencoded in your request header.

### Q: Can I use OAuth with the SOAP endpoints and REST APIs?

A: No. OAuth is only supported in the REST APIs at this point.

<!-- ENDSECTION --> 

## Related articles

- [Choosing the right authentication method](authentication-guidance.md)
- [Default permissions and access for Azure DevOps](../../../organizations/security/permissions-access.md)
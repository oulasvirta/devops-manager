---
layout: post
title:  "Unit 1 - OAuth and Azure DevOps REST API"
date:   2021-01-02 12:04:50 +0200
categories: Azure DevOps REST OAuth
---

## Introduction to Azure DevOps REST authorization

In this unit I'll walkthrough steps to create an application that gets Azure DevOps user's profile details using Azure DevOps REST API. Authentication is done using OAuth so that username and password is not needed. Azure DevOps Services uses the OAuth 2.0 protocol to authorize your app for a user and generate an access token. Use this token when you call the REST APIs from your app.

> 💡 TIP: Azure DevOps Services uses the [OAuth 2.0 protocol](https://oauth.net/2/) to authorize registered app for a user and generate an **access token** and **refresh token**.

### Learning objectives

After completing steps in this module, you will be able to:

- Create dotnet core MVC app.
- Register your app as Azure DevOps authorized application.
- Make requests to [Azure DevOps REST API](https://docs.microsoft.com/en-us/rest/api/azure/devops) using OAuth.
- Understand basics of dotnet core app settings configuration.
- Debug Azure DevOps authorization and access token flow and REST API calls.

### Prerequisites

- Basic knowledge of Azure DevOps.
- Basic knowledge about OAuth.
- [.NET Core SDK](https://dotnet.microsoft.com/download/dotnet/5.0) installed.
- [VS code](https://code.visualstudio.com/download) installed.

### OAuth flow to request Auzure DevOps REST API

OAuth authorization sequence is a nutshell following:

- **Request auhtorization for user**: Using App ID you got from `VisualStudio.com` (old address) or `aex.dev.azure.com` registration, you can send users to Azure DevOps Services, and ask them to authorize your app to access their organizations with predefined _Authorized scope(s)_.
- **Send authorization**: When user grants authorization for your app, `aex.dev.azure.com` will send short lived authorization code as query string parameter to your app's _Authorization callback URL_.
- **Get Access token**: Authorization code and your app's _client secret_ is then used to get an _access token_ for that user.
- **CALL REST API with access token**:  When you call Azure DevOps REST APIs for that user, use that user's access token. Access tokens do expire. Refresh token is used to get new access token if it's expired.

  ![oauth flow](https://docs.microsoft.com/azure/devops/integrate/get-started/authentication/media/oauth-overview.png)

### Create a .NET Core app

Use .NET command-line interface (CLI) create new Model-View-Controller app by executing following command in the root of your repository.

```powershell
dotnet new mvc -n DevOps.Manager -o src
```

This will create a simple csharp project `DevOps.Manager.csproj` with MVC file structure into `src` folder. In later steps you will be adding Model and Controller for to handle OAuth authorization flow. But for now, we need for next step the port where you app is executing when running it locally. Open `src\Properties\launchSettings.json` and review and copy the `sslPort` setting value.

```json
{
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "http://localhost:38538",
      "sslPort": 44307
    }
  },
  ...
}
```

> 💡 TIP: The .NET CLI is included with the [.NET SDK](https://docs.microsoft.com/dotnet/core/sdk). To learn how to install the .NET SDK, see [Install .NET Core](https://docs.microsoft.com/dotnet/core/install/windows).

### Register app to Azure DevOps

To debug locally `aex.dev.azure.com` OAuth flow, register your app with _Callback URL_ that uses your dotnet core app's local iisExpress address as baseurl https://localhost:44307 (verify your sslPort setting).

> 💡 TIP: Azure DevOps applications can be registered at: [https://aex.dev.azure.com/app/register](https://aex.dev.azure.com/app/register).

Application registration has three sections:

- **Company information**: these details are shown to user when they asked to grant OAuth authorization request.
- **Application information**: for now use localhost based settings.
  - _Website URL_: `https://localhost:44307/`
  - _Callback URL_: `https://localhost:44307/oauth/callback`
- **Authorized scopes**: to access user's details check _User profile (read)_ selection, which maps to `vso.profile` REST API scope that grants the app to read user's profile, accounts, collections, projects, teams, and other top-level organizational details.

> 💡 NOTE: Authorized [scopes](https://docs.microsoft.com/azure/devops/integrate/get-started/authentication/oauth?#scopes) selection cannot be changed afterwards. You need delete and re-create the application registration to changes scope selection. This will generate new App ID and Client secrets and requires your user's to reauthorize the app.

After registration web app's **Application Settings** are shown. These details are used to authorize the web app to access, in this case, user's profile details on behalf of user.

Review your app's **Application Settings**:

- **Authorize URL** is called with **App ID** and **Authorized Scopes** (vso.profile) to ask user's consent for authorize the web app to access their user profile details.
- **Access Token URL** is called with **Client Secret** when web app needs to get an _access token_ to call an Azure DevOps REST API or when the access token needs to be refreshed with _refresh token_.

> 💡 TIP: _Application Settings_ can be accessed and edited in Azure DevOps profile page: `https://aex.dev.azure.com/me` in _Applications and services_ section.

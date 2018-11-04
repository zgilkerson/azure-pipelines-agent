using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.OAuth;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Configuration
{
    public interface ICredentialProvider
    {
        CredentialData CredentialData { get; set; }
        VssCredentials GetVssCredentials(IHostContext context);
        void EnsureCredential(IHostContext context, CommandSettings command, string serverUrl);
    }

    public abstract class CredentialProvider : ICredentialProvider
    {
        public CredentialProvider(string scheme)
        {
            CredentialData = new CredentialData();
            CredentialData.Scheme = scheme;
        }

        public CredentialData CredentialData { get; set; }

        public abstract VssCredentials GetVssCredentials(IHostContext context);
        public abstract void EnsureCredential(IHostContext context, CommandSettings command, string serverUrl);
    }

    public sealed class AadDeviceCodeAccessToken : CredentialProvider
    {
        public AadDeviceCodeAccessToken() : base(Constants.Configuration.AAD) { }

        public override VssCredentials GetVssCredentials(IHostContext context)
        {
            ArgUtil.NotNull(context, nameof(context));
            Tracing trace = context.GetTrace(nameof(AadDeviceCodeAccessToken));
            trace.Info(nameof(GetVssCredentials));
            ArgUtil.NotNull(CredentialData, nameof(CredentialData));
            // string account;
            // if (!CredentialData.Data.TryGetValue(Constants.Agent.CommandLine.Args.Account, out account))
            // {
            //     account = null;
            // }

            string serverUrl;
            if (!CredentialData.Data.TryGetValue(Constants.Agent.CommandLine.Args.Url, out serverUrl))
            {
                serverUrl = null;
            }

            // ArgUtil.NotNullOrEmpty(account, nameof(account));
            ArgUtil.NotNullOrEmpty(serverUrl, nameof(serverUrl));

            // string aadAuthority;
            // string serverUrlHostName = new Uri(serverUrl).Host;
            // if (serverUrlHostName.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase) ||
            //     serverUrlHostName.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
            // {
            //     aadAuthority = "https://login.microsoftonline.com";
            // }
            // else if (serverUrlHostName.EndsWith(".vsts.io", StringComparison.OrdinalIgnoreCase) ||
            //          serverUrlHostName.Equals("codeapp.ms", StringComparison.OrdinalIgnoreCase) ||
            //          serverUrlHostName.EndsWith(".vsts.me", StringComparison.OrdinalIgnoreCase) ||
            //          serverUrlHostName.Equals("codedev.ms", StringComparison.OrdinalIgnoreCase))
            // {
            //     aadAuthority = "https://login.windows-ppe.net";
            // }
            // else
            // {
            //     throw new NotSupportedException($"Server url '{serverUrl}' is not support AAD login.");
            // }

            // trace.Info("AAD account: {account}");
            // MailAddress email = new MailAddress(account);
            LoggerCallbackHandler.Callback = new AadTrace(trace);
            LoggerCallbackHandler.UseDefaultLogging = false;
            var tenantUrl = GetAccountTenantUrl(context, serverUrl);
            if (tenantUrl == null)
            {
                throw new NotSupportedException($"Server url '{serverUrl}' is not support AAD login.");
            }

            AuthenticationContext ctx = new AuthenticationContext(tenantUrl.AbsoluteUri);
            AuthenticationResult authResult = null;
            DeviceCodeResult codeResult = null;
            var term = context.GetService<ITerminal>();
            codeResult = ctx.AcquireDeviceCodeAsync("499b84ac-1321-427f-aa17-267ca6975798", "872cd9fa-d31f-45e0-9eab-6e460a02d1f1").GetAwaiter().GetResult();
            term.WriteLine($"You need to finish AAD device login flow. {codeResult.Message}");
#if OS_WINDOWS
            Process.Start(new ProcessStartInfo() { FileName = codeResult.VerificationUrl, UseShellExecute = true });
#elif OS_LINUX
            Process.Start(new ProcessStartInfo() { FileName = "xdg-open", Arguments = codeResult.VerificationUrl, UseShellExecute = true });
#else
            Process.Start(new ProcessStartInfo() { FileName = "open", Arguments = codeResult.VerificationUrl, UseShellExecute = true });
#endif
            authResult = ctx.AcquireTokenByDeviceCodeAsync(codeResult).GetAwaiter().GetResult();
            var aadCred = new VssAadCredential(new VssAadToken(authResult));
            VssCredentials creds = new VssCredentials(null, aadCred, CredentialPromptType.DoNotPrompt);
            trace.Info("cred created");

            return creds;
        }

        public override void EnsureCredential(IHostContext context, CommandSettings command, string serverUrl)
        {
            ArgUtil.NotNull(context, nameof(context));
            Tracing trace = context.GetTrace(nameof(AadDeviceCodeAccessToken));
            trace.Info(nameof(EnsureCredential));
            ArgUtil.NotNull(command, nameof(command));
            // CredentialData.Data[Constants.Agent.CommandLine.Args.Account] = command.GetAccount();
            CredentialData.Data[Constants.Agent.CommandLine.Args.Url] = serverUrl;
        }

        // MSA backed accounts will return Guid.Empty
        private Uri GetAccountTenantUrl(IHostContext context, string serverUrl)
        {
            using (var client = new HttpClient(context.CreateHttpClientHandler()))
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("X-TFS-FedAuthRedirect", "Suppress");
                HttpResponseMessage response = client.GetAsync($"{serverUrl.Trim('/')}/_apis/connectiondata").GetAwaiter().GetResult();

                // Get the tenant from the Login URL
                var bearerResult = response.Headers.WwwAuthenticate.Where(p => p.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (bearerResult != null && bearerResult.Parameter.StartsWith("authorization_uri=", StringComparison.OrdinalIgnoreCase))
                {
                    var authorizationUri = bearerResult.Parameter.Substring("authorization_uri=".Length);
                    if (Uri.TryCreate(authorizationUri, UriKind.Absolute, out Uri aadTenantUrl))
                    {
                        return aadTenantUrl;
                    }
                }

                return null;
            }
        }

        private class AadTrace : IAdalLogCallback
        {
            private Tracing _trace;

            public AadTrace(Tracing trace)
            {
                _trace = trace;
            }

            public void Log(LogLevel level, string message)
            {
                switch (level)
                {
                    case LogLevel.Information:
                        _trace.Info(message);
                        break;
                    case LogLevel.Verbose:
                        _trace.Verbose(message);
                        break;
                    case LogLevel.Error:
                        _trace.Error(message);
                        break;
                    case LogLevel.Warning:
                        _trace.Warning(message);
                        break;
                    default:
                        break;
                }
            }
        }
    }

    public sealed class PersonalAccessToken : CredentialProvider
    {
        public PersonalAccessToken() : base(Constants.Configuration.PAT) { }

        public override VssCredentials GetVssCredentials(IHostContext context)
        {
            ArgUtil.NotNull(context, nameof(context));
            Tracing trace = context.GetTrace(nameof(PersonalAccessToken));
            trace.Info(nameof(GetVssCredentials));
            ArgUtil.NotNull(CredentialData, nameof(CredentialData));
            string token;
            if (!CredentialData.Data.TryGetValue(Constants.Agent.CommandLine.Args.Token, out token))
            {
                token = null;
            }

            ArgUtil.NotNullOrEmpty(token, nameof(token));

            trace.Info("token retrieved: {0} chars", token.Length);

            // PAT uses a basic credential
            VssBasicCredential basicCred = new VssBasicCredential("VstsAgent", token);
            VssCredentials creds = new VssCredentials(null, basicCred, CredentialPromptType.DoNotPrompt);
            trace.Info("cred created");

            return creds;
        }

        public override void EnsureCredential(IHostContext context, CommandSettings command, string serverUrl)
        {
            ArgUtil.NotNull(context, nameof(context));
            Tracing trace = context.GetTrace(nameof(PersonalAccessToken));
            trace.Info(nameof(EnsureCredential));
            ArgUtil.NotNull(command, nameof(command));
            CredentialData.Data[Constants.Agent.CommandLine.Args.Token] = command.GetToken();
        }
    }

    public sealed class ServiceIdentityCredential : CredentialProvider
    {
        public ServiceIdentityCredential() : base(Constants.Configuration.ServiceIdentity) { }

        public override VssCredentials GetVssCredentials(IHostContext context)
        {
            ArgUtil.NotNull(context, nameof(context));
            Tracing trace = context.GetTrace(nameof(ServiceIdentityCredential));
            trace.Info(nameof(GetVssCredentials));
            ArgUtil.NotNull(CredentialData, nameof(CredentialData));
            string token;
            if (!CredentialData.Data.TryGetValue(Constants.Agent.CommandLine.Args.Token, out token))
            {
                token = null;
            }

            string username;
            if (!CredentialData.Data.TryGetValue(Constants.Agent.CommandLine.Args.UserName, out username))
            {
                username = null;
            }

            ArgUtil.NotNullOrEmpty(token, nameof(token));
            ArgUtil.NotNullOrEmpty(username, nameof(username));

            trace.Info("token retrieved: {0} chars", token.Length);

            // ServiceIdentity uses a service identity credential
            VssServiceIdentityToken identityToken = new VssServiceIdentityToken(token);
            VssServiceIdentityCredential serviceIdentityCred = new VssServiceIdentityCredential(username, "", identityToken);
            VssCredentials creds = new VssCredentials(null, serviceIdentityCred, CredentialPromptType.DoNotPrompt);
            trace.Info("cred created");

            return creds;
        }

        public override void EnsureCredential(IHostContext context, CommandSettings command, string serverUrl)
        {
            ArgUtil.NotNull(context, nameof(context));
            Tracing trace = context.GetTrace(nameof(ServiceIdentityCredential));
            trace.Info(nameof(EnsureCredential));
            ArgUtil.NotNull(command, nameof(command));
            CredentialData.Data[Constants.Agent.CommandLine.Args.Token] = command.GetToken();
            CredentialData.Data[Constants.Agent.CommandLine.Args.UserName] = command.GetUserName();
        }
    }

    public sealed class AlternateCredential : CredentialProvider
    {
        public AlternateCredential() : base(Constants.Configuration.Alternate) { }

        public override VssCredentials GetVssCredentials(IHostContext context)
        {
            ArgUtil.NotNull(context, nameof(context));
            Tracing trace = context.GetTrace(nameof(AlternateCredential));
            trace.Info(nameof(GetVssCredentials));

            string username;
            if (!CredentialData.Data.TryGetValue(Constants.Agent.CommandLine.Args.UserName, out username))
            {
                username = null;
            }

            string password;
            if (!CredentialData.Data.TryGetValue(Constants.Agent.CommandLine.Args.Password, out password))
            {
                password = null;
            }

            ArgUtil.NotNull(username, nameof(username));
            ArgUtil.NotNull(password, nameof(password));

            trace.Info("username retrieved: {0} chars", username.Length);
            trace.Info("password retrieved: {0} chars", password.Length);

            VssBasicCredential loginCred = new VssBasicCredential(username, password);
            VssCredentials creds = new VssCredentials(null, loginCred, CredentialPromptType.DoNotPrompt);
            trace.Info("cred created");

            return creds;
        }

        public override void EnsureCredential(IHostContext context, CommandSettings command, string serverUrl)
        {
            ArgUtil.NotNull(context, nameof(context));
            Tracing trace = context.GetTrace(nameof(AlternateCredential));
            trace.Info(nameof(EnsureCredential));
            ArgUtil.NotNull(command, nameof(command));
            CredentialData.Data[Constants.Agent.CommandLine.Args.UserName] = command.GetUserName();
            CredentialData.Data[Constants.Agent.CommandLine.Args.Password] = command.GetPassword();
        }
    }
}
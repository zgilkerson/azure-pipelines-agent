using System;
using System.Diagnostics;
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
            string account;
            if (!CredentialData.Data.TryGetValue(Constants.Agent.CommandLine.Args.Account, out account))
            {
                account = null;
            }

            ArgUtil.NotNullOrEmpty(account, nameof(account));

            trace.Info("AAD account: {account}");
            MailAddress email = new MailAddress(account);
            LoggerCallbackHandler.UseDefaultLogging = false;
            AuthenticationContext ctx = new AuthenticationContext($"https://login.microsoftonline.com/{email.Host}");
            AuthenticationResult result = null;
            DeviceCodeResult codeResult = null;
            var term = context.GetService<ITerminal>();
            try
            {
                codeResult = ctx.AcquireDeviceCodeAsync("499b84ac-1321-427f-aa17-267ca6975798", "872cd9fa-d31f-45e0-9eab-6e460a02d1f1").GetAwaiter().GetResult();
                term.WriteLine($"You need to finish AAD device login flow. {codeResult.UserCode}");
                Process.Start(new ProcessStartInfo() { FileName = codeResult.VerificationUrl, UseShellExecute = true });
                result = ctx.AcquireTokenByDeviceCodeAsync(codeResult).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                trace.Error(ex);
                term.WriteError(ex);
            }

            term.WriteLine($"Token: {result.AccessToken}");
            var aadCred = new VssAadCredential(new VssAadToken(result));
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
            CredentialData.Data[Constants.Agent.CommandLine.Args.Account] = command.GetAccount();
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
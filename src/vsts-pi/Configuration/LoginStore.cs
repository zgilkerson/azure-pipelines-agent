using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Security.Cryptography;

namespace Microsoft.VisualStudio.Services.Agent.Configuration
{
    //
    // Settings are persisted in this structure
    //
    [DataContract]
    public sealed class LoginSettings
    {
        [DataMember(EmitDefaultValue = false)]
        public string ServerUrl { get; set; }
    }

    [ServiceLocator(Default = typeof(LoginStore))]
    public interface ILoginStore : IAgentService
    {
        bool IsLoggedIn();
        bool HasCredentials();
        CredentialData GetCredentials();
        LoginSettings GetSettings();
        void SaveCredentialData(CredentialData credential);
        void SaveSettings(LoginSettings settings);
        void DeleteCredential();
        void DeleteSettings();
    }

    public sealed class LoginStore : AgentService, ILoginStore
    {
        private string _rootPath;
        private string _workPath;
        private string _loginSettingsPath;
        private string _credFilePath;
        private CredentialData _creds;
        private LoginSettings _settings;
        // private IAgentCredentialStore _credStore;

        public override void Initialize(IHostContext context)
        {
            base.Initialize(context);

            if (context.GetType() != typeof(PipelineContext)) {
                throw new ArgumentException("hostContext");
            }

            var currentAssemblyLocation = System.Reflection.Assembly.GetEntryAssembly().Location;
            Trace.Info("currentAssemblyLocation: {0}", currentAssemblyLocation);

            _rootPath = context.GetDirectory(WellKnownDirectory.Root);
            Trace.Info("rootPath: {0}", _rootPath);

            _loginSettingsPath = Path.Combine(_rootPath, ".pipeline");;
            Trace.Info("LoginSettingsPath: {0}", _loginSettingsPath);

            _credFilePath = Path.Combine(_rootPath, ".credentials");
            Trace.Info("CredFilePath: {0}", _credFilePath);
        }

        public bool HasCredentials()
        {
            ArgUtil.Equal(RunMode.Local, HostContext.RunMode, nameof(HostContext.RunMode));
            Trace.Info("HasCredentials()");
            bool credsStored = (new FileInfo(_credFilePath)).Exists;
            Trace.Info("stored {0}", credsStored);
            return credsStored;
        }

        public bool IsLoggedIn()
        {
            Trace.Info("IsLogged()");
            bool loggedIn = (new FileInfo(_loginSettingsPath)).Exists;
            Trace.Info("IsLoggedIn: {0}", loggedIn);
            return loggedIn;
        }

        public CredentialData GetCredentials()
        {
            Trace.Info("GetCredentials()");
            ArgUtil.Equal(RunMode.Normal, HostContext.RunMode, nameof(HostContext.RunMode));
            if (_creds == null)
            {
                _creds = IOUtil.LoadObject<CredentialData>(_credFilePath);
            }

            return _creds;
        }

        public LoginSettings GetSettings()
        {
            Trace.Info("GetSettings()");
            if (_settings == null)
            {
                if (File.Exists(_loginSettingsPath))
                {
                    _settings = IOUtil.LoadObject<LoginSettings>(_loginSettingsPath);
                }
            }

            return _settings;
        }      

        public void SaveCredentialData(CredentialData credential)
        {
            Trace.Info("Saving {0} credential data @ {1}", credential.Scheme, _credFilePath);
            if (File.Exists(_credFilePath))
            {
                // Delete existing credential file first, since the file is hidden and not able to overwrite.
                Trace.Info("Delete exist agent credential file.");
                IOUtil.DeleteFile(_credFilePath);
            }

            IOUtil.SaveObject(credential, _credFilePath);
            Trace.Info("Credentials Saved.");
            File.SetAttributes(_credFilePath, File.GetAttributes(_credFilePath) | FileAttributes.Hidden);
        }

        public void SaveSettings(LoginSettings settings)
        {
            Trace.Info("Saving login settings.");
            if (File.Exists(_loginSettingsPath))
            {
                // Delete existing agent settings file first, since the file is hidden and not able to overwrite.
                Trace.Info("Delete exist agent settings file.");
                IOUtil.DeleteFile(_loginSettingsPath);
            }

            IOUtil.SaveObject(settings, _loginSettingsPath);
            Trace.Info("Settings Saved.");
            File.SetAttributes(_loginSettingsPath, File.GetAttributes(_loginSettingsPath) | FileAttributes.Hidden);
        }

        public void DeleteCredential()
        {
            Trace.Info("DeleteCredential()");
            IOUtil.Delete(_credFilePath, default(CancellationToken));
        }

        public void DeleteSettings()
        {
            Trace.Info("DeleteSettings()");
            IOUtil.Delete(_loginSettingsPath, default(CancellationToken));
        }
    }
}
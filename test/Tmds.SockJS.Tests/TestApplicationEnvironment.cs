using Microsoft.Framework.Runtime;
using System.Runtime.Versioning;

namespace Tmds.SockJS.Tests
{
    // copied from https://github.com/aspnet/Mvc
    public class TestApplicationEnvironment : IApplicationEnvironment
    {
        private readonly IApplicationEnvironment _originalAppEnvironment;
        private readonly string _applicationBasePath;
        private readonly string _applicationName;

        public TestApplicationEnvironment(IApplicationEnvironment originalAppEnvironment, string appBasePath, string appName)
        {
            _originalAppEnvironment = originalAppEnvironment;
            _applicationBasePath = appBasePath;
            _applicationName = appName;
        }

        public string ApplicationName
        {
            get { return _applicationName; }
        }

        public string Version
        {
            get { return _originalAppEnvironment.Version; }
        }

        public string ApplicationBasePath
        {
            get { return _applicationBasePath; }
        }

        public string Configuration
        {
            get
            {
                return _originalAppEnvironment.Configuration;
            }
        }

        public FrameworkName RuntimeFramework
        {
            get { return _originalAppEnvironment.RuntimeFramework; }
        }
    }
}

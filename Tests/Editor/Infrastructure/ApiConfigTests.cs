using NUnit.Framework;
using System.Collections.Generic;

namespace Game.Tests.EditMode.Infrastructure
{
    public class ApiConfigTests
    {
        private ApiConfig _apiConfig;

        [SetUp]
        public void Setup()
        {
            // Initializes the global config (loads any .env.dev if available)
            _apiConfig = new ApiConfig();
        }

        [Test]
        public void LoadConfig_EnsureDefaultsAreValid()
        {
            // Act
            var allConfig = _apiConfig.GetAllConfig();

            // Assert
            Assert.IsNotNull(allConfig, "Configuration dictionary should not be null after initialization.");
            
            // Check essential properties
            Assert.IsNotNull(_apiConfig.API_BASE_URL, "API_BASE_URL should not be null. Either fallback or populated.");
            Assert.IsNotNull(_apiConfig.API_BEARER, "API_BEARER should not be null.");
            
            // Note: Depending on whether a .env exists during the test run, 
            // the values will either be defaults ("https://your-api.com/api") or the env values.
            Assert.IsNotEmpty(_apiConfig.API_BASE_URL);
        }

        [Test]
        public void MobilePlatform_ReturnsCorrectString_ForEditor()
        {
            // Act
            string platformKey = _apiConfig.GetPlatformRun();

            // Assert
            // Since NUnit in EditMode runs in WindowsEditor / OSXEditor
            Assert.IsNotEmpty(platformKey);
            Assert.IsTrue(platformKey == "android" || platformKey == "server", "Editor platforms fallback to android or server");
        }
    }
}

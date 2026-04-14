using NUnit.Framework;
using NSubstitute;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

namespace Game.Tests.EditMode.Infrastructure
{
    public class AuthApiServiceTests
    {
        private AuthApiService _authApiService;
        private IGameApiService _mockGameApiService;
        private IApiConfig _mockApiConfig;

        [SetUp]
        public void Setup()
        {
            // Arrange Mock Dependencies
            _mockGameApiService = Substitute.For<IGameApiService>();
            _mockApiConfig = Substitute.For<IApiConfig>();

            // Setup default mocked configuration values
            _mockApiConfig.API_BASE_URL.Returns("https://mock-api.com/api");
            _mockApiConfig.API_SALT_PASSWORD.Returns("testsalt");

            // Instantiate service injecting mocks
            _authApiService = new AuthApiService(_mockGameApiService, _mockApiConfig);
        }

        [Test]
        public async Task LoginAsync_Calls_PostAsync_WithCorrectValues()
        {
            // Arrange
            string testEmail = "test@example.com";
            string testPassword = "password123";
            
            // Expected URL constructed inside AuthApiService
            string expectedUrl = "https://mock-api.com/api/auth/signin/email";

            // Mock the response
            var fakeResponse = new ApiResponseDto<SignInDataDto> { Message = "Mock Success" };
            
            _mockGameApiService
                .PostAsync<SignInDataDto>(expectedUrl, Arg.Any<object>(), default)
                .Returns(UniTask.FromResult(fakeResponse));

            // Act
            var response = await _authApiService.LoginAsync(testEmail, testPassword, default);

            // Assert
            Assert.IsNotNull(response, "Response should not be null.");
            
            // Verify that PostAsync was invoked exactly once with the appropriate URL
            await _mockGameApiService.Received(1).PostAsync<SignInDataDto>(expectedUrl, Arg.Any<object>(), default);
        }

        [Test]
        public async Task RegisterAsync_Calls_PostAsync_WithCorrectValues()
        {
            // Arrange
            string testEmail = "testuser@example.com";
            string testPassword = "securePassword";
            string testReferral = "REF123";

            string expectedUrl = "https://mock-api.com/api/auth/sign-up";

            var fakeResponse = new ApiResponseDto<SignUpDataDto> { Message = "Registered successfully" };

            _mockGameApiService
                .PostAsync<SignUpDataDto>(expectedUrl, Arg.Any<object>(), default)
                .Returns(UniTask.FromResult(fakeResponse));

            // Act
            var response = await _authApiService.RegisterAsync(testEmail, testPassword, testReferral, default);

            // Assert
            Assert.IsNotNull(response, "Response should not be null.");

            // Verify
            await _mockGameApiService.Received(1).PostAsync<SignUpDataDto>(expectedUrl, Arg.Any<object>(), default);
        }

        [Test]
        public async Task RefreshTokenAsync_Calls_PostAsync_WithCorrectValues()
        {
            // Arrange
            string mockRefreshToken = "xyz-mock-refresh-token";
            string expectedUrl = "https://mock-api.com/api/auth/refresh";

            var fakeResponse = new ApiResponseDto<RefreshTokenDto>();

            _mockGameApiService
                .PostAsync<RefreshTokenDto>(expectedUrl, Arg.Any<object>(), default)
                .Returns(UniTask.FromResult(fakeResponse));

            // Act
            var response = await _authApiService.RefreshTokenAsync(mockRefreshToken, default);

            // Assert
            Assert.IsNotNull(response);

            // Verify
            await _mockGameApiService.Received(1).PostAsync<RefreshTokenDto>(expectedUrl, Arg.Any<object>(), default);
        }
    }
}

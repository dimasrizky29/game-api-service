using NUnit.Framework;

namespace Game.Tests.EditMode.Infrastructure
{
    public class ApiResponseDtoTests
    {
        [TestCase("success", 200, true)]
        [TestCase("SUCCESS", 200, true)]
        [TestCase("error", 200, false)]   // body error wins over HTTP 200
        [TestCase(null, 200, true)]       // no status field, 2xx -> success
        [TestCase("", 204, true)]
        [TestCase(null, 400, false)]
        [TestCase(null, 0, false)]        // network failure
        [TestCase("active", 200, false)]  // unknown explicit status is not success
        public void IsSuccess_FollowsBodyStatusThenHttpCode(string status, int code, bool expected)
        {
            var dto = new ApiResponseDto<object> { Status = status, Code = code };
            Assert.AreEqual(expected, dto.IsSuccess);
        }
    }
}

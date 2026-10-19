/// <summary>
/// Endpoint paths appended to <see cref="IApiConfig.API_BASE_URL"/>.
/// Defaults match the standard yatech backend; override per game with
/// <c>builder.RegisterInstance(new ApiEndpoints { Refresh = "/refresh" });</c>
/// </summary>
public class ApiEndpoints
{
    public string SignInEmail { get; set; } = "/auth/signin/email";
    public string SignUp { get; set; } = "/auth/sign-up";
    public string SignInOAuth { get; set; } = "/auth/signin/oauth";
    public string SignOut { get; set; } = "/auth/signout";
    public string Refresh { get; set; } = "/auth/refresh";
    public string User { get; set; } = "/users";
    public string Username { get; set; } = "/users/username";
    public string Avatar { get; set; } = "/users/avatar";
    public string Deactivate { get; set; } = "/users/deactivate";
}

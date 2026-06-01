using Microsoft.Extensions.Options;

namespace RandomVideoCallWebpage.Services;

public class AdminAuthService
{
    private readonly AdminOptions _options;

    public AdminAuthService(IOptions<AdminOptions> options)
    {
        _options = options.Value;
    }

    public bool ValidateCredentials(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.Password))
        {
            return false;
        }

        return string.Equals(email.Trim(), _options.Email.Trim(), StringComparison.OrdinalIgnoreCase)
            && password == _options.Password;
    }
}

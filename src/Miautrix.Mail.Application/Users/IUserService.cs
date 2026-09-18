using Miautrix.Mail.Domain;

namespace Miautrix.Mail.Application.Users;

public interface IUserService
{
    User? GetUserByUsername(string username);
}
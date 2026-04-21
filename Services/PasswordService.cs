using Isopoh.Cryptography.Argon2;

namespace SRAAS.Api.Services;

public interface IPasswordService
{
    string Hash(string plainPassword);
    bool Verify(string plainPassword, string storedHash);
}

public class PasswordService : IPasswordService
{
    public string Hash(string plainPassword)
    {
        return Argon2.Hash(plainPassword);
    }

    public bool Verify(string plainPassword, string storedHash)
    {
        return Argon2.Verify(storedHash, plainPassword);
    }
}

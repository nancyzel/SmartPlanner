using System.Security.Cryptography;
using System.Text;

namespace SmartPlanner.Services
{
    public class AuthService
    {
        public (string hash, string salt) HashPassword(string password)
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
            string salt = Convert.ToBase64String(saltBytes);

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password + salt));
            return (Convert.ToBase64String(hash), salt);
        }

        public bool Verify(string password, string hash, string salt)
        {
            var newHash = SHA256.HashData(Encoding.UTF8.GetBytes(password + salt));
            return Convert.ToBase64String(newHash) == hash;
        }
    }
}

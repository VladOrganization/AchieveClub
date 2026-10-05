using AchieveClub.Server.ApiContracts.Auth.Request;
using AchieveClub.Server.ApiContracts.Auth.Response;
using AchieveClub.Server.Auth;
using AchieveClub.Server.RepositoryItems;
using AchieveClub.Server.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AchieveClub.Server.Controllers.v1_1
{
    [Route("api/[controller]")]
    [ApiVersion("1.1")]
    [ApiController]
    public class AuthController(
        JwtTokenCreator jwtCreator,
        ApplicationContext db,
        HashService hasher,
        EmailProofService emailProof,
        GoogleAuthService google
        ) : ControllerBase
    {

        [HttpPost("login")]
        public ActionResult<TokenPairResponce> Login([FromBody] LoginRequest model)
        {
            var user = db.Users.Include(u => u.Role).FirstOrDefault(u => u.Email == model.Email);

            if (user == null) return BadRequest();

            if (hasher.ValidPassword(model.Password, user.Password))
            {
                user.RefreshToken = GenerateRefreshToken();

                db.Users.Update(user);
                if (db.SaveChanges() != 1)
                    return Unauthorized();

                (string token, long expire) = GenerateJwtByUser(user);

                return new TokenPairResponce(user.Id, token, user.RefreshToken, expire, user.Role.Id);
            }
            else return BadRequest();
        }

        [HttpPost("registration")]
        public ActionResult<TokenPairResponce> Registration([FromBody] RegistrationRequest model)
        {
            //Uniq Email
            if (db.Users.Any(u => u.Email == model.EmailAddress))
                return Conflict("email");

            //Proof Code
            if (emailProof.ValidateProofCode(model.EmailAddress, model.ProofCode) == false)
                return Unauthorized();

            //Uniq Name
            if (db.Users.Any(u => u.FirstName == model.FirstName && u.LastName == model.LastName))
                return Conflict("name");

            //Hash Password
            var passwordHash = hasher.HashPassword(model.Password).ToString();

            //Create new user
            var newUser = new UserDbo
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Avatar = model.AvatarURL,
                Email = model.EmailAddress,
                Password = passwordHash,
                RefreshToken = GenerateRefreshToken(),
                RoleRefId = 1,
                Role = db.Roles.First(r => r.Id == 1)
            };

            //add to db
            db.Users.Add(newUser);
            if (db.SaveChanges() != 1)
                return BadRequest();

            db.Users.Include(u => u.Role);

            (string token, long expire) = GenerateJwtByUser(newUser);

            emailProof.DeleteProofCode(model.EmailAddress);
            
            return new TokenPairResponce(newUser.Id, token, newUser.RefreshToken, expire, newUser.Role.Id);
        }

        /// <summary>Публичный Google Client ID для фронтенда (берется из env бэкенда).</summary>
        [HttpGet("google/client-id")]
        public ActionResult<string> GoogleClientId([FromServices] GoogleSettings settings) => settings.ClientId;

        [HttpPost("google/login")]
        public async Task<ActionResult<TokenPairResponce>> GoogleLogin([FromBody] GoogleAuthRequest model)
        {
            var profile = await google.ValidateAsync(model.IdToken);
            if (profile == null) return Unauthorized();

            var email = profile.Email.ToLower();
            var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (user == null) return NotFound("not_registered");

            // Пользователь, зарегистрированный по email/паролю, входит через Google по совпадению email
            user.Avatar ??= await google.DownloadAvatarAsync(profile.PictureUrl);
            return await IssueTokens(user);
        }

        [HttpPost("google/registration")]
        public async Task<ActionResult<TokenPairResponce>> GoogleRegistration([FromBody] GoogleAuthRequest model)
        {
            var profile = await google.ValidateAsync(model.IdToken);
            if (profile == null) return Unauthorized();

            var email = profile.Email.ToLower();
            if (await db.Users.AnyAsync(u => u.Email.ToLower() == email))
                return Conflict("email");

            if (await db.Users.AnyAsync(u => u.FirstName == profile.FirstName && u.LastName == profile.LastName))
                return Conflict("name");

            // Пароль неизвестен никому: валидный хеш случайного значения.
            // Задать свой пароль можно через "Забыли пароль?" (код на почту).
            var unusablePassword = hasher.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))).ToString();

            var newUser = new UserDbo
            {
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                Email = profile.Email,
                Password = unusablePassword,
                Avatar = await google.DownloadAvatarAsync(profile.PictureUrl),
                RoleRefId = 1,
                Role = await db.Roles.FirstAsync(r => r.Id == 1)
            };

            db.Users.Add(newUser);
            return await IssueTokens(newUser);
        }

        private async Task<ActionResult<TokenPairResponce>> IssueTokens(UserDbo user)
        {
            user.RefreshToken = GenerateRefreshToken();
            if (await db.SaveChangesAsync() < 1)
                return Unauthorized();

            (string token, long expire) = GenerateJwtByUser(user);
            return new TokenPairResponce(user.Id, token, user.RefreshToken, expire, user.Role.Id);
        }

        [HttpPost("refresh")]
        public ActionResult<TokenPairResponce> Refresh([FromBody] RefreshRequest refreshModel)
        {
            var user = db.Users.Include(u => u.Role).FirstOrDefault(u => u.Id == refreshModel.UserId);

            if (user == null)
                return Unauthorized();

            if (user.RefreshToken == null || user.RefreshToken != refreshModel.RefreshToken)
                return Unauthorized();

            user.RefreshToken = GenerateRefreshToken();

            db.Users.Update(user);
            if (db.SaveChanges() != 1)
                return Unauthorized();

            (string token, long expire) = GenerateJwtByUser(user);

            return new TokenPairResponce(user.Id, token, user.RefreshToken, expire, user.Role.Id);
        }

        private (string, long) GenerateJwtByUser(UserDbo user)
        {
            (string token, DateTime expire) = jwtCreator.Generate(user.Id, user.Role.Title);

            return (token, ((DateTimeOffset)expire).ToUnixTimeSeconds());
        }

        private string GenerateRefreshToken()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
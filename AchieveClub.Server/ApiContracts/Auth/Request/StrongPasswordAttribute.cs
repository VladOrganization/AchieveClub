using System.ComponentModel.DataAnnotations;

namespace AchieveClub.Server.ApiContracts.Auth.Request
{
    /// <summary>Минимум 8 символов, одна заглавная латинская буква и одна цифра.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public sealed class StrongPasswordAttribute : ValidationAttribute
    {
        public const int MinLength = 8;
        public const int MaxLength = 100;

        public StrongPasswordAttribute() : base("Пароль должен содержать минимум 8 символов, одну заглавную букву и одну цифру")
        {
        }

        public static bool IsValid(string? password) =>
            password is { Length: >= MinLength and <= MaxLength }
            && password.Any(c => c is >= 'A' and <= 'Z')
            && password.Any(c => c is >= '0' and <= '9');

        public override bool IsValid(object? value) => value is string s && IsValid(s);
    }
}

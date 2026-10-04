using System.ComponentModel.DataAnnotations;
using AchieveClub.Server.ApiContracts.Auth.Request;
using AchieveClub.Server.Auth;
using AchieveClub.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AchieveClub.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailController(
        ILogger<EmailController> logger,
        EmailProofService emailProof,
        ResendEmailSender emailSender,
        EmailSettings emailSettings,
        ApplicationContext db
        ) : ControllerBase
    {

        [HttpPost("change_password")]
        public async Task<ActionResult> SendChangePasswordCode([FromBody] string emailAddress, CancellationToken ct)
        {
            if (emailProof.WasSentRecently(emailAddress))
            {
                logger.LogWarning("Timeout limit for email sending. Email: {emailAddress}", emailAddress);
                return Conflict("timeout");
            }

            if (db.Users.Any(u => u.Email == emailAddress) == false)
            {
                logger.LogWarning("User with this email address does not exist. Email: {emailAddress}", emailAddress);
                return Conflict("email");
            }
            
            int proofCode = emailProof.GenerateProofCode(emailAddress);

            var (subject, htmlContent) = EmailTemplates.ProofCode(ProofCodePurpose.ChangePassword, proofCode, EmailProofService.CacheDurationMinutes, AssetsUrl);
            if (await emailSender.SendAsync(emailAddress, subject, htmlContent, ct) == false)
            {
                logger.LogError("Could not send proof code email");
                emailProof.DeleteProofCode(emailAddress);
                return StatusCode(StatusCodes.Status502BadGateway, "email_send_failed");
            }

            logger.LogInformation("Code sent successfully for {emailAddress}", emailAddress);
            return NoContent();
        }
        
        [Authorize]
        [HttpPost("change_email")]
        public async Task<ActionResult> SendChangeEmail([FromBody, EmailAddress] string emailAddress, CancellationToken ct)
        {
            var userIdString = HttpContext.User.Identity?.Name;
            if (userIdString == null || int.TryParse(userIdString, out int userId) == false)
            {
                logger.LogWarning("Access token not contains userId or userId is the wrong format: {userIdString}",
                    userIdString);
                return NotFound($"Access token not contains userId or userId is the wrong format: {userIdString}");
            }

            if (await db.Users.AnyAsync(u => u.Id == userId) == false)
            {
                logger.LogWarning("User with userId:{userId} not found", userId);
                return NotFound($"User with userId:{userId} not found");
            }
            
            if (emailProof.WasSentRecently(emailAddress))
            {
                logger.LogWarning("Timeout limit for email sending. Email: {emailAddress}", emailAddress);
                return Conflict("timeout");
            }

            if (db.Users.Any(u => u.Email == emailAddress))
            {
                logger.LogWarning("User with this email address already exist. Email: {emailAddress}", emailAddress);
                return Conflict("email");
            }
            
            int proofCode = emailProof.GenerateProofCode(emailAddress);

            var (subject, htmlContent) = EmailTemplates.ProofCode(ProofCodePurpose.ChangeEmail, proofCode, EmailProofService.CacheDurationMinutes, AssetsUrl);
            if (await emailSender.SendAsync(emailAddress, subject, htmlContent, ct) == false)
            {
                logger.LogError("Could not send proof code email");
                emailProof.DeleteProofCode(emailAddress);
                return StatusCode(StatusCodes.Status502BadGateway, "email_send_failed");
            }

            logger.LogInformation("Code sent successfully for {emailAddress}", emailAddress);
            return NoContent();
        }
        
        [HttpPost("proof_email")]
        public async Task<ActionResult> SendEmailProofCode([FromBody] string emailAddress, CancellationToken ct)
        {
            if (emailProof.WasSentRecently(emailAddress))
            {
                logger.LogWarning("Timeout limit for email sending. Email: {emailAddress}", emailAddress);
                return Conflict("timeout");
            }

            if (db.Users.Any(u => u.Email == emailAddress))
            {
                logger.LogWarning("User with this email address does not exist. Email: {emailAddress}", emailAddress);
                return Conflict("email");
            }

            int proofCode = emailProof.GenerateProofCode(emailAddress);

            var (subject, htmlContent) = EmailTemplates.ProofCode(ProofCodePurpose.Registration, proofCode, EmailProofService.CacheDurationMinutes, AssetsUrl);
            if (await emailSender.SendAsync(emailAddress, subject, htmlContent, ct) == false)
            {
                logger.LogError("Could not send proof code email");
                emailProof.DeleteProofCode(emailAddress);
                return StatusCode(StatusCodes.Status502BadGateway, "email_send_failed");
            }

            logger.LogInformation("Code sent successfully for {emailAddress}", emailAddress);
            return NoContent();
        }

        private string AssetsUrl => emailSettings.AssetsBaseUrl
                                  ?? $"{Request.Scheme}://{Request.Host}/email";

        [HttpPost("validate_code")]
        public ActionResult ValidateProofCode([FromBody] ProofCodeRequest model)
        {
            if (emailProof.ValidateProofCode(model.EmailAddress, model.ProofCode))
                return NoContent();
            else
                return BadRequest();
        }
    }
}
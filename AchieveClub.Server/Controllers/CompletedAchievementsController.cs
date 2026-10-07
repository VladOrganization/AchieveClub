using AchieveClub.Server.RepositoryItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using AchieveClub.Server.ApiContracts.CompletedAchievements.Response;

namespace AchieveClub.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CompletedAchievementsController(
        ApplicationContext db,
        IHubContext<AchieveHub> hub,
        ILogger<CompletedAchievementsController> logger
    ) : ControllerBase
    {
        [Authorize]
        [HttpGet("current")]
        public async Task<ActionResult<List<SmallCompletedAchievementResponse>>> GetForCurrentUser()
        {
            var userIdString = HttpContext.User.Identity?.Name;
            if (userIdString == null || int.TryParse(userIdString, out int userId) == false)
            {
                logger.LogWarning("Access token not contains userId or userId is the wrong format: {userIdString}",
                    userIdString);
                return NotFound($"Access token not contains userId or userId is the wrong format: {userIdString}");
            }

            if (db.Users.Any(x => x.Id == userId) == false)
            {
                logger.LogWarning("User with userId:{userId} not found", userId);
                return NotFound($"User with userId:{userId} not found");
            }

            var completedAchievements = await db.CompletedAchievements
                .Where(ca => ca.UserRefId == userId)
                .Include(ca => ca.Achievement)
                .GroupBy(ca => ca.AchieveRefId)
                .ToListAsync();

            return completedAchievements.Select(group =>
            {
                var timeLimit = group.First().Achievement!.TimeLimitInDays;

                if (timeLimit == null)
                    return new SmallCompletedAchievementResponse(group.Key, group.Count(), null);

                var lastCompletionDate = group.Select(ca => ca.DateOfCompletion).Max();

                var newTryDate = lastCompletionDate.AddDays(timeLimit.Value);

                if (DateTime.Now > newTryDate)
                {
                    return new SmallCompletedAchievementResponse(group.Key, group.Count(), null);
                }

                return new SmallCompletedAchievementResponse(group.Key, group.Count(),
                    ((DateTimeOffset)newTryDate).ToUnixTimeSeconds());
            }).ToList();
        }

        [HttpGet("{userId:int}/detailed")]
        public ActionResult<List<DetailedCompletedAchievementResponce>> GetDetailed([FromRoute] int userId)
        {
            if (db.Users.Any(x => x.Id == userId) == false)
            {
                logger.LogWarning("User with userId:{userId} not found", userId);
                return NotFound($"User with userId:{userId} not found");
            }

            return db.CompletedAchievements
                .Where(ca => ca.UserRefId == userId)
                .Include(ca => ca.Supervisor)
                .Select(a => a.MapToDetailed())
                .ToList();
        }

        [HttpGet("{userId:int}")]
        public async Task<ActionResult<List<SmallCompletedAchievementResponse>>> GetByUserId([FromRoute] int userId)
        {
            if (await db.Users.AnyAsync(x => x.Id == userId) == false)
            {
                logger.LogWarning("User with userId:{userId} not found", userId);
                return NotFound($"User with userId:{userId} not found");
            }

            var completedAchievements = await db.CompletedAchievements
                .Where(ca => ca.UserRefId == userId)
                .Include(ca => ca.Achievement)
                .GroupBy(ca => ca.AchieveRefId)
                .ToListAsync();

            return completedAchievements.Select(group =>
            {
                var timeLimit = group.First().Achievement!.TimeLimitInDays;

                if (timeLimit == null)
                    return new SmallCompletedAchievementResponse(group.Key, group.Count(), null);

                var lastCompletionDate = group.Select(ca => ca.DateOfCompletion).Max();

                var newTryDate = lastCompletionDate.AddDays(timeLimit.Value);

                if (DateTime.Now > newTryDate)
                {
                    return new SmallCompletedAchievementResponse(group.Key, group.Count(), null);
                }

                return new SmallCompletedAchievementResponse(group.Key, group.Count(),
                    ((DateTimeOffset)newTryDate).ToUnixTimeSeconds());
            }).ToList();
        }

        [Authorize(Roles = "Supervisor, Admin")]
        [HttpDelete]
        public async Task<ActionResult> CancelCompleteAchievements(CompleteAchievementRequest model,
            CancellationToken ct)
        {
            var supervisorIdString = HttpContext.User.Identity?.Name;
            if (supervisorIdString == null || int.TryParse(supervisorIdString, out int supervisorId) == false)
            {
                logger.LogWarning(
                    "Access token not contains supervisorId or supervisorId is the wrong format: {supervisorIdString}",
                    supervisorIdString);
                return NotFound(
                    $"Access token not contains supervisorId or supervisorId is the wrong format: {supervisorIdString}");
            }

            if (await db.Users.AnyAsync(x => x.Id == supervisorId, ct) == false)
            {
                logger.LogWarning("Supervisor with userId:{supervisorId} not found", supervisorId);
                return NotFound($"Supervisor with userId:{supervisorId} not found");
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == model.UserId, ct);

            if (user == null)
            {
                logger.LogWarning("User with userId:{model.UserId} not found", model.UserId);
                return NotFound($"User with userId:{model.UserId} not found");
            }

            foreach (var achieveId in model.AchievementIds)
            {
                var completedAchievement = await db.CompletedAchievements.Include(ca => ca.Achievement)
                    .FirstOrDefaultAsync(ca => ca.UserRefId == model.UserId && ca.AchieveRefId == achieveId, ct);

                if (completedAchievement == null)
                {
                    logger.LogWarning("AchieveId:{achieveId} is invalid or not completed!", achieveId);
                    return BadRequest($"AchieveId:{achieveId} is invalid or not completed!");
                }

                user.Balance += completedAchievement.Achievement!.Xp;

                db.CompletedAchievements.Remove(completedAchievement);
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Completed Achievements Canceled: {model}", model);
            return NoContent();
        }

        [Authorize(Roles = "Supervisor, Admin")]
        [HttpPost]
        public async Task<ActionResult> CompleteAchievements(CompleteAchievementRequest model, CancellationToken ct)
        {
            var supervisorIdString = HttpContext.User.Identity?.Name;
            if (supervisorIdString == null || int.TryParse(supervisorIdString, out int supervisorId) == false)
            {
                logger.LogWarning(
                    "Access token not contains supervisorId or supervisorId is the wrong format: {supervisorIdString}",
                    supervisorIdString);
                return NotFound(
                    $"Access token not contains supervisorId or supervisorId is the wrong format: {supervisorIdString}");
            }

            if (await db.Users.AnyAsync(x => x.Id == supervisorId, ct) == false)
            {
                logger.LogWarning("Supervisor with userId:{supervisorId} not found", supervisorId);
                return NotFound($"Supervisor with userId:{supervisorId} not found");
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == model.UserId, ct);

            if (user == null)
            {
                logger.LogWarning("User with userId:{model.UserId} not found", model.UserId);
                return NotFound($"User with userId:{model.UserId} not found");
            }

            var achievements = new List<AchievementDbo>();
            foreach (var achieveId in model.AchievementIds)
            {
                var achievement = await db.Achievements.FirstOrDefaultAsync(u => u.Id == achieveId, ct);

                if (achievement == null)
                {
                    logger.LogWarning("Achievement with achieveId:{achieveId} not found", achieveId);
                    return NotFound($"Achievement with achieveId:{achieveId} not found");
                }

                if (achievements.Any(a => a.Id == achievement.Id) ||
                    await db.CompletedAchievements.AnyAsync(
                        ca => ca.UserRefId == user.Id && ca.AchieveRefId == achievement.Id, ct))
                {
                    logger.LogWarning(
                        "This achievement:{achieveId} has already been completed for this user:{model.UserId}. You cannot complete this achievement more than once",
                        achieveId, model.UserId);
                    return BadRequest(
                        $"This achievement:{achieveId} has already been completed for this user:{model.UserId}. You cannot complete this achievement more than once");
                }

                var timeLimit = achievement.TimeLimitInDays;

                if (timeLimit != null)
                {
                    if (await db.CompletedAchievements
                            .Where(ca => ca.UserRefId == user.Id && ca.AchieveRefId == achievement.Id)
                            .AnyAsync(ct))
                    {
                        var lastCompletionDate = await db.CompletedAchievements
                            .Where(ca => ca.UserRefId == user.Id && ca.AchieveRefId == achievement.Id)
                            .Select(ca => ca.DateOfCompletion)
                            .MaxAsync(ct);

                        var newTryDate = lastCompletionDate.AddDays(timeLimit.Value);

                        if (DateTime.Now < newTryDate)
                        {
                            logger.LogWarning(
                                "Time limit for achievement:{achieveId} applies. User:{user.Id}. Last completion date: {lastCompletionDate}. Next try date: {newTryDate}.",
                                achieveId, user.Id, lastCompletionDate, newTryDate);
                            return BadRequest(
                                $"Time limit for achievement:{achieveId} applies. User:{user.Id}. Last completion date: {lastCompletionDate}. Next try date: {newTryDate}.");
                        }
                    }
                }

                user.Balance += achievement.Xp;

                achievements.Add(achievement);
            }

            foreach (var achievement in achievements)
                db.CompletedAchievements.Add(new CompletedAchievementDbo
                {
                    UserRefId = user.Id,
                    AchieveRefId = achievement.Id,
                    DateOfCompletion = DateTime.Now,
                    SupervisorRefId = supervisorId
                });

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Achievements Completed: {model}", model);
            string message = "completed:" + model.UserId;
            await hub.Clients.All.SendAsync(message, ct);
            logger.LogInformation("SignalR message: {message} sended for user: {model.UserId}", message, model.UserId);
            return NoContent();
        }

        public record CompleteAchievementRequest(
            [Required, Range(1, double.PositiveInfinity)]
            int UserId,
            [Required] List<int> AchievementIds);
    }
}
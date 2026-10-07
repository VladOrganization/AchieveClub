using AchieveClub.Server.ApiContracts.Achievements.Request;
using AchieveClub.Server.ApiContracts.Achievements.Response;
using AchieveClub.Server.RepositoryItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace AchieveClub.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AchievementsController(
        ApplicationContext db,
        ILogger<AchievementsController> logger
        ) : ControllerBase
    {
        [HttpGet]
        [OutputCache(Duration = (3 * 60), Tags = ["achievements"])]
        public async Task<ActionResult<List<AchievementResponse>>> GetAll()
        {
            var achievements = await db.Achievements.ToListAsync();
            return achievements.Select(a=>a.ToResponse()).ToList();
        }

        [HttpGet("{achieveId:int}")]
        public async Task<ActionResult<AchievementResponse>> GetById([FromRoute] int achieveId)
        {
            var achieve = await db.Achievements.FirstOrDefaultAsync(a => a.Id == achieveId);

            if (achieve == null)
            {
                logger.LogWarning("Achievement with name: {achieveId} not exists", achieveId);
                return NotFound($"Achievement with name: {achieveId} not exists");
            }

            return achieve.ToResponse();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<int>> Create([FromBody] CreateAchievementRequest model)
        {
            if (await db.Achievements.AnyAsync(a => a.Title == model.Title))
            {
                logger.LogWarning("Achievement with name: {model.Title} already exists: {model}", model.Title, model);
                return Conflict($"Achievement with name: {model.Title} already exists");
            }

            var newAchievement = new AchievementDbo
            {
                Title = model.Title,
                Description = model.Description,
                LogoURL = model.LogoURL,
                Xp = model.Xp
            };

            var entry = await db.Achievements.AddAsync(newAchievement);

            await db.SaveChangesAsync();
            logger.LogInformation("Achievement created: {newAchievement.Title}[{newAchievement.Id}]", newAchievement.Title, newAchievement.Id);
            
            return entry.Entity.Id;
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{achieveId:int}")]
        public async Task<ActionResult> Update([FromRoute] int achieveId, [FromBody] CreateAchievementRequest request)
        {
            var achievement = await db.Achievements.FirstOrDefaultAsync(a => a.Id == achieveId);

            if (achievement == null)
            {
                logger.LogWarning($"Achievement with id {achieveId} not found!", achieveId);
                return NotFound($"Achievement with id {achieveId} not found!");
            }

            achievement.Title = request.Title;
            achievement.Description = request.Description;
            achievement.LogoURL = request.LogoURL;
            achievement.Xp = request.Xp;

            await db.SaveChangesAsync();
            logger.LogInformation("Achievement updated: {request}", request);
            
            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{achieveId:int}")]
        public async Task<ActionResult> Delete([FromRoute] int achieveId)
        {
            var achievement = await db.Achievements.FirstOrDefaultAsync(a => a.Id == achieveId);

            if (achievement == null)
            {
                logger.LogWarning("Achievement with id {achieveId} not found!", achieveId);
                return NotFound($"Achievement with id {achieveId} not found!");
            }

            db.Remove(achievement);
            await db.SaveChangesAsync();
            
            logger.LogInformation("Achievement deleted: {achievement}", achievement);
            return NoContent();
        }
    }
}

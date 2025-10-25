using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace AchieveClub.Server.Services
{
    public class EmailProofService(ILogger<EmailProofService> logger, IDistributedCache distributedCache)
    {
        private const string EmailProofCacheKey = "EmailProof";
        private const int CacheDurationMinutes = 5;

        public record EmailProofItem(string Email, int ProofCode, DateTime CreatedAt);
        
        public int GenerateProofCode(string emailAddress)
        {
            logger.LogDebug($"GenerateProofCode for email address: {emailAddress}");
            var random = new Random();
            int proofCode = random.Next(1000, 9999);
            StoreProofCode(emailAddress, proofCode);
            return proofCode;
        }

        public bool Contains(string emailAddress)
        {
            var items = GetValidProofItems();
            bool exists = items.Any(x => x.Email.Equals(emailAddress, StringComparison.OrdinalIgnoreCase));
            
            logger.LogDebug($"Contains check for email {emailAddress}: {exists}");
            return exists;
        }

        private void StoreProofCode(string emailAddress, int proofCode)
        {
            var items = GetAllProofItems();
            
            // Удалить старый элемент с этим email если есть
            items.RemoveAll(x => x.Email.Equals(emailAddress, StringComparison.OrdinalIgnoreCase));
            
            // Добавить новый элемент
            var newItem = new EmailProofItem(emailAddress, proofCode, DateTime.UtcNow);
            items.Add(newItem);
            
            SaveProofItems(items);
            logger.LogInformation($"Stored proof code for email: {emailAddress}");
        }

        public bool ValidateProofCode(string emailAddress, int userCode)
        {
            var items = GetValidProofItems();
            var item = items.FirstOrDefault(x => x.Email.Equals(emailAddress, StringComparison.OrdinalIgnoreCase));
            
            if (item == null)
            {
                logger.LogWarning($"No proof code found for email: {emailAddress}");
                return false;
            }
            
            if (item.ProofCode != userCode)
            {
                logger.LogWarning(
                    $"Invalid proof code for email: {emailAddress}. " +
                    $"Expected: {item.ProofCode}, Got: {userCode}"
                );
                return false;
            }
            
            logger.LogInformation($"Proof code validated for email: {emailAddress}");
            return true;
        }

        public void DeleteProofCode(string emailAddress)
        {
            var items = GetAllProofItems();
            var initialCount = items.Count;
            
            items.RemoveAll(x => x.Email.Equals(emailAddress, StringComparison.OrdinalIgnoreCase));
            
            if (items.Count < initialCount)
            {
                SaveProofItems(items);
                logger.LogInformation($"Deleted proof code for email: {emailAddress}");
            }
            else
            {
                logger.LogWarning($"No proof code found to delete for email: {emailAddress}");
            }
        }

        public List<EmailProofItem> GetValidProofItems()
        {
            var allItems = GetAllProofItems();
            var now = DateTime.UtcNow;
            var validItems = allItems
                .Where(x => (now - x.CreatedAt) < TimeSpan.FromMinutes(CacheDurationMinutes))
                .ToList();
            
            logger.LogDebug($"Retrieved {validItems.Count} valid proof items (total: {allItems.Count})");
            return validItems;
        }

        private List<EmailProofItem> GetAllProofItems()
        {
            var cached = distributedCache.GetString(EmailProofCacheKey);
            
            if (string.IsNullOrEmpty(cached))
            {
                return new List<EmailProofItem>();
            }
            
            try
            {
                var items = JsonSerializer.Deserialize<List<EmailProofItem>>(cached);
                return items ?? new List<EmailProofItem>();
            }
            catch (JsonException ex)
            {
                logger.LogError($"Error deserializing proof items: {ex.Message}");
                return new List<EmailProofItem>();
            }
        }

        private void SaveProofItems(List<EmailProofItem> items)
        {
            var json = JsonSerializer.Serialize(items);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes)
            };
            
            distributedCache.SetString(EmailProofCacheKey, json, options);
        }
    }
}
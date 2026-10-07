using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AchieveClub.Server.RepositoryItems;

[Table("Categories")]
public class CategoryDbo
{
    [Key]
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Color { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [MaxLength(1000)]
    public string? AvailableBanner  { get; set; }
    [MaxLength(1000)]
    public string? UnavailableBanner  { get; set; }
    public bool Show { get; set; } = true;
}
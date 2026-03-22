using Microsoft.AspNetCore.Identity;

namespace AkilliKutuphane.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
}

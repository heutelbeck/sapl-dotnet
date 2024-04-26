using System.ComponentModel.DataAnnotations;

namespace SAPL.WebAPIDemo.ExampleData.Models
{
    public class ApplicationUser
    {
        [Required]
        public string Username { get; set; } = null!;

        [Required]
        public string Password { get; set; } = null!;
    }
}

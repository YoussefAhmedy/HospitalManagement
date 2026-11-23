using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hospital.DAL.Entities
{
    [Table("Admins")]
    public class Admin : ApplicationUser
    {
        public decimal Salary { get; set; }

        [MaxLength(60)]
        public string? JobTitle { get; set; }
    }
}

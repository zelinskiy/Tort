using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Tort.Models
{
    public class ApplicationUser:IdentityUser
    {
        public virtual List<Game> Games { get; set; }
        public virtual List<Question> Questions { get; set; }

        public string AvatarUrl { get; set; }
    }
}

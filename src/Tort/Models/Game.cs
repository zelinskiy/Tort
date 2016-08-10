using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Tort.Models
{
    public class Game
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Condition { get; set; }
        public bool IsActive { get; set; }
        
        public virtual ApplicationUser Author { get; set; }
        public virtual List<Question> Questions { get; set; }
    }
}

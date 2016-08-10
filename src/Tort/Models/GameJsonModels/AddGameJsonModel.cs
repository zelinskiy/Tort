using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Tort.Models.GameJsonModels
{
    public class AddGameJsonModel
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Condition { get; set; }
    }
}

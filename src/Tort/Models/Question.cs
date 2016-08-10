using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tort.Models
{
    public class Question
    {
        public int Id { get; set; }
        public string Text { get; set; }
        //0 - no answer; 1 - yes; 2 - no matter; 3 - no; 4 - winner
        public int State { get; set; }

        public virtual ApplicationUser User { get; set; }
        public virtual Game Game { get; set; }
    }
}

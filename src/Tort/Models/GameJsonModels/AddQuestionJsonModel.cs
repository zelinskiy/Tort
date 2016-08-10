using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tort.Models.GameJsonModels
{
    public class AddQuestionJsonModel
    {
        public string UserId { get; set; }
        public int GameId { get; set; }
        public string Text { get; set; }
    }
}

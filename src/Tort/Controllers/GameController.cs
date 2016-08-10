using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tort.Data;
using Tort.Models;
using Tort.Models.GameJsonModels;

namespace Tort.Controllers
{
    [Route("api/game")]
    [Authorize(ActiveAuthenticationSchemes = "Bearer")]
    public class GameController : Controller
    {
        readonly static int[] allowedStates = new int[] { 0, 1, 2, 3, 4 };
        readonly UserManager<ApplicationUser> _userManager;
        readonly SignInManager<ApplicationUser> _signInManager;
        readonly ILogger _logger;
        readonly ApplicationDbContext _context;

        public GameController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILoggerFactory loggerFactory,
            ApplicationDbContext context
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = loggerFactory.CreateLogger<GameController>();
            _context = context;
        }

        private ApplicationUser Me
        {
            get
            {
                var username = User.Claims.First(c => c.Type.EndsWith("nameidentifier")).Value;
                var user = _userManager.Users.First(u => u.UserName == username);
                return user;
            }
        }

        /*
         * example
         * 
        [HttpPost("route")]
        public async Task<JsonResult> Action([FromBody]ActionModel model)
        {
            var responce = new Dictionary<string, string>();
            if (ModelState.IsValid)
            {
                
            }
            else
            {
                responce.Add("errors", JsonConvert.SerializeObject(ModelState.Root.Errors.Select(e =>e.ErrorMessage)));
                responce.Add("status", "fail");
            }
            return Json(responce);
        }
        */

        [HttpGet("current")]
        public async Task<JsonResult> GetCurrentGame()
        {
            var model = await _context.Games
                .SingleOrDefaultAsync(g => g.Author.Id == Me.Id 
                    && g.IsActive == true);
            return Json(JsonConvert.SerializeObject(model, new JsonSerializerSettings
                            {
                                Formatting = Formatting.None,
                                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                            }));
        }

        [HttpGet("{id}")]
        public async Task<JsonResult> SingleGame(int id)
        {
            var model = await _context.Games.SingleOrDefaultAsync(g => g.Id == id);
            return Json(model);
        }

        [HttpGet("all")]
        public async Task<JsonResult> AllGames()
        {
            var model = new AllGamesJsonModel()
            {
                Games = JsonConvert.SerializeObject(
                    await _context.Games
                        .Include(g => g.Author)
                        .ToListAsync(),
                    new JsonSerializerSettings
                    {
                        Formatting = Formatting.None,
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    })
            };

            return Json(model);
        }

        [HttpPost("new")]
        public async Task<JsonResult> NewGame([FromBody]AddGameJsonModel model)
        {
            var responce = new Dictionary<string, string>();
            if (ModelState.IsValid)
            {
                if (await _context.Games.Include(g => g.Author).CountAsync(g => g.Author.Id == Me.Id && g.IsActive) == 0)
                {
                    var newGame = new Game()
                    {
                        Name = model.Name,
                        Condition = model.Condition,
                        Author = Me,
                        IsActive = true
                    };
                    _context.Games.Add(newGame);
                    await _context.SaveChangesAsync();
                    responce.Add("status", "success");
                }
                else
                {
                    responce.Add("errors", JsonConvert.SerializeObject(new string[] { "Another Game is Active" }));
                    responce.Add("status", "fail");
                }
            }
            else
            {
                responce.Add("errors", JsonConvert.SerializeObject(ModelState.Root.Errors.Select(e => e.ErrorMessage)));
                responce.Add("status", "fail");
            }
            return Json(responce);
        }

        [HttpGet("{id}/questions/all")]
        public async Task<JsonResult> AllQuestions(int id)
        {
            var model = new AllQuestionsJsonModel()
            {
                Questions = JsonConvert.SerializeObject(
                    await _context.Questions
                        .Include(q => q.User)
                        .Where(q=>q.Game.Id == id)
                        .ToListAsync(),
                    new JsonSerializerSettings
                    {
                        Formatting = Formatting.None,
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }),
                IsMyGame = (await _context.Games
                    .SingleOrDefaultAsync(g=>g.Id == id 
                        && g.Author.Id == Me.Id)) != null
            };

            return Json(model);
        }

        [HttpGet("{id}/questions/pending")]
        public async Task<JsonResult> PendingQuestions(int id)
        {
            var model = new AllQuestionsJsonModel()
            {
                Questions = JsonConvert.SerializeObject(
                     await _context.Questions
                        .Include(q => q.Game)
                        .Include(q => q.User)
                        .Where(q => q.User.Id == Me.Id
                            && q.Game.IsActive == true
                            && q.State == 0)
                        .ToListAsync(),
                    new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    })
            };

            return Json(model);
        }

        [HttpPost("{id}/questions/new")]
        public async Task<JsonResult> NewQuestion([FromBody]AddQuestionJsonModel model)
        {
            var responce = new Dictionary<string, string>();
            if (ModelState.IsValid)
            {
                var game = await _context.Games.SingleOrDefaultAsync(g => g.Id == model.GameId);
                if (game != null)
                {
                    var newQuestion = new Question()
                    {
                        Game = game,
                        State = 0,
                        Text = model.Text,
                        User = Me
                    };
                    _context.Questions.Add(newQuestion);
                    await _context.SaveChangesAsync();
                    responce.Add("status", "success");
                }
                else
                {
                    responce.Add("errors", $"Game {model.GameId} not found");
                    responce.Add("status", "fail");
                }
            }
            else
            {
                responce.Add("errors", JsonConvert.SerializeObject(ModelState.Root.Errors.Select(e => e.ErrorMessage)));
                responce.Add("status", "fail");
            }
            return Json(responce);
        }

        [HttpGet("{gid}/questions/{qid}/state/{state}")]
        public async Task<JsonResult> SetQuestionState(int gid, int qid, int state)
        {
            var responce = new Dictionary<string, string>();

            var game = await _context.Games
                .Include(g=>g.Author)
                .SingleOrDefaultAsync(g => g.Id == gid);

            var question = await _context.Questions
                .SingleOrDefaultAsync(q => q.Id == qid);

            if (!allowedStates.Contains(state))
            {
                responce.Add("errors", $"State {state} not allowed");
                responce.Add("status", "fail");
            }
            else if(game == null)
            {
                responce.Add("errors", $"Game {gid} not found");
                responce.Add("status", "fail");
            }
            else if(game.Author.Id != Me.Id)
            {
                responce.Add("errors", $"Game {gid} is not created by you");
                responce.Add("status", "fail");
            }
            else if (question == null)
            {
                responce.Add("errors", $"Question {qid} not found");
                responce.Add("status", "fail");
            }
            else if(state == 4)
            {
                question.State = state;
                game.IsActive = false;
                _context.Questions.Update(question);
                await _context.SaveChangesAsync();
            }
            else
            {
                question.State = state;
                _context.Questions.Update(question);
                await _context.SaveChangesAsync();
                responce.Add("status", "succeed");
            }

            return Json(responce);
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Tort.Data;
using Tort.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Tort.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.Net.WebSockets;
using System.Threading;
using Tort.Middleware;

namespace Tort
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            if (env.IsDevelopment())
            {
                builder.AddUserSecrets();
            }
            
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = false;

                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 1;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
            })
                .AddRoleManager<ApplicationRoleManager>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            //Setting service for storing our TokenProviderOptions

            services.Configure<TokenProviderOptions>(options =>
            {
                options.Audience = Configuration["Tokens:Audience"];
                options.Issuer = Configuration["Tokens:Issuer"];
                options.SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Configuration["TokenSigningKey"])),
                    SecurityAlgorithms.HmacSha256);
                options.Expiration = TimeSpan.FromSeconds(int.Parse(Configuration["Tokens:Expiration"]));
            });


            var policy = new CorsPolicy();
            policy.Headers.Add("*");
            policy.Methods.Add("*");
            policy.Origins.Add("*");
            policy.SupportsCredentials = true;

            services.AddCors(options => options.AddPolicy("MyPolicy", policy));

            // Add framework services.
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();
            var logger = loggerFactory.CreateLogger("Tokens");

            app.UseStaticFiles();
            app.UseIdentity();


            //Setting our tokens validator
            var tokenparams = new TokenValidationParameters()
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Configuration["TokenSigningKey"])),
                ValidIssuer = Configuration["Tokens:Issuer"],
                ValidateLifetime = true,
                SaveSigninToken = false,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.Zero,
                LifetimeValidator = ((DateTime? notBefore,
                    DateTime? expires,
                    SecurityToken securityToken,
                    TokenValidationParameters validationParameters) =>
                !(expires.Value < DateTime.UtcNow)),
            };


            var opts = new JwtBearerOptions()
            {
                TokenValidationParameters = tokenparams,
                AutomaticAuthenticate = true,
                AutomaticChallenge = true,
                Audience = Configuration["Tokens:Audience"],

                Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        logger.LogError("Authentication failed.", context.Exception);
                        return Task.FromResult(0);
                    },
                    OnTokenValidated = context =>
                    {

                        if (context.SecurityToken.ValidTo < DateTime.UtcNow)
                        {
                            return Task.FromResult(0);
                        }
                        var claimsIdentity = context.Ticket.Principal.Identity as ClaimsIdentity;
                        claimsIdentity.AddClaim(new Claim("id_token",
                            context.Request.Headers["Authorization"][0].Substring(context.Ticket.AuthenticationScheme.Length + 1)));

                        // OPTIONAL: you can read/modify the claims that are populated based on the JWT
                        // claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, claimsIdentity.FindFirst("name").Value));
                        return Task.FromResult(0);
                    }
                }
            };

            app.UseJwtBearerAuthentication(opts);

            app.UseCors("MyPolicy");


            //*******************************POLICE LINE DO NOT CROSS********************************************
            app.UseWebSockets();
            app.Map("/ws", SocketHandler.Map);
            /*
            app.Map("/ws", builder =>
            {
                builder.Use(async (context, next) =>
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        byte[] buffer = new byte[1024 * 4];
                        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        while (!result.CloseStatus.HasValue)
                        {
                            await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
                            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        }
                        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                        return;
                    }
                    await next();
                });
            });
            */
            /*
            app.Use(async (http, next) =>
            {
                if (http.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await http.WebSockets.AcceptWebSocketAsync();
                    while (webSocket.State == WebSocketState.Open)
                    {
                        var token = CancellationToken.None;
                        var buffer = new ArraySegment<byte>(new Byte[4096]);
                        var received = await webSocket.ReceiveAsync(buffer, token);

                        switch (received.MessageType)
                        {
                            case WebSocketMessageType.Text:
                                var request = Encoding.UTF8.GetString(buffer.Array,
                                                        buffer.Offset,
                                                        buffer.Count);  
                                var type = WebSocketMessageType.Text;
                                var data = Encoding.UTF8.GetBytes("Echo from server :" + request);
                                buffer = new ArraySegment<Byte>(data);
                                await webSocket.SendAsync(buffer, type, true, token);
                                break;
                        }
                    }
                }
                else
                {
                    await next();
                }
            });
            */
            //***************************************************************

            app.UseMvc();
        }
    }
}

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using RaisedHands.Api.BackgroundServices;
using RaisedHands.Api.Services;
using RaisedHands.Api.Settings;
using RaisedHands.Data;
using RaisedHands.Data.Entities;
using Microsoft.AspNetCore.SignalR;
using RaisedHands.Api.Hubs; 

namespace RaisedHands.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetValue<string>("ConnectionStrings:DefaultConnection"), options =>
            {
                options.UseNodaTime();
            });
        });

        builder.Services.AddIdentityCore<User>(options =>
            options.SignIn.RequireConfirmedAccount = true)
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 6;
            options.Password.RequiredUniqueChars = 1;
        });

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);

        builder.Services.AddSingleton<IClock>(SystemClock.Instance);
        builder.Services.AddScoped<EmailSenderService>();
        builder.Services.AddHostedService<EmailSenderBackgroundService>();
        builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
        builder.Services.AddControllers()
            .AddNewtonsoftJson();

        builder.Services.AddIdentity<User, Role>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
        })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Register SignalR
        builder.Services.AddSignalR();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // Map the SignalR hub
        app.MapHub<QuestionHub>("/questionHub");

        app.MapControllers();

        app.Run();
    }
}

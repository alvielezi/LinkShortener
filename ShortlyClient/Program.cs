using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shortly.Data.Services;
using ShortlyClient.Data;
using ShortlyClient.Data.AutoMapper;
using ShortlyData;
using ShortlyData.Models;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Load user secrets in Development
if (builder.Environment.IsDevelopment())
{
    // This reads secrets associated with the project's UserSecretsId.
    // You can run: dotnet user-secrets set "Auth:Google:ClientId" "..." --project ShortlyClient
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure the AppDbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//Configure Authentication
//1. Add identity service
builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

//2. Configure the application cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.LoginPath = "/Authentication/Login";
    options.SlidingExpiration = true;
});

//3. Update the default password settings
builder.Services.Configure<IdentityOptions>(options =>
{
    //Password settings
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 5;

    //Lockout settings
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);

    //Signin settings
    options.SignIn.RequireConfirmedEmail = true;
});

//Add services to the container
builder.Services.AddScoped<IUrlsService, UrlsService>();
builder.Services.AddScoped<IUsersService, UsersService>();

builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Auth:Google:ClientId"];
        options.ClientSecret = builder.Configuration["Auth:Google:ClientSecret"];
    })
    .AddGitHub(options =>
    {
        options.ClientId = builder.Configuration["Auth:GitHub:ClientId"];
        options.ClientSecret = builder.Configuration["Auth:GitHub:ClientSecret"];

        // Ask GitHub for email addresses
        options.Scope.Add("user:email");

        // When creating the ticket, call GitHub's /user/emails endpoint to get the primary email
        options.Events.OnCreatingTicket = async context =>
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", context.AccessToken);
                request.Headers.UserAgent.ParseAdd("ShortlyClient");

                var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();

                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                string primaryEmail = null;

                foreach (var e in payload.RootElement.EnumerateArray())
                {
                    if (e.TryGetProperty("primary", out var primaryProp) && primaryProp.GetBoolean())
                    {
                        primaryEmail = e.GetProperty("email").GetString();
                        break;
                    }

                    if (primaryEmail == null && e.TryGetProperty("email", out var emailProp))
                    {
                        primaryEmail = emailProp.GetString();
                    }
                }

                if (!string.IsNullOrEmpty(primaryEmail))
                {
                    context.Identity.AddClaim(new Claim(ClaimTypes.Email, primaryEmail));
                }
            }
            catch
            {
                // swallow - user may still have email in other claims; logging will help
            }
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Add authentication middleware before authorization
app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

DbInitializer.SeedDefaultUsersAndRolesAsync(app).Wait();
app.Run();

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shortly.Data.Services;
using ShortlyClient.Data;
using ShortlyClient.Data.AutoMapper;
using ShortlyData;
using ShortlyData.Models;

var builder = WebApplication.CreateBuilder(args);

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

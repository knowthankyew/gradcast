using GradCast.Api.Configuration;
using GradCast.Api.Endpoints;
using GradCast.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<CollegeScorecardOptions>(
    builder.Configuration.GetSection(CollegeScorecardOptions.SectionName));

// HttpClient for College Scorecard API
builder.Services.AddHttpClient<ICollegeScorecardService, CollegeScorecardService>(client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Caching
builder.Services.AddMemoryCache();

// CORS for Vue dev server
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

// Map API endpoints
app.MapSchoolEndpoints();

app.Run();

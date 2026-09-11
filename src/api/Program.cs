using GradCast.Api.Endpoints;
using GradCast.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGradCastServices(builder.Configuration, builder.Environment);
builder.Services.AddGradCastCorsPolicy();
builder.Services.AddGradCastOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

app.MapSchoolEndpoints();
app.MapLocationEndpoints();
app.MapFinanceEndpoints();
app.MapJobEndpoints();

app.Run();

public partial class Program
{
}

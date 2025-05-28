var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScoped<DatabaseAnalysis.Api.Services.AnalysisService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Enable CORS for frontend running on http://localhost:4201
app.UseCors(policy =>
{
    policy.WithOrigins(
          "http://localhost:4201",
          "https://agreeable-tree-0ccafbf1e.azurestaticapps.net",
          "https://agreeable-tree-0ccafbf1e-6.azurestaticapps.net",
          "https://*.azurestaticapps.net",
          "https://databaseanalysisbackend-g4f4dneed4f2f8ad.westus-01.azurewebsites.net",
          "https://agreeable-tree-0ccafbf1e.6.azurestaticapps.net")
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials();
});

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
// Redirect root URL to Swagger UI
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/swagger/index.html");
        return;
    }
    await next();
});
//}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

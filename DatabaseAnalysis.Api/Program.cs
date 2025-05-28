var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScoped<DatabaseAnalysis.Api.Services.AnalysisService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Enable CORS with comprehensive configuration
builder.Services.AddCors(options =>
{
    // Default policy with allowed origins
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
              "http://localhost:4201",
              "https://agreeable-tree-0ccafbf1e.azurestaticapps.net",
              "https://agreeable-tree-0ccafbf1e-6.azurestaticapps.net",
              "https://*.azurestaticapps.net") // Wildcard for subdomains
              .AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowedToAllowWildcardSubdomains() // This allows subdomains to match
              .WithExposedHeaders("X-API-Version", "X-Request-ID")
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10)); // Cache preflight results to reduce OPTIONS requests
    });

    // Additional permissive policy for testing
    options.AddPolicy("DevelopmentPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Enable CORS with the configured policy
// Use the default policy instead of inline configuration
app.UseCors();

// Add middleware to log all requests (useful for debugging)
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation($"Request: {context.Request.Method} {context.Request.Path} from {context.Connection.RemoteIpAddress}");
    logger.LogInformation($"Origin: {context.Request.Headers["Origin"]}");

    // Add diagnostic headers to response
    context.Response.Headers.Append("X-API-Version", "1.0.5");
    context.Response.Headers.Append("X-Request-ID", Guid.NewGuid().ToString());

    await next();

    logger.LogInformation($"Response: {context.Response.StatusCode}");
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

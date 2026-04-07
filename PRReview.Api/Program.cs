using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using PRReview.Api.Configuration;
using PRReview.Api.Services;
using PRReview.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────
builder.Services.Configure<AzureDevOpsOptions>(
    builder.Configuration.GetSection(AzureDevOpsOptions.SectionName));

builder.Services.Configure<ClaudeOptions>(
    builder.Configuration.GetSection(ClaudeOptions.SectionName));

// ── Named HTTP Clients ─────────────────────────────────────────────────────
builder.Services.AddHttpClient("AzureDevOps", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<AzureDevOpsOptions>>().Value;
    var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{opts.PatToken}"));
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Basic", credentials);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ── Services ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAzureDevOpsService, AzureDevOpsService>();
// Uses the local claude CLI (claude -p) — no API key required
builder.Services.AddScoped<IClaudeReviewService, ClaudeCliReviewService>();

// ── CORS ───────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ── Controllers & Swagger ──────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PR Review API",
        Version = "v1",
        Description = "Azure DevOps pull request code review powered by Claude Code CLI."
    });
});

// ── Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PR Review API v1"));
}

app.UseCors("AllowAngularDev");
app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();
app.Run();

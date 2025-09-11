using Sentry.Extensibility;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var url = $"http://0.0.0.0:{port}";
var sentryDsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? null;

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

if (sentryDsn?.Length > 1)
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.Debug = true;
        o.TracesSampleRate = 1.0;
        o.SendDefaultPii = true;
        o.MaxRequestBodySize = RequestSize.Always;
        o.MinimumBreadcrumbLevel = LogLevel.Debug;
        o.MinimumEventLevel = LogLevel.Warning;
        o.AttachStacktrace = true;
        o.DiagnosticLevel = SentryLevel.Error;
    });   
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (sentryDsn?.Length > 1)
{
    app.UseSentryTracing();
}

app.MapControllers();

app.Run(url);
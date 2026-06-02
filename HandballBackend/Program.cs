using HandballBackend;
using HandballBackend.Arguments;
using HandballBackend.Authentication;
using HandballBackend.Converters;
using HandballBackend.Database.Models;
using HandballBackend.EndpointHelpers;
using HandballBackend.EndpointHelpers.GameManagement;
using HandballBackend.ErrorTypes;
using HandballBackend.Events;
using HandballBackend.FixtureGenerator;
using HandballBackend.Services;
using HandballBackend.Utils;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers().AddJsonOptions(options => {
    // Global settings: use the defaults, but serialize enums as strings
    // (because it really should be the default)
    options.JsonSerializerOptions.Converters.Add(new NumberConverter());
    options.JsonSerializerOptions.Converters.Add(new EnumConverter<OfficialRole>());
    options.JsonSerializerOptions.Converters.Add(new EnumConverter<GameEventType>());
    options.JsonSerializerOptions.Converters.Add(new EnumConverter<Document.DocumentType>());
});
builder.Services.AddDbContext<HandballContext>();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IGameManagementService, GameManagementService>();
builder.Services.AddScoped<ICustomPermissionService, CustomPermissionService>();
builder.Services.AddScoped<IBackupService, PostgresBackupService>();
builder.Services.AddScoped<ISocketService, SocketService>();
builder.Services.AddScoped<ITextingService, TwilioTextingService>();
builder.Services.AddScoped<IGameEventSynchroniser, GameEventSynchroniser>();

//we need to register it as both a fixture generator and an event handler.
builder.Services.AddScoped<IFixtureGeneratorService, FixtureGeneratorService>();
builder.Services.AddScoped<IEventHandler<RoundEndEvent>, FixtureGeneratorService>();
builder.Services.AddScoped<IEventHandler<GameEndEvent>, EventManager>();
builder.Services.AddScoped<IEventHandler<UpdateElosEvent>, EloService>();

builder.Services.AddScoped<IEventPublisher, EventPublisher>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpLogging(o => { });
builder.Services.AddAuthentication(options => {
        options.DefaultAuthenticateScheme = "TokenAuthentication";
        options.DefaultChallengeScheme = "TokenAuthentication";
    })
    .AddScheme<AuthenticationSchemeOptions, TokenAuthenticator>(
        "TokenAuthentication", null);

builder.Services.AddAuthorization(Policies.RegisterPolicies);

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => { policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); });
});


builder.Services.AddDbContext<HandballContext>();

ArgsHandler.Parse(args, builder);

var app = builder.Build();

using (var serviceScope = app.Services.CreateScope()) {
    var services = serviceScope.ServiceProvider;

    var db = services.GetRequiredService<HandballContext>();
    new EloService(db).UpdatePlayerElos();
    if (Config.BACKUP_TIME > 0) {
        var postgres = services.GetRequiredService<IBackupService>();
        postgres.PeriodicBackups();
    }

    if (Config.GIT_CHECK_TIME > 0) {
        ServerManagementHelper.StartCheckingForUpdates(Config.GIT_CHECK_TIME);
    }
}

// Configure the HTTP request pipeline.
if (Config.LOGGING) {
    app.UseMiddleware<RequestLogger>();
}

if (Config.SAVE_ERRORS) {
    // app.UseExceptionHandler();
    app.UseExceptionLogging();
}

app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection();


app.UseCors();

app.UseWebSockets();

app.MapControllers();

app.Run();
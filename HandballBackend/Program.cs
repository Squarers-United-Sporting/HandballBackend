using HandballBackend;
using HandballBackend.Arguments;
using HandballBackend.Authentication;
using HandballBackend.Converters;
using HandballBackend.Database.Models;
using HandballBackend.ErrorTypes;
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

ArgsHandler.Parse(args, builder);

var app = builder.Build();


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
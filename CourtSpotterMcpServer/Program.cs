var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHttpClient("CourtSpotterClient", client =>
{
    client.BaseAddress = new Uri("https://api-courtspotter.azurewebsites.net/");
}).AddStandardResilienceHandler();

builder.Services.AddMcpServer().WithHttpTransport(opt =>
    {
        opt.Stateless = true;
    })
    .WithToolsFromAssembly();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.MapMcp();
app.UseCors();

app.Run();
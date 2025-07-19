var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

builder.Services.AddHttpClient<HttpClient>("CourtSpotterClient", client =>
{
    client.BaseAddress = new Uri("api-courtspotter.azurewebsites.net/");
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

app.UseHttpsRedirection();
app.MapMcp();
app.UseCors();

app.Run();
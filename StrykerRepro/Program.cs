using StrykerRepro.Endpoints;
using StrykerRepro.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureAppOptions(builder.Configuration);

var app = builder.Build();

app.MapGreetingEndpoints();

app.Run();

public partial class Program { }

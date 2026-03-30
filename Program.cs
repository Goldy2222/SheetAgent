var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddSingleton<PipelineService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();